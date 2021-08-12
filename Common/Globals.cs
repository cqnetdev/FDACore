using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using uPLibrary.Networking.M2Mqtt;

namespace Common
{
    //disable warning about non-readonly static fields (that's the whole purpose of this class)
#pragma warning disable CA2211

    public static class Globals
    {
        /* not .NET Core compatible
        //[System.Security.SuppressUnmanagedCodeSecurity, System.Runtime.InteropServices.DllImport("kernel32.dll")]
        //static extern void GetSystemTimePreciseAsFileTime(out PreciseTime pFileTime);
        */

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct PreciseTime
        {
            public const long FILETIME_TO_DATETIMETICKS = 504911232000000000;   // 146097 = days in 400 year Gregorian calendar cycle. 504911232000000000 = 4 * 146097 * 86400 * 1E7
            public uint TimeLow;    // least significant digits
            public uint TimeHigh;   // most significant digits
            public long TimeStamp_FileTimeTicks { get { return TimeHigh * 4294967296 + TimeLow; } }     // ticks since 1-Jan-1601 (1 tick = 100 nanosecs). 4294967296 = 2^32
            public DateTime Timestamp { get { return new DateTime(TimeStamp_FileTimeTicks + FILETIME_TO_DATETIMETICKS); } }
        }

        public enum ConnStatus { Disconnected, ConnectionRetry_Delay, Connected_Ready, Connecting, Connected_Delayed }

        public readonly static List<string> SupportedProtocols = new(new string[] { "ROC", "MODBUS", "MODBUSTCP", "ENRONMODBUS", "BSAP", "BSAPUDP", "OPC" });

        public static bool RunConsoleCommand(string command, string args, string workingDir = "")
        {
            try
            {
                var processStartInfo = new ProcessStartInfo()
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                };

                if (workingDir != "")
                    processStartInfo.WorkingDirectory = workingDir;

                var process = new Process
                {
                    StartInfo = processStartInfo
                };

                process.Start();
                //string output = process.StandardOutput.ReadToEnd();
                //string error = process.StandardError.ReadToEnd();

                //process.WaitForExit();
            }
            catch
            {
                return false;
            }
            return true;

            //if (string.IsNullOrEmpty(error)) { return output; }
            //else { return error; }
        }

        public static DateTime FDANow()
        {
            /* not .NET Core Compatible
            PreciseTime ft;
            GetSystemTimePreciseAsFileTime(out ft);
            */
            return DateTime.UtcNow.AddHours(UTCOffset);
        }

        public static bool DetailedMessaging = false;

        public enum AppState { Starting, Normal, ShuttingDown, Pausing, Paused, Stopped };

        private static AppState _FDAStatus;
        public static AppState FDAStatus { get { return _FDAStatus; } set { _FDAStatus = value; /*ReportFDAStatus(value);*/ } }

        //public static bool ShuttingDown = false;

        public static bool ConsoleMode = false;

        public static string FDAVersion;

        public static bool FDAIsElevated;

        public static DateTime ExecutionTime = DateTime.MinValue;

        public static DateTime SQLMinDate = new(1900, 1, 1, 0, 0, 0);

        public static FDASystemManager SystemManager { get; set; }

        public static Object DBManager { get; set; }

        public static MqttClient MQTT { get; set; }

        public static bool MQTTEnabled = false;

        public static int UTCOffset = 0;

        public enum RequesterType { Schedule, Demand, System }

        [Serializable]
        // a serializable structure containing the information about a connection
        // used for passing connection information to MQTT
        public class ConnDetails : ISerializable, INotifyPropertyChanged
        {
            private string _ID;
            private bool _connEnabled;
            private bool _commsEnabled;
            private string _connectionType;
            private long _lastComms;
            private int _requestRetryDelay;
            private int _socketConnectionAttemptTimeout;
            private int _maxSocketConnectionAttempts;
            private int _socketConnectionRetryDelay;
            private int _postConnectionCommsDelay;
            private int _interRequestDelay;
            private int _maxRequestAttempts;
            private int _requestResponseTimeout;
            private string _connectionStatus;
            private bool _idleDisconnect;
            private int _idleDisconnectTime;
            private string _description;
            private DateTime _lastCommsDT;

