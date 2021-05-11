using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    public abstract class DerivedTag : FDADataPointDefinitionStructure, IDisposable
    {
        public bool IsValid { get => _valid; }
        public string ErrorMessage { get => _errorMessage; }
     
        protected string _errorMessage = "";
        protected bool _valid = false;
        protected string _derived_tag_type;

        // these three properties are the setup value that were passed in when the tag was created
        protected string _tagID;
        protected string _arguments;
        protected bool _enabled;

        public static Dictionary<Guid, FDADataPointDefinitionStructure> Tags;

        public delegate void OnUpdateHandler(object sender, EventArgs e);
        public event OnUpdateHandler OnUpdate;

        public static DerivedTag Create(string tagID, string tagtype, string arguments,bool enabled)
        {
            switch (tagtype)
            {
                case "bitser": return new BitSeriesDerivedTag(tagID, arguments,enabled);
                case "bitmask": return new BitmaskDerivedTag(tagID,arguments,enabled); 
                //case "summation": return new SummationDerivedTag(tagID,arguments,enabled);   
                default: return null;
            }
        }
 
        // base class constructor
        protected DerivedTag(string tagID, string arguments,bool enabled)
        {
            _tagID = tagID;
            _arguments = arguments;
            physical_point = arguments;
            _enabled = enabled;
            
            if (IsValidGUID(_tagID))
            {
                DPDUID = Guid.Parse(tagID);
            }
        }

        public abstract void Initialize();

        public abstract void Alter(string arguments);
 

        protected void RaiseOnUpdateEvent()
        {
            OnUpdate?.Invoke(this, new EventArgs());
        }

        // helper classes
        protected static bool IsIntegerInRange(string testVal, UInt32 min, UInt32 max)
        {
            UInt32 testInt;
            if (!UInt32.TryParse(testVal, out testInt))
                return false;

            if (testInt >= min && testInt <= max)
                return true;
            else
                return false;
        }

        protected static bool IsValidGUID(string testval)
        {
            Guid testGUID;
            return Guid.TryParse(testval, out testGUID);
        }

        protected static UInt32 UInt32FromByteArray(BitArray bitArray)
        {
            Byte[] array = new byte[4];
            bitArray.CopyTo(array, 0);
            return BitConverter.ToUInt32(array);
        }

        public abstract void Dispose();

    }

    public class BitSeriesDerivedTag : DerivedTag
    {
        public byte StartBit { get => _startBit; }
        public byte NumBits { get => _numBits; }
        public Guid SourceTag { get => _sourceTag.DPDUID; }

        private byte _startBit;
        private byte _numBits;
        private FDADataPointDefinitionStructure _sourceTag;


        public BitSeriesDerivedTag(string tagid, string arguments, bool enabled) : base(tagid, arguments, enabled)
        {
            _derived_tag_type = "bitser";
            read_detail_01 = "bitser";
        }

        public override void Initialize()
        {
            // tag id is valid
            if (!IsValidGUID(_tagID))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Invalid Tag ID '" + _tagID + "', must be a valid UID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
                return;
            }
            

            // arguments string is not null
            if (_arguments == null)
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Invalid Argument (NULL)";
                return;
            }

            // arguments string is not empty
            if (_arguments == String.Empty)
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Invalid Argument ''";
                return;
            }

            DPDUID = Guid.Parse(_tagID);
            _valid = true;
            DPDSEnabled = _enabled;


            // examine the arguments and make sure they're all there and valid
            string[] argumentsList = _arguments.Split(':');

            // at least 3 arguments
            if (argumentsList.Length < 3)
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Not enough arguments, requires three arguments 'source tag id:start bit:bit count'";
                return;
            }

            // first argument (source tag) must be a valid guid
            if (!IsValidGUID(argumentsList[0]))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Argument 1 (source tag) must be a valid UID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
                return;
            }
            Guid sourceTagID = Guid.Parse(argumentsList[0]);

            // first argument (source tag) must exist
            if (!Tags.ContainsKey(sourceTagID))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Argument 1 (source tag) '" + sourceTagID.ToString() + "', tag not found";
                return;
            }
            _sourceTag = Tags[sourceTagID];

            // second argument must be an integer between 0 and 31
            if (!IsIntegerInRange(argumentsList[1], 0, 31))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Argument 2 (start bit) must be an integer between 0 and 31";
                return;
            }
            _startBit = byte.Parse(argumentsList[1]);

            // third argument (count) must be an integer, and the sum of the start bit # and the count must between 1 and 32           
            UInt32 maxCount = (UInt32)(32 - _startBit);
            if (!IsIntegerInRange(argumentsList[2], 1, maxCount))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Argument 3 (bit count) must be an integer, and start bit + bit count must be less than or equal to 32";
                return;
            }
            _numBits = byte.Parse(argumentsList[2]);


            // valid configuration, subscribe to update events from the source tag
            _valid = true;
            _sourceTag.PropertyChanged += _sourceTag_PropertyChanged;
        }

        private void _sourceTag_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // we're only interested in changes to the timestamp, this means the value has updated (even if it hasn't changed)
            if (e.PropertyName != "LastReadDataTimestamp")
                return;

            // don't do anything if this derived tag is disabled
            if (!DPDSEnabled)
                return;

            // don't do anything if the source tag is disabled
            if (!_sourceTag.DPDSEnabled)
                return;

            // don't do anything if the datatype isn't an int type
            string typename = _sourceTag.LastReadDataType.Name.ToLower();
            if (!(typename == "uint8" || typename == "uint16" || typename == "uint32"))
            {
                Globals.SystemManager?.LogApplicationEvent(this,ID.ToString(),"Soft tag " + ID + " unable to calculate, because of incompatible data type. Bitseries soft tags require an unsigned int (8,16, or 32 bits)");
                return;
            }

            // don't do anything if the source tag has bad quality
            if (_sourceTag.LastReadQuality != 192)
            {
                Globals.SystemManager?.LogApplicationEvent(this, ID.ToString(), "Soft tag " + ID + " unable to calculate, because the source tag (" + _sourceTag.DPDUID + " has bad quality (" + _sourceTag.LastReadQuality + ")");
                return;
            }

            // get the new value
            Double dblVal = _sourceTag.LastReadDataValue; 

            // cast it to an Int32
            UInt32 intVal = Convert.ToUInt32(dblVal);

            // break out the bits
            BitArray sourceBits = new BitArray(BitConverter.GetBytes(intVal));

            // copy the requested bits into a new array
            BitArray dstBits = new BitArray(32);
            int destIdx = 0;
            for (int srcIdx = _startBit; srcIdx < (_startBit + _numBits); srcIdx++)
            {
                dstBits[destIdx] = sourceBits[srcIdx];
                destIdx++;
            }

            // convert the requested bits into an int and update the value,timestamp, and quality
            LastReadDataValue = (double)UInt32FromByteArray(dstBits);
            LastReadDataType = _sourceTag.LastReadDataType;
            LastReadQuality = _sourceTag.LastReadQuality;
            LastReadDatabaseWriteMode = _sourceTag.LastReadDatabaseWriteMode;
            LastReadDestTable = _sourceTag.LastReadDestTable;
            LastReadDataTimestamp = _sourceTag.LastReadDataTimestamp;
            

            RaiseOnUpdateEvent(); // let anyone who's listening know that this derived tag has been updated
        }

        public override void Dispose()
        {
            if (_sourceTag != null)
            {
                _sourceTag.PropertyChanged -= _sourceTag_PropertyChanged;
            }
        }

        public override void Alter(string newArguments)
        {
            _valid = false;
            if (_sourceTag != null)
            {
                _sourceTag.PropertyChanged -= _sourceTag_PropertyChanged;
            }
            _sourceTag = null;
            _arguments = newArguments;
            Initialize();
        }
    }
    


    public class BitmaskDerivedTag : DerivedTag
    {
        public UInt32 Bitmask { get => _bitmask; }
        public Guid SourceTag { get => _sourceTag.DPDUID; }

        private FDADataPointDefinitionStructure _sourceTag;
        private UInt32 _bitmask;


        public BitmaskDerivedTag(string tagid, string arguments, bool enabled) : base(tagid, arguments, enabled)
        {          
            _derived_tag_type = "bitmask";
            read_detail_01 = "bitmask";
        }

        public override void Initialize()
        {
            // tag id is valid
            if (!IsValidGUID(_tagID))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Invalid Tag ID '" + _tagID + "', must be a valid UID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
                return;
            }

            // arguments string is not null
            if (_arguments == null)
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Invalid Argument (NULL)";
                return;
            }

            // arguments string is not empty
            if (_arguments == String.Empty)
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Invalid Argument ''";
                return;
            }

            DPDUID = Guid.Parse(_tagID);
            _valid = true;

            // examine the arguments and make sure they're all there and valid
            string[] argumentsList = _arguments.Split(':');

            // at least 2 arguments
            if (argumentsList.Length < 2)
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Not enough arguments, requires two arguments 'source tag id:bitmask'";
                return;
            }

            // first argument (source tag) must be a valid guid
            if (!IsValidGUID(argumentsList[0]))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Argument 1 (source tag) must be a valid UID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
                return;
            }
            Guid sourceTagID = Guid.Parse(argumentsList[0]);

            // first argument (source tag) must exist
            if (!Tags.ContainsKey(sourceTagID))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Argument 1 (source tag) '" + sourceTagID.ToString() + "', tag not found";
                return;
            }
            _sourceTag = Tags[sourceTagID];

            // second argument must be an integer in the UInt32 range
            if (!IsIntegerInRange(argumentsList[1], 0, UInt32.MaxValue))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Argument 2 (bitmask) must be an integer between 0 and " + UInt32.MaxValue;
                return;
            }
            _bitmask = UInt32.Parse(argumentsList[1]);

            // valid configuration, subscribe to update events from the source tag
            _valid = true;
            _sourceTag.PropertyChanged += _sourceTag_PropertyChanged;
        }

        private void _sourceTag_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {

            // we're only interested in changes to the timestamp, this means the value has updated (even if it hasn't changed)
            if (e.PropertyName != "LastReadDataTimestamp")
                return;

            // don't do anything if this derived tag is disabled or invalid
            if (!(DPDSEnabled && _valid))
                return;

            // don't do anything if the source tag is disabled
            if (!_sourceTag.DPDSEnabled)
                return;

            // don't do anything if the datatype isn't an int type
            string typename = _sourceTag.LastReadDataType.Name.ToLower();
            if (!(typename == "uint8" || typename == "uint16" || typename == "uint32"))
            {
                Globals.SystemManager?.LogApplicationEvent(this, ID.ToString(), "Soft tag " + ID + " unable to calculate, because of incompatible data type. Bitmask soft tags requires an unsigned int (8,16, or 32 bits)");
                return;
            }

            // don't do anything if the source tag has bad quality
            if (_sourceTag.LastReadQuality != 192)
            {
                Globals.SystemManager?.LogApplicationEvent(this, ID.ToString(), "Soft tag " + ID + " unable to calculate, because the source tag (" + _sourceTag.DPDUID + " has bad quality (" + _sourceTag.LastReadQuality + ")");
                return;
            }

            // get the new value
            Double dblVal = _sourceTag.LastReadDataValue;

            // cast it to an Int32
            UInt32 intVal = Convert.ToUInt32(dblVal);

            LastReadDataValue = (double)(intVal & _bitmask);
            LastReadDataType = _sourceTag.LastReadDataType;
            LastReadQuality = _sourceTag.LastReadQuality;
            LastReadDatabaseWriteMode = _sourceTag.LastReadDatabaseWriteMode;
            LastReadDestTable = _sourceTag.LastReadDestTable;
            LastReadDataTimestamp = _sourceTag.LastReadDataTimestamp;

            RaiseOnUpdateEvent(); // let anyone who's listening know that this derived tag has been updated 
        }

        public override void Dispose()
        {
            if (_sourceTag != null)
                _sourceTag.PropertyChanged -= _sourceTag_PropertyChanged;
        }

        public override void Alter(string newArguments)
        {
            _valid = false;
            if (_sourceTag != null)
            {
                _sourceTag.PropertyChanged -= _sourceTag_PropertyChanged;
            }
            _sourceTag = null;
            _arguments = newArguments;
            Initialize();
        }
    }

    public class SummationDerivedTag : DerivedTag
    {
        List<FDADataPointDefinitionStructure> _sourceTags;

        public SummationDerivedTag(string tagid, string arguments, bool enabled) : base(tagid, arguments, enabled)
        {
            // if base constructor found something invalid, no need to continue
            if (!_valid) return;

            // examine the arguments and make sure they're all there and valid
            string[] argumentsList = arguments.Split(':');

            // at least 1 argument
            if (argumentsList.Length < 1)
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Not enough arguments, requires at least 1 source tag id";
                return;
            }

            _sourceTags = new List<FDADataPointDefinitionStructure>();

            // for each source tag in the arugments list
            Guid sourceTagIDguid;
            foreach (string sourcetagIDstring in argumentsList)
            {
                // argument (source tag) must be a valid guid
                if (!IsValidGUID(sourcetagIDstring))
                {
                    DPDSEnabled = false;
                    _valid = false;
                    _errorMessage = "Source tag '" + sourcetagIDstring + "' is not a valid UID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
                    return;
                }
                sourceTagIDguid = Guid.Parse(sourcetagIDstring);

                // first argument (source tag) must exist
                if (!Tags.ContainsKey(sourceTagIDguid))
                {
                    DPDSEnabled = false;
                    _valid = false;
                    _errorMessage = "Source tag '" + sourcetagIDstring + "' not found";
                    return;
                }
                _sourceTags.Add(Tags[sourceTagIDguid]);
            }

            // all source tag id's were valid guid's and source tags with these id's were all found, so
            _valid = true;

            // subscribe to changes to all the source tags
            foreach (FDADataPointDefinitionStructure sourceTag in _sourceTags)
            {
                sourceTag.PropertyChanged += SourceTag_PropertyChanged;
            }

        }

        public override void Alter(string arguments)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public override void Initialize()
        {
            // to do: move validation and activation from the constructor to here
        }

        private void SourceTag_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // we're only interested in changes to the timestamp, this means the value has updated (even if it hasn't changed)
            if (e.PropertyName != "LastReadDataTimestamp")
                return;

            FDADataPointDefinitionStructure updatedSourceTag = (FDADataPointDefinitionStructure)sender;
            
            // the tag that changed is disabled, ignore it
            if (!updatedSourceTag.DPDSEnabled)
                return;
            // this derived tag is disabled, ignore the change to the source tag
            if (DPDSEnabled)
                return;


            Double sum = 0;
            int quality = 192;
            DateTime timestamp = updatedSourceTag.LastReadDataTimestamp;

            foreach (FDADataPointDefinitionStructure sourceTag in _sourceTags)
            {
                // add the the values of all enabled source tags
                // Buddy question: should disabled source tags be excluded from the summation?
                if (sourceTag.DPDSEnabled)
                    sum += sourceTag.LastReadDataValue;

                // Buddy Question: how to handle variety of qualities in the source tags, what quality do I set the derived tag to?
                if (quality == 192 && sourceTag.LastReadQuality != 192)
                    quality = sourceTag.LastReadQuality;
            }

            LastReadDataValue = sum;
            LastReadQuality = quality;
            LastReadDataTimestamp = timestamp;
            

            //RaiseOnUpdateEvent(); // let anyone who's listening know that this tag has been updated


        }


    }
}
