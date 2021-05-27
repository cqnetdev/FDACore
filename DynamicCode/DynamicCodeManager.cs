
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Common;
using FDA;

namespace DynamicCode
{
    public sealed class DynamicCodeManager
    {
        // singleton pattern
        public static DynamicCodeManager Instance { get => _instance; }
        private readonly static DynamicCodeManager _instance = new DynamicCodeManager();

        /********************* public ****************************/
        public Dictionary<Guid, FDADataPointDefinitionStructure> Tags { get => _tags; set { _tags = value; Module.Tags = value; } }
        public Dictionary<Guid, ConnectionManager> ConnMgrs { get => _connMgrs; set { _connMgrs = value; Module.ConnMgrs = value; } }
        public string[] NameSpaces { 
            set {
                  string temp = "";
                   foreach (string ns in value) temp += "using " + ns + ";";
                  _namespaces = temp;
                } 
            }
        public List<string> LoadedModules { get { return UserCodeModules.Keys.ToList<string>(); } }

        public delegate void UserMethodExecuteHandler(string methodName);
        public event UserMethodExecuteHandler UserMethodExecuted;

        public delegate void UserMethodRuntimeErrorHandler(string methodName, string errorMsg);
        public event UserMethodRuntimeErrorHandler UserMethodRuntimeError;

