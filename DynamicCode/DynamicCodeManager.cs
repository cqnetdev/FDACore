
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;


namespace DynamicCode
{
    public class DynamicCodeManager
    {
        /********************* public ****************************/
        public static object UserAccessibleObjects { get => _userAccessibleObjects; set { _userAccessibleObjects = value; Module.UserAccessibleObjects = value; } }
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


        /******************** private *****************************/
        private static string UserCodeWrapper = @"using System;
                                                  using System.Collections.Generic;
                                                  <TYPES NAMESPACE>
                                                  public class UserCode
                                                  {
                                                    public static Dictionary<string, Tag> Tags;
                                                    public UserCode(Dictionary<string, Tag> tags)
                                                    {
                                                       Tags = tags;
                                                    }
                                                    <USERCODE>
                                                  }";

        private static object _userAccessibleObjects;
        private static string _namespaces = "";
        private static readonly Dictionary<string, Module> UserCodeModules = new Dictionary<string, Module>();


        /// <summary>
        /// Load a dynamic code module (group of methods)
        /// </summary>
        /// <param name="moduleName">A unique name for referencing this module</param>
        /// <param name="userCode">C# code to be compiled and loaded</param>
        /// <returns></returns>
        public static List<Tuple<string, string>> LoadModule(string moduleName, string userCode)
        {
            Module newModule = null;
            string userCodeMinusRunSpecs = "";
            List<Tuple<string, string>> runspecs = GetRunSpecs(userCode, ref userCodeMinusRunSpecs);

            // put the user defined functions inside a class called "UserCode", with a constructor that accepts a structure containing data from the main program that the user code will have access to
            string fullUserCode = UserCodeWrapper.Replace("<TYPES NAMESPACE>", _namespaces).Replace("<USERCODE>", userCodeMinusRunSpecs);

            // compile that
            byte[] compiledAssembly = null;

            compiledAssembly = Compiler.Compile(fullUserCode);


            if (compiledAssembly != null)
            {
                using (MemoryStream asm = new MemoryStream(compiledAssembly))
                {
                    SimpleUnloadableAssemblyLoadContext assemblyLoadContext = new SimpleUnloadableAssemblyLoadContext();
                    Assembly assembly = assemblyLoadContext.LoadFromStream(asm);
                    Type type = assembly.GetType("UserCode");

                    // create a new user code module
                    newModule = new Module(moduleName, type, assemblyLoadContext, runspecs);

                    newModule.UserMethodExecuted += UserModuleMethodExecuted;

                    UserCodeModules.Add(moduleName, newModule);
                }

            }

            return newModule.Methods;
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
                UserCodeModules[moduleName].Dispose();
                UserCodeModules.Remove(moduleName);
            }
            else
            {
                throw new Exception("failed to unload dynamic code module '" + moduleName + ", module not found");
            }
        }


        private static List<Tuple<string, string>> GetRunSpecs(string userCode, ref string outputUserCode)
        {
            List<Tuple<string, string>> output = new List<Tuple<string, string>>();
            char[] delims = new[] { '\r', '\n' };

            string[] userCodeLines = userCode.Split(delims, StringSplitOptions.RemoveEmptyEntries);
            string functionDefLine;
            string[] functionDefLinePieces;
            string functionName;
            string runSpec;

            for (int i = 0; i < userCodeLines.Length; i++)
            {
                if (userCodeLines[i].StartsWith("public void"))
                {
                    // we've found a function defnition
                    functionDefLine = userCodeLines[i];

                    // remove any multiple spaces
                    while (functionDefLine.Contains("  ")) functionDefLine = functionDefLine.Replace("  ", " ");

                    // break it up into words
                    functionDefLinePieces = functionDefLine.Split(' ');

                    // function name should be in element 2 (remove the braces, leaving just the name)
                    functionName = functionDefLinePieces[2].Replace("(", "").Replace(")", "");


                    // now re-split the line, using $ as the split character
                    functionDefLinePieces = functionDefLine.Split('$');

                    // first element is the function declaration, any additional elements are run specs
                    // record the run spec(s) for this function in the output
                    if (functionDefLinePieces.Length > 1)
                    {
                        for (int j = 1; j < functionDefLinePieces.Length; j++)
                        {
                            runSpec = functionDefLinePieces[j];
                            output.Add(new Tuple<string, string>(functionName, runSpec));
                        }

                        // remove the run specs from the user code, so it'll compile
                        userCodeLines[i] = functionDefLinePieces[0];
                    }

                }
            }

            // put the code back together, minus the run specs
            outputUserCode = string.Join(Environment.NewLine, userCodeLines);
            return output;
        }
    }
}
