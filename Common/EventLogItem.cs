using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Support;

namespace Common
{
    public class EventLogItem
    {
        public DateTime Timestamp;
        public AppEventType EventType;
        public string ObjectType;
        public string ObjectName;
        public string Description;
        public string ErrorCode;
        public string StackTrace;

        public enum AppEventType {Debug, Error, ConfigErr}

        public EventLogItem(DateTime timestamp,Exception ex,string description)
        {
            Timestamp = timestamp;
            EventType = AppEventType.Error;
            ObjectType = ex.Source;
            if (ex.Data.Contains("ObjectName"))
                ObjectName = ex.Data["ObjectName"].ToString();
            Description = description + ", exception: " + ex.Message;
            ErrorCode = "0x"+Convert.ToString(ex.HResult, 16);
            StackTrace = ex.StackTrace;
        }

        public EventLogItem(DateTime timestamp, string objectType, string objectName, string description,bool ConfigError=false)
        {
            Timestamp = timestamp;
            ObjectType = objectType;
            ObjectName = objectName;
            Description = description;
            if (ConfigError)
                EventType = AppEventType.ConfigErr;
            else
                EventType = AppEventType.Debug;
        }

        public void ToSQL(Guid executionID,StringBuilder sb,bool firstItem)
        {
            string objname;
            string desc;
            string stacktrace;

            objname = ObjectName?.Replace("'", "''");
            if (objname == null) objname = "";

            desc = Description?.Replace("'", "''");
            if (desc == null) desc = "";

            stacktrace = StackTrace?.Replace("'", "''");
            if (stacktrace == null) stacktrace = "";

            //            sb.Clear();
            //           sb.Append("INSERT INTO ");
            //            sb.Append(tablename);
            //            sb.Append(" (FDAExecutionID,Timestamp,EventType,ObjectType,ObjectName,Description,ErrorCode,StackTrace) VALUES ('");
            if (!firstItem)
                sb.Append(',');
            sb.Append("('");
            sb.Append(executionID);
            sb.Append("','");
            sb.Append(DateTimeHelpers.FormatDateTime(Timestamp));
            sb.Append("','");
            sb.Append(EventType.ToString());
            sb.Append("','");
            sb.Append(ObjectType);
            sb.Append("','");
            
            sb.Append(objname);
            sb.Append("','");
            sb.Append(desc);
            sb.Append("','");
            sb.Append(ErrorCode);
            sb.Append("','");
            sb.Append(stacktrace);
            sb.Append("')");

            /* old way, slower
            sql = "INSERT INTO " + Globals.SystemManager.GetTableName("AppLog") + " (FDAExecutionID,Timestamp,EventType,ObjectType,ObjectName,Description,ErrorCode,StackTrace) VALUES ('";
            sql += executionID + "','";
            sql += Helpers.FormatDateTime(Timestamp) + "','";
            sql += EventType.ToString() + "','";
            sql += ObjectType + "','";
            sql += ObjectName?.Replace("'","''") + "','";
            sql +=  Description?.Replace("'","''") + "','";
            sql += ErrorCode + "','";
            sql += StackTrace?.Replace("'","''") + "');";

            return sql;
            */
        }
    }
}