        /******************** private *****************************/
        private readonly string UserCodeWrapper = @"public class UserCode
                                                  {                           
                                                    private static Dictionary<Guid, FDADataPointDefinitionStructure> _tagsref;
                                                    private static Dictionary<Guid, ConnectionManager> _connectionsref;

                                                    public UserCode(Dictionary<Guid, FDADataPointDefinitionStructure> tags,Dictionary<Guid, ConnectionManager> connMgrs)
                                                    {
                                                       _tagsref = tags;
                                                       _connectionsref = connMgrs;  
                                                    }

                                                    private class Tag
                                                    {
                                                        private FDADataPointDefinitionStructure _tagref;
                                                        
                                                        public Double Value {get => _tagref.LastRead.Value;}
                                                        public DateTime Timestamp {get => _tagref.LastRead.Timestamp;}
                                                        public int Quality { get => _tagref.LastRead.Quality;}
                                                        
                                                        public void SetValue(Double value, int quality, DateTime timestamp,string histdest="""",lastvaluesdest="""")
                                                        {
                                                            _tagref.LastRead = new FDADataPointDefinitionStructure.Datapoint(value, quality, timestamp,histdest, DataType.UNKNOWN, DataRequest.WriteMode.Insert);
                                                        }

                                                        public Tag(FDADataPointDefinitionStructure tagref)
                                                        {
                                                            _tagref = tagref;
                                                        }
                                                    }

                                                    private class Connection
                                                    {
                                                        private ConnectionManager _connref;
                                                        public bool ConnectionEnabled {get => _connref.ConnectionEnabled; set => _connref.ConnectionEnabled = value;}
                                                        public bool CommunicationsEnabled {get => _connref.CommunicationsEnabled; set => _connref.CommunicationsEnabled = value;}
                                                        public bool CommsLogEnabled {get => _connref.CommsLogEnabled; set => _connref.CommsLogEnabled = value;}
                                                        public Connection(ConnectionManager connref)
                                                        {
                                                            _connref = connref;
                                                        }
                                                    }

                                                    private Tag GetTag(string tagid)
                                                    {
                                                        Guid tagGuid = Guid.Parse(tagid);
                                                        try
                                                        {
                                                           return new Tag(_tagsref[tagGuid]);
                                                        } catch (Exception ex)
                                                        {
                                                            throw new Exception(""Tag with ID "" + tagid + "" not found"",ex);
                                                        }
                                                    }

                                                    private Connection GetConnection(string connid)
                                                    {
                                                        Guid connuid = Guid.Parse(connid);                   
                                                        try
                                                        {
                                                           return new Connection(_connectionsref[connuid]);
                                                        } catch (Exception ex)
                                                        {
                                                            throw new Exception(""Connection with ID "" + connid + "" not found"",ex);
                                                        }
                                                    }
                                                    <USERCODE>
                                                  }";

        private Dictionary<Guid,FDADataPointDefinitionStructure> _tags;
        private Dictionary<Guid, ConnectionManager> _connMgrs;

        private string _namespaces = "";
        private readonly Dictionary<string, Module> UserCodeModules = new Dictionary<string, Module>();

        /// <summary>
        /// Load a dynamic code module (group of methods)
        /// </summary>
        /// <param name="moduleName">A unique name for referencing this module</param>
        /// <param name="userCode">C# code to be compiled and loaded</param>
        /// <returns></returns>
        public List<string> LoadModule(string moduleName, string userCode,string runspec)
        {
            Module newModule = null;
            //string userCodeMinusRunSpecs = "";
            
            //List<Tuple<string, string>> runspecs = GetRunSpecs(userCode, ref userCodeMinusRunSpecs);

            // put the user defined functions inside a class called "UserCode", with a constructor that accepts a structure containing data from the main program that the user code will have access to
            string completeCode = _namespaces + UserCodeWrapper.Replace("<USERCODE>", userCode);

            // compile that
            byte[] compiledAssembly = null;

            compiledAssembly = Compiler.Compile(completeCode);

            if (compiledAssembly != null)
            {
                using (MemoryStream asm = new MemoryStream(compiledAssembly))
                {
                    SimpleUnloadableAssemblyLoadContext assemblyLoadContext = new SimpleUnloadableAssemblyLoadContext();
                    Assembly assembly = assemblyLoadContext.LoadFromStream(asm);
                    Type type = assembly.GetType("UserCode");

                    // create a new user code module
                    try
                    {
                        newModule = new Module(moduleName, type, assemblyLoadContext, runspec);
                    } catch (Exception ex)
                    {
                        Console.WriteLine("Failed to load user code module " + moduleName + ": " + ex.Message);
                        return new List<string>();
                    }

   

                    // a module with this name already exists, unload it
                    if (UserCodeModules.ContainsKey(moduleName))
                    {
                        UnloadModule(moduleName);
                    }

                    // subscribe to execution notifications
                    newModule.UserMethodExecuted += UserModuleMethodExecuted;

                    // subscribe to user script error notifications
                    newModule.UserMethodRuntimeError += UserModuleMethodRuntimeError;

                    // add it to the dictionary of loaded modules
                    UserCodeModules.Add(moduleName, newModule);
                }

            }

            return newModule.Methods;
        }

        private void UserModuleMethodRuntimeError(string methodName, string errorMsg)
        {
            UserMethodRuntimeError?.Invoke(methodName, errorMsg);
        }

        private void UserModuleMethodExecuted(string methodName)
        {
            UserMethodExecuted?.Invoke(methodName);
        }

        /// <summary>
        /// Unload a dynamic code module (group of methods)
        /// </summary>
        /// <param name="moduleName">The name of the module to be unloaded</param>
        public void UnloadModule(string moduleName)
        {
            if (UserCodeModules.ContainsKey(moduleName))
            {
                UserCodeModules[moduleName].UserMethodExecuted -= UserModuleMethodExecuted;
                UserCodeModules[moduleName].UserMethodRuntimeError -= UserModuleMethodRuntimeError;
                UserCodeModules[moduleName].Dispose();
                UserCodeModules.Remove(moduleName);
            }
            else
            {
                throw new Exception("Failed to unload user code module '" + moduleName + ", module not found");
            }
        }

        public void UnloadAllUserModules()
        {
            foreach (Module module in UserCodeModules.Values)
            {
                module.UserMethodExecuted -= UserModuleMethodExecuted;
                module.UserMethodRuntimeError -= UserModuleMethodRuntimeError;
                module.Dispose();
            }

            UserCodeModules.Clear();
        }
    }
}
