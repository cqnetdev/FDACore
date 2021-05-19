using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using Common;

namespace DynamicCode
{
    /// <summary>
    /// A loaded dynamic code module (group of methods)
    /// </summary>
    internal class Module : IDisposable
    {
        /****************** public ************************/
        public static object UserAccessibleObjects;  // a structure containing objects from the host program that should be acessible to dynamic code
        public string Name { get => _moduleName; }  // the name of the module
        public List<Tuple<string, string>> Methods { get => _methods; }  // a list of the methods contained in the module (name, run specification)


        /****************** internal event that is raised whenever one of the methods in this module is executed **********************/
        internal delegate void UserMethodExecuteHandler(string methodName);
        internal event UserMethodExecuteHandler UserMethodExecuted;


        /****************** private ***********************/
        readonly List<Tuple<string, string>> _methods;               // a list of the methods contained in the module (name, run specification)
        readonly string _moduleName;                                 // the name of this module
        readonly Type _userCodeClass;                                // the class that contains the dynamic methods in this module
        readonly object _userCodeObject;                             // an instance of the user class
        readonly SimpleUnloadableAssemblyLoadContext _loadContext;   // for unloading the module when it is being disposed

        readonly List<Timer> _timers = new List<Timer>();            // timers for executing user methods on set schedules
        readonly Dictionary<string, List<string>> _onChangeHandlers = new Dictionary<string, List<string>>();  // for OnChange handling (trigger object ID, list of methods to execute on change)

        public Module(string moduleName, Type userclass, SimpleUnloadableAssemblyLoadContext loadContext, List<Tuple<string, string>> methodRunSpecs)
        {
            _moduleName = moduleName;
            _userCodeClass = userclass;
            _loadContext = loadContext;

            if (UserAccessibleObjects != null)
                _userCodeObject = Activator.CreateInstance(userclass, new object[] { UserAccessibleObjects });
            else
                _userCodeObject = Activator.CreateInstance(userclass);

            string[] runspec;
            double quantity;
            Int32 milliseconds;
            Timer newTimer;

            _methods = new List<Tuple<string, string>>();

            foreach (Tuple<string, string> method in methodRunSpecs)
            {
                runspec = method.Item2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                switch (runspec[0].ToLower())
                {
                    case "onschedule":
                        if (!Double.TryParse(runspec[1], out quantity))
                        {
                            throw new Exception("User Method '" + method.Item1 + "': Invalid run specification (invalid time quantity '" + runspec[1] + "', must be numeric)");
                        }
                        switch (runspec[2].ToLower())
                        {
                            case "seconds":
                                milliseconds = Convert.ToInt32(quantity * 1000);
                                newTimer = new Timer(OnTimerTick, method.Item1, milliseconds, milliseconds);
                                _timers.Add(newTimer);
                                break;
                            case "minutes":
                                milliseconds = Convert.ToInt32(quantity * 60 * 1000);
                                newTimer = new Timer(OnTimerTick, method.Item1, milliseconds, milliseconds);
                                _timers.Add(newTimer);
                                break;
                            case "hours":
                                milliseconds = Convert.ToInt32(quantity * 24 * 60 * 1000);
                                newTimer = new Timer(OnTimerTick, method.Item1, milliseconds, milliseconds);
                                _timers.Add(newTimer);
                                break;
                            default:
                                throw new Exception("User Method '" + method.Item1 + "': Invalid run specification (unrecognized time unit '" + runspec[2] + "')");
                        }
                        break;
                    case "onchange":
                        Dictionary<string, Tag> objects = (Dictionary<string, Tag>)UserAccessibleObjects;
                        string[] trigger;
                        for (int i = 1; i < runspec.Length; i++)
                        {
                            trigger = runspec[i].Split(".");  // split into objectname and property name  (trigger is defined as object.property)
                            if (objects.ContainsKey(trigger[0]))
                            {
                                objects[trigger[0]].PropertyChanged += OnChangeHandler;

                                if (_onChangeHandlers.ContainsKey(runspec[i]))
                                    _onChangeHandlers[runspec[i]].Add(method.Item1);
                                else
                                    _onChangeHandlers.Add(runspec[i], new List<string>() { method.Item1 });
                            }
                        }
                        break;
                    default:
                        throw new Exception("User Method '" + method.Item1 + "': Invalid run specification (unrecognized trigger type '" + runspec[0] + "')");

                }
                _methods.Add(method);
            }
        }

        private void OnChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            
            Tag triggerObject = (Tag)sender;
            string trigger = triggerObject.ID + "." + e.PropertyName;

            if (_onChangeHandlers.ContainsKey(trigger))
            {
                foreach (string methodName in _onChangeHandlers[trigger])
                {
                    UserMethodExecuted?.Invoke(_moduleName + "." + methodName);

                    _userCodeClass.GetMethod(methodName).Invoke(_userCodeObject, new object[] { });
                }
            }
        }

        private void OnTimerTick(object o)
        {
            string methodName = (string)o;
            UserMethodExecuted?.Invoke(_moduleName + "." + methodName);

            _userCodeClass.GetMethod(methodName).Invoke(_userCodeObject, new object[] { });
        }

        public void Dispose()
        {
            // stop any timers
            foreach (Timer timer in _timers)
            {
                timer.Dispose();
            }

            _timers.Clear();

            // remove any OnChange handlers
            Dictionary<string, Tag> objects = (Dictionary<string, Tag>)UserAccessibleObjects;
            foreach (string onChangeObject in _onChangeHandlers.Keys)
            {
                if (objects.ContainsKey(onChangeObject))
                {
                    objects[onChangeObject].PropertyChanged -= OnChangeHandler;
                }
            }
            _onChangeHandlers.Clear();

            WeakReference assemblyReference = new WeakReference(_loadContext);

            // unload the module, call the garbage collector and wait for it to be garbage collected
            _loadContext.Unload();
            for (var i = 0; i < 8 && assemblyReference.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

        }
    }
}
