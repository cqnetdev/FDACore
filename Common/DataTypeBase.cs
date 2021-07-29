using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class DataType : DataTypeBase
    {
        public static readonly DataType UNKNOWN = new("UNKNOWN", 1, typeof(byte));
        private DataType(string name, byte size, Type hostDataType) : base(name, size, hostDataType)
        {
        }
    }
        public abstract class DataTypeBase
    {
        public string Name { get; private set; }
        public byte Size { get; private set; }
        public Type HostDataType { get; private set; }

        protected DataTypeBase(string name, byte size, Type hostDataType)
        {
            Name = name;
            Size = size;
            HostDataType = hostDataType;
        }   

    }

}