            public string ID { get { return _ID; } set { _ID = value; NotifyPropertyChanged(); } }
            public bool ConnectionEnabled { get { return _connEnabled; } set { _connEnabled = value; NotifyPropertyChanged(); } }
            public bool CommunicationsEnabled { get { return _commsEnabled; } set { _commsEnabled = value; NotifyPropertyChanged(); } }
            public string ConnectionType { get { return _connectionType; } set { _connectionType = value; NotifyPropertyChanged(); } }
            public long LastCommsTime { get { return _lastComms; } set { _lastComms = value; NotifyPropertyChanged(); } }
            public int RequestRetryDelay { get { return _requestRetryDelay; } set { _requestRetryDelay = value; NotifyPropertyChanged(); } }
            public int SocketConnectionAttemptTimeout { get { return _socketConnectionAttemptTimeout; } set { _socketConnectionAttemptTimeout = value; NotifyPropertyChanged(); } }
            public int MaxSocketConnectionAttempts { get { return _maxSocketConnectionAttempts; } set { _maxSocketConnectionAttempts = value; NotifyPropertyChanged(); } }
            public int SocketConnectionRetryDelay { get { return _socketConnectionRetryDelay; } set { _socketConnectionRetryDelay = value; NotifyPropertyChanged(); } }
            public int PostConnectionCommsDelay { get { return _postConnectionCommsDelay; } set { _postConnectionCommsDelay = value; NotifyPropertyChanged(); } }
            public int InterRequestDelay { get { return _interRequestDelay; } set { _interRequestDelay = value; NotifyPropertyChanged(); } }
            public int MaxRequestAttempts { get { return _maxRequestAttempts; } set { _maxRequestAttempts = value; NotifyPropertyChanged(); } }
            public int RequestResponseTimeout { get { return _requestResponseTimeout; } set { _requestResponseTimeout = value; NotifyPropertyChanged(); } }
            public String ConnectionStatus { get { return _connectionStatus; } set { _connectionStatus = value; NotifyPropertyChanged(); } }
            public bool IdleDisconnect { get { return _idleDisconnect; } set { _idleDisconnect = value; NotifyPropertyChanged(); } }
            public int IdleDisconnectTime { get { return _idleDisconnectTime; } set { _idleDisconnectTime = value; NotifyPropertyChanged(); } }
            public string Description { get { return _description; } set { _description = value; NotifyPropertyChanged(); } }
            public DateTime LastCommsDT { get { return _lastCommsDT; } set { _lastCommsDT = value; NotifyPropertyChanged(); } }

            public event PropertyChangedEventHandler PropertyChanged;

            public ConnDetails()
            {
            }

            protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public void Update(string propertyName, byte[] valueBytes)
            {
                switch (propertyName)
                {
                    case "id": ID = Encoding.UTF8.GetString(valueBytes); break;
                    case "connectionenabled": ConnectionEnabled = BitConverter.ToBoolean(valueBytes, 0); break;
                    case "communicationsenabled": CommunicationsEnabled = BitConverter.ToBoolean(valueBytes, 0); break;
                    case "connectiontype": ConnectionType = Encoding.UTF8.GetString(valueBytes); break;
                    case "lastcommstime": long ticks = BitConverter.ToInt64(valueBytes, 0); LastCommsTime = ticks; LastCommsDT = new DateTime(ticks); break;
                    case "requestretrydelay": RequestRetryDelay = BitConverter.ToInt32(valueBytes, 0); break;
                    case "socketconnectionattempttimeout": SocketConnectionAttemptTimeout = BitConverter.ToInt32(valueBytes, 0); break;
                    case "maxsocketconnectionattempts": MaxSocketConnectionAttempts = BitConverter.ToInt32(valueBytes, 0); break;
                    case "socketconnectionretrydelay": SocketConnectionRetryDelay = BitConverter.ToInt32(valueBytes, 0); break;
                    case "postconnectioncommsdelay": PostConnectionCommsDelay = BitConverter.ToInt32(valueBytes, 0); break;
                    case "interrequestdelay": InterRequestDelay = BitConverter.ToInt32(valueBytes, 0); break;
                    case "maxrequestattempts": MaxRequestAttempts = BitConverter.ToInt32(valueBytes, 0); break;
                    case "requestresponsetimeout": RequestResponseTimeout = BitConverter.ToInt32(valueBytes, 0); break;
                    case "connectionstatus": ConnectionStatus = Encoding.UTF8.GetString(valueBytes); break;
                    case "idledisconnect": IdleDisconnect = BitConverter.ToBoolean(valueBytes, 0); break;
                    case "idledisconnecttime": IdleDisconnectTime = BitConverter.ToInt32(valueBytes, 0); break;
                    case "description": Description = Encoding.UTF8.GetString(valueBytes); break;
                }
            }

