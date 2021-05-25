
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Common;
using FDA;

namespace DynamicCode
{
    public static class DynamicCodeManager 
    {
        /********************* public ****************************/
        public static Dictionary<Guid, FDADataPointDefinitionStructure> Tags { get => _tags; set { _tags = value; Module.Tags = value; } }
        public static Dictionary<Guid, ConnectionManager> ConnMgrs { get => _connMgrs; set { _connMgrs = value; Module.ConnMgrs = value; } }
        public static string[] NameSpaces { 
            set {
                  string temp = "";
                   foreach (string ns in value) temp += "using " + ns + ";";
                  _namespaces = temp;
                } 
            }
        public static List<string> LoadedModules { get { return UserCodeModules.Keys.ToList<string>(); } }

        public delegate void UserMethodExecuteHandler(string methodName);
        public static event UserMethodExecuteHandler UserMethodExecuted;

        public delegate void UserMethodRuntimeErrorHandler(string methodName, string errorMsg);
        public static event UserMethodRuntimeErrorHandler UserMethodRuntimeError;

        /******************** private *****************************/
        private static string UserCodeWrapper = @"public class UserCode
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
                                                        
                                                        public void SetValue(Double value, int quality, DateTime timestamp,string destination="""")
                                                        {
                                                            _tagref.LastRead = new FDADataPointDefinitionStructure.Datapoint(value, quality, timestamp,destination, DataType.UNKNOWN, DataRequest.WriteMode.Insert);
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

        private static Dictionary<Guid,FDADataPointDefinitionStructure> _tags;
        private static Dictionary<Guid, ConnectionManager> _connMgrs;

        private static string _namespaces = "";
        private static readonly Dictionary<string, Module> UserCodeModules = new Dictionary<string, Module>();


        /// <summary>
        /// Load a dynamic code module (group of methods)
        /// </summary>
        /// <param name="moduleName">A unique name for referencing this module</param>
        /// <param name="userCode">C# code to be compiled and loaded</param>
        /// <returns></returns>
        public static List<string> LoadModule(string moduleName, string userCode,string runspec)
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

        private static void UserModuleMethodRuntimeError(string methodName, string errorMsg)
        {
            UserMethodRuntimeError?.Invoke(methodName, errorMsg);
        }

        private static void UserModuleMethodExecuted(string methodName)
        {
            UserMethodExecuted?.Invoke(methodName);
        }

        /// <summary>
        /// Unload a dynamic code module (group of methods)
        /// </summary>
        /// <param name="moduleName">The name of the module to be unloaded</param>
        public static void UnloadModule(string moduleName)
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

        public static void UnloadAllUserModules()
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
