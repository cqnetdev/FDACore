using System;
using System.Collections.Generic;
using System.Text;

namespace DBManager
{
    public class SQLDBManager : DBManagerBase, IDisposable
    {

        public new void Dispose()
        {
            // put SQL specific cleanup


            // call base class cleanup
            base.Dispose();

        }
    }
}
