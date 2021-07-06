using System;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    public class TransactionEventArgs : EventArgs
    {
        private readonly DataRequest _requestRef;

        public TransactionEventArgs(DataRequest request)
        {
            _requestRef = request;
        }

        public DataRequest RequestRef
        {
            get { return _requestRef; }
        }



        public string Message
        {
            get { return Message; }
        }

    }
}
