using System;
using System.Text;

namespace Common
{

     public abstract class CommsLogItemBase
    {
        public Guid ConnectionID;
        public abstract bool ToSQL(string tablename,Guid runIdx,StringBuilder sb,bool firstItem);
        protected byte EventType = 99;

        public CommsLogItemBase(Guid connectionID)
        {
            ConnectionID = connectionID;
        }
    }

    public class AcknowledgementEvent : CommsLogItemBase
    {
        public DateTime EventTime = DateTime.MinValue;
        public TimeSpan Elapsed = TimeSpan.Zero;
        public byte[] AckBytes;
        public byte Status = 1;
        public string Message = "N/A";
        public string ProtocolNote = "";
        public int AttemptNum = 0;
        public Guid GroupID;
        public string GroupIdx;
        public int GroupSize;
        public string Protocol;
        public string DeviceAddress;


        public AcknowledgementEvent(DataRequest request,Guid connectionID,DateTime eventTime,byte[] ackBytes, Guid groupID, int groupSize, string groupIdx) : base(connectionID)
        {
            EventTime = eventTime;
            AckBytes = ackBytes;
            GroupID = groupID;
            GroupIdx = groupIdx;
            GroupSize = groupSize;
            EventType = 2;
            ProtocolNote = "Acknowledgement";
            Protocol = request.Protocol;
            if (request.Protocol.ToUpper() == "BSAPUDP")
                DeviceAddress = request.UDPIPAddr;
            else
                DeviceAddress = request.NodeID;
        }

        public override bool ToSQL(string tablename, Guid executionID, StringBuilder sb, bool firstItem)
        {
            try
            {
                Int64 elapsed = 0; // Convert.ToInt64(ResponseTimestamp.Subtract(RequestTimestamp).TotalMilliseconds);
                if (!firstItem)
                    sb.Append(",");

                //  (FDAExecutionID, connectionID, DeviceAddress, Attempt, TimestampUTC1, TimestampUTC2, ElapsedPeriod, TransCode, TransStatus, ApplicationMessage,DBRGUID,DBRGIdx,DBRGSize,Details01,TxSize,Details02,RxSize,ProtocolNote,Protocol) values ");

                sb.Append("('");
                sb.Append(executionID);  
                sb.Append("','");
                sb.Append(ConnectionID.ToString());
                sb.Append("','");
                sb.Append(DeviceAddress);
                sb.Append("',");
                sb.Append(AttemptNum);
                sb.Append(",'");
                sb.Append(Helpers.FormatDateTime(EventTime));
                sb.Append("','");
                sb.Append(Helpers.FormatDateTime(EventTime));
                sb.Append("',");
                sb.Append(elapsed);
                sb.Append(",");
                sb.Append(EventType);
                sb.Append(",CAST(1 as bit),");
                sb.Append("'','"); // app message
                sb.Append(GroupID);
                sb.Append("','");
                sb.Append(GroupIdx);
                sb.Append("',");
                sb.Append(GroupSize);
                sb.Append(",'Tx: ");
                sb.Append(BitConverter.ToString(AckBytes));
                sb.Append("',");
                sb.Append(AckBytes.Length);
                sb.Append(",'N/A',0,'");
                sb.Append(ProtocolNote);
                sb.Append("','");
                sb.Append(Protocol);
                sb.Append("')");
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while adding a comms log comms transaction item to an SQL insert batch");
                return false;
            }
            return true;
        }

    }
    

    public class CommsConnectionEventLogItem : CommsLogItemBase
    {
        public DateTime StartTime = DateTime.MinValue;
        public TimeSpan Elapsed = TimeSpan.Zero;
        public byte Status = 99;
        public string Message = "N/A";
        public int Attempt = 0;

        public CommsConnectionEventLogItem(Guid connectionID, int attemptNumber, DateTime startTime, TimeSpan elapsed, byte status, string message) : base(connectionID)
        {
            StartTime = startTime;
            Elapsed = elapsed;
            Status = status;
            Message = message;
            EventType = 0;
            Attempt = attemptNumber;
        }

        public override bool ToSQL(string tablename, Guid executionID,StringBuilder sb,bool firstItem)
        {
            //  (FDAExecutionID, connectionID, DeviceAddress, Attempt, TimestampUTC1, TimestampUTC2, ElapsedPeriod, TransCode, TransStatus, ApplicationMessage,DBRGUID,DBRGIdx,DBRGSize,Details01,TxSize,Details02,RxSize,ProtocolNote,Protocol) values ");
            try
            {
                DateTime endTime = StartTime.Add(Elapsed);
                if (!firstItem)
                    sb.Append(",");
                sb.Append("('");
                sb.Append(executionID);
                sb.Append("','");
                sb.Append(ConnectionID.ToString());
                sb.Append("','',");
                sb.Append(Attempt);
                sb.Append(",'");
                sb.Append(Helpers.FormatDateTime(StartTime));
                sb.Append("','");
                sb.Append(Helpers.FormatDateTime(endTime));
                sb.Append("',");
                sb.Append(Elapsed.TotalMilliseconds);
                sb.Append(",");
                sb.Append(EventType);
                sb.Append(",");
                sb.Append("cast(" + Status + " as bit)");
                sb.Append(",'");
                sb.Append(Message.Replace("'", "''"));
                sb.Append("','00000000-0000-0000-0000-000000000000','0',0,'N/A',0,'N/A',0,'N/A','N/A')");
            } catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while adding a comms log connection event to an SQL insert batch");
                return false;
            }

