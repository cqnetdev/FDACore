using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Common
{
    public class Tag
    {
        public string DeviceTagAddress { get; set; }
        public string DeviceTagName { get; set; }
        public Guid TagID { get; set; }
        public object Value { get; set; } 
        public DateTime Timestamp { get; set; }
        public int Quality { get; set; }
        public DateTime LastRead { get; set;}
        public DataTypeBase ProtocolDataType { get; set;}
        public bool SwapBytes = false;
        public bool SwapWords = false;

     
        public Tag(Guid ID)
        {
            TagID = ID;
            Timestamp = DateTime.MinValue;
            ProtocolDataType = null;
            Value = null;
            Quality = 0;
            LastRead = DateTime.MinValue;
        }

    }
}
