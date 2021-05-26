using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Support;

namespace Common
{
    public class DerivedTag : FDADataPointDefinitionStructure, IDisposable
    {
        public bool IsValid { get => _valid; }
        public string ErrorMessage { get => _errorMessage; }

        protected string _errorMessage = "";
        protected bool _valid = false;
        protected string _derived_tag_type;

        // these three properties are the setup value that were passed in when the tag was created
        protected string _tagID;
        protected string _arguments;

        public static Dictionary<Guid, FDADataPointDefinitionStructure> Tags;

        public delegate void OnUpdateHandler(object sender, EventArgs e);
        public event OnUpdateHandler OnUpdate;

        public static DerivedTag Create(string tagID, string tagtype, string arguments)
        {
            return tagtype switch
            {
                "bitser" => new BitSeriesDerivedTag(tagID, arguments),
                "bitmask" => new BitmaskDerivedTag(tagID, arguments),
                "" => new DerivedTag(tagID,""), // no derived tag type specified, create a basic derived tag that doesn't use any other tag as a source, can only be updated by a script
                _ => null   // unrecognized derived  tag type
            };

        }

        // base class constructor
        protected DerivedTag(string tagID, string arguments)
        {
            _tagID = tagID;
            _arguments = arguments;
            physical_point = arguments;

            if (ValidationHelpers.IsValidGuid(_tagID))
            {
                DPDUID = Guid.Parse(tagID);
            }

            this.PropertyChanged += DerivedTag_PropertyChanged;
        }

