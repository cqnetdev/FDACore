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
        public Guid ID { get; }
        public string ChangeType { get; }


        public ConfigEventArgs(string changetype, string table, Guid item)
        {
            ChangeType = changetype;
            TableName = table;
            ID = item;
        }
    }
}
