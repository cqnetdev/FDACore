using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Common
{

    public class TableAlias
    {
        string DefaultTableName { get; set; }
        string Alias { get; set; }
    }

    public class FDADataBlockRequestGroup : ICloneable
    {
        public Guid DRGUID { get; set; }
        public string Description { get; set; }
        public bool DRGEnabled { get; set; }
        public string DPSType { get; set; }
        public bool CommsLogEnabled { get; set; }
        //public string DataPointBlockRequestListUIds { get; set; }
        public string DataPointBlockRequestListVals { get; set; }


        public object Clone()
        {
            FDADataBlockRequestGroup copy = new FDADataBlockRequestGroup()
            {
                DRGUID = DRGUID,
                Description = Description,
                DRGEnabled = DRGEnabled,
                DPSType = DPSType,
                CommsLogEnabled = CommsLogEnabled,
                //DataPointBlockRequestListUIds = DataPointBlockRequestListUIds,
                DataPointBlockRequestListVals = DataPointBlockRequestListVals
            };
            return copy;
        }
    }

    public class FDARequestGroupDemand
    {
        public Guid FRGDUID { get; set; }
        public bool FRGDEnabled { get; set; }
        public string Description { get; set; }
        public DateTime UTCTimeStamp { get; set; }
        public bool DestroyDRG { get; set; }
        public bool DestroyFRGD { get; set; }
        public int Priority { get; set; }
        public string RequestGroupList { get; set; }
        public bool CommsLogEnabled { get; set; }
 
    }

    public class FDARequestGroupScheduler
    {
        public Guid FRGSUID { get; set; }
        public string Description { get; set; }
        public bool FRGSEnabled { get; set; }
        public string FRGSType { get; set; }
        public int RealTimeRate { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int Second { get; set; }
        public int Priority { get; set; }
        public string RequestGroupList { get; set; }
        public List<RequestGroup> RequestGroups;
        public List<FDATask> Tasks;

        public FDARequestGroupScheduler()
        {
            RequestGroups = new List<RequestGroup>();
            Tasks = new List<FDATask>();
        }

    }

    public class FDADataPointDefinitionStructure : SubscriptionManager.SubscribeableObject
    {
        public Guid DPDUID { get => _DPDUID; set { if (_DPDUID != value) { _DPDUID = value; base.ID = value.ToString(); NotifyPropertyChanged(); } } }
        public Boolean DPDSEnabled { get => _DPDSEnabled; set { if (_DPDSEnabled != value) { _DPDSEnabled = value; NotifyPropertyChanged(); } } }
        public string DPSType { get => _dPSType; set { if (_dPSType != value) { _dPSType = value; NotifyPropertyChanged(); } } }
        public Boolean read_scaling { get => _read_scaling; set { if (_read_scaling != value) { _read_scaling = value; NotifyPropertyChanged(); } } }

        public Double read_scale_raw_low { get => _read_scale_raw_low; set { if (_read_scale_raw_low != value) { _read_scale_raw_low = value; NotifyPropertyChanged(); } }  }
        public Double read_scale_raw_high { get => _read_scale_raw_high; set { if (_read_scale_raw_high != value) { _read_scale_raw_high = value; NotifyPropertyChanged(); } } }
        public Double read_scale_eu_low { get => _read_scale_eu_low; set => _read_scale_eu_low = value; }

        public Double read_scale_eu_high { get => _read_scale_eu_high; set { if ( _read_scale_eu_high != value) { _read_scale_eu_high = value; NotifyPropertyChanged(); }} }
        public Boolean write_scaling { get => _write_scaling; set { if ( _write_scaling != value) { _write_scaling = value; NotifyPropertyChanged(); }} }
        public Double write_scale_raw_low { get => _write_scale_raw_low; set { if ( _write_scale_raw_low != value) { _write_scale_raw_low = value; NotifyPropertyChanged(); }} }
        public Double write_scale_raw_high { get => _write_scale_raw_high; set { if ( _write_scale_raw_high != value) { _write_scale_raw_high = value; NotifyPropertyChanged(); }} }
        public Double write_scale_eu_low { get => _write_scale_eu_low; set { if ( _write_scale_eu_low != value) { _write_scale_eu_low = value; NotifyPropertyChanged(); }} }
        public Double write_scale_eu_high { get => _write_scale_eu_high; set { if ( _write_scale_eu_high != value) { _write_scale_eu_high = value; NotifyPropertyChanged(); }} }
        public Boolean backfill_enabled { get => _backfill_enabled; set { if ( _backfill_enabled != value) { _backfill_enabled = value; NotifyPropertyChanged(); }} }
        public int backfill_data_ID { get => _backfill_data_ID; set { if ( _backfill_data_ID != value) { _backfill_data_ID = value; NotifyPropertyChanged(); }} }
        public double LastReadDataValue { get => _lastReadDataValue; set { if (_lastReadDataValue != value) { _lastReadDataValue = value; NotifyPropertyChanged("LastReadDataValue",0,new string[] { "LastReadDataTimestamp", "LastReadQuality" }); }} }
        public DateTime LastReadDataTimestamp { get => _lastReadDataTimestamp; set { if (_lastReadDataTimestamp != value) { _lastReadDataTimestamp = value; NotifyPropertyChanged("LastReadDataTimestamp",0, new string[] { "LastReadDataValue", "LastReadQuality" }); } } }
        public int LastReadQuality { get => _lastReadQuality; set { if (_lastReadQuality != value) { _lastReadQuality = value; NotifyPropertyChanged("LastReadQuality", 0, new string[] { "LastReadDataTimestamp","LastReadDataValue" }); } }}
        public int backfill_data_structure_type { get => _backfill_data_structure_type; set { if ( _backfill_data_structure_type != value) { _backfill_data_structure_type = value; NotifyPropertyChanged(); }} }
        public double backfill_data_lapse_limit { get => _backfill_data_lapse_limit; set { if ( _backfill_data_lapse_limit != value) { _backfill_data_lapse_limit = value; NotifyPropertyChanged(); }} }
        public double backfill_data_interval { get => _backfill_data_interval; set { if ( _backfill_data_interval != value) { _backfill_data_interval = value; NotifyPropertyChanged(); }} }
        public DateTime PreviousTimestamp { get => _previousTimestamp; set { if ( _previousTimestamp != value) { _previousTimestamp = value; NotifyPropertyChanged(); }} }
 
        public String DeviceTagName { get => _deviceTagName; set => _deviceTagName = value; }

        public String DeviceTagAddress { get => _deviceTagAddress; set => _deviceTagAddress = value; }


        private Guid _DPDUID;
        private double _lastReadDataValue;
        private DateTime _lastReadDataTimestamp;
        private int _lastReadQuality;
        private bool _DPDSEnabled;
        private string _dPSType;
        private bool _read_scaling;
        private double _read_scale_raw_low;
        private double _read_scale_raw_high;
        private double _read_scale_eu_low;
        private double _read_scale_eu_high;
        private bool _write_scaling;
        private double _write_scale_raw_low;
        private double _write_scale_raw_high;
        private double _write_scale_eu_low;
        private double _write_scale_eu_high;
        private bool _backfill_enabled;
        private int _backfill_data_ID;
        private int _backfill_data_structure_type;
        private double _backfill_data_lapse_limit;
        private double _backfill_data_interval;
        private DateTime _previousTimestamp;
        private string _deviceTagName = "";
        private string _deviceTagAddress = "-1";

        public FDADataPointDefinitionStructure()
        {
            base.ObjectType = "Tag";
        }
    }

    public class FDASourceConnection
    {
        public Guid SCUID { get; set; }
        public bool ConnectionEnabled { get; set; }
        public bool CommunicationsEnabled { get; set; }
        public bool CommsLogEnabled { get; set; }
        public string SCType { get; set; }
        public string SCDetail01 { get; set; }
        public string SCDetail02 { get; set; }
        public string Description { get; set; }
        public int RequestRetryDelay { get; set; }
        public int SocketConnectionAttemptTimeout { get; set; }
        public int MaxSocketConnectionAttempts { get; set; }
        public int SocketConnectionRetryDelay { get; set; }
        public int PostConnectionCommsDelay { get; set; }
        public int InterRequestDelay { get; set; }
        public int MaxRequestAttempts { get; set; }
        public int RequestResponseTimeout { get; set; }
    }

    public class FDADevice
    {
        public Guid device_id { get; set; }
        public int request_timeout { get; set;}
        public int max_request_attempts { get; set;}
        public int inter_request_delay { get; set; }
        public int request_retry_delay { get; set; }
    }

    public class FDATask
    {
        public Guid task_id { get; set; }
        public string task_type { get; set; }
        public string task_details { get; set; }
    }

    public class FDAConfig
    {
        public string OptionName { get; set; }
        public string OptionValue { get; set; }
        public int ConfigType {get; set; }
    }


    public class RocDataTypes
    {
        public int PointType { get; set; }
        public int Parm { get; set; }
        public string DataType { get; set; }
        public string DescShort { get; set; }
        public string DescLong { get; set; }
        public string Key { get { return PointType + ":" + Parm; } }
    }


    public class RocEventFormats
    {
        public int PointType { get; set; }
        public int Format { get; set; }
        public string DescShort { get; set; }
        public string DescLong { get; set; }
    }
}