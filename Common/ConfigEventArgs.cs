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
        public object ID { get; }
        public string ChangeType { get; }


        public ConfigEventArgs(string changetype, string table, object itemref)
        {
            ChangeType = changetype;
            TableName = table;
            ID = itemref;
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
