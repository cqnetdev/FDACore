using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
namespace Common
{
    public class FDAStatus
    {
        public TimeSpan UpTime { get; set; }
        public String RunStatus { get; set; }
        public String Version { get; set; }
        public String DB { get; set; }

        public String RunMode { get; set; }

        public int TotalQueueCount { get; set; }

        public FDAStatus()
        {
            UpTime = new TimeSpan(0);
            RunStatus = "";
            Version = "";
            DB = "";
            RunMode = "";
        }

        public string JSON()
        {
            string jsonString = JsonSerializer.Serialize<FDAStatus>(this);
            return jsonString;
        }

        public static FDAStatus Deserialize(string json)
        {
            FDAStatus status;
            try
            {
                status = JsonSerializer.Deserialize<FDAStatus>(json);
            } catch
            {
                return new FDAStatus();
            }

            return status;
        }

        
       
    }
}