            protected ConnDetails(SerializationInfo info, StreamingContext context)
            {
                // deserializer
                ID = info.GetString("ID");
                ConnectionEnabled = info.GetBoolean("ConnectionEnabled");
                CommunicationsEnabled = info.GetBoolean("CommunicationsEnabled");
                ConnectionType = info.GetString("ConnectionType");
                LastCommsTime = info.GetInt64("LastCommsTime");
                LastCommsDT = new DateTime(LastCommsTime);
                RequestRetryDelay = info.GetInt32("RequestRetryDelay");
                SocketConnectionAttemptTimeout = info.GetInt32("SocketConnectionAttemptTimeout");
                MaxSocketConnectionAttempts = info.GetInt32("MaxSocketConnectionAttempts");
                SocketConnectionRetryDelay = info.GetInt32("SocketConnectionRetryDelay");
                PostConnectionCommsDelay = info.GetInt32("PostConnectionCommsDelay");
                InterRequestDelay = info.GetInt32("InterRequestDelay");
                MaxRequestAttempts = info.GetInt32("MaxRequestAttempts");
                RequestResponseTimeout = info.GetInt32("RequestResponseTimeout");
                ConnectionStatus = info.GetString("ConnectionStatus");
                IdleDisconnect = info.GetBoolean("IdleDisconnect");
                IdleDisconnectTime = info.GetInt32("IdleDisconnectTime");
                Description = info.GetString("Description");
            }

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                // serializer
                info.AddValue("ID", ID);
                info.AddValue("ConnectionEnabled", ConnectionEnabled);
                info.AddValue("CommunicationsEnabled", CommunicationsEnabled);
                info.AddValue("ConnectionType", ConnectionType);
                info.AddValue("LastCommsTime", LastCommsTime);
                info.AddValue("RequestRetryDelay", RequestRetryDelay);
                info.AddValue("SocketConnectionAttemptTimeout", SocketConnectionAttemptTimeout);
                info.AddValue("MaxSocketConnectionAttempts", MaxSocketConnectionAttempts);
                info.AddValue("SocketConnectionRetryDelay", SocketConnectionRetryDelay);
                info.AddValue("PostConnectionCommsDelay", PostConnectionCommsDelay);
                info.AddValue("InterRequestDelay", InterRequestDelay);
                info.AddValue("MaxRequestAttempts", MaxRequestAttempts);
                info.AddValue("RequestResponseTimeout", RequestResponseTimeout);
                info.AddValue("ConnectionStatus", ConnectionStatus);
                info.AddValue("IdleDisconnect", IdleDisconnect);
                info.AddValue("IdleDisconnectTime", IdleDisconnectTime);
                info.AddValue("Description", Description);
            }
        }
    }

    public class CustomJsonConvert : JsonConverter
    {
        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            // Handle only boolean types.
            return objectType == typeof(bool);
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader"/> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>
        /// The object value.
        /// </returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.Value.ToString().ToLower().Trim())
            {
                case "true":
                case "yes":
                case "y":
                case "1":
                    return true;

                case "false":
                case "no":
                case "n":
                case "0":
                    return false;
            }

            // If we reach here, we're pretty much going to throw an error so let's let Json.NET throw it's pretty-fied error message.
            return new JsonSerializer().Deserialize(reader, objectType);
        }

        /// <summary>
        /// Specifies that this converter will not participate in writing results.
        /// </summary>
        public override bool CanWrite { get { return false; } }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter"/> to write to.</param><param name="value">The value.</param><param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
        }
    }

    public static class Encrypt
    {
        // This size of the IV (in bytes) must = (keysize / 8).  Default keysize is 256, so the IV must be
        // 32 bytes long.  Using a 16 character string here gives us 32 bytes when converted to a byte array.
        private const string initVector = "p1MggfiEgGfqvVgz";

        // This constant is used to determine the keysize of the encryption algorithm
        private const int keysize = 256;

        //Encrypt
        public static string EncryptString(string plainText, string passPhrase)
        {
            try
            {
                byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                PasswordDeriveBytes password = new(passPhrase, null);
                byte[] keyBytes = password.GetBytes(keysize / 8);
                RijndaelManaged symmetricKey = new();
                symmetricKey.Mode = CipherMode.CBC;
                ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
                MemoryStream memoryStream = new();
                CryptoStream cryptoStream = new(memoryStream, encryptor, CryptoStreamMode.Write);
                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                cryptoStream.FlushFinalBlock();
                byte[] cipherTextBytes = memoryStream.ToArray();
                memoryStream.Close();
                cryptoStream.Close();
                return Convert.ToBase64String(cipherTextBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        //Decrypt
        public static string DecryptString(string cipherText, string passPhrase)
        {
            try
            {
                byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
                byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
                PasswordDeriveBytes password = new(passPhrase, null);
                byte[] keyBytes = password.GetBytes(keysize / 8);
                RijndaelManaged symmetricKey = new();
                symmetricKey.Mode = CipherMode.CBC;
                ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
                MemoryStream memoryStream = new(cipherTextBytes);
                CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Read);
                byte[] plainTextBytes = new byte[cipherTextBytes.Length];
                int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                memoryStream.Close();
                cryptoStream.Close();
                return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}