        private void DerivedTag_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName=="LastRead")
            {
                OnUpdate?.Invoke(this, new EventArgs());
            }
        }

        public virtual void Initialize()
        {
            // tag id is valid
            if (!ValidationHelpers.IsValidGuid(_tagID))
            {
                DPDSEnabled = false;
                _valid = false;
                _errorMessage = "Invalid Tag ID '" + _tagID + "', must be a valid UID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
                return;
            }
            DPDUID = Guid.Parse(_tagID);
            _valid = true;
        }

        public virtual void AlterArguments(string arguments)
        {

        }

        //protected void RaiseOnUpdateEvent()
        //{
        //    OnUpdate?.Invoke(this, new EventArgs());
        //}

        // helper classes
        protected static bool IsIntegerInRange(string testVal, uint min, uint max)
        {
            if (!uint.TryParse(testVal, out uint testInt))
                return false;

            if (testInt >= min && testInt <= max)
                return true;
            else
                return false;
        }


        protected static UInt32 UInt32FromByteArray(BitArray bitArray)
        {
            Byte[] array = new byte[4];
            bitArray.CopyTo(array, 0);
            return BitConverter.ToUInt32(array);
        }

        public virtual void Dispose()
        {

        }

    }

    public class BitSeriesDerivedTag : DerivedTag
    {
        public byte StartBit { get => _startBit; }
        public byte NumBits { get => _numBits; }
        public Guid SourceTag { get => _sourceTag.DPDUID; }

        private byte _startBit;
        private byte _numBits;
        private FDADataPointDefinitionStructure _sourceTag;


        public BitSeriesDerivedTag(string tagid, string arguments) : base(tagid, arguments)
        {
            _derived_tag_type = "bitser";
            read_detail_01 = "bitser";
        }

        public override void Initialize()
        {
            base.Initialize();

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

            _valid = true;



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
            if (!ValidationHelpers.IsValidGuid(argumentsList[0]))
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
            _sourceTag.PropertyChanged += SourceTag_PropertyChanged;
        }

        private void SourceTag_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // we're only interested if the last read property has changed
            if (e.PropertyName != "LastRead")
                return;

            // don't do anything if this derived tag is disabled
            if (!DPDSEnabled)
                return;

            // don't do anything if the source tag is disabled
            if (!_sourceTag.DPDSEnabled)
                return;

            // don't do anything if the datatype isn't an int type
            string typename = _sourceTag.LastRead.DataType.Name.ToLower();
            if (!(typename == "uint8" || typename == "uint16" || typename == "uint32"))
            {
                Globals.SystemManager?.LogApplicationEvent(this,ID.ToString(),"Soft tag " + ID + " unable to calculate, because of incompatible data type. Bitseries soft tags require an unsigned int (8,16, or 32 bits)");
                return;
            }

            // don't do anything if the source tag has bad quality
            if (_sourceTag.LastRead.Quality != 192)
            {
                Globals.SystemManager?.LogApplicationEvent(this, ID.ToString(), "Soft tag " + ID + " unable to calculate, because the source tag (" + _sourceTag.DPDUID + " has bad quality (" + _sourceTag.LastRead.Quality + ")");
                return;
            }

            // get the new value
            Double dblVal = _sourceTag.LastRead.Value;

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

            // convert the requested bits into an int and update the last read for this tag
            LastRead = new Datapoint(
                    (double)UInt32FromByteArray(dstBits),
                    _sourceTag.LastRead.Quality,
                    _sourceTag.LastRead.Timestamp,
                    _sourceTag.LastRead.DestTable,
                    _sourceTag.LastRead.DataType,
                    _sourceTag.LastRead.WriteMode);
            
            /* old style
            LastReadDataValue = (double)UInt32FromByteArray(dstBits);
            LastReadDataType = _sourceTag.LastReadDataType;
            LastReadQuality = _sourceTag.LastReadQuality;
            LastReadDatabaseWriteMode = _sourceTag.LastReadDatabaseWriteMode;
            LastReadDestTable = _sourceTag.LastReadDestTable;
            LastReadDataTimestamp = _sourceTag.LastReadDataTimestamp;
            */

            //RaiseOnUpdateEvent(); // let anyone who's listening know that this derived tag has been updated
        }

        public override void Dispose()
        {
            if (_sourceTag != null)
            {
                _sourceTag.PropertyChanged -= SourceTag_PropertyChanged;
            }
        }

        public override void AlterArguments(string newArguments)
        {
            _valid = false;
            if (_sourceTag != null)
            {
                _sourceTag.PropertyChanged -= SourceTag_PropertyChanged;
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


        public BitmaskDerivedTag(string tagid, string arguments) : base(tagid, arguments)
        {          
            _derived_tag_type = "bitmask";
            read_detail_01 = "bitmask";
        }

        public override void Initialize()
        {
            base.Initialize();

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
            if (!ValidationHelpers.IsValidGuid(argumentsList[0]))
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
            _sourceTag.PropertyChanged += SourceTag_PropertyChanged;
        }

        private void SourceTag_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {

            // we're only interested in changes to the timestamp, this means the value has updated (even if it hasn't changed)
            if (e.PropertyName != "LastRead")
                return;

            // don't do anything if this derived tag is disabled or invalid
            if (!(DPDSEnabled && _valid))
                return;

            // don't do anything if the source tag is disabled
            if (!_sourceTag.DPDSEnabled)
                return;

            // don't do anything if the datatype isn't an int type
            string typename = _sourceTag.LastRead.DataType.Name.ToLower();
            if (!(typename == "uint8" || typename == "uint16" || typename == "uint32"))
            {
                Globals.SystemManager?.LogApplicationEvent(this, ID.ToString(), "Soft tag " + ID + " unable to calculate, because of incompatible data type. Bitmask soft tags requires an unsigned int (8,16, or 32 bits)");
                return;
            }

            // don't do anything if the source tag has bad quality
            if (_sourceTag.LastRead.Quality != 192)
            {
                Globals.SystemManager?.LogApplicationEvent(this, ID.ToString(), "Soft tag " + ID + " unable to calculate, because the source tag (" + _sourceTag.DPDUID + " has bad quality (" + _sourceTag.LastRead.Quality + ")");
                return;
            }

            // get the new value
            Double dblVal = _sourceTag.LastRead.Value;

            // cast it to an Int32
            UInt32 intVal = Convert.ToUInt32(dblVal);
            LastRead = new Datapoint(
                 (double)(intVal & _bitmask),
                 _sourceTag.LastRead.Quality,
                 _sourceTag.LastRead.Timestamp,
                 _sourceTag.LastRead.DestTable,
                  _sourceTag.LastRead.DataType,
                  _sourceTag.LastRead.WriteMode
                );


            //RaiseOnUpdateEvent(); // let anyone who's listening know that this derived tag has been updated 
        }

        public override void Dispose()
        {
            if (_sourceTag != null)
                _sourceTag.PropertyChanged -= SourceTag_PropertyChanged;
        }

        public override void AlterArguments(string newArguments)
        {
            _valid = false;
            if (_sourceTag != null)
            {
                _sourceTag.PropertyChanged -= SourceTag_PropertyChanged;
            }
            _sourceTag = null;
            _arguments = newArguments;
            Initialize();
        }
    }

    /* not doing summation tags yet
    public class SummationDerivedTag : DerivedTag
    {
        List<FDADataPointDefinitionStructure> _sourceTags;

        public SummationDerivedTag(string tagid, string arguments, bool enabled) : base(tagid, arguments)
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
    */
}
