using System;
using System.Collections.Generic;
using Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Data.OleDb;
using FDA;
using System.CodeDom;
using System.Security.Policy;
using Support;

namespace BSAP
{
    public static class BSAPProtocol
    {
        private const ushort CRCInitial = 0xFFFF;
        private const ushort CRCPoly = 0x8408;
        private const bool CRCSwapOutputBytes = false;
        private const byte DLE = 0x10;
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const byte VersionHigh = 0xF3;
        private const byte VersionLow  = 0xE6;
        private const byte DSTFuncCode = 0xA0;
        private const byte ReadMSDFuncCode = 0x00;
        private const byte ReadNameFuncCode = 0x04;
        private const byte WriteByMSDFuncCode = 0x80;
        private const byte WriteByNameFuncCode = 0x84;
        private const byte NodeStatus = 0x00;
        private const byte FSS = 0x01;
        private const byte FS1_typeAndvalue = 0xC0;
        private const byte FS1_typeAndvalueAndMSD = 0xD0;
        private static byte SerialNumber = 0; // only used for serial protocol
        private static UInt32 SeqNumber = 1;
        private static UInt32 LastSeqRcvd = 0;
        private static ushort SerialSeqNumber = 1;
        private const byte SRCFuncCode = 3;
        private const int absoluteMaxMessageLength = 245;
        private const int MaxSignalsPerMessage = 38; // prevent responses that go over the message size limit
        private static List<string> validationErrors;

        // for calling from the Database Validator tool
        public static string[] ValidateRequestString(string request, Dictionary<Guid, FDADataPointDefinitionStructure> tags, Dictionary<Guid, FDADevice> devices, ref HashSet<Guid> referencedTags)
        {
            validationErrors = new List<string>();
            ValidateRequestString(validationErrors, "", "DBValidator", request, tags, devices,false, referencedTags);
            return validationErrors.ToArray();
        }


        // obj is a reference to the DataAcqManager if the requestor is the FDA
        // obj is a reference to the string[] error list if the requestor is the Database Validator tool
        public static bool ValidateRequestString(object obj, string groupID, string requestor, string request, Dictionary<Guid, FDADataPointDefinitionStructure> pointDefs, Dictionary<Guid, FDADevice> deviceParams,bool isUDP, HashSet<Guid> referencedTags = null)
        {
            bool valid = true;

            string[] headerAndTagList = request.Split('|');
            string[] header = headerAndTagList[0].Split(':');


            // the header has the correct number of elements
            if (header.Length < 3) // header can be 3,4,5 or 6. less than 3 is invalid, elements 7+ will just be ignored
            {
                RecordValidationError(obj, groupID, requestor, "contains a request with an invalid header(should be 'GlobalAddr/IPAddress^deviceref(optional):LocalAddr/Port:READ/WRITE:login1(optional):login2(optional):MaxDataSize(optional)'");
                valid = false;
            }
            else
            {
                // check if a device reference is present in header element 1
                string deviceRefString;
                string localAddrStr;
                string[] headerElement1 = header[1].Split('^');
                localAddrStr = headerElement1[0];

                
                // if a device reference is present, check that the ID is valid and references an existing device
                if (headerElement1.Length > 1)
                {
                    deviceRefString = headerElement1[1];
                    Guid deviceRefID;

                    // is a valid GUID
                    if (!Guid.TryParse(deviceRefString, out deviceRefID))
                    {
                        RecordValidationError(obj, groupID, requestor, "contains a request with an invalid device ID '" + deviceRefString + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.");
                        valid = false;
                    }

                    // if the GUID is valid, check that it references an existing device object
                    if (valid)
                    {
                        // ask the DB manager if this device exists
                        if (!deviceParams.ContainsKey(deviceRefID))
                        {
                            RecordValidationError(obj, groupID, requestor, "contains a request with an invalid device reference (the device ID '" + deviceRefString + "' is not found).");
                            valid = false;
                        }
                    }
                }
                
                int n;

                // UDP has IP:port as the first two elements
                if (isUDP)
                {
                    if (!IPAddress.TryParse(header[0], out _))
                    {
                        RecordValidationError(obj, groupID, requestor, "contains a request with an invalid IP address in the header '" + header[0] + "'");
                        valid = false;
                    }

                    // port doesn't really matter, we aren't looking at it anyways
                }
                else
                {  // for serial BSAP, the first two elements are the global and local addresses

                    // global address is an number and in range
                    if (!int.TryParse(header[0], out n))
                    {
                        RecordValidationError(obj, groupID, requestor, "contains a request with an invalid element in the header (Global Address '" + header[0] + "' is not a number).");
                        valid = false;
                    }
                    else
                    {
                        if (n < 0 || n > 255)
                        {
                            RecordValidationError(obj, groupID, requestor, "contains a request with an invalid element in the header (Global Address '" + header[0] + "' is out of range (0-255).");
                            valid = false;
                        }
                    }

                    // local address is an number and in range
                    if (!int.TryParse(localAddrStr, out n))
                    {
                        RecordValidationError(obj, groupID, requestor, "contains a request with an invalid element in the header (Local Address '" + localAddrStr + "' is not a number).");
                        valid = false;
                    }
                    else
                    {
                        if (n < 0 || n > 255)
                        {
                            RecordValidationError(obj, groupID, requestor, "contains a request with an invalid element in the header (Local Address '" + localAddrStr + "' is out of range (0-255).");
                            valid = false;
                        }
                    }
                }

                // operation is either 'read' or 'write'
                string operation = header[2].ToUpper();
                if (operation != "READ" && operation != "WRITE")
                {
                    RecordValidationError(obj, groupID, requestor, "contains a request with an invalid element in the header (Operation '" + header[2] + "' is not a recognized, should be READ or WRITE).");
                    valid = false;
                }

                if (header.Length > 5)
                {
                    if (!int.TryParse(header[5],out n))
                    {
                        RecordValidationError(obj, groupID, requestor, "contains a request with an invalid element in the header (Maximum packet size '" + header[5] + "' is not a number).");
                        valid = false;
                    }
                    else
                    {
                        if (n < 30)
                        {
                            RecordValidationError(obj, groupID, requestor, "contains a request with an invalid element in the header (Maximum packet size '" + header[5] + "' is too low, minimum of 30).");
                            valid = false;
                        }
                    }
                }
            }

            // check each tag
            string[] tagData;
            for (int i = 1; i < headerAndTagList.Length; i++)
            {
                tagData = headerAndTagList[i].Split(':');

                if (tagData.Length != 3)
                {
                    RecordValidationError(obj, groupID, requestor, "Tag " + i + " is missing element(s), should be 'Name:DataType:ID'");
                    valid = false;
                }
                else
                {
                    // tagData[0] is the signal name, check that length is 1 to 21 characters
                    if (tagData[0].Length < 1 || tagData[0].Length > 21)
                    {
                        RecordValidationError(obj, groupID, requestor, "Tag " + i + " has an invalid element (tag name '" + tagData[0] + "' is too short or too long (must be 1 to 21 characters).");
                        valid = false;
                    }

                    // tagData[1] is the type
                    string datatype = tagData[1].ToUpper();
                    if (datatype != "FL" && datatype != "BIN")
                    {
                        RecordValidationError(obj, groupID, requestor, "Tag " + i + " has an invalid element (data type '" + tagData[1] + "' is not recognized, must be FL or BIN.");
                        valid = false;
                    }

                    //tagData[2] is the tag ID
                    Guid id;
                    if (!Guid.TryParse(tagData[2], out id)) // tag has a valid ID
                    {
                        RecordValidationError(obj, groupID, requestor, "Tag " + i + " has an invalid element (tag ID '" + tagData[2] + "' is not a valid UID, should be in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx");
                        valid = false;
                    }
                    else  // the valid guid references an existing tag
                    {
                        if (!pointDefs.ContainsKey(id))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid tag information (the tag ID '" + tagInfo[counter] + "' is not found).", true);
                            RecordValidationError(obj, groupID, requestor, "Tag " + i + " has an invalid element (the tag ID '" + tagData[2] + "' is not found).");
                            valid = false;
                        }
                        else // the existing tag has the correct DPSType (matches the group that references it) and is enabled
                        {
                            referencedTags?.Add(id);

                            // the DPSType of the tag matches the DPStype of the RequestGroup
                            Guid result;
                            FDADataPointDefinitionStructure tag = pointDefs[id];
                            if (Guid.TryParse(groupID,out result))
                            {                            
                                string tagDPSType = tag.DPSType.ToUpper();
                                string groupDPSType = ((DBManager)(Globals.DBManager)).GetRequestGroup(result).DPSType;
                                if (tagDPSType != groupDPSType)
                                {
                                    RecordValidationError(obj, groupID, requestor, "Tag " + i + " references DataPointDefinitionStructure '" + tagData[2] + "' with an incorrect DPSType '" + tagDPSType + "'. The tag DPSType must match the RequestGroup DPSType");
                                }
                            }

                            // the tag is enabled
                            if (!tag.DPDSEnabled)
                            {
                                RecordValidationError(obj, groupID, requestor, "contains a reference to a tag'" + tagData[2] + "' that is disabled", true);
                            }
                        }
                    }
                }

            }

            return valid;
        }

