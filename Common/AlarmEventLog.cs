using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public abstract class AlarmEventRecord : object
    {
        public abstract string GetWriteSQL();
        public abstract string GetUpdateLastRecordSQL();
        public bool Valid = true;
    }

  
}
