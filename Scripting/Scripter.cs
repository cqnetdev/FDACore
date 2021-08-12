using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Scripting
{
    public static class Scripter
    {
        // private
        private static readonly Dictionary<string, UserScript> _scripts = new();

        // internal
        internal static ScriptInterface _scriptInterface = new();

        internal static List<string> _imports = new();
        internal static List<string> _references = new();

        // public
        public delegate void CompileErrorHandler(string scriptID, List<string> errors);

        public static event CompileErrorHandler CompileError;

        public delegate void ScriptTriggeredHandler(string scriptID);

        public static event ScriptTriggeredHandler ScriptTriggered;

        public delegate void RuntimeErrorHandler(string scriptID, string error);

        public static event RuntimeErrorHandler RunTimeError;

        private static bool Enabled = false;

        static Scripter()
        {
            _references.Add("Microsoft.CSharp.dll");
        }

        public static bool IsEnabled()
        {
            return Enabled;
        }

        public static void SetEnabled(bool enb)
        {
            Enabled = enb;
        }

        public static void AddScriptableObject(ScriptableObject obj)
        {
            _scriptInterface.AddObject(obj);
        }

        public static void AddScriptableObject(IEnumerable<ScriptableObject> objs)
        {
            _scriptInterface.AddObjects(objs);
        }

        public static void AddNamespace(string ns)
        {
            _imports.Add(ns);
        }

        public static void AddNamespace(IEnumerable<string> ns)
        {
            _imports.AddRange(ns);
        }

        public static void AddReference(string lib)
        {
            _references.Add(lib);
        }

        public static void AddReference(IEnumerable<string> libs)
        {
            _references.AddRange(libs);
        }

        public static ImmutableArray<Diagnostic> CheckScript(string code)
        {
            Script _script = CSharpScript.Create(code, ScriptOptions.Default.AddReferences(_references).WithImports(_imports), typeof(ScriptInterface));

            ImmutableArray<Diagnostic> result = _script.Compile();

            return result;
        }

        public static void LoadScript(string id, string code, bool enabled = false, string runspec = "")
        {
            string completeCode = code;

            UserScript newScript;

            newScript = new UserScript(id, completeCode, runspec, true) { Enabled = enabled };
            newScript.CompileError += HandleCompileError;
            newScript.ScriptExecuted += HandleScriptExecutedEvent;
            newScript.ScriptError += HandleScriptErrorEvent;
            _scripts.Add(id, newScript);
        }

        public static void UnloadScript(string id)
        {
            if (_scripts.ContainsKey(id))
            {
                _scripts[id].CompileError -= HandleCompileError;
                _scripts[id].ScriptExecuted -= HandleScriptExecutedEvent;
                _scripts[id].ScriptError -= HandleScriptErrorEvent;
                _scripts[id].Dispose();
                _scripts.Remove(id);
            }
        }

        private static void HandleScriptErrorEvent(string scriptID, Exception ex)
        {
            RunTimeError?.Invoke(scriptID, ex.GetBaseException().Message);
        }

        private static void HandleScriptExecutedEvent(string scriptID)
        {
            ScriptTriggered?.Invoke(scriptID);
        }

        private static void HandleCompileError(string scriptid, System.Collections.Immutable.ImmutableArray<Diagnostic> errors)
        {
            List<string> errorMsgs = new();
            foreach (Diagnostic diag in errors)
            {
                errorMsgs.Add(diag.ToString());
            }
            CompileError?.Invoke(scriptid, errorMsgs);
        }

        public static UserScript GetScript(string ID)
        {
            if (_scripts.ContainsKey(ID))
                return _scripts[ID];
            else
                return null;
        }

        public static void Cleanup()
        {
            foreach (UserScript script in _scripts.Values)
            {
                script.Dispose();
            }
            _scripts.Clear();
        }
    }
}