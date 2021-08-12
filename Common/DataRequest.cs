using System;
using System.Collections.Generic;

namespace Common
{
    public class DataRequest
    {
        public enum RequestStatus { Idle, Queued, Pending, Success, Error, PartialSuccess };

        public enum RequestType { Read, Write, ReadbackAfterWrite, Backfill, PreWriteRead, AlarmEventPointers, HourHistoryPtr, Alarms, Events };

        public enum WriteMode { Insert, Update };

        private DateTime _requestTimestamp;
        private DateTime _queuedTimestamp;
        private DateTime _ResponseTimestamp;
        private Byte[] _requestBytes;
        private Byte[] _responseBytes;
        private Byte[] _ackBytes;
        private string _requestString;
        private string _responseString;
        private string _ackString;
        private RequestStatus _status;
        private string _errorMessage;
        private DataTypeBase _returnDataType;
        private int _expectedResponseSize;
        private string _requestID = "";
        private object _protocolSpecificParams;
        private string _protocol;
        private List<FDADataPointDefinitionStructure> _dataPointconfig;
        private string _deviceLogin;
        private short _devicePass;
        private string _UDPIPAddr;
        public FDADevice DeviceSettings;

        public string UDPIPAddr { get => _UDPIPAddr; set => _UDPIPAddr = value; }
        public string RequestID { get => _requestID; set => _requestID = value; }
        public DateTime QueuedTimestamp { get => _queuedTimestamp; set => _queuedTimestamp = value; }
        public DateTime RequestTimestamp { get => _requestTimestamp; set => _requestTimestamp = value; }
        public DateTime ResponseTimestamp { get => _ResponseTimestamp; set => _ResponseTimestamp = value; }
        public String QueuedTime { get { if (_requestTimestamp != DateTime.MinValue) return _requestTimestamp.Subtract(_queuedTimestamp).ToString(@"mm\:ss\.fff"); else return "00:00.000"; } }
        public String TransactionTime { get { if (_ResponseTimestamp != DateTime.MinValue) return _ResponseTimestamp.Subtract(_requestTimestamp).ToString(@"mm\:ss\.fff"); else return "00:00.000"; } }
        public byte[] RequestBytes { get { return _requestBytes; } set { _requestBytes = value; _requestString = BitConverter.ToString(_requestBytes); } }
        public byte[] ResponseBytes { get { return _responseBytes; } set { _responseBytes = value; _responseString = BitConverter.ToString(_responseBytes); } }
        public byte[] AckBytes { get { return _ackBytes; } set { _ackBytes = value; _ackString = BitConverter.ToString(_ackBytes); } }
        public string Request { get => _requestString; }
        public string Response { get => _responseString; }
        public string Acknowledgement { get => _ackString; }
        public RequestStatus Status { get => _status; }
        public string ErrorMessage { get => _errorMessage; set => _errorMessage = value; }
        public List<Tag> TagList { get; set; }
        public DataTypeBase ReturnDataType { get => _returnDataType; set => _returnDataType = value; }
        public string DataTypeName { get { if (_returnDataType != null) return _returnDataType.Name; else return string.Empty; } }
        public int ExpectedResponseSize { get => _expectedResponseSize; set => _expectedResponseSize = value; }
        public int ActualResponseSize { get { if (_responseBytes != null) return _responseBytes.Length; else return 0; } }
        public string Protocol { get => _protocol; set => _protocol = value; }
        public object ProtocolSpecificParams { get => _protocolSpecificParams; set => _protocolSpecificParams = value; }

        public string Destination;
        public DataRequest ErrorCorrectionRequest { get; set; }
        public bool ErrorCorrectionAttempted = false;
        public bool SkipRequestFlag = false;

        public List<FDADataPointDefinitionStructure> DataPointConfig { get => _dataPointconfig; set => _dataPointconfig = value; }
        public string DeviceLogin { get => _deviceLogin; set => _deviceLogin = value; }
        public short DevicePass { get => _devicePass; set => _devicePass = value; }
        public RequestType MessageType { get; set; }
        public Dictionary<Guid, double> ValuesToWrite { get; set; }
        public WriteMode DBWriteMode = WriteMode.Insert;   // default to 'insert' mode (add a new record to the database historical table)
        public Guid ConnectionID;
        public Guid GroupID;
        public RequestGroup ParentTemplateGroup;
        public RequestGroup ParentGroup;
        public RequestGroup SuperPriorityRequestGroup;
        public RequestGroup NextGroup;
        public string GroupIdxNumber;
        public int GroupSize;
        public List<Tag> PreReadValues = null;
        public string NodeID;

        public DateTime BackfillStartTime = DateTime.MinValue;
        public DateTime BackfillEndTime = DateTime.MinValue;

        public Globals.RequesterType RequesterType { get; set; }
        public Guid RequesterID { get; set; }

        //public delegate void StatusChangeHandler(object sender, EventArgs e);
        //public event StatusChangeHandler StatusChange;

        public DataRequest()
        {
            TagList = new List<Tag>();
            _status = RequestStatus.Idle;

            // default to a read request, unless otherwise indicated
            MessageType = RequestType.Read;
        }

        public void SetStatus(RequestStatus status)
        {
            _status = status;
            // raise an event to notify anyone monitoring that the status of this request has changed
            //StatusChange?.Invoke(this, new EventArgs());
        }
    }
}