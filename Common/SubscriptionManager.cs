using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Common
{
    public class SubscriptionManager
    {
        private readonly List<Subscription> Subscriptions;

        public delegate void SubscriptionTriggerEventHandler(object sender, SubscriptionEventArgs eventArgs);

        public event SubscriptionTriggerEventHandler SubscriptionTriggered;

        public SubscriptionManager()
        {
            Subscriptions = new List<Subscription>();
        }

        public Subscription GetSubscription(Guid client, string triggerObjID, string triggerProperty)
        {
            Subscription foundSub = null;
            foreach (Subscription sub in Subscriptions)
            {
                if (sub.Subscriber == client && sub.Trigger.Obj.ID == triggerObjID && sub.Trigger.Property == triggerProperty)
                {
                    foundSub = sub;
                    break;
                }
            }

            return foundSub;
        }

        public static Subscription NewSubscription(Guid subscriber, SubscribeableObject obj, string property, out string result)
        {
            System.Reflection.PropertyInfo propInfo = obj.GetType().GetProperty(property);
            if (propInfo == null)
            {
                result = "unrecognized property name";
                return null;
            }

            result = "success";
            return new Subscription(subscriber, obj, property);
        }

        public string AddSubscription(Subscription newSub)
        {
            // check if subscription already exists
            foreach (Subscription sub in Subscriptions)
            {
                if (sub == newSub)
                    return "A subscription to " + sub.Trigger.ToString() + " already exists";
            }

            newSub.Triggered += SubscriptionTriggerHandler;

            Subscriptions.Add(newSub);
            return "success";
        }

        public void RemoveSubscriber(Guid subscriber)
        {
            List<Subscription> subsToRemove = new();
            foreach (Subscription sub in Subscriptions)
            {
                if (sub.Subscriber == subscriber)
                {
                    subsToRemove.Add(sub);
                }
            }

            foreach (Subscription sub in subsToRemove)
            {
                sub.Triggered -= SubscriptionTriggerHandler;
                Subscriptions.Remove(sub);
                sub.Dispose();
            }
            subsToRemove.Clear();
        }

        public void RemoveSubscription(Guid subscriber, SubscribeableObject obj, string property)
        {
            Subscription subToRemove = null;
            foreach (Subscription sub in Subscriptions)
            {
                if (sub.Subscriber == subscriber && sub.Trigger.Obj == obj && sub.Trigger.Property == property)
                {
                    subToRemove = sub;
                    break;
                }
            }

            if (subToRemove != null)
            {
                subToRemove.Triggered -= SubscriptionTriggerHandler;
                Subscriptions.Remove(subToRemove);
                subToRemove.Dispose();
            }

            subToRemove = null;
        }

        private void SubscriptionTriggerHandler(object sender, SubscriptionEventArgs e)
        {
            SubscriptionTriggered?.Invoke(sender, e);
        }

        public class SubscribeableObject : INotifyPropertyChanged
        {
            private string _ID;
            private string _type;
            private bool _MQTTEnabled;
            private readonly Type _derivedType;
            public string ID { get { return _ID; } set { _ID = value.ToLower(); } }
            public string ObjectType { get { return _type; } set { _type = value.ToLower(); } }
            public bool MQTTEnabled { get => _MQTTEnabled; set { if (_MQTTEnabled != value) { _MQTTEnabled = value; if (value) PublishAll(); else UnpublishAll(); } } }
            private readonly HashSet<string> MQTTRetainProperties;
            private readonly HashSet<string> MQTTincludeTimestamp;
            private readonly HashSet<string> MQTTalwaysPublishProperties;

            public event PropertyChangedEventHandler PropertyChanged;

            private static Dictionary<Type, int> DataTypes;
            private readonly Dictionary<string, PropertyInfo> propertyDictionary;

            public SubscribeableObject()
            {
                // get the derived type and properties and store them for later use (reflection is expensive, just do it once at object initialization)
                _derivedType = GetType();
                PropertyInfo[] propertiesArray = _derivedType.GetProperties();
                propertyDictionary = new Dictionary<string, PropertyInfo>();
                foreach (PropertyInfo property in propertiesArray)
                {
                    propertyDictionary.Add(property.Name, property);
                }

                MQTTRetainProperties = new HashSet<string>();
                MQTTincludeTimestamp = new HashSet<string>();
                MQTTalwaysPublishProperties = new HashSet<string>();
                MQTTEnabled = false;

                if (DataTypes == null)
                {
                    DataTypes = new Dictionary<Type, int>
                    {
                        { typeof(string), 0 },
                        { typeof(double), 1 },
                        { typeof(int), 2 },
                        { typeof(DateTime), 3 },
                        { typeof(bool), 4 }
                    };
                }
            }

            protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "", long timestamp = 0, string[] additionalProperties = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

                if (!Globals.MQTTEnabled || !(MQTTEnabled || MQTTalwaysPublishProperties.Contains(propertyName)))
                    return;

                if (Globals.MQTT != null)
                {
                    if (Globals.MQTT.IsConnected)
                    {
                        string topic = ObjectType + "/" + ID + "/" + propertyName.ToLower();

                        object value = propertyDictionary[propertyName].GetValue(this);

                        byte[] message = ObjectToByteArray(value, timestamp);

                        if (timestamp > 0)
                        {
                            message = AppendTimestamp(message, timestamp);
                        }

                        bool retain = MQTTRetainProperties.Contains(propertyName);

                        if (Globals.MQTT != null)
                            if (Globals.MQTT.IsConnected)
                            {
                                Globals.MQTT.Publish(topic, message, 1, retain);
                            }

                        if (additionalProperties != null)
                        {
                            foreach (string additionalProperty in additionalProperties)
                            {
                                topic = ObjectType + "/" + ID + "/" + additionalProperty.ToLower();

                                value = propertyDictionary[propertyName].GetValue(this);

                                message = ObjectToByteArray(value, timestamp);

                                if (timestamp > 0)
                                {
                                    message = AppendTimestamp(message, timestamp);
                                }

                                retain = MQTTRetainProperties.Contains(propertyName);
                                Globals.MQTT.Publish(topic, message, 0, retain);
                            }
                        }
                    }
                }
            }

            protected void SetMQTTRetainedProperties(string[] properties)
            {
                foreach (string prop in properties)
                    MQTTRetainProperties.Add(prop);
            }

            protected void SetMQTTAlwaysPublishProperties(string[] properties)
            {
                foreach (string prop in properties)
                    MQTTalwaysPublishProperties.Add(prop);
            }

            protected void SetMQTTIncludeTimestampProperties(string[] properties)
            {
                foreach (string prop in properties)
                    MQTTincludeTimestamp.Add(prop);
            }

            private static byte[] AppendTimestamp(byte[] original, long timestamp)
            {
                byte[] appended = new byte[original.Length + 8];
                Array.Copy(original, 0, appended, 0, original.Length);
                Array.Copy(BitConverter.GetBytes(timestamp), 0, appended, original.Length, 8);

                return appended;
            }

            public void PublishAll()
            {
                //PropertyInfo[] properties = GetType().GetProperties();
                if (!MQTTEnabled)
                    return;

                foreach (PropertyInfo prop in propertyDictionary.Values)
                {
                    string topic = ObjectType + "/" + ID + "/" + prop.Name.ToLower();

                    object value = prop.GetValue(this, null);

                    byte[] valueBytes = ObjectToByteArray(value);

                    //
                    if (MQTTincludeTimestamp.Contains(prop.Name))
                    {
                        valueBytes = AppendTimestamp(valueBytes, Globals.FDANow().Ticks);
                    }

                    bool retain = MQTTRetainProperties.Contains(prop.Name);

                    Globals.MQTT.Publish(topic, valueBytes, 1, retain);
                }
            }

            public void UnpublishAll() // unpublish all retained values
            {
                string topic;
                byte[] empty = Array.Empty<byte>();
                foreach (PropertyInfo prop in propertyDictionary.Values)
                {
                    topic = ObjectType + "/" + ID.ToLower() + "/" + prop.Name.ToLower();
                    Globals.MQTT.Publish(topic, empty, 0, true);
                }
            }

            private static byte[] ObjectToByteArray(Object obj, long timestamp = 0)
            {
                if (obj == null)
                    return null;

                Type dataType = obj.GetType();

                byte[] timestampBytes;

                if (timestamp > 0)
                {
                    timestampBytes = BitConverter.GetBytes(timestamp);
                }
                else
                {
                    timestampBytes = Array.Empty<byte>();
                }
                byte[] databytes;
                byte[] messagebytes;
                if (DataTypes.ContainsKey(dataType))
                {
                    switch (DataTypes[dataType])
                    {
                        case 0: // string
                            databytes = Encoding.UTF8.GetBytes(obj.ToString()); break;
                        case 1: // Double
                            databytes = BitConverter.GetBytes((Double)obj); break;
                        case 2: // int
                            databytes = BitConverter.GetBytes((int)obj); break;
                        case 3: // datetime
                            databytes = BitConverter.GetBytes(((DateTime)obj).Ticks); break;
                        case 4: // boolean
                            databytes = new byte[1] { (byte)((bool)obj ? 1 : 0) }; break;
                        default:
                            databytes = Encoding.UTF8.GetBytes(obj.ToString()); break;
                    }

                    messagebytes = new byte[databytes.Length + timestampBytes.Length];
                    Array.Copy(databytes, 0, messagebytes, 0, databytes.Length);
                    Array.Copy(timestampBytes, 0, messagebytes, databytes.Length, timestampBytes.Length);
                    return messagebytes;
                }
                else
                    return Encoding.UTF8.GetBytes(obj.ToString());
            }
        }

        public class SubscriptionEventArgs : EventArgs
        {
            public Guid Subscriber;
            public string DataObjectType;
            public string TriggerObjectID;
            public string TriggerProperty;
            public string[] Report;

            public SubscriptionEventArgs(string[] report)
            {
                Report = report;
            }
        }

        public class ObjectProperty
        {
            public SubscribeableObject Obj;
            public string Property;
            public object Value { get { return Obj.GetType().GetProperty(Property).GetValue(Obj, null); } }

            public ObjectProperty(SubscribeableObject obj, string property)
            {
                Obj = obj;
                Property = property;
            }

            public static bool operator ==(ObjectProperty a, ObjectProperty b)
            {
                if (a.Obj == b.Obj && a.Property == b.Property)
                    return true;
                else
                    return false;
            }

            public static bool operator !=(ObjectProperty a, ObjectProperty b)
            {
                if (a.Obj != b.Obj || a.Property != b.Property)
                    return true;
                else
                    return false;
            }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override string ToString()
            {
                return Obj.GetType().Name + "." + Obj.ID + "." + Property;
            }
        }

        public class Subscription : IDisposable
        {
            public Guid Subscriber;

            public delegate void PropertyChangedHandler(object sender, SubscriptionEventArgs e);

            public event PropertyChangedHandler Triggered;

            public ObjectProperty Trigger;
            public List<ObjectProperty> ReportProperties;
            public DateTime TriggerTime;

            public Subscription(Guid subscriber, SubscribeableObject triggerObj, string triggerProperty)
            {
                Subscriber = subscriber;
                Trigger = new ObjectProperty(triggerObj, triggerProperty);
                Trigger.Obj.PropertyChanged += SubscribedObject_PropertyChanged;
            }

            public void AddReportProperty(SubscribeableObject obj, string property)
            {
                if (ReportProperties == null)
                    ReportProperties = new List<ObjectProperty>();

                ReportProperties.Add(new ObjectProperty(obj, property));
            }

            private void SubscribedObject_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == Trigger.Property)
                {
                    TriggerTime = Globals.FDANow();
                    string[] report;

                    // if the report properties list is empty, just return the value of the trigger property
                    if (ReportProperties == null)
                    {
                        report = new string[1];
                        report[0] = Trigger.ToString() + "." + Trigger.Value;
                    }
                    else
                    { // otherwise return the values of the properties in the report list
                        report = new string[ReportProperties.Count];

                        for (int i = 0; i < report.Length; i++)
                        {
                            report[i] = ReportProperties[i].ToString() + "." + ReportProperties[i].Value;
                        }
                    }

                    // raise an event, this subscription was activated (ie: the trigger property's value has changed)
                    Triggered?.Invoke(this, new SubscriptionEventArgs(report));
                }
            }

            public void Dispose()
            {
                Trigger.Obj.PropertyChanged -= SubscribedObject_PropertyChanged;
                Trigger.Obj = null;
                if (ReportProperties != null)
                    ReportProperties.Clear();

                GC.SuppressFinalize(this);
            }
        }
    }
}