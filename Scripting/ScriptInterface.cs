﻿using System;
using System.Collections.Generic;

namespace Scripting
{
    public class ScriptInterface
    {
        private Dictionary<string, ScriptableObject> _scriptables = new();

        public dynamic GetTag(string ID)
        {
            return Get(ID);
        }

        public dynamic GetConnection(string ID)
        {
            return Get(ID);
        }

        internal dynamic Get(string ID)
        {
            ID = ID.ToLower();
            if (_scriptables.ContainsKey(ID))
            {
                return _scriptables[ID];
            }
            else
                throw new Exception("The requested object '" + ID + "' was not found");
        }

        public void AddObject(ScriptableObject obj)
        {
            if (!_scriptables.ContainsKey(obj.ID))
                _scriptables.Add(obj.ID, obj);
        }

        public void AddObjects(IEnumerable<ScriptableObject> objs)
        {
            foreach (ScriptableObject obj in objs)
            {
                if (!_scriptables.ContainsKey(obj.ID))
                    _scriptables.Add(obj.ID, obj);
            }
        }
    }
}