        // obj is a reference to the DataAcqManager if the requestor is the FDA
        // obj is a reference to the string[] error list if the requestor is the Database Validator tool
        private static void RecordValidationError(object obj, string groupID, string requestor, string error, bool isWarning = false)
        {
            if (requestor != "DBValidator" && !isWarning)
            {
                error = "The group '" + groupID + "', requested by " + requestor + ": " + error + ". This request group will not be processed";
                Globals.SystemManager.LogApplicationEvent(obj, "", error, true);
            }
            else
            {
                if (isWarning)
                    error += "<warning>";

                validationErrors.Add(error);
            }
        }



        public static void CancelDeviceRequests(DataRequest failedReq)
        {
            if (failedReq.RequestBytes.Length < 2)
                return;

            byte failedDeviceAddr = failedReq.RequestBytes[2];

            if (failedReq.ParentGroup != null)
            {
                foreach (DataRequest req in failedReq.ParentGroup.ProtocolRequestList)
                {
                    if (req.RequestBytes.Length < 2)
                        continue;

                    if (req.RequestBytes[2] == failedDeviceAddr)
                    {
                        req.SkipRequestFlag = true;
                    }
                }
            }
        }

        public static void CreateRequests(RequestGroup requestGroup, object protocolOptions = null)
        {
            //BSAP    -> Global Address (req) : Local Address (req) : READ/WRITE (req): Login1 (opt): Login2 (opt) : Max Data Size (opt)|tagname:type:ID|.....
            //BSAPUDP ->  IPAddress (req) : Port (req) : READ/WRITE (req): Login1 (opt): Login2 (opt) : Max Data Size (opt)|tagname:type:ID|.....
            string requestString = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals;

            string[] requestStrings;     // delimited by $
            string[] HeaderAndBody;      // delimited by |
            string[] Header;             // delimited by :
            byte globalAddress = 0;
            byte localAddress = 0;
            string ip = String.Empty;

            string operation;
            string signalName;
            string login1 = "";
            string login2 = "";
            FDADevice deviceSettings = null;

            Guid tagID;
            List<ushort> MSDRequestList = new();
            List<Tag> MSDTagList = new List<Tag>();
            List<string> SignalNameRequestList = new List<string>();
            List<Tag> SignalNameTagList = new List<Tag>();
            int maxRequestLength = absoluteMaxMessageLength;
            // check for null or empty group definition, exit if found
            if (requestString == null || requestString == string.Empty)
                return;

            requestStrings = requestString.Split('$');
            Dictionary<Guid, FDADataPointDefinitionStructure> allTagConfigs = null;

            if (Globals.DBManager != null)
                allTagConfigs = ((DBManager)Globals.DBManager).GetAllTagDefs();

            for (int requestIdx = 0; requestIdx < requestStrings.Length; requestIdx++)
            {
                HeaderAndBody = requestStrings[requestIdx].Split('|');
                Header = HeaderAndBody[0].Split(':');

                // check for a device reference in the first element of the request header, pull it out if present
                string[] headerElement1 = Header[0].Split('^');
                {
                    if (headerElement1.Length > 1)
                    {
                        Guid deviceRef = Guid.Parse(headerElement1[1]);
                        if (Globals.DBManager != null)
                            deviceSettings = ((DBManager)Globals.DBManager).GetDevice(deviceRef);
                    }
                }

                string[] address;
                if (requestGroup.Protocol == "BSAP")
                {
                    address = Header[0].Split('^');
                    globalAddress = byte.Parse(address[0]);                    
                    localAddress = byte.Parse(Header[1]);
                }
                else
                {
                    // BSAP UDP
                    address = Header[0].Split('^');
                    ip = address[0];
                }
                
                    operation = Header[2].ToUpper();

                if (Header.Length > 3)
                    login1 = Header[3];

                if (Header.Length > 4)
                    login2 = Header[4];

                if (Header.Length > 5)
                    maxRequestLength = int.Parse(Header[5]);

                string dataType;
                ushort MSD;
                string[] tagInfo;
                Tag newTag;
                for (int tagIdx = 1; tagIdx < HeaderAndBody.Length; tagIdx++)
                {
                    tagInfo = HeaderAndBody[tagIdx].Split(':');

                    signalName = tagInfo[0];
                    dataType = tagInfo[1];
                    tagID = Guid.Parse(tagInfo[2]);

                    // do we already know the MSD for this signal?
                    int MSDtemp = int.Parse(requestGroup.TagsRef[tagID].DeviceTagAddress);

                    // double check.. does the signal name match? only use this address if the signal name matches
                    string recordedSigname = requestGroup.TagsRef[tagID].DeviceTagName;
                    if (recordedSigname != signalName)
                        MSDtemp = -1;

                    // temporary override, never request by MSD (we need the ACCOL Load version # to be able to request by MSD)
                    MSDtemp = -1;
                    // ********************************************************************************************************
                    
                    if (MSDtemp >= 0)
                    {
                        MSD = (ushort)MSDtemp;
                        // we can identify this one by MSD, so add the MSD to the MSD request list
                        // unless the operation is a write, and the value to write is NaN (the value to write wasn't found in the DB)...skip these
                        if (!(operation == "WRITE" && double.IsNaN(requestGroup.writeLookup[tagID])))
                        {
                            MSDRequestList.Add(MSD);
                            newTag = new Tag(tagID);
                            newTag.DeviceTagName = signalName;
                            newTag.DeviceTagAddress = MSD.ToString();
                            newTag.ProtocolDataType = DataType.GetType(dataType);
                            MSDTagList.Add(newTag);
                        }
                    }
                    else
                    {
                        // we'll have to identify it by name and request the MSD for future use
                        // add the name to the signal name request list
                        // unless the operation is a write, and the value to write is NaN (the value to write wasn't found in the DB)...skip these
                        if (!(operation == "WRITE" && double.IsNaN(requestGroup.writeLookup[tagID])))
                        {
                            SignalNameRequestList.Add(signalName);
                            newTag = new Tag(tagID);
                            newTag.DeviceTagName = signalName;
                            newTag.DeviceTagAddress = signalName;
                            newTag.ProtocolDataType = DataType.GetType(dataType);
                            SignalNameTagList.Add(newTag);
                        }
                    }
                }

                if (MSDRequestList.Count > 0)
                {
                    List<DataRequest> MSDRequests = new List<DataRequest>();
                    if (operation == "READ")
                    {   
                        if (requestGroup.Protocol.ToUpper() == "BSAP")
                            MSDRequests = SerialReadValuesByMSD(localAddress, MSDRequestList.ToArray(), maxRequestLength);
                        if (requestGroup.Protocol.ToUpper() == "BSAPUDP")
                            MSDRequests = UDPReadValuesByMSD(ip,deviceSettings, MSDRequestList.ToArray(), maxRequestLength);

                        int tagIdx = 0;
                        foreach (DataRequest req in MSDRequests)
                        {
                            for (int i = 0; i < req.TagList.Capacity; i++)
                            {                                
                                if (allTagConfigs != null)
                                {
                                    req.TagList.Add(MSDTagList[tagIdx]);
                                    req.DataPointConfig.Add(allTagConfigs[req.TagList[i].TagID]);
                                }
                                tagIdx++;
                            }
                        }
                    }

                    if (operation == "WRITE")
                    {
                        List<float> values = new List<float>();
                        List<string> types = new List<string>();

                        foreach (Tag tag in MSDTagList)
                        {
                            if (requestGroup.writeLookup.ContainsKey(tag.TagID))
                            {
                                types.Add(tag.ProtocolDataType.Name);
                                values.Add((float)requestGroup.writeLookup[tag.TagID]);
                            }
                            
                        }

                        if (requestGroup.Protocol.ToUpper() == "BSAP")
                            MSDRequests = SerialWriteValuesByMSD(localAddress, MSDRequestList.ToArray(), types.ToArray(), values.ToArray());

                        if (requestGroup.Protocol.ToUpper() == "BSAPUDP")
                            MSDRequests = UDPWriteValuesByMSD(ip,deviceSettings,MSDRequestList.ToArray(), types.ToArray(), values.ToArray());

                    }
                    foreach (DataRequest req in MSDRequests)
                        req.ValuesToWrite = requestGroup.writeLookup;

                    requestGroup.ProtocolRequestList.AddRange(MSDRequests);
                }

                if (SignalNameRequestList.Count > 0)
                {
                    List<DataRequest> SignalNameRequests = new List<DataRequest>();
                    if (operation == "READ")
                    {
                        if (requestGroup.Protocol.ToUpper()=="BSAP")
                            SignalNameRequests = SerialReadValuesByName(localAddress, SignalNameRequestList.ToArray(), maxRequestLength);
                        
                        if (requestGroup.Protocol.ToUpper()=="BSAPUDP")
                            SignalNameRequests = UDPReadValuesByName(ip,deviceSettings,SignalNameRequestList.ToArray(), maxRequestLength);

                        int tagIdx = 0;
                        foreach (DataRequest req in SignalNameRequests)
                        {
                            for (int i = 0; i < req.TagList.Capacity; i++)
                            {
                                if (allTagConfigs != null)
                                {
                                    req.TagList.Add(SignalNameTagList[tagIdx]);
                                    req.DataPointConfig.Add(allTagConfigs[req.TagList[i].TagID]);
                                }
                                tagIdx++;
                            }
                        }

                    }

                    if (operation == "WRITE")
                    {
                        List<float> values = new List<float>();
                        List<string> types = new List<string>();


                        foreach (Tag tag in SignalNameTagList)
                        {
                            if (requestGroup.writeLookup.ContainsKey(tag.TagID))
                            {            
                                types.Add(tag.ProtocolDataType.Name);
                                values.Add((float)requestGroup.writeLookup[tag.TagID]);
                            }
                        }

  
                        if (requestGroup.Protocol.ToUpper() == "BSAP")
                            SignalNameRequests = SerialWriteValuesByName(localAddress, SignalNameRequestList.ToArray(), types.ToArray(), values.ToArray());
                        if (requestGroup.Protocol.ToUpper() == "BSAPUDP")
                            SignalNameRequests = UDPWriteValuesByName(ip,deviceSettings, SignalNameRequestList.ToArray(), types.ToArray(), values.ToArray());  
                    }

                    requestGroup.ProtocolRequestList.AddRange(SignalNameRequests);

                    
                }
            }

            int idx = 0;
    
            foreach (DataRequest req in requestGroup.ProtocolRequestList)
            {
                req.Protocol = requestGroup.Protocol;
                req.GroupID = requestGroup.ID;
                req.GroupIdxNumber = idx.ToString();
                req.ParentGroup = requestGroup;
                req.ExpectedResponseSize = CalculateExpectedResponseSize(req);
                idx++;
            }
        }

