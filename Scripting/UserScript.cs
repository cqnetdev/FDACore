using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Scripting
{
    public class UserScript : IDisposable
    {
        public string ID { get => _scriptID; }
        public bool Enabled { get => _enabled; set => _enabled = value; }
        public string Code { get => _code; set { if (_code != value) { _code = value; _script = null; } } }
        public string RunSpec { set { if (_runspec != value) { SetupTimersOrTriggers(value); } } }
        private readonly string _scriptID;
        private string _code;
        private Script _script;      // compiled script, ready for executing
        private Timer _timer;     // timer for executing user script on a set schedule

        //private Timer _runOnceTimer;      // timer for executing user script only once, at a particular time
        private readonly object _scriptableObjects;

        private readonly Type _scriptableObjectsType;
        private bool _enabled = false;
        private string _runspec = "";

        private readonly Dictionary<string, List<string>> _onChangeHandlers = new();  // for OnChange handling (trigger object ID, list of properties to watch for changes)

        /****************** internal event that is raised whenever this module is executed **********************/

        internal delegate void ScriptExecuteHandler(string scriptID);

        internal event ScriptExecuteHandler ScriptExecuted;

        internal delegate void UserScriptErrorHandler(string scriptID, Exception ex);

        internal event UserScriptErrorHandler ScriptError;

        internal delegate void CompileErrorHandler(string scriptID, ImmutableArray<Diagnostic> errors);

        internal event CompileErrorHandler CompileError;

        public UserScript(string id, string code, string runspec = "", bool enabled = false)
        {
            _enabled = enabled;
            _scriptID = id;
            _code = code;
            _scriptableObjects = Scripter._scriptInterface;
            _scriptableObjectsType = typeof(ScriptInterface);

            SetupTimersOrTriggers(runspec);
        }

        private void SetupTimersOrTriggers(string runspec)
        {
            _runspec = runspec;
            // remove any existing timers or triggers
            if (_timer != null)
                _timer.Dispose();

            ClearTriggers();

            string[] runspecparts = runspec.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (runspecparts.Length > 0)
            {
                switch (runspecparts[0].ToLower())
                {
                    case "every":
                        _timer = CreateSchedule(runspecparts);
                        break;

                    case "onchange":
                        SubscribeToTriggers(runspecparts);
                        break;

                    case "onceat":
                        _timer = ScheduleExecution(runspecparts);
                        break;

                    default:
                        throw new Exception("Invalid run specification (unrecognized trigger type '" + runspecparts[0] + "')");
                }
            }
        }

        private Timer ScheduleExecution(string[] runspecparts)
        {
            if (runspecparts.Length < 2)
                throw new Exception("Invalid run specification (missing scheduled executation time (HH:mm:ss)");

            DateTime scheduledTime = GetDateTime(runspecparts, 1);

            TimeSpan waitTime = scheduledTime.Subtract(DateTime.Now);

            if (waitTime < TimeSpan.Zero)
                throw new Exception("Invalid run specification. Script run time must be in the future");

            return new Timer(OnTimerTick, null, (long)waitTime.TotalMilliseconds, -1);
        }

        private static DateTime GetDateTime(string[] runspecparts, int idx)
        {
            string timestring = runspecparts[idx];

            if (!DateTime.TryParseExact(timestring, "HH:mm:ss", new CultureInfo("en-US"), System.Globalization.DateTimeStyles.None, out DateTime datetime))
                throw new Exception("Script run time must be supplied in this format (24 hr clock): HH:mm:ss");

            // if it's in the past, add a day
            if (datetime < DateTime.Now)
                datetime = datetime.AddDays(1);

            return datetime;
        }

        private Timer CreateSchedule(string[] runspec)
        {
            Int32 intervalms;
            Int32 waitms = -1;

            if (!Double.TryParse(runspec[1], out double quantity))
            {
                throw new Exception("Invalid run specification (invalid time quantity '" + runspec[1] + "', must be numeric)");
            }

            DateTime scheduledTime = DateTime.MinValue;
            if (runspec.Length >= 5)
            {
                scheduledTime = GetDateTime(runspec, 4);
                TimeSpan waitTime = scheduledTime.Subtract(DateTime.Now);
                waitms = (int)waitTime.TotalMilliseconds;
            }

            switch (runspec[2].ToLower())
            {
                case "seconds":
                    intervalms = Convert.ToInt32(quantity * 1000);
                    if (waitms < 0)
                        waitms = intervalms;
                    return new Timer(OnTimerTick, null, waitms, intervalms);

                case "second":
                    intervalms = Convert.ToInt32(quantity * 1000);
                    if (waitms < 0)
                        waitms = intervalms;
                    return new Timer(OnTimerTick, null, waitms, intervalms);

                case "minutes":
                    intervalms = Convert.ToInt32(quantity * 60 * 1000);
                    if (waitms < 0)
                        waitms = intervalms;
                    return new Timer(OnTimerTick, null, waitms, intervalms);

                case "minute":
                    intervalms = Convert.ToInt32(quantity * 60 * 1000);
                    if (waitms < 0)
                        waitms = intervalms;
                    return new Timer(OnTimerTick, null, waitms, intervalms);

                case "hours":
                    intervalms = Convert.ToInt32(quantity * 24 * 60 * 1000);
                    if (waitms < 0)
                        waitms = intervalms;
                    return new Timer(OnTimerTick, null, waitms, intervalms);

                case "hour":
                    intervalms = Convert.ToInt32(quantity * 24 * 60 * 1000);
                    if (waitms < 0)
                        waitms = intervalms;
                    return new Timer(OnTimerTick, null, waitms, intervalms);

                default:
                    throw new Exception("Invalid run specification (unrecognized time unit '" + runspec[2] + "')");
            }
        }

        private void OnTimerTick(object o)
        {
            Run();
        }

        public void Run()
        {
            if (!_enabled || !Scripter.IsEnabled()) return;

            // if it hasn't been run before, compile it
            if (_script is null)
            {
                ScriptOptions options = ScriptOptions.Default;

                if (Scripter._imports.Count > 0)
                    options = options.AddImports(Scripter._imports);
                if (Scripter._references.Count > 0)
                    options = options.AddReferences(Scripter._references);

                _script = CSharpScript.Create(_code, options, _scriptableObjectsType);
            }

            // run it
            try
            {
                Task<ScriptState> result = null;
                ScriptExecuted?.Invoke(_scriptID);
                try
                {
                    result = _script.RunAsync(_scriptableObjects);
                    result.Wait();
                }
                catch (CompilationErrorException ex)
                {
                    CompileError?.Invoke(_scriptID, ex.Diagnostics);
                    return;
                }
            }
            catch (Exception ex)
            {
                ScriptError?.Invoke(_scriptID, ex);
            }
        }

        private void SubscribeToTriggers(string[] runspec)
        {
            string[] trigger;
            string triggerID;
            string triggerobjectProperty;

            for (int i = 1; i < runspec.Length; i++)
            {
                trigger = runspec[i].Split(".");
                triggerID = trigger[0].ToLower();
                triggerobjectProperty = trigger[1];
                ScriptInterface objects = Scripter._scriptInterface;

                ScriptableObject triggerObj;
                try
                {
                    triggerObj = objects.Get(triggerID);
                }
                catch
                {
                    _enabled = false;
                    throw new Exception("The trigger object '" + triggerID + "' for the script '" + ID + "' was not found, the script has been disabled");
                }

                if (_onChangeHandlers.ContainsKey(triggerID))
                    _onChangeHandlers[triggerID].Add(triggerobjectProperty);
                else
                    _onChangeHandlers.Add(triggerID, new List<string>() { triggerobjectProperty });

                triggerObj.PropertyChanged += OnChangeHandler;
            }
        }

        private void OnChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            ScriptableObject triggerObj = (ScriptableObject)sender;

            string triggerID = triggerObj.ID;
            if (_onChangeHandlers.ContainsKey(triggerID))
            {
                // check if the property that changed is one of the ones we're looking for
                if (_onChangeHandlers[triggerID].Contains(e.PropertyName))
                {
                    // trigger is a match, run this script
                    Run();
                }
            }
        }

        private void ClearTriggers()
        {
            // remove any event handlers (onchange triggers)
            foreach (string id in _onChangeHandlers.Keys)
            {
                try
                {
                    ((ScriptableObject)Scripter._scriptInterface.Get(ID)).PropertyChanged -= OnChangeHandler;
                }
                catch { };
            }

            _onChangeHandlers.Clear();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _enabled = false;

            // dispose of the timer
            _timer?.Dispose();

            ClearTriggers();
        }
    }
}