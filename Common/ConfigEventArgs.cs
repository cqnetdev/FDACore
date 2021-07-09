using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class ConfigEventArgs : EventArgs
    {
        public string TableName { get; }
        public object Item { get; }
        public object OldItem { get; }
        public string ChangeType { get; }


        public ConfigEventArgs(string changetype, string table, object itemref,object olditem=null)
        {
            ChangeType = changetype;
            TableName = table;
            Item = itemref;
            OldItem = olditem;
        }
    }

    public class BoolEventArgs : EventArgs
    {
        public bool Value;

        public BoolEventArgs(bool value)
        {
            Value = value;
        }
    }
}
