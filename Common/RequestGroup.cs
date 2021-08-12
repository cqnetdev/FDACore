using System;
using System.Collections.Generic;

namespace Common
{
    public class RequestGroup : ICloneable
    {
        public enum ValidationState { Valid, Invalid, Unvalidated };

        public ValidationState Validation;

        public Guid ID;
        public string Description;
        public DateTime QueuedTimestamp;
        public FDADataBlockRequestGroup DBGroupRequestConfig;
        public Guid ConnectionID;
        public Guid BackupConnectionID;
        public string DestinationID;
        public List<DataRequest> ProtocolRequestList;
        public DateTime CommsInitiatedTimestamp;
        public DateTime CommsCompletedTimestamp;
        public int Priority;
        public bool CommsLogEnabled;
        public Dictionary<Guid, FDADataPointDefinitionStructure> TagsRef;
        public bool Enabled;
        public Globals.RequesterType RequesterType;
        public Guid RequesterID;
        public string Protocol;
        public Dictionary<Guid, Double> writeLookup;
        public int RequeueCount = 0;
        public bool containsFailedReq = false;

        public DateTime BackfillStartTime = DateTime.MinValue;
        public DateTime BackfillEndTime = DateTime.MinValue;
        public int BackfillStep = 1;

        public RequestGroup()
        {
            ProtocolRequestList = new List<DataRequest>();
            Validation = ValidationState.Unvalidated;
        }

        public Object Clone()
        {
            RequestGroup groupCopy = new()
            {
                ID = this.ID,
                Description = this.Description,
                ConnectionID = this.ConnectionID,
                DestinationID = this.DestinationID,
                Priority = this.Priority,
                CommsLogEnabled = this.CommsLogEnabled,
                DBGroupRequestConfig = (FDADataBlockRequestGroup)DBGroupRequestConfig.Clone(),
                TagsRef = this.TagsRef,
                Enabled = this.Enabled,
                Protocol = this.Protocol,
                Validation = this.Validation,
            };

            return groupCopy;
        }
    }
}