        public static int CalculateExpectedResponseSize(DataRequest req)
        {
            int totalbytes = 0;
            bool expectMSD = false;
            bool isReadRequest = true;
          
            if (req.Protocol.ToUpper()=="BSAPUDP")
            {
                totalbytes = 25;
                expectMSD = req.RequestBytes[27] == 0xD0;
                isReadRequest = req.RequestBytes[25] == 0x00 || req.RequestBytes[25] == 0x04;
            }

            if (req.Protocol.ToUpper() == "BSAP")
            {
                totalbytes = 14;
                expectMSD = req.RequestBytes[11] == 0xD0;
                isReadRequest = req.RequestBytes[9] == 0x00 || req.RequestBytes[9] == 0x04;
            }

            // for reads, the response size depends on the # of signals and their types
            if (isReadRequest)
            {
                int nonValueBytes=1; // the type byte
                if (expectMSD)
                    nonValueBytes += 2; // two extra bytes for the address

                foreach (Tag tag in req.TagList)
                {
                    if (tag.ProtocolDataType == DataType.Analog)
                        totalbytes += 4 + nonValueBytes;

                    if (tag.ProtocolDataType == DataType.Logical)
                        totalbytes += 1 + nonValueBytes;
                }
            }

            // for writes, the response should be 24 (unless there were errors)

            return totalbytes;
        }

        public static void IdentifyWrites(RequestGroup requestGroup)
        {
            
            if (requestGroup.writeLookup == null)
                requestGroup.writeLookup = new Dictionary<Guid, double>();

            // add any tags that are being written to the 'writeLookup' list, the values to be written will be looked up from the database
            // like this

            string header = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals.Split('|')[0];
            string operation = header.Split(':')[2];

            if (operation.ToUpper() != "WRITE")
                return;

            string[] tags = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals.Split('|');
            string[] tagInfo;

            for (int i = 1; i < tags.Length; i++)
            {
                tagInfo = tags[i].Split(':');
                requestGroup.writeLookup.Add(Guid.Parse(tagInfo[2]), double.NaN);
            }
        }

        public static void ValidateResponse(DataRequest requestObj, bool interpret = true)
        {
            if (requestObj.Protocol == "BSAP")
            {
                ValidateSerialResponse(requestObj, interpret);
            }
            if (requestObj.Protocol == "BSAPUDP")
            {
                ValidateUDPResponse(requestObj, interpret);
            }
        }


        private static void ValidateSerialResponse(DataRequest requestObj, bool interpret = true)
        {
            try
            {
                byte[] request = UnescapeDLE(new List<byte>(requestObj.RequestBytes)).ToArray();
                byte[] response = UnescapeDLE(new List<byte>(requestObj.ResponseBytes)).ToArray();

                // check for bad CRC,message length, etc...

                // minimum length
                if (requestObj.ActualResponseSize < 11)
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "Unexpected number of bytes (" + requestObj.ActualResponseSize + "), expected at least 11";
                    goto BadResponse;
                }

                bool isResponseToWriteReq = (IntHelpers.GetBit(request[9], 7));

                // check the CRC       
                if (!CRCCheck(response))
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "CRC Fail";
                    goto BadResponse;
                }

                // the serial number of the response matches the request
                if (response[3] != request[3])
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "Serial number mismatch";
                    goto BadResponse;
                }

