using System;
using System.Collections.Generic;
using System.Text;

namespace Scripting
{
    public class ScriptInterface
    {
        Dictionary<string, ScriptableObject> _scriptables = new Dictionary<string, ScriptableObject>();
       
        public dynamic Get(string ID)
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