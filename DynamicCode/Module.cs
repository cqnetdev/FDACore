using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Threading;
using Common;
using FDA;

namespace DynamicCode
{
    /// <summary>
    /// A loaded dynamic code module (group of methods)
    /// </summary>
    internal class Module : IDisposable
    {
        /****************** public ************************/
        public static Dictionary<Guid, FDADataPointDefinitionStructure> Tags;
        public static Dictionary<Guid, ConnectionManager> ConnMgrs;

        //public static Dictionary<Guid, ConnectionManager> ConnMgrs; 
        public string Name { get => _moduleName; }  // the name of the module
        public List<string> Methods
        {
            get
            {
                List<string> methods = new List<string>();
                if (_userCodeClass != null)
                {

                    foreach (MethodInfo method in _userCodeClass.GetMethods())
                    {
                        if (method.DeclaringType.Name == "UserCode")
                        {
                            methods.Add(method.Name);
                        }
                    }
                }
                return methods;
            }
        }


        /****************** internal event that is raised whenever this module is executed **********************/
        internal delegate void UserMethodExecuteHandler(string methodName);
        internal event UserMethodExecuteHandler UserMethodExecuted;

        internal delegate void UserMethodRuntimeErrorHandler(string methodName, string errorMsg);
        internal event UserMethodRuntimeErrorHandler UserMethodRuntimeError;

        /****************** private ***********************/
        readonly List<string> _userMethods;                      // a list of the methods contained in the module (name, run specification)
        readonly string _moduleName;                                 // the name of this module
        readonly Type _userCodeClass;                                // the class that contains the dynamic methods in this module
        readonly object _userCodeObject;                             // an instance of the user class
        readonly SimpleUnloadableAssemblyLoadContext _loadContext;   // for unloading the module when it is being disposed

        readonly Timer _timer;                                       // timer for executing user methods on set schedules
        readonly Dictionary<string, List<string>> _onChangeHandlers = new Dictionary<string, List<string>>();  // for OnChange handling (trigger object ID, list of properties to watch for changes)

        public Module(string moduleName, Type userclass, SimpleUnloadableAssemblyLoadContext loadContext, string moduleRunSpec)
        {
            _moduleName = moduleName;
            _userCodeClass = userclass;
            _loadContext = loadContext;

            // check for user functions with parameters defined (this is not allowed)
            foreach (MethodInfo method in _userCodeClass.GetMethods())
            {
                if (method.DeclaringType.Name == "UserCode")
                {
                    if (method.ReturnParameter.ParameterType.Name != "Void")
                    {
                        throw new Exception("Method " + method.Name + " has an invalid definition (return type must be void)");
                    }

                    if (!method.IsPublic)
                    {
                        throw new Exception("Method " + method.Name + " has an invalid definition  (access modifier must be public)");
                    }

                    if (method.GetParameters().Length > 0)
                    {
                        throw new Exception("Method " + method.Name + " has an invalid definition (must be parameterless)");
                    }
                }
            }

            if (Tags != null)
                _userCodeObject = Activator.CreateInstance(userclass, new object[] { Tags,ConnMgrs });
            else
                _userCodeObject = Activator.CreateInstance(userclass);

            string[] runspecparts;
            double quantity;
            Int32 milliseconds;
            Timer newTimer;

            _userMethods = new List<string>();

            runspecparts = moduleRunSpec.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            

            switch (runspecparts[0].ToLower())
            {
                case "onschedule":
                    if (!Double.TryParse(runspecparts[1], out quantity))
                    {
                        throw new Exception("Invalid run specification (invalid time quantity '" + runspecparts[1] + "', must be numeric)");
                    }
                    switch (runspecparts[2].ToLower())
                    {
                        case "seconds":
                            milliseconds = Convert.ToInt32(quantity * 1000);
                            _timer = new Timer(OnTimerTick,null, milliseconds, milliseconds);
                            break;
                        case "minutes":
                            milliseconds = Convert.ToInt32(quantity * 60 * 1000);
                            _timer = new Timer(OnTimerTick,null, milliseconds, milliseconds);
                            break;
                        case "hours":
                            milliseconds = Convert.ToInt32(quantity * 24 * 60 * 1000);
                        newTimer = new Timer(OnTimerTick, null, milliseconds, milliseconds);
                            break;
                        default:
                            throw new Exception("Invalid run specification (unrecognized time unit '" + runspecparts[2] + "')");
                    }
                    break;
                case "onchange":
                    string[] trigger;
                    string triggerobjectIDString;
                    string triggerobjectProperty;

                    for (int i = 1; i < runspecparts.Length; i++)
                    {
                        trigger = runspecparts[i].Split(".");
                        triggerobjectIDString = trigger[0];
                        triggerobjectProperty = trigger[1];
                        Guid triggerID = Guid.Parse(triggerobjectIDString);
                        if (Tags.ContainsKey(triggerID))
                        {
                            Tags[triggerID].PropertyChanged += OnChangeHandler;

                        if (_onChangeHandlers.ContainsKey(triggerobjectIDString))
                                _onChangeHandlers[triggerobjectIDString].Add(triggerobjectProperty);
                        else
                            _onChangeHandlers.Add(triggerobjectIDString, new List<string>() { triggerobjectProperty });
                        }
                    }
                    break;
                default:
                    throw new Exception("Invalid run specification (unrecognized trigger type '" + runspecparts[0] + "')");

            }
   
            
        }

        private void ExecuteAllMethodsInModule()
        {
            foreach (MethodInfo method in _userCodeClass.GetMethods())
            {
                if (method.DeclaringType.Name == "UserCode")
                {
                    UserMethodExecuted?.Invoke(_moduleName + "." + method.Name);
                    try
                    {
                        _userCodeClass.GetMethod(method.Name).Invoke(_userCodeObject, new object[] { });
                    } catch(Exception ex)
                    {
                        UserMethodRuntimeError?.Invoke(_moduleName + "." + method.Name, ex.InnerException.Message);
                    }
                }
            }
        }

        private void OnChangeHandler(object sender, PropertyChangedEventArgs e)
        {
            
            FDADataPointDefinitionStructure triggerObject = (FDADataPointDefinitionStructure)sender;

            string triggerIDString = triggerObject.ID.ToString().ToUpper();
            if (_onChangeHandlers.ContainsKey(triggerIDString))
            {
                // check if the property that changed is one of the ones we're looking for
                if (_onChangeHandlers[triggerIDString].Contains(e.PropertyName))
                {
                    ExecuteAllMethodsInModule();
                }
             
                  
           
            }
        }

        private void OnTimerTick(object o)
        {
            ExecuteAllMethodsInModule();
        }

        public void Dispose()
        {
            // stop the timer if present
            _timer?.Dispose();

            // remove any OnChange handlers
            Guid guid;
            foreach (string onChangeObject in _onChangeHandlers.Keys)
            {
                guid = Guid.Parse(onChangeObject);
                if (Tags.ContainsKey(guid))
                {
                    Tags[guid].PropertyChanged -= OnChangeHandler;
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