            return true;
        }

    }

    public class CommsEventLogItem : CommsLogItemBase
    {
        public DateTime Timestamp;
        public string Message;

        public CommsEventLogItem(Guid connectionID,DateTime timestamp,string message,byte TransCode=0) : base(connectionID)
        {
            Timestamp = timestamp;
            Message = message;
            EventType = TransCode;
        }

        public override bool ToSQL(string tablename,Guid executionID,StringBuilder sb,bool firstItem)
        {
            //  (FDAExecutionID, connectionID, DeviceAddress, Attempt, TimestampUTC1, TimestampUTC2, ElapsedPeriod, TransCode, TransStatus, ApplicationMessage,DBRGUID,DBRGIdx,DBRGSize,Details01,TxSize,Details02,RxSize,ProtocolNote,Protocol) values ");

            try
            {
                string timestampStr = Helpers.FormatDateTime(Timestamp);

                if (!firstItem)
                    sb.Append(",");
                sb.Append("('");
                sb.Append(executionID);
                sb.Append("','");
                sb.Append(ConnectionID.ToString());
                sb.Append("','',0,'");
                sb.Append(timestampStr);
                sb.Append("','");
                sb.Append(timestampStr);
                sb.Append("',0,");
                sb.Append(EventType);
                sb.Append(",cast(1 as bit),'");
                sb.Append(Message.Replace("'", "''"));
                sb.Append("','00000000-0000-0000-0000-000000000000','0',0,'N/A',0,'N/A',0,'N/A','N/A')");
            } catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while adding a comms log comms event item to an SQL insert batch");
                return false;
            }
            return true;
        }
    }


    public class TransactionLogItem : CommsLogItemBase
    {
        public DateTime RequestTimestamp;
        public DateTime ResponseTimestamp;
        public byte[] RequestBytes;
        public byte[] ResponseBytes;
        public int AttemptNum;
        public DataRequest.RequestStatus ResultStatus;
        public DataRequest.RequestType MessageType;
        public Guid GroupID;
        public string GroupIdx;
        public int GroupSize;
        public string Details="";
        public int TransType;
        public string ApplicationMessage;
        public string Protocol;
        public string DeviceAddress;
                   
           


        public TransactionLogItem(DataRequest request,int attemptNum,Guid connectionID, Guid groupID, int groupSize, string groupIdx,string AppMsg="N/A") : base(connectionID)
        {
            RequestTimestamp = request.RequestTimestamp;
            ResponseTimestamp = request.ResponseTimestamp;
            RequestBytes = request.RequestBytes;
            ResponseBytes = request.ResponseBytes;
            if (request.Protocol.ToUpper() == "BSAPUDP")
                DeviceAddress = request.UDPIPAddr;
            else
                DeviceAddress = request.NodeID;
            AttemptNum = attemptNum;
            ConnectionID = connectionID;
            GroupID = groupID;
            GroupSize = groupSize;
            GroupIdx = groupIdx;
            ResultStatus = request.Status;
            TransType = (int)request.MessageType;
            Details = request.ErrorMessage; // aka protocol note
            Protocol = request.Protocol;
            EventType = 1;
            ApplicationMessage = AppMsg;
        }

        public override bool ToSQL(string tablename,Guid executionID,StringBuilder sb,bool firstItem)
        {
            try
            {
                Int64 elapsed = Convert.ToInt64(ResponseTimestamp.Subtract(RequestTimestamp).TotalMilliseconds);
                if (!firstItem)
                    sb.Append(",");

                //  (FDAExecutionID, connectionID, DeviceAddress, Attempt, TimestampUTC1, TimestampUTC2, ElapsedPeriod, TransCode, TransStatus, ApplicationMessage,DBRGUID,DBRGIdx,DBRGSize,Details01,TxSize,Details02,RxSize,ProtocolNote,Protocol) values ");

                sb.Append("('");
                sb.Append(executionID);
                sb.Append("','");
                sb.Append(ConnectionID.ToString());
                sb.Append("','");
                sb.Append(DeviceAddress);
                sb.Append("',");
                sb.Append(AttemptNum);
                sb.Append(",'");
                sb.Append(Helpers.FormatDateTime(RequestTimestamp));
                sb.Append("','");
                sb.Append(Helpers.FormatDateTime(ResponseTimestamp));
                sb.Append("',");
                sb.Append(elapsed);
                sb.Append(",");
                sb.Append(EventType);
                sb.Append(",");
                if (ResultStatus == DataRequest.RequestStatus.Success)
                    sb.Append("cast(1 as bit),");
                else
                    sb.Append("cast(0 as bit),");
                sb.Append("'");
                sb.Append(ApplicationMessage);
                sb.Append("','");
                sb.Append(GroupID);
                sb.Append("','");
                sb.Append(GroupIdx);
                sb.Append("',");
                sb.Append(GroupSize);
                sb.Append(",'Tx: ");
                sb.Append(BitConverter.ToString(RequestBytes));
                sb.Append("',");
                sb.Append(RequestBytes.Length);
                if (ResponseBytes != null)
                {
                    sb.Append(",'Rx: ");
                    sb.Append(BitConverter.ToString(ResponseBytes));
                    sb.Append("',");
                    sb.Append(ResponseBytes.Length);
                    sb.Append(",'");
                }
                else
                {
                    sb.Append(",'N/A',0,'");
                }
                sb.Append(Details.Replace("'", "''"));
                sb.Append("','");
                sb.Append(Protocol);
                sb.Append("')");
            } catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while adding a comms log comms transaction item to an SQL insert batch");
                return false;
            }
            return true;
        }
    }
}