                // the application sequence number of the response matches the request
                if (response[5] != request[5] || response[6] != request[6])
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "Application sequence number mismatch";
                    goto BadResponse;
                }

                // the destination function code is correct (A0 = RDB Access)
                if (response[7] != 0xA0)
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "Incorrect destination function code " + response[7] + ", expected 160 (0xA0)";
                    goto BadResponse;
                }

                // check for/handle error codes
                if (response[9] != 0x00)
                {
                    byte goodcount = 0;
                    byte badcount = 0;
                    byte errorCode = response[9];
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    List<string> errorMessages = new List<string>();
                    if (IntHelpers.GetBit(errorCode, 7)) // bit 7 is on (remote is reporting an error)
                    {

                        if (IntHelpers.GetBit(errorCode, 6))
                            errorMessages.Add("Invalid data base function requested");
                        if (IntHelpers.GetBit(errorCode, 5))
                            errorMessages.Add("Version number does not match");
                        if (IntHelpers.GetBit(errorCode, 4))
                            errorMessages.Add("No match found");
                        if (IntHelpers.GetBit(errorCode, 3))
                            errorMessages.Add("Analog value found in packed logical processing");
                        if (IntHelpers.GetBit(errorCode, 2))
                            errorMessages.Add("No match on name found or Memory Read request exceeds maximum allow message buffer byte count");
                        if (errorMessages.Count > 0)
                        {
                            requestObj.ErrorMessage = string.Join(", ", errorMessages.ToArray());
                            goto BadResponse;
                        }
                        else
                        {
                            // if only bit 7 is on, there are error code(s) in specific element(s), find them
                            int numSignals = response[10];
                            int i = 11;
                            byte EER;
                            int signalIndex = 1;
                            byte FS1 = request[11];
                            string datatype;
                            while (i < response.Length && signalIndex <= numSignals)
                            {
                                EER = response[i];
                                if (EER != 0)
                                {
                                    badcount++;
                                    // this element has an error code
                                    switch (EER)
                                    {
                                        case 0x02: errorMessages.Add("signal index " + signalIndex + ": attempt to write to a read only area"); break;
                                        case 0x04: errorMessages.Add("signal index " + signalIndex + ": Invalid MSD address or signal name"); break;
                                        case 0x05: errorMessages.Add("signal index " + signalIndex + ": Security Violation"); break;
                                        case 0x06: errorMessages.Add("signal index " + signalIndex + ": Attempt to write to manual inhibited signal"); break;
                                        case 0x07: errorMessages.Add("signal index " + signalIndex + ": Invalid write attempt (eg. set an analog signal to ON)"); break;
                                        case 0x08: errorMessages.Add("signal index " + signalIndex + ": Premature End_of_Data encountered"); break;
                                        case 0x09: errorMessages.Add("signal index " + signalIndex + ": Incomplete Remote Data Base request"); break;
                                        case 0x10: errorMessages.Add("signal index " + signalIndex + ": No match for indicated signal name"); break;
                                        default: errorMessages.Add("signal index " + signalIndex + ": unrecognized error code " + EER); break;
                                    }
                                    i += 1;
                                }
                                else
                                {
                                    // this element is good
                                    goodcount++;

                                    if (!isResponseToWriteReq)
                                    {
                                        // get the data type (so we know how many bytes to skip to find the next EER byte)
                                        i += 1;
                                        datatype = GetDataType(response[i]);
                                        if (datatype == "analog") i += 4;
                                        if (datatype == "logical") i += 1;
                                        if (datatype == "string")
                                        {
                                            while (response[i] != 0x00) // find the null terminator
                                                i += 1;
                                        }

                                        // if the MSD was requested, skip an extra two bytes
                                        if (FS1 == FS1_typeAndvalueAndMSD)
                                            i += 2;
                                    }
                                    i++; // move to the first byte of the next element
                                }
                                signalIndex++;
                            }
                            if (goodcount == 0)
                                requestObj.SetStatus(DataRequest.RequestStatus.Error);

                            if (goodcount > 0 && badcount > 0)
                                requestObj.SetStatus(DataRequest.RequestStatus.PartialSuccess);

                            requestObj.ErrorMessage = string.Join(", ", errorMessages.ToArray());
                            goto BadResponse;
                        }

                    }

                }

                if (requestObj.Status != DataRequest.RequestStatus.PartialSuccess)
                    requestObj.SetStatus(DataRequest.RequestStatus.Success);


                // do interpretation of the returned data
                if (interpret)
                    InterpretSerialResponse(ref requestObj);
                return;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "BSAP Protocol ValidateSerialResponse() general error");
                return;
            }



        // handle any additional operations required after a bad response
        BadResponse:

            return;
        }

        public static void ValidateUDPResponse(DataRequest requestObj,bool interpret = true)
        {
            try
            {
                byte[] request = requestObj.RequestBytes.ToArray();
                byte[] response = requestObj.ResponseBytes.ToArray();

                if (response.Length == 0)
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "No response";
                    goto BadResponse;
                }

                // check for "nothing at this address" response
                if (response.Length == 14)
                {
                    if (BitConverter.ToUInt16(new byte[] { response[2], response[3] }, 0) == 0)
                    {
                        requestObj.SetStatus(DataRequest.RequestStatus.Error);
                        requestObj.ErrorMessage = "Incorrect device address (" + request[21] + ")";
                        goto BadResponse;
                    }
                }
            
                // minimum length
                if (requestObj.ActualResponseSize < 14)
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "Too short, minimum response size is 14 bytes";
                    goto BadResponse;
                }

                bool isResponseToWriteReq = (IntHelpers.GetBit(request[25], 7));

      
         
                byte[] responsePacket = requestObj.ResponseBytes;

                // the sequence number of the response matches the request
                if (responsePacket[10] != request[6] || responsePacket[11] != request[7] || responsePacket[12] != request[8] || responsePacket[13] != request[9])
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "Sequence number mismatch";
                    goto BadResponse;
                }
                requestObj.ErrorMessage = "";


                // make sure the data subpacket is complete
                ushort dataSize = BitConverter.ToUInt16(responsePacket, 14);
                if (responsePacket.Length < 14 + dataSize)
                {
                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    requestObj.ErrorMessage = "Unexpected end of data";
                }

                // check for/handle error codes
                byte errorCode = responsePacket[23];
                if (errorCode != 0x00)
                {
                    byte goodcount = 0;
                    byte badcount = 0;

                    requestObj.SetStatus(DataRequest.RequestStatus.Error);
                    List<string> errorMessages = new List<string>();
                    if (IntHelpers.GetBit(errorCode, 7)) // bit 7 is on (remote is reporting an error)
                    {
                        if (IntHelpers.GetBit(errorCode, 6))
                            errorMessages.Add("Invalid data base function requested");
                        if (IntHelpers.GetBit(errorCode, 5))
                            errorMessages.Add("Version number does not match");
                        if (IntHelpers.GetBit(errorCode, 4))
                            errorMessages.Add("No match found");
                        if (IntHelpers.GetBit(errorCode, 3))
                            errorMessages.Add("Analog value found in packed logical processing");
                        if (IntHelpers.GetBit(errorCode, 2))
                            errorMessages.Add("No match on name found or Memory Read request exceeds maximum allow message buffer byte count");
                        if (errorMessages.Count > 0)
                        {
                            requestObj.ErrorMessage = string.Join(", ", errorMessages.ToArray());
                            goto BadResponse;
                        }
                        else
                        {
                            // if only bit 7 is on, there are error code(s) in specific element(s), find them
                            int numSignals = responsePacket[24];
                            int i = 25;
                            byte EER;
                            int signalIndex = 1;
                            byte FS1 = request[27];
                            string datatype;
                            while (i < response.Length && signalIndex <= numSignals)
                            {
                                EER = responsePacket[i];
                                if (EER != 0)
                                {
                                    badcount++;
                                    // this element has an error code
                                    switch (EER)
                                    {
                                        case 0x02: errorMessages.Add("signal index " + signalIndex + ": attempt to write to a read only area"); break;
                                        case 0x04: errorMessages.Add("signal index " + signalIndex + ": Invalid MSD address or signal name"); break;
                                        case 0x05: errorMessages.Add("signal index " + signalIndex + ": Security Violation"); break;
                                        case 0x06: errorMessages.Add("signal index " + signalIndex + ": Attempt to write to manual inhibited signal"); break;
                                        case 0x07: errorMessages.Add("signal index " + signalIndex + ": Invalid write attempt (eg. set an analog signal to ON)"); break;
                                        case 0x08: errorMessages.Add("signal index " + signalIndex + ": Premature End_of_Data encountered"); break;
                                        case 0x09: errorMessages.Add("signal index " + signalIndex + ": Incomplete Remote Data Base request"); break;
                                        case 0x10: errorMessages.Add("signal index " + signalIndex + ": No match for indicated signal name"); break;
                                        default: errorMessages.Add("signal index " + signalIndex + ": unrecognized error code " + EER); break;
                                    }
                                    i += 1;
                                }
                                else
                                {
                                    // this element is good
                                    goodcount++;

                                    if (!isResponseToWriteReq)
                                    {
                                        // get the data type (so we know how many bytes to skip to find the next EER byte)
                                        i += 1;
                                        datatype = GetDataType(responsePacket[i]);
                                        if (datatype == "analog") i += 4;
                                        if (datatype == "logical") i += 1;
                                        if (datatype == "string")
                                        {
                                            while (responsePacket[i] != 0x00) // find the null terminator
                                                i += 1;
                                        }

                                        // if the MSD was requested, skip an extra two bytes
                                        if (FS1 == FS1_typeAndvalueAndMSD)
                                            i += 2;
                                    }
                                    i++; // move to the first byte of the next element
                                }
                                signalIndex++;
                            }
                            if (goodcount == 0)
                                requestObj.SetStatus(DataRequest.RequestStatus.Error);

                            if (goodcount > 0 && badcount > 0)
                                requestObj.SetStatus(DataRequest.RequestStatus.PartialSuccess);

                            if (errorMessages.Count > 0)
                                requestObj.ErrorMessage = string.Join(", ", errorMessages.ToArray());
                        }

                    }

                }
                    

                if (requestObj.Status != DataRequest.RequestStatus.PartialSuccess)
                    requestObj.SetStatus(DataRequest.RequestStatus.Success);

                // do interpretation of the returned data
                if (interpret)
                    InterpretUDPResponse(ref requestObj);
                return;
            }       
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "BSAP Protocol ValidateUDPResponse() general error");
                return;
            }

            // handle any additional operations required after a bad response
            BadResponse:

            return;
        }

        public static void InterpretUDPResponse(ref DataRequest requestObj)
        {
            try
            { 
                byte[] response = requestObj.ResponseBytes;
                byte[] request = requestObj.RequestBytes;

                // get the returned seq number
                UInt32 lastSeqRcvd = BitConverter.ToUInt32(response, 6);
                if (lastSeqRcvd > 0)
                    LastSeqRcvd = lastSeqRcvd;

                // if this was a write operation, there's nothing for us to do with the response (it's already been checked for error codes)
                // just send an ack back
                if (IntHelpers.GetBit(request[25], 7))
                {
                    requestObj.AckBytes = new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, requestObj.ResponseBytes[6], requestObj.ResponseBytes[7], requestObj.ResponseBytes[8], requestObj.ResponseBytes[9] };
                    return;
                }
                // if there is no data subpacket included in this response, there's nothing more to do
                if (BitConverter.ToUInt16(response, 2) == 0)
                {
                    foreach (Tag tag in requestObj.TagList)
                    {
                        tag.TagID = Guid.Empty; 
                    }
                    return;
                }
                bool includesEERByte = false;
                int numSignals = response[24];

                int tagIdx = 0;

                string datatype;


                bool includesMSD = (request[25] == 0x04); // we've set up all read-by-name requests to include the MSD in the response


                if (IntHelpers.GetBit(response[23], 7))  // if bit 7 of byte 9 is on, there is at least one signal specific error (an extra byte is included at the beginning of each signal to indcate the error)
                    includesEERByte = true;

                int byteIdx = 25; // data starts at byte 25
                while (tagIdx < numSignals)
                {
                    // if EER bytes are included, check it
                    if (includesEERByte)
                    {
                        // if the EER byte has an error, move to the next signal
                        if (response[byteIdx] != 0x00)
                        {
                            requestObj.TagList[tagIdx].TagID = Guid.Empty; // mark this tag so it gets ignored from here on (did not get a good value for this tag)
                            byteIdx++;
                            tagIdx++;
                            continue;
                        }
                        else // move to the next byte in this signal
                            byteIdx++;
                    }

                    // get the data type from the type byte
                    datatype = GetDataType(response[byteIdx]);

                    // move to the next byte (the first value byte)
                    byteIdx++;

                    // get the value (and set the timestamp/quality)
                    switch (datatype)
                    {
                        case "analog":
                            requestObj.TagList[tagIdx].Value = BitConverter.ToSingle(response, byteIdx);
                            byteIdx += 4;
                            break;
                        case "logical":
                            requestObj.TagList[tagIdx].Value = response[byteIdx];
                            byteIdx += 1;
                            break;
                    }
                    requestObj.TagList[tagIdx].Timestamp = requestObj.ResponseTimestamp;
                    requestObj.TagList[tagIdx].Quality = 192;

                    // get the MSD (if included), store it in the tag config for future use
                    if (includesMSD)
                    {
                        ushort MSD = BitConverter.ToUInt16(response, byteIdx);
                        byteIdx += 2;
                        lock (requestObj.DataPointConfig[tagIdx])
                        {
                            requestObj.DataPointConfig[tagIdx].DeviceTagAddress = MSD.ToString();
                            requestObj.DataPointConfig[tagIdx].DeviceTagName = requestObj.TagList[tagIdx].DeviceTagName;
                        }
                    }

                    // move to  the next signal
                    tagIdx++;
                }
            } catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "BSAP Protocol InterpretUDPResponse() general error");
                return;
            }

            // now we need to acknowledge receipt of the data
            requestObj.AckBytes = new byte[] { 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, requestObj.ResponseBytes[6], requestObj.ResponseBytes[7], requestObj.ResponseBytes[8], requestObj.ResponseBytes[9] };
        }

        public static void InterpretSerialResponse(ref DataRequest requestObj)
        {
            try
            {
                byte[] response = UnescapeDLE(new List<byte>(requestObj.ResponseBytes)).ToArray();
                byte[] request = UnescapeDLE(new List<byte>(requestObj.RequestBytes)).ToArray();

                // if this was a write operation, there's nothing for us to do with the response (it's already been checked for error codes)
                if (IntHelpers.GetBit(request[9], 7))
                    return;


                bool includesEERByte = false;
                int numSignals = response[10];

                int tagIdx = 0;

                string datatype;


                bool includesMSD = (request[9] == 0x04); // we've set up all read-by-name requests to include the MSD in the response


                if (IntHelpers.GetBit(response[9], 7))  // if bit 7 of byte 9 is on, there is at least one signal specific error (an extra byte is included at the beginning of each signal to indcate the error)
                    includesEERByte = true;

                int byteIdx = 11; // data starts at byte 11
                while (tagIdx < numSignals)
                {
                    // if EER bytes are included, check it
                    if (includesEERByte)
                    {
                        // if the EER byte has an error, move to the next signal
                        if (response[byteIdx] != 0x00)
                        {
                            requestObj.TagList[tagIdx].TagID = Guid.Empty; // mark this tag so it gets ignored from here on
                            byteIdx++;
                            tagIdx++;
                            continue;
                        }
                        else // move to the next byte in this signal
                            byteIdx++;
                    }

                    // get the data type from the type byte
                    datatype = GetDataType(response[byteIdx]);

                    // move to the next byte (the first value byte)
                    byteIdx++;

                    // get the value (and set the timestamp/quality)
                    switch (datatype)
                    {
                        case "analog":
                            requestObj.TagList[tagIdx].Value = BitConverter.ToSingle(response, byteIdx);
                            byteIdx += 4;
                            break;
                        case "logical":
                            requestObj.TagList[tagIdx].Value = response[byteIdx];
                            byteIdx += 1;
                            break;
                    }
                    requestObj.TagList[tagIdx].Timestamp = requestObj.ResponseTimestamp;
                    requestObj.TagList[tagIdx].Quality = 192;

                    // get the MSD (if included), store it in the tag config for future use
                    if (includesMSD)
                    {
                        ushort MSD = BitConverter.ToUInt16(response, byteIdx);
                        byteIdx += 2;
                        lock (requestObj.DataPointConfig[tagIdx])
                        {
                            requestObj.DataPointConfig[tagIdx].DeviceTagAddress = MSD.ToString();
                            requestObj.DataPointConfig[tagIdx].DeviceTagName = requestObj.TagList[tagIdx].DeviceTagName;
                        }
                    }

                    // move to  the next signal
                    tagIdx++;
                }
            } catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "BSAP Protocol InterpretSerialResponse() general error");
                return;
            }
            

        }


        public static string GetDataType(byte typeByte)
        {
            if (!IntHelpers.GetBit(typeByte, 1))
                return "logical";
            else
            {
                if (IntHelpers.GetBit(typeByte, 0))
                    return "string";
                else
                    return "analog";
            }
        }

        public static bool CRCCheck(byte[] data)
        {
            if (data.Length < 2)
                return false;

            ushort responseCRC = IntHelpers.ToUInt16(data[data.Length - 2], data[data.Length - 1]);

            byte[] dataForCRCCalculation = new byte[data.Length - 5];
            Array.Copy(data, 2,dataForCRCCalculation,0, data.Length - 6);
            dataForCRCCalculation[dataForCRCCalculation.Length - 1] = 0x03;
            byte[] unescapedDataforCRC = UnescapeDLE(new List<byte>(dataForCRCCalculation)).ToArray();
            ushort calcdCRC = CRCHelpers.CalcCRC16(unescapedDataforCRC, CRCInitial, CRCPoly, CRCSwapOutputBytes);
            calcdCRC = IntHelpers.SwapBytes(calcdCRC);

            return (responseCRC == calcdCRC);
        }


        public sealed class DataType : DataTypeBase
        {
            public static readonly DataType Analog = new DataType("FL", 4, typeof(float));
            public static readonly DataType Logical = new DataType("BIN", 1, typeof(byte));
            private static readonly List<string> TypeList = new List<string>(new string[] { "FL", "BIN" });
            private DataType(string name, byte size, Type hostDataType) : base(name, size, hostDataType)
            {

            }

            public static bool IsValidType(string type)
            {
                return TypeList.Contains(type);
            }

            public byte[] ToBytes(object value)
            {
                byte[] bytes = null;

                if (this == Analog)
                {
                    bytes = BitConverter.GetBytes(Convert.ToSingle(value));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == Logical)
                {
                    bytes = new byte[1] { Convert.ToByte(value) };
                }

       

                return bytes;
            }

            public object FromBytes(byte[] bytes)
            {

                // wrong number of bytes returned for the data type, return null
                if (bytes.Length != this.Size && this.Size != 0)
                    return null;

                if (this == Logical)
                {
                    return (byte)bytes[0];
                }

                if (this == Analog)
                {
                    return BitConverter.ToSingle(bytes, 0);

                }

 
                // shouldn't be possible to get here, but all paths must return a value so...return null
                return null;
            }

            public static DataType GetType(string typeName)
            {

                switch (typeName.ToUpper())
                {
                    case "FL": return Analog;
                    case "BIN": return Logical;
                    default: return null;
                }
            }
        }
    

        public static List<DataRequest> UDPReadValuesByMSD(string ipaddress,FDADevice deviceSettings, ushort[] signalList, int maxMessageSize = absoluteMaxMessageLength)
        {
            List<DataRequest> output = new List<DataRequest>();
            if (maxMessageSize > absoluteMaxMessageLength)
                maxMessageSize = absoluteMaxMessageLength;

            int totalSignalCount = signalList.Length;
           

            // find the maximum number of signals that we can put into a single request
            // 32 bytes of overhead, 2 bytes per MSD
            byte maxSignalsPerRequest = (byte)Math.Floor((maxMessageSize - 32) / 2.0);

            // find the number of signals we'll put in each message (if fewer than max signals were requested put them all in, otherwise use the max)
            byte perMessageSignalCount;
            if (totalSignalCount <= maxSignalsPerRequest)
                perMessageSignalCount = (byte)totalSignalCount;
            else
                perMessageSignalCount = maxSignalsPerRequest;

            // limit the number of signals per request to the maximum that can be responded to in a single message
            if (perMessageSignalCount > MaxSignalsPerMessage)
                perMessageSignalCount = MaxSignalsPerMessage;

            // build the message header template (serial and sequence numbers to be filled in later)
            List<byte> header = new List<byte>(new byte[]
            {
              0x0E, // request init
              0x00,
              0x01,
              0x00,
              0x06,
              0x00,
              0x00,      // application sequence number (low byte)        - to update for specific request
              0x00,      // application sequence number                   - to update for specific request
              0x00,      // application sequence number                   - to update for specific request
              0x00,      // application sequence number (high byte)       - to update for specific request
              0x00,      // last requence # received (low byte)           - to update for specific request
              0x00,      // last requence # received                      - to update for specific request
              0x00,      // last requence # received                      - to update for specific request
              0x00,      // last requence # received (high byte)          - to update for specific request
              0x00,      // number of bytes to follow (including this byte)  - to update for specific request
              0x00,      // number of bytes to follow 
              0x00,      // data type
              0x00,      // data type
              0x00,      // message exchange code
              0x00,      // application sequence # again (low byte)     - to update for specific request
              0x00,      // application sequence # again (high byte)    - to update for specific request
              0x00,      // local address of the device (use 0 for BSAPUDP)
              0x00,      // local address of the device
              DSTFuncCode, // A0 = RDB access
              0x00,      // routing table version
              ReadMSDFuncCode,  // 0x00 = read by MSD    
              FSS,
              FS1_typeAndvalue,
              VersionLow,
              VersionHigh,
              0x0F,
              0x00  // signal count                  - to update for specific request
            });

            List<byte> thisMessage = new List<byte>(header);

            byte thisMessageSignalCount = 0;
            DataRequest request;
            List<Tag> tags = new List<Tag>();
            for (int i = 0; i < signalList.Length; i++)
            {

                thisMessage.Add(IntHelpers.GetLowByte(signalList[i]));
                thisMessage.Add(IntHelpers.GetHighByte(signalList[i]));
                thisMessageSignalCount++;

                if (thisMessageSignalCount == perMessageSignalCount || i == signalList.Length - 1)
                {
                    byte dataSize = (byte)(18 + thisMessageSignalCount * 2);
                    thisMessage[31] = thisMessageSignalCount;
                    UDPFinalizeMessage(ref thisMessage, dataSize);
                  

                    // create a DataRequest object for it
                    request = CreateDataRequestObject(thisMessage.ToArray());
                    request.Protocol = "BSAPUDP";
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.DeviceSettings = deviceSettings;
                    request.UDPIPAddr = ipaddress;

                    // add it to the request list
                    if (thisMessageSignalCount > 0)
                        output.Add(request);

                    // start a new message
                    thisMessage = new List<byte>(header);

                    thisMessageSignalCount = 0;
                }
            }

            return output;
        }

        public static List<DataRequest> UDPReadValuesByName(string ip,FDADevice deviceSettings, string[] signalList, int maxMessageSize = absoluteMaxMessageLength)
        {
            if (maxMessageSize > absoluteMaxMessageLength)
                maxMessageSize = absoluteMaxMessageLength;

            List<DataRequest> output = new List<DataRequest>();

            int totalSignalCount = signalList.Length;


            // build the message header
            List<byte> header = new List<byte>(new byte[]
            {
              0x0E,     // request init
              0x00,
              0x01,
              0x00,
              0x06,
              0x00,
              0x00,      // application sequence number (low byte)        - to update for specific request
              0x00,      // application sequence number                   - to update for specific request
              0x00,      // application sequence number                   - to update for specific request
              0x00,      // application sequence number (high byte)       - to update for specific request
              0x00,      // last requence # received (low byte)           - to update for specific request
              0x00,      // last requence # received                      - to update for specific request
              0x00,      // last requence # received                      - to update for specific request
              0x00,      // last requence # received (high byte)          - to update for specific request
              0x00,      // number of bytes to follow (including this byte)  - to update for specific request
              0x00,      // number of bytes to follow (including this byte)  - to update for specific request
              0x00,      // type of data
              0x00,      // type of data
              0x00,      // message exchange code
              0x00,      // application sequence # (low byte)  again (low byte)     - to update for specific request
              0x00,      // application sequence # (low byte)  again (high byte)    - to update for specific request
              0x00,      // local address of the device (use 0 for UDP)
              0x00,      // local address of the device (high byte)
              DSTFuncCode, // A0 = RDB access
              0x00,      // node status byte      
              ReadNameFuncCode,  // 0x04 = read by Name    
              0x03, // FSS
              FS1_typeAndvalueAndMSD,
              0x00,
              0x0F,
              0x00  // signal count                  - to update for specific request
            });

            List<byte> thisMessage = new List<byte>(header);

            int thisMessageSize = header.Count; 
            byte thisMessageSignalCount = 0;
            int sigNameCharsCount = 0;
            DataRequest request;

            for (int signalIndex = 0; signalIndex < signalList.Length; signalIndex++)
            {

                // if adding this signal name would put the length over the limit
                if (thisMessageSize + signalList[signalIndex].Length + 1 > maxMessageSize || thisMessageSignalCount == MaxSignalsPerMessage)
                {
                    // complete the current message
                    byte datasize = (byte)(17 + sigNameCharsCount);
                    thisMessage[30] = thisMessageSignalCount;
                    UDPFinalizeMessage(ref thisMessage, datasize);
   

                    request = CreateDataRequestObject(thisMessage.ToArray());
                    request.Protocol = "BSAPUDP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);

                    // add it to the list (if signal count > 0)
                    if (thisMessageSignalCount > 0)
                        output.Add(request);

                    // and reset for the next one
                    thisMessage = new List<byte>(header);
                    thisMessageSize = header.Count;
                    thisMessageSignalCount = 0;
                    sigNameCharsCount = 0;
                }

                thisMessage.AddRange(Encoding.ASCII.GetBytes(signalList[signalIndex]));
                thisMessage.Add(0x00); // null terminate the string
                thisMessageSize += signalList[signalIndex].Length + 1;
                sigNameCharsCount += signalList[signalIndex].Length + 1;
                thisMessageSignalCount++;

                if (signalIndex == signalList.Length - 1)
                {
                    // complete the current message

                    // update the signal count byte
                    thisMessage[30] = thisMessageSignalCount;
                    byte dataSize = (byte)(17 + sigNameCharsCount);
                    
                    UDPFinalizeMessage(ref thisMessage, dataSize);


                    request = CreateDataRequestObject(thisMessage.ToArray());
                    request.Protocol = "BSAPUDP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    request.DeviceSettings = deviceSettings;
                    request.UDPIPAddr = ip;
                    if (thisMessageSignalCount > 0)
                        output.Add(request);
                }
            }

            return output;
        }

        public static List<DataRequest> UDPWriteValuesByMSD(string ipaddress,FDADevice deviceSettings, ushort[] signalList, string[] types, float[] values, int maxMessageSize = absoluteMaxMessageLength)
        {
            List<DataRequest> output = new List<DataRequest>();

            // build the message header
            List<byte> header = new List<byte>(new byte[]
            {
              0x0E,     // request init (6 bytes)
              0x00,
              0x01,
              0x00,
              0x06,
              0x00,
              0x00,      // application sequence number (low byte)        - to update for specific request
              0x00,      // application sequence number                   - to update for specific request
              0x00,      // application sequence number                   - to update for specific request
              0x00,      // application sequence number (high byte)       - to update for specific request
              0x00,      // last requence # received (low byte)           - to update for specific request
              0x00,      // last requence # received                      - to update for specific request
              0x00,      // last requence # received                      - to update for specific request
              0x00,      // last requence # received (high byte)          - to update for specific request
              0x00,      // number of bytes to follow (including this byte)  - to update for specific request
              0x00,      // not used
              0x00,      // not used
              0x00,      // not used
              0x00,      // not used
              0x00,      // application sequence # again (low byte)     - to update for specific request
              0x00,      // application sequence # again (high byte)    - to update for specific request
              0x00,      // local address of the device (use 0 for UDP)
              0x00,      // not used
              DSTFuncCode, // A0 = RDB access
              0x00,      // node status
              WriteByMSDFuncCode, // 0x80 = write by MSD
              VersionLow,  // version # low byte      
              VersionHigh, // version # high byte    
              0x0f,        // Security byte
              0x00,      // signal count - to update for a spcific request               
            });


            List<byte> thisMessage = new List<byte>(header);
            int thisMessageSize = thisMessage.Count;
            byte thisMessageDataSize = 16;
            byte thisMessageSignalCount = 0;
            byte WFD;
            DataRequest request;
            int newMessageSize;
            for (int i = 0; i < signalList.Length; i++)
            {
                // check if adding this signal will put the message size over the limit
                newMessageSize = 3; // WFD + 2 byte address (if analog, 4 more byes will  be added next)
                if (types[i] == "FL")
                {
                    newMessageSize += 4;
                }
                if (newMessageSize > maxMessageSize)
                {
                    // complete the current message
                    thisMessage[29] = thisMessageSignalCount;
                    UDPFinalizeMessage(ref thisMessage, thisMessageDataSize);

                    request = CreateDataRequestObject(thisMessage.ToArray());
                    request.Protocol = "BSAPUDP";
                    request.DeviceSettings = deviceSettings;
                    request.UDPIPAddr = ipaddress;
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);

                    // add it to the request list
                    output.Add(request);

                    // and reset for the next one
                    thisMessage = new List<byte>(header);
                    thisMessageSize = thisMessage.Count;
                    thisMessageSignalCount = 0;
                    thisMessageDataSize = 16;
                }


                // add the address of the signal
                thisMessage.Add(IntHelpers.GetLowByte(signalList[i]));
                thisMessage.Add(IntHelpers.GetHighByte(signalList[i]));
                thisMessageSize += 2;
                thisMessageDataSize += 2;


                switch (types[i])
                {
                    case "FL":
                        thisMessage.Add(0x0B); // write field descriptor (WFD), 0x0B = set analog value                       
                        byte[] valueBytes = BitConverter.GetBytes(Convert.ToSingle(values[i]));
                        thisMessage.AddRange(valueBytes);  // value to be written
                        thisMessageSize += 5;  // WFD + 4 byte analog value
                        thisMessageDataSize += 5;
                        break;
                    case "BIN":
                        if (values[i] == 0)
                            WFD = 0x0A; // set logical "off"
                        else
                            WFD = 0x09; // set logical "on"
                        thisMessage.Add(WFD);
                        thisMessageSize += 1;
                        thisMessageDataSize += 1;
                        break;
                }
                thisMessageSignalCount++;

                // if that was the last signal, complete the current message and we're done
                if (i == signalList.Length - 1)
                {
                    // complete the current message
                    thisMessage[29] = thisMessageSignalCount;
                    UDPFinalizeMessage(ref thisMessage, thisMessageDataSize);

                    request = CreateDataRequestObject(thisMessage.ToArray());
                    request.Protocol = "BSAPUDP";
                    request.DeviceSettings = deviceSettings;
                    request.UDPIPAddr = ipaddress;
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    output.Add(request);
                }
            }

            return output;

        }


        public static List<DataRequest> UDPWriteValuesByName(string ipaddress,FDADevice deviceSettings, string[] signalList, string[] types, float[] values, int maxMessageSize = absoluteMaxMessageLength)
        {
            List<DataRequest> output = new List<DataRequest>();

            // build the message header
            List<byte> header = new List<byte>(new byte[]
            {
              0x0E,     // request init (6 bytes)
              0x00,
              0x01,
              0x00,
              0x06,
              0x00,
              0x00,      // application sequence number (low byte)        - to update for specific request
              0x00,      // application sequence number                   - to update for specific request
              0x00,      // application sequence number                   - to update for specific request
              0x00,      // application sequence number (high byte)       - to update for specific request
              0x00,      // last requence # received (low byte)           - to update for specific request
              0x00,      // last requence # received                      - to update for specific request
              0x00,      // last requence # received                      - to update for specific request
              0x00,      // last requence # received (high byte)          - to update for specific request
              0x00,      // number of bytes to follow (including this byte)  - to update for specific request
              0x00,      // not used
              0x00,      // not used
              0x00,      // not used
              0x00,      // not used
              0x00,      // application sequence # again (low byte)     - to update for specific request
              0x00,      // application sequence # again (high byte)    - to update for specific request
              0x00,      // local address of the device (use 0 for UDP)
              0x00,      // not used
              DSTFuncCode, // A0 = RDB access
              0x00,      // node status
              WriteByNameFuncCode, // 0x84 = write by Name
              0x0f,      // security
              0x00,      // signal count - to update for a specific request               
            });


            List<byte> thisMessage = new List<byte>(header);
            int thisMessageSize = thisMessage.Count;
            byte thisMessageSignalCount = 0;
            byte thisMessageDataSize = 14;
            byte WFD;
            DataRequest request;
            int newMessageSize;

            for (int i = 0; i < signalList.Length; i++)
            {
                // check if adding this signal will put the message size over the limit
                newMessageSize = 2 + signalList[i].Length; // WFD + signalname length + null terminator               
                if (types[i] == "FL")
                    newMessageSize += 4;

                if (newMessageSize > maxMessageSize)
                {
                    // complete the current message
                    thisMessage[27] = thisMessageSignalCount;
                    UDPFinalizeMessage(ref thisMessage, thisMessageDataSize);

                    request = CreateDataRequestObject(thisMessage.ToArray());
                    request.Protocol = "BSAPUDP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    request.DeviceSettings = deviceSettings;
                    request.UDPIPAddr = ipaddress;

                    // add it to the request list
                    output.Add(request);

                    // and reset for the next one
                    thisMessage = new List<byte>(header);
                    thisMessageSize = thisMessage.Count;
                    thisMessageSignalCount = 0;
                    thisMessageDataSize = 14;
                }


                // add the name of the signal
                thisMessage.AddRange(Encoding.ASCII.GetBytes(signalList[i]));
                thisMessage.Add(0x00);
                thisMessageSize += signalList[i].Length + 1;
                thisMessageDataSize += (byte)(signalList[i].Length + 1);

                switch (types[i])
                {
                    case "FL":
                        thisMessage.Add(0x0B); // write field descriptor (WFD), 0x0B = set analog value
                        thisMessage.AddRange(BitConverter.GetBytes(values[i]));  // value to be written
                        thisMessageSize += 5;  // WFD + 4 byte analog value
                        thisMessageDataSize += 5;
                        break;
                    case "BIN":
                        if (values[i] == 0)
                            WFD = 0x0A; // set logical "off"
                        else
                            WFD = 0x09; // set logical "on"
                        thisMessage.Add(WFD);
                        thisMessageSize += 1;
                        thisMessageDataSize += 1;
                        break;
                }
                thisMessageSignalCount++;

                // if that was the last signal, complete the current message and we're done
                if (i == signalList.Length - 1)
                {
                    // complete the current message
                    thisMessage[27] = thisMessageSignalCount;
                    UDPFinalizeMessage(ref thisMessage, thisMessageDataSize);

                    request = CreateDataRequestObject(thisMessage.ToArray());
                    request.Protocol = "BSAPUDP";
                    request.DeviceSettings = deviceSettings;
                    request.UDPIPAddr = ipaddress;
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    request.RequestID = Guid.NewGuid().ToString();
                    output.Add(request);
                }
            }

            return output;
        }


        private static void UDPFinalizeMessage(ref List<byte> message,byte dataSize)
        {
            UInt32 appSeqNumber = UDPGetNextApplicationSeqNumber();
            byte[] seqNumberBytes = BitConverter.GetBytes(appSeqNumber);
            message[6] = seqNumberBytes[0];
            message[7] = seqNumberBytes[1];
            message[8] = seqNumberBytes[2];
            message[9] = seqNumberBytes[3];

            // update the 'last sequence number received' bytes
            byte[] lastSeqNum = BitConverter.GetBytes(LastSeqRcvd);
            message[10] = lastSeqNum[0];
            message[11] = lastSeqNum[1];
            message[12] = lastSeqNum[2];
            message[13] = lastSeqNum[3];

            // update the 'message sequence number' bytes
            message[19] = message[6];
            message[20] = message[7];


            // update the data size byte
            message[14] = dataSize;
        }

        public static List<DataRequest> SerialReadValuesByName(byte localAddr, string[] signalList, int maxMessageSize = absoluteMaxMessageLength)
        {
            if (maxMessageSize > absoluteMaxMessageLength)
                maxMessageSize = absoluteMaxMessageLength;

            List<DataRequest> output = new List<DataRequest>();

            int totalSignalCount = signalList.Length;


            // build the message header
            List<byte> header = new List<byte>(new byte[]
            {
              localAddr,
              0x00,         // serial number
              DSTFuncCode,
              0x00,         // App sequence number (low)
              0x00,         // App sequence number(high)
              SRCFuncCode,
              NodeStatus,
              ReadNameFuncCode,
              0x03, // FSS has to use FS1 and FS2 when requesting by name (reason??), 
              FS1_typeAndvalueAndMSD,
              0x00, // FS2 not used
              0x0F, // FS3 not used, but needs to be 0F
            });


            List<byte> thisMessage = new List<byte>(header);
            List<byte> finalized;

            thisMessage.Add(0x00); // placeholder for the number of signals (to be updated later)


            int thisMessageSize = 19; // 19 bytes of overhead to start with
            byte thisMessageSignalCount = 0;
            DataRequest request;
            for (int signalIndex = 0; signalIndex < signalList.Length; signalIndex++)
            {

                // if adding this signal name would put the length over the limit
                if (thisMessageSize + signalList[signalIndex].Length + 1 > maxMessageSize || thisMessageSignalCount == MaxSignalsPerMessage)
                {
                    // complete the current message
                    thisMessage[12] = thisMessageSignalCount;
                    finalized = SerialFinalizeMessage(thisMessage);
                    request = CreateDataRequestObject(finalized.ToArray());
                    request.Protocol = "BSAP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);

                    // add it to the list (if signal count > 0)
                    if (thisMessageSignalCount > 0)
                        output.Add(request);

                    // and reset for the next one
                    thisMessage = new List<byte>(header);
                    thisMessage.Add(0x00); // placeholder for the number of signals (to be updated later)
                    thisMessageSize = 19;
                    thisMessageSignalCount = 0;
                }

                thisMessage.AddRange(Encoding.ASCII.GetBytes(signalList[signalIndex]));
                thisMessage.Add(0x00); // null terminate the string
                thisMessageSize += signalList[signalIndex].Length + 1;
                thisMessageSignalCount++;

                if (signalIndex == signalList.Length - 1)
                {
                    // complete the current message
                    thisMessage[12] = thisMessageSignalCount;
                    finalized = SerialFinalizeMessage(thisMessage);
                    request = CreateDataRequestObject(finalized.ToArray());
                    request.Protocol = "BSAP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    if (thisMessageSignalCount > 0)
                        output.Add(request);
                }
            }


            return output;
        }

        public static List<DataRequest> SerialReadValuesByMSD(byte localAddr, ushort[] signalList, int maxMessageSize = absoluteMaxMessageLength)
        {
            if (maxMessageSize > absoluteMaxMessageLength)
                maxMessageSize = absoluteMaxMessageLength;

            List<DataRequest> output = new List<DataRequest>();

            int totalSignalCount = signalList.Length;

            // find the maximum number of signals that we can put into a single request
            // 20 bytes of overhead, 2 bytes per MSD
            byte maxSignalsPerRequest = (byte)Math.Floor((maxMessageSize - 20) / 2.0);

            // find the number of signals we'll put in each message (if fewer than max signals were requested put them all in, otherwise use the max)
            byte perMessageSignalCount;
            if (totalSignalCount <= maxSignalsPerRequest)
                perMessageSignalCount = (byte)totalSignalCount;
            else
                perMessageSignalCount = maxSignalsPerRequest;

            // limit the number of signals per request to the maximum that can be responded to in a single message
            if (perMessageSignalCount > MaxSignalsPerMessage)
                perMessageSignalCount = MaxSignalsPerMessage;

            // build the message header template (serial and sequence numbers to be filled in later)
            List<byte> header = new List<byte>(new byte[]
            {
              localAddr,
              0x00,         // serial number
              DSTFuncCode,
              0x00,         // sequence number (low byte)
              0x00,         // sequence number (high byte)
              SRCFuncCode,
              NodeStatus,
              ReadMSDFuncCode,
              FSS,        
              FS1_typeAndvalue, 
              VersionLow,  
              VersionHigh,
              0xFF,
              0x00  // signal count to be updated when message is complete
            });


            List<byte> thisMessage = new List<byte>(header);
            List<byte> finalized;

            byte thisMessageSignalCount = 0;
            DataRequest request;
            List<Tag> tags = new List<Tag>();

            for (int i = 0; i < signalList.Length; i++)
            {

                thisMessage.Add(IntHelpers.GetLowByte(signalList[i]));
                thisMessage.Add(IntHelpers.GetHighByte(signalList[i]));
                thisMessageSignalCount++;

                if (thisMessageSignalCount == perMessageSignalCount || i == signalList.Length-1)
                {
                    thisMessage[13] = thisMessageSignalCount; // update the signal count byte

                    finalized = SerialFinalizeMessage(thisMessage);

                    // create a DataRequest object for it
                    request = CreateDataRequestObject(finalized.ToArray());
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    request.Protocol = "BSAP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                
                    // add it to the request list
                    if (thisMessageSignalCount > 0)
                        output.Add(request);

                    // start a new message
                    thisMessage = new List<byte>(header);                   

                    thisMessageSignalCount = 0;
                }
            }

            return output;
        }


        public static List<DataRequest> SerialWriteValuesByMSD(byte localAddress, ushort[] signalList,string[] types,float[] values, int maxMessageSize = absoluteMaxMessageLength)
        {
            if (maxMessageSize > absoluteMaxMessageLength)
                maxMessageSize = absoluteMaxMessageLength;

            List<DataRequest> output = new List<DataRequest>();



            // build the message header (starting at local address)
            List<byte> header = new List<byte>(new byte[]
            {
              localAddress,
              0x00, // serial number
              DSTFuncCode,
              0x00, // AppSeqNumber (low byte)
              0x00, // AppSeqNumber (high byte)
              SRCFuncCode,
              NodeStatus,
              WriteByMSDFuncCode,
              VersionLow,
              VersionHigh,
              0xFF, // security level
              0x00 // number of signals (to be filled in later)
            });
  

            List<byte> thisMessage = new List<byte>(header);
            int thisMessageSize = thisMessage.Count;
            byte thisMessageSignalCount = 0;
            List<byte> finalized;
            byte WFD;
            DataRequest request;
            int newMessageSize;
            for(int i= 0; i < signalList.Length; i++)
            {
                // check if adding this signal will put the message size over the limit
                newMessageSize = 3; // WFD + 2 byte address
                if (types[i] == "analog")
                    newMessageSize += 4;
                
                if (newMessageSize > maxMessageSize)
                {
                    // complete the current message
                    thisMessage[11] = thisMessageSignalCount;
                    finalized = SerialFinalizeMessage(thisMessage);
                    request = CreateDataRequestObject(finalized.ToArray());
                    request.Protocol = "BSAP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);

                    // add it to the request list
                    output.Add(request);

                    // and reset for the next one
                    thisMessage = new List<byte>(header);
                    thisMessageSize = thisMessage.Count;
                    thisMessageSignalCount = 0;
                }


                // add the address of the signal
                thisMessage.Add(IntHelpers.GetLowByte(signalList[i]));
                thisMessage.Add(IntHelpers.GetHighByte(signalList[i]));
                thisMessageSize += 2;


                switch (types[i]) 
                {
                    case "FL":
                        thisMessage.Add(0x0B); // write field descriptor (WFD), 0x0B = set analog value
                        thisMessage.AddRange(BitConverter.GetBytes(values[i]));  // value to be written
                        thisMessageSize += 5;  // WFD + 4 byte analog value
                        break;
                    case "BIN":
                        if (values[i] == 0)
                            WFD = 0x0A; // set logical "off"
                        else
                            WFD = 0x09; // set logical "on"
                        thisMessage.Add(WFD);
                        thisMessageSize += 1;
                        break;
                }
                thisMessageSignalCount++;

                // if that was the last signal, complete the current message and we're done
                if (i == signalList.Length - 1)
                {
                    // complete the current message
                    thisMessage[11] = thisMessageSignalCount;
                    finalized = SerialFinalizeMessage(thisMessage);
                    request = CreateDataRequestObject(finalized.ToArray());
                    request.Protocol = "BSAP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    output.Add(request);
                }
            }

            return output;

        }

        public static List<DataRequest> SerialWriteValuesByName(byte localAddress, string[] signalList, string[] types, float[] values, int maxMessageSize = absoluteMaxMessageLength)
        {
            if (maxMessageSize > absoluteMaxMessageLength)
                maxMessageSize = absoluteMaxMessageLength;

            List<DataRequest> output = new List<DataRequest>();



            // build the message header (starting at local address)
            List<byte> header = new List<byte>(new byte[]
            {
              localAddress,
              0x00,   // serial # to be filled in later
              DSTFuncCode,
              0x00,   // sequence # to be filled in later
              0x00,   // sequence # to be filled in later
              SRCFuncCode,
              NodeStatus,
              WriteByNameFuncCode,
              0xFF, // security level
              0x00 // number of signals (to be filled in later)
            });


            List<byte> thisMessage = new List<byte>(header);
            int thisMessageSize = thisMessage.Count;
            byte thisMessageSignalCount = 0;
            List<byte> finalized;
            byte WFD;
            DataRequest request;
            int newMessageSize;

            for (int i = 0; i < signalList.Length; i++)
            {
                // check if adding this signal will put the message size over the limit
                newMessageSize = 2 + signalList[i].Length; // WFD + signalname length + null terminator               
                if (types[i] == "analog")
                    newMessageSize += 4;

                if (newMessageSize > maxMessageSize)
                {
                    // complete the current message
                    thisMessage[9] = thisMessageSignalCount;
                    finalized = SerialFinalizeMessage(thisMessage);
                    request = CreateDataRequestObject(finalized.ToArray());
                    request.Protocol = "BSAP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);

                    // add it to the request list
                    output.Add(request);

                    // and reset for the next one
                    thisMessage = new List<byte>(header);
                    thisMessageSize = thisMessage.Count;
                    thisMessageSignalCount = 0;
                }


                // add the name of the signal
                thisMessage.AddRange(Encoding.ASCII.GetBytes(signalList[i]));
                thisMessage.Add(0x00);
                thisMessageSize += signalList[i].Length + 1;


                switch (types[i])
                {
                    case "FL":
                        thisMessage.Add(0x0B); // write field descriptor (WFD), 0x0B = set analog value
                        thisMessage.AddRange(BitConverter.GetBytes(values[i]));  // value to be written
                        thisMessageSize += 5;  // WFD + 4 byte analog value
                        break;
                    case "BIN":
                        if (values[i] == 0)
                            WFD = 0x0A; // set logical "off"
                        else
                            WFD = 0x09; // set logical "on"
                        thisMessage.Add(WFD);
                        thisMessageSize += 1;
                        break;
                }
                thisMessageSignalCount++;

                // if that was the last signal, complete the current message and we're done
                if (i == signalList.Length - 1)
                {
                    // complete the current message
                    thisMessage[9] = thisMessageSignalCount;
                    finalized = SerialFinalizeMessage(thisMessage);
                    request = CreateDataRequestObject(finalized.ToArray());
                    request.Protocol = "BSAP";
                    request.ExpectedResponseSize = CalculateExpectedResponseSize(request);
                    request.TagList = new List<Tag>(thisMessageSignalCount);
                    request.RequestID = Guid.NewGuid().ToString();
                    output.Add(request);
                }
            }

            return output;

        }

        private static List<byte> SerialFinalizeMessage(List<byte> partialMessage)
        {
            // fill in the serial and sequence numbers
            ushort appSeqNumber = SerialGetNextApplicationSeqNumber();
            byte serialNum = GetNextSerialNumber();
            partialMessage[1] = serialNum;
            partialMessage[3] = IntHelpers.GetLowByte(appSeqNumber);
            partialMessage[4] = IntHelpers.GetHighByte(appSeqNumber);


            // add the ETX for CRC calculation purposes
            partialMessage.Add(ETX);

            // calculate CRC
            ushort CRC = CRCHelpers.CalcCRC16(partialMessage.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);

            // escape any DLEs in the message body
            List<byte> escapedMessageBody = EscapeDLE(partialMessage);

            // put the whole message together
            List<byte> final = new List<byte>();
            final.Add(DLE);
            final.Add(STX);
            final.AddRange(escapedMessageBody);
            final.Insert(final.Count - 1, DLE);  // escape the ETX

            final.Add(IntHelpers.GetLowByte(CRC));
            final.Add(IntHelpers.GetHighByte(CRC));

            return final;
        }


        private static ushort SerialGetNextApplicationSeqNumber()
        {
            ushort toReturn = SerialSeqNumber;
            if (SerialSeqNumber + 1 > ushort.MaxValue)
                SerialSeqNumber = 1;
            else
                SerialSeqNumber++;

            // skip 0x10 (temporary)
            if (SerialSeqNumber == 0x10)
                SerialSeqNumber++;

            return toReturn;
        }

        private static UInt32 UDPGetNextApplicationSeqNumber()
        {
            UInt32 toReturn = SeqNumber;

            if (SeqNumber + 1 > UInt32.MaxValue)
                SeqNumber = 1;
            else
                SeqNumber++;

            return toReturn;
        }

        private static byte GetNextSerialNumber()
        {
            byte toReturn = SerialNumber;

            if (SerialNumber + 1 > byte.MaxValue)
                SerialNumber = 1;
            else
                SerialNumber++;

            // skip 0x10 (temporary)
            if (SerialNumber == 0x10)
                SerialNumber++;

            return toReturn;
        }

        private static DataRequest CreateDataRequestObject(byte[] requestBytes)
        {
            DataRequest request = new DataRequest()
            {
                //Protocol = "BSAP",
                RequestBytes = requestBytes,
                RequestID = Guid.NewGuid().ToString(),
                MessageType = DataRequest.RequestType.Read
            };
            request.DataPointConfig = new List<FDADataPointDefinitionStructure>();
            request.SetStatus(DataRequest.RequestStatus.Idle);

            return request;
        }

        public static List<byte> UnescapeDLE(List<byte> original)
        {
            List<byte> unescaped = new List<byte>();
            byte previous = 0x00;
            foreach (byte b in original)
            {
                if (!(b == previous && b == 0x10))
                {
                    unescaped.Add(b);
                    previous = b;
                }
                else
                    previous = 0x00;
                
            }

            return unescaped;
        }

        public static List<byte> EscapeDLE(List<byte> original)
        {
            List<byte> escaped = new List<byte>();
            foreach (byte b in original)
            {
                escaped.Add(b);
                if (b == 0x10)
                {
                    escaped.Add(b);
                }
            }
            return escaped;
        }

        public static void ResetSequenceNumbers()
        {
            SerialNumber = 1;
            SeqNumber = 1;
            SerialSeqNumber = 1;
            LastSeqRcvd = 1;
        }


      

    }


}
