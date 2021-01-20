using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common;
using FDA;

namespace Modbus
{
    public static class ModbusProtocol
    {
        private const ushort CRCInitial = 0xFFFF;
        private const ushort CRCPoly = 0xA001;
        private const bool CRCSwapOutputBytes = true;

        private static List<string> validationErrors;

        static internal Dictionary<Byte, String> _errorMessages = new Dictionary<byte, string>
        {
            { 1, "Illegal Function" },
            { 2, "Illegal Data Address" },
            { 3, "Illegal Data Value" },
            { 4, "Slave Device Failure" },
            { 5, "Acknowledge" },
            { 6, "Slave Device Busy" },
            { 7, "Negative Acknowledge" },
            { 8, "Memory Parity Error" }
        };

        #region Interface Functions

        // CreateRequests looks at the requestGroup.dbGroupRequestConfig.DataPointBlockRequestListVals string (which defines the requests that are part of the group)
        // and generates DataRequest objects
        // the function stores the DataRequest objects in the requestGroup.ProtocolRequestList 
        // if a request is a write, it generates the DataRequest object with just the message header bytes, and sets the MessageType of the DataRequestObject to DataRequest.RequestType.Write
        // DataAcq will look up the values to be written and call CompleteWriteRequest() for finish request construction 
        public static void CreateRequests(RequestGroup requestGroup,object protocolOptions=null)
        {
            try
            {
                bool tcp = false;
                bool enron = false;
                if (protocolOptions != null)
                {
                    tcp = (protocolOptions.ToString() == "TCP");
                    enron = (protocolOptions.ToString() == "ENRON");
                }

                // get the group config string
                string dbGroupDefinition = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals;


                string[] requests = dbGroupDefinition.Split('$');

                FDADevice deviceSettings = null;
                int requestIdx = 1;
                foreach (string request in requests)
                {
                    // TODO: turn each request definition string ("request") into a DataRequestObject
                    // and add it to the requestGroup.ProtocolRequestList
                    // eg: "14:3:10|UINT:4bb8d0af-0dc8-437e-a0f3-0b773a7b0083"
                    //14:3:10 | FL:120021 | UINT:120022 | MT16 | MT16 | FL:120023

                    List<DataRequest> subRequestList = null;// = new List<DataRequest>();
                    ushort slave = 0;
                    ushort startReg = 0;
                    ushort numRegisters = 0;
                    byte opCode = 0;
                    ushort byteCount = 0;
                    List<FDADataPointDefinitionStructure> tagConfigsList = new List<FDADataPointDefinitionStructure>();
                    List<Tag> tagList = new List<Tag>();
                    Tag newTag;
                    string dataTypeName;
                    Guid tagID;
                    int maxDataSize = 245; // default to the data size (245 data bytes plus 11 overhead bytes - the largest possible overhead, MODbus TCP with Extended addressing)
                    bool readback = false;
                    try
                    {
                        string[] tags = request.Split('|');
                        string[] header = tags[0].Split(':');


                        string[] headerElement0 = header[0].Split('^');
                        {
                            if (headerElement0.Length > 1)
                            {
                                Guid deviceRef = Guid.Parse(headerElement0[1]);
                                deviceSettings = ((DBManager)Globals.DBManager).GetDevice(deviceRef);
                            }
                        }


                        slave = ushort.Parse(headerElement0[0]);
                        string opCodeString = header[1];

                        readback = (opCodeString[0] == '+');

                        if (readback)
                            opCode = byte.Parse(opCodeString.Substring(1, opCodeString.Length - 1));
                        else
                            opCode = byte.Parse(opCodeString);

                        startReg = ushort.Parse(header[2]);

                        if (header.Length > 3)
                            maxDataSize = int.Parse(header[3]);

                        if (maxDataSize > 245 || maxDataSize < 20)
                        {

                            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "Protocol", "Modbus", "Request group " + requestGroup.ID + " maximum data size " + maxDataSize + " is invalid (must be between 20 and 245 bytes). Using max data size of 245 bytes");
                            maxDataSize = 256;
                        }

                      

                        string[] tagInfo;

                        // for INT8s and UINT8s
                        Guid lowByteTagID;
                        Guid highByteTagID; 

                        for (int i = 1; i < tags.Length; i++) // start at 1 (index 0 is the header)
                        {
                            tagInfo = tags[i].Split(':');
                            dataTypeName = tagInfo[0];

                            //******************************* new stuff *******************************************************************
                            bool swapBytes = false;
                            bool swapWords = false;

                            for (int idx = 0; idx < 2; idx++)
                            {
                                if (dataTypeName.Length > 2)
                                {
                                    // check for swap word or swap byte flags in the last two chars of the data type ('SB' or 'SW')
                                    string lastTwoChars = dataTypeName.Substring(dataTypeName.Length - 2, 2).ToUpper();
                                    if (lastTwoChars == "SB")
                                    {
                                        swapBytes = true;
                                        dataTypeName = dataTypeName.Substring(0, dataTypeName.Length - 2);
                                    }

                                    if (lastTwoChars == "SW")
                                    {
                                        swapWords = true;
                                        dataTypeName = dataTypeName.Substring(0, dataTypeName.Length - 2);
                                    }
                                }

                                // if the last two chars aren't SW or SB, don't bother checking the second last two chars
                                if (!swapBytes && !swapWords)
                                    break;
                            }
                            

                            //*************************************************************************************************************



                            if (dataTypeName.StartsWith("MT"))
                            {                               
                                if (dataTypeName.EndsWith("16"))
                                {
                                    tagList.Add(new Tag(Guid.Empty) { ProtocolDataType = DataType.INT16 });
                                    byteCount += 2;
                                }
                                else
                                {
                                    if (dataTypeName.EndsWith("32"))
                                    {
                                        tagList.Add(new Tag(Guid.Empty) { ProtocolDataType = DataType.INT32 });
                                        byteCount += 4;
                                    }
                                    else
                                        if (dataTypeName.EndsWith("1"))
                                        {
                                            tagList.Add(new Tag(Guid.Empty) { ProtocolDataType = DataType.BIN });
                                        }
                                    else
                                        throw new Exception("'" + dataTypeName + "' is not a valid MT length, use MT16 or MT32");
                                }
                                tagConfigsList.Add(new FDADataPointDefinitionStructure()); // dummy tag config, to keep the arrays aligned
                            }
                            else
                            {
                                // special case for INT8 and UINT8, optionally two tag defs (for high and a low byte)
                                if (dataTypeName == "INT8" || dataTypeName == "UINT8")
                                {
                                    highByteTagID = Guid.Empty;
                                    lowByteTagID = Guid.Empty;
                                    if (tagInfo[1] != "0")
                                        highByteTagID = Guid.Parse(tagInfo[1]);
                                    if (tagInfo.Length > 2)
                                        if (tagInfo[2] != "0")
                                            lowByteTagID = Guid.Parse(tagInfo[2]);

                                    if (highByteTagID != Guid.Empty)
                                    {
                                        newTag = new Tag(highByteTagID) {ProtocolDataType = DataType.GetType(dataTypeName) };
                                        tagConfigsList.Add(requestGroup.TagsRef[highByteTagID]);
                                    }
                                    else
                                    {
                                        newTag = new Tag(Guid.Empty) { ProtocolDataType = DataType.GetType(dataTypeName) }; // dummy tag (contains only the data type, so we know how many bytes to skip
                                        tagConfigsList.Add(new FDADataPointDefinitionStructure()); // dummy tag config, to keep the tag and tagConfig arrays aligned
                                    }
                                    tagList.Add(newTag);


                                    if (lowByteTagID != Guid.Empty)
                                    {
                                        newTag = new Tag(lowByteTagID) { ProtocolDataType = DataType.GetType(dataTypeName) };
                                        tagConfigsList.Add(requestGroup.TagsRef[lowByteTagID]);
                                    }
                                    else
                                    {
                                        newTag = new Tag(Guid.Empty) { ProtocolDataType = DataType.GetType(dataTypeName) };
                                        tagConfigsList.Add(new FDADataPointDefinitionStructure());
                                    }

                                    tagList.Add(newTag);
                                    byteCount += 2; // whether we use one byte or the other or both, it's always two bytes of data (1 register)                                    
                                }
                                else
                                {
                                    if (dataTypeName == "BIN")
                                    {
                                        for (int j = 1; j < tagInfo.Length; j++)
                                        {
                                            tagID = Guid.Empty;
                                            if (tagInfo[j] != "0")
                                                tagID = Guid.Parse(tagInfo[j]);
                                            
                                            if (tagID == Guid.Empty)
                                            {
                                                tagList.Add(new Tag(Guid.Empty) { ProtocolDataType = DataType.BIN });
                                                tagConfigsList.Add(new FDADataPointDefinitionStructure());
                                            }
                                            else
                                            {
                                                newTag = new Tag(tagID) { ProtocolDataType = DataType.GetType(dataTypeName) };
                                                tagList.Add(newTag);
                                                tagConfigsList.Add(requestGroup.TagsRef[tagID]);
                                            }
                                        }

                                    }
                                    else
                                    {
                                        tagID = Guid.Parse(tagInfo[1]);
                                        tagConfigsList.Add(requestGroup.TagsRef[tagID]);
                                        newTag = new Tag(tagID) {ProtocolDataType = DataType.GetType(dataTypeName)};

                                        // new stuff *****************
                                        // set the swap byte/swap word flags if they were specified AND if the make sense for the data type (eg. can't swap words if the data type is less than 2 words)
                                        if (swapBytes && (newTag.ProtocolDataType.Size == 2 || newTag.ProtocolDataType.Size == 4))
                                            newTag.SwapBytes = true;
                                        if (swapWords && newTag.ProtocolDataType.Size == 4)
                                            newTag.SwapWords = true;
                                        // ***************************
                                        tagList.Add(newTag);
                                        byteCount += DataType.GetType(dataTypeName).Size; // note, for coils (opcodes 1,2,5,15) this value will be overridden below
                                    }
                                }                               
                            }
                        }


                        if (opCode == 3 || opCode == 4 || opCode == 6 || opCode == 16)
                            numRegisters = (ushort)(byteCount / 2);

                        if (opCode == 1 || opCode == 2 || opCode == 5 || opCode == 15)
                        {
                            numRegisters = (ushort)tagList.Count();
                            byteCount = (ushort)(Math.Ceiling(numRegisters / 8.0));
                        }

                        if (opCode == 7)
                        {
                            numRegisters = 1;
                            byteCount = 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while parsing the request definition string '" + request + "' in request group '" + requestGroup.Description + "'");
                        continue;
                    }

                  
                    if ((opCode >= 1 && opCode <= 4) || opCode == 7)
                        subRequestList = GenerateReadRequest(opCode, slave, startReg, numRegisters,requestIdx, tcp, enron,maxDataSize, tagConfigsList,tagList,DataRequest.RequestType.Read);

                    if (opCode == 5 || opCode == 6 || opCode == 16 || opCode == 15)
                         subRequestList = GenerateWriteRequest(opCode, slave, startReg, tagConfigsList, tagList, maxDataSize, requestIdx, tcp, enron, requestGroup.writeLookup,requestGroup.ID);

                    if (subRequestList != null)
                    {
                        foreach (DataRequest subRequest in subRequestList)
                        {
                            subRequest.GroupID = requestGroup.ID;
                        }
                        requestGroup.ProtocolRequestList.AddRange(subRequestList);
                    }

                    // if a readback was requested (+ in front of the opcode), generate a read request for the same register(s) and insert it into the request list right after the write
                    if (readback)
                    {
                        List<DataRequest> readbackSubRequestList = new List<DataRequest>();
                        byte readbackOpCode = 3;
                        if (opCode == 5 || opCode == 15)
                            readbackOpCode = 1;
                        readbackSubRequestList = GenerateReadRequest(readbackOpCode, slave, startReg, numRegisters,requestIdx+1,tcp,enron,maxDataSize, tagConfigsList, tagList,DataRequest.RequestType.ReadbackAfterWrite);

                        foreach (DataRequest readbackSubRequest in readbackSubRequestList)
                        {
                            readbackSubRequest.GroupID = requestGroup.ID;
                        }
                        requestGroup.ProtocolRequestList.AddRange(readbackSubRequestList);
                    }

                    requestIdx++;
                }

                foreach (DataRequest req in requestGroup.ProtocolRequestList)
                {
                    req.ParentGroup = requestGroup;
                    req.DeviceSettings = deviceSettings;
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "An error occurred while generating the request(s) for the group : " + requestGroup.ID);
            }
        }

 

        // Validate Response takes a completed request and looks at the response bytes to determine if the response is valid
        // it sets the result by calling request.SetStatus() and can put any error messages in request.ErrorMessage
        // it can optionally provide a datarequest to be sent to the device that corrects an error indicated in the response, by creating a Request object for the error correction and placing it in the ErrorCorrectionRequestProperty (also set ErrorCorrectionAttempted=true)
        // if the response is valid, it should then pass the request on to InterpretResponse()
        public static void ValidateResponse(DataRequest request)
        {
 

            byte[] response = request.ResponseBytes;
            bool tcp = (request.Protocol == "MODBUSTCP");


            if (request.ActualResponseSize != request.ExpectedResponseSize)
            {
                bool valid = false;
                if (request.ActualResponseSize > request.ExpectedResponseSize)
                {
                    // check if the response starts with a valid message, but has trailing garbage
                    byte[] initialResponseBytes = request.ResponseBytes.Take(request.ExpectedResponseSize).ToArray();


                    if (CRCCheck(initialResponseBytes,tcp) && RespondingDeviceCheck(request.RequestBytes, initialResponseBytes,tcp))
                    {
                        // it's from the right device, and passes the CRC check, so it looks like a good message. replace the oversized message with these bytes and continue
                        response = initialResponseBytes;
                        Globals.SystemManager.LogCommsEvent(request.ConnectionID, Globals.FDANow(), "MODBUSProtocol: The first " + request.ExpectedResponseSize + " bytes of an unexpectedly long response form a valid message.Using this as the device response and ignoring the remaining bytes");
                        valid = true;
                    }

                }


                if (!valid)
                {
                    request.SetStatus(DataRequest.RequestStatus.Error);
                    request.ErrorMessage = "Unexpected response length (expected " + request.ExpectedResponseSize + " bytes)";
                    return;
                }
            }


            if (response.Length > 255)
            {
                request.SetStatus(DataRequest.RequestStatus.Error);
                request.ErrorMessage = "Invalid Response (over maximum message length at " + response.Length + " bytes)";
            }

            // check CRC
 
            if (response.Length >= 2)
            {
                if (!CRCCheck(response,tcp))
                {
                    request.ErrorMessage = "CRC check failed";
                    request.SetStatus(DataRequest.RequestStatus.Error);
                    return;
                }
            }


            // maybe move modbus error response checking to here, instead of in InterpretResponse()?


            // check that response is from the correct device
            if (!RespondingDeviceCheck(request.RequestBytes,response,tcp))
            {
                request.SetStatus(DataRequest.RequestStatus.Error);
                request.ErrorMessage = "Response from unexpected device";
                return;
            }
                   
            request.ErrorMessage = "";
            request.SetStatus(DataRequest.RequestStatus.Success);  
            InterpretResponse(request);
            return;
        }



        public static void CancelDeviceRequests(DataRequest failedReq)
        {
            // get the address of the device that failed
            ushort addressByte = 0;
            ushort failedDevAddr;
            if (failedReq.Protocol == "MODBUSTCP")
                addressByte = 6;

            failedDevAddr = failedReq.RequestBytes[addressByte];
            if (failedDevAddr == 0xFF)
                failedDevAddr = BitConverter.ToUInt16(failedReq.RequestBytes, addressByte);

            // look through the groups request list, mark any other requests to this device to be skipped
            if (failedReq.ParentGroup != null)
            {
                if (failedReq.ParentGroup.Protocol == "MODBUSTCP")
                    addressByte = 6;
                
                ushort thisReqDeviceAddr; 

                foreach (DataRequest req in failedReq.ParentGroup.ProtocolRequestList)
                {
                   
                    if (req.RequestBytes.Length < 1 || (req.Protocol == "MODBUSTCP" && req.RequestBytes.Length < 7))
                        continue;
                    
                    thisReqDeviceAddr = req.RequestBytes[addressByte];
                    if (thisReqDeviceAddr == 0xFF)
                        thisReqDeviceAddr = BitConverter.ToUInt16(req.RequestBytes, addressByte);

                    if (thisReqDeviceAddr == failedDevAddr)
                    {
                        req.SkipRequestFlag = true;
                    }
                }
            }
        }


        // Interpret Response takes the Response Bytes from a request, calculates the typed values, 
        // and puts them with their read timestamps in the request object's ReturnValues list (a list of Tag objects)
        // also checks for and interprets any error codes in the returned message 
        public static void InterpretResponse(DataRequest request)
        {
            try
            {
                bool tcp = (request.Protocol.ToUpper() == "MODBUSTCP");
                byte[] response = request.ResponseBytes;

                // locate the opCode and data block start bytes (depends on mODBUS RTU vs MODBUS tcp and if CMS extended addressing is being used)
                byte opCodeByte = 1;
                int dataBlockIdx = 2;

                if (tcp)
                {
                    opCodeByte += 6;
                    dataBlockIdx += 6;

                    if (response[6] > 254)
                    {
                        opCodeByte += 2;
                        dataBlockIdx += 2;
                    }
                }
                else
                    if (response[0] > 254)
                    {
                        opCodeByte += 2;
                        dataBlockIdx += 2;
                    }

               


                // check for modbus errors
                if ((response[opCodeByte] & 0x80) == 0x80)
                {
                    byte errorCode = response[opCodeByte + 1];
                    request.ErrorMessage = "Modbus Error: " + _errorMessages[errorCode];
                    request.SetStatus(DataRequest.RequestStatus.Error);
                    return;
                }

                // opcodes 5,6,15,16 response is just an ack of the write request, so if we get a matching opcode back, the write was successful
                byte opCode = response[opCodeByte];
                if ((opCode == 5 || opCode == 6 || opCode == 15 || opCode == 16) && request.RequestBytes[opCodeByte] == opCode)
                {
                    request.SetStatus(DataRequest.RequestStatus.Success);
                    request.ErrorMessage = "";
                    return;
                }


                // extract the data portion of the message
                ushort numDataBytes = response[dataBlockIdx];

                // opcode 7 always returns 1 byte of data so there is no 'data length' byte
                if (response[opCodeByte] == 0x07)
                    numDataBytes = 1;

                Byte[] data = new byte[numDataBytes];
               
                if (response[opCodeByte] != 0x07)
                    Array.Copy(response, dataBlockIdx + 1, data, 0, numDataBytes);
                else
                    data[0] = response[dataBlockIdx];        

                int byteIndex = 0;
                int tagIndex = 0;
                //int configIndex = 0;
                object value;
                DataType MBDatatype;
                Type hostDataType;
                Tag currentTag;
                List<byte> bitData = null;

                if (response[opCodeByte] == 1 || response[opCodeByte] == 2)
                    bitData = new List<byte>();


                while (byteIndex < data.Length)
                {
                    currentTag = request.TagList[tagIndex];

                    MBDatatype = (DataType)currentTag.ProtocolDataType;

                    // get the size (in bytes) of each value for this datatype
                    byte dataSize;

                    dataSize = MBDatatype.Size;

                    // unless it's opcode 7, which always returns 1 byte
                    if (response[opCodeByte] == 7)
                        dataSize = 1;

                    // get the C# datatype for this Modbus datatype
                    hostDataType = MBDatatype.HostDataType;

                    byte[] valueBytes = new byte[dataSize];

                    if (hostDataType != typeof(DBNull) && currentTag.TagID != Guid.Empty)
                    {
                        // convert the bytes for this returned value into whatever it should be (float, int, ASCII..etc)

                        // LINQ way, slow
                        //valueBytes = data.Skip(byteIndex).Take(dataSize).ToArray();

                        // faster
                        Array.Copy(data, byteIndex, valueBytes, 0, dataSize);

                        if (currentTag.SwapBytes)
                            valueBytes = Helpers.SwapBytes(valueBytes);
                        if (currentTag.SwapWords)
                            valueBytes = Helpers.SwapWords(valueBytes);

                        value = MBDatatype.FromBytes(valueBytes);
                        value = Convert.ChangeType(value, hostDataType); 
                        
                        /* Scaling now handled by DataAcq in TransactionCompleteHandler()
                        FDADataPointDefinitionStructure config = request.DataPointConfig[tagIndex];

                        if (config.read_scaling && config.read_scale_raw_high != config.read_scale_raw_low)
                        {
                            double numericValue = Double.Parse(Convert.ToString(value));
                            value = (object)(((numericValue - config.read_scale_raw_low) * (config.read_scale_eu_high - config.read_scale_eu_low) / (config.read_scale_raw_high - config.read_scale_raw_low)) + config.read_scale_eu_low);
                        }
                        */

                        // this is bitwise data (opcode 1 or 2) so break up the byte into individual bits and update the tags with the bit values
                        if (response[opCodeByte] == 1 || response[opCodeByte] == 2)
                        {
                            byte mask;
                            byte byteValue = (byte)value;
                            for (byte i = 0; i < 8; i++)
                            {
                                if (tagIndex + i < request.TagList.Count)
                                {
                                    mask = (byte)Math.Pow(2, i);
                                    Tag tag = (Tag)request.TagList[tagIndex + i];
                                    tag.Value = (byte)((byteValue & mask) >> i);
                                    tag.Timestamp = request.ResponseTimestamp;
                                    tag.Quality = 192;
                                }
                            }
                            tagIndex += 7;
                        }
                        else
                        {
                            // update the tag with the value
                            Tag thisTag = (Tag)request.TagList[tagIndex];
                            thisTag.Value = value;
                            thisTag.Timestamp = request.ResponseTimestamp;
                            thisTag.Quality = 192; // for now (need to figure out how I'm supposed to calculate the Quality)
                        }
                     }

                    // increment the byte index (add the number of bytes for one value)
                    byteIndex += dataSize;

                    // increment the tag index (the index through the tag config string, including MT16 and MT32's)
                    tagIndex++;
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "An error occurred while interpreting the response to the request group " + request.GroupID + ", request " + request.GroupIdxNumber + ": " + ex.Message);
                request.SetStatus(DataRequest.RequestStatus.Error); // this prevents the request that failed interpretation from being logged (causes failed queries)
            }
        }

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

        public static string[] ValidateRequestString(string request, Dictionary<Guid, FDADataPointDefinitionStructure> tags, Dictionary<Guid, FDADevice> devices, ref HashSet<Guid> referencedTags)
        {
            validationErrors = new List<string>();


            // will have to work on the ValidateRequestString function to detect null DataAcq and return a string[] with error data instead of logging to the console and the DB
            ValidateRequestString(validationErrors, "", "DBValidator", request, tags, referencedTags);

            return validationErrors.ToArray();
        }

        // obj is a reference to the DataAcqManager if the requestor is the FDA
        // obj is a reference to the string[] error list if the requestor is the Database Validator tool
        public static bool ValidateRequestString(object obj, string groupID, string requestor, string request, Dictionary<Guid, FDADataPointDefinitionStructure> pointDefs,HashSet<Guid> referencedTags = null)
        {
            validationErrors = new List<string>();
            bool valid = true;

            // string contains at least 1 header / body
            string[] headerAndTagList = request.Split('|');
            int n;
            if (headerAndTagList.Length < 2)
            {
  //            Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an invalid request (requires at least 1 | character)'. This request group will not be processed",true);
                RecordValidationError(obj,groupID,requestor,"contains an invalid request (requires at least 1 | character)'");
                return false;
            }

            // the header has the correct number of elements
            string[] header = headerAndTagList[0].Split(':');


            if (header.Length < 3) // header can be 3 or 4 (with the optional max data size paramenter). 
            {
                //Globals.SystemManager.LogApplicationEvent(obj, "", "The group '" + groupID + "', reqested by " + requestor + " contains an request with an invalid header (should be 'SlaveAddress:OpCode:MaxDataSize[optional])'. This request group will not be processed",true);
                RecordValidationError(obj,groupID,requestor,"contains a request with an invalid header (should be 'SlaveAddress:OpCode:MaxDataSize[optional])'");
                valid = false;
            }
            else
            {
                string[] headerElement0 = header[0].Split('^');
                string slaveString = headerElement0[0];
                string deviceRefString;

                // if a device reference is present, check that the ID is valid and references an existing device
                if (headerElement0.Length > 1)
                {
                    deviceRefString = headerElement0[1];
                    Guid deviceRefID;

                    // it's a valid GUID?
                    if (!Guid.TryParse(deviceRefString, out deviceRefID))
                    {
                        // Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with an invalid device ID '" + deviceRefString + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx. This request group will not be processed", true);
                        RecordValidationError(obj,groupID,requestor,"contains a request with an invalid device ID '" + deviceRefString + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx");
                        valid = false;
                    }

                    // if the GUID is valid, check that it references an existing device object
                    if (valid)
                    {
                        // TO DO: ask the DB manager if this device exists
                    }
                }

                // header lement 0 (minus the device reference, if it was present) is numeric
                if (!int.TryParse(headerElement0[0], out n))
                {
                   // Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with an invalid element in the header (SlaveAddress '" + header[0] + "' is not a number). This request group will not be processed",true);
                   RecordValidationError(obj,groupID,requestor,"contains a request with an invalid element in the header (SlaveAddress '" + header[0] + "' is not a number)");
                    valid = false;
                }

                // all header elements are numeric
                string opCodewithNoPlus = header[1].Replace("+", "");
                bool opCodeIsNumeric = true;
                if (!int.TryParse(opCodewithNoPlus, out n))
                {
                    //Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with an invalid element in the header (Opcode '" + header[1] + "' is not a number). This request group will not be processed",true);
                    RecordValidationError(obj,groupID,requestor,"contains a request with an invalid element in the header (Opcode '" + header[1] + "' is not a number)");
                    valid = false;
                    opCodeIsNumeric = false;
                }

                if (!int.TryParse(header[2], out n))
                {
                    //Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with an invalid element in the header (StartingRegister '" + header[2] + "' is not a number). This request group will not be processed",true);
                   RecordValidationError(obj,groupID,requestor,"contains an request with an invalid element in the header (StartingRegister '" + header[2] + "' is not a number)");
                    valid = false;
                }

                if (header.Length > 3)
                {
                    if (!int.TryParse(header[3], out n))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with an invalid element in the header (MaxDataSize '" + header[3] + "' is not a number). This request group will not be processed",true);
                        RecordValidationError(obj,groupID,requestor,"contains an request with an invalid element in the header (MaxDataSize '" + header[3] + "' is not a number)");

                        valid = false;
                    }
                }

                // opcode is supported
                if (opCodeIsNumeric)
                {
                    int.TryParse(opCodewithNoPlus, out n);
                    if ( n < 1 || (n > 7 && n!=15 && n!= 16))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with an invalid element in the header (Opcode '" + header[1] + "' is not supported). This request group will not be processed",true);
                        RecordValidationError(obj,groupID,requestor,"contains an request with an invalid element in the header (Opcode '" + header[1] + "' is not supported)");
                        valid = false;
                    }
                }
            }

            // each tag contains 2 elements (or 1 element with 'MT16' or 'MT32')
            string[] tagInfo;
            for (int i = 1; i < headerAndTagList.Length; i++)
            {
                tagInfo = headerAndTagList[i].Split(':');
                if (tagInfo.Length < 2)
                {
                    if (tagInfo[0] != "MT16" && tagInfo[0] != "MT32" && tagInfo[0] != "MT1")
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with invalid tag information, should be 'DataType:DataPointID' or 'MT16' or 'MT32'. This request group will not be processed",true);
                        RecordValidationError(obj,groupID,requestor, "contains an request with invalid tag information, should be 'DataType:DataPointID' or 'MT16/MT32/MT1'");
                        valid = false;
                    }
                }
                  
                // element 0 is a valid data type
                if (!DataType.IsValidType(tagInfo[0].ToUpper()))
                {
                    //Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with invalid tag information (the data type '" + tagInfo[0] + "' is not recognized). This request group will not be processed",true);
                    RecordValidationError(obj,groupID,requestor, "contains an request with invalid tag information (the data type '" + tagInfo[0] + "' is not recognized)");
                    valid = false;
                }

                if (tagInfo.Length > 1)
                {
                    //element 1 is a valid guid
                    if (!Helpers.IsValidGuid(tagInfo[1]))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with invalid tag information (the tag ID '" + tagInfo[1] + "' is not a valid UID, should be in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx). This request group will not be processed",true);
                        RecordValidationError(obj,groupID,requestor,"contains an request with invalid tag information (the tag ID '" + tagInfo[1] + "' is not a valid UID, should be in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)");
                        valid = false;
                    }
                    else  // element 1 references an existing tag
                    {
                        if (!pointDefs.ContainsKey(Guid.Parse(tagInfo[1])))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "The group " + groupID + ", reqested by " + requestor + " contains an request with  invalid tag information (the tag ID '" + tagInfo[1] + "' is not found. This request group will not be processed",true);
                            RecordValidationError(obj, groupID, requestor, "contains an request with  invalid tag information (the tag ID '" + tagInfo[1] + "' is not found");
                            valid = false;
                        }
                        else
                        {                
                            referencedTags?.Add(Guid.Parse(tagInfo[1]));
                            FDADataPointDefinitionStructure tag = pointDefs[Guid.Parse(tagInfo[1])];
                            string tagDPSType = tag.DPSType.ToUpper();
                            if (!tagDPSType.Contains("MODBUS"))
                            {
                                RecordValidationError(obj, groupID, requestor, "contains a reference to tag '" + tagInfo[1] + ", with an incorrect DPSType '" + tagDPSType + "'. The tag DPSType must match the RequestGroup DPSType");
                            }

                            if (!tag.DPDSEnabled)
                            {
                                RecordValidationError(obj, groupID, requestor, "contains a reference to a disabled tag '" + tagInfo[1],true);
                            }

                        }
                    }
                }             
            }

            return valid;
        }

        #endregion

        private static void InsertMBAPHeader(List<byte> bytes)
        {
            // transaction identifier (2 bytes)
            ushort transIdentifier = (ushort)Globals.FDANow().GetHashCode();
            bytes.Add(Helpers.GetHighByte(transIdentifier));
            bytes.Add(Helpers.GetLowByte(transIdentifier));

            // protocol identifier (2 bytes, 0 for MODBUS)
            bytes.Add(0);
            bytes.Add(0);

            // placeholder (2 bytes) for the number of bytes to follow (to be updated when message is complete)
            bytes.Add(0);
            bytes.Add(0);
        }

        private static void InsertSlaveAddress(List<byte> bytes,ushort slave)
        {
            // CMS Extended Addressing for addresses > 254
            if (slave > 254)
            {
                bytes.Add(0xFF);
                bytes.Add(Helpers.GetHighByte(slave));
                bytes.Add(Helpers.GetLowByte(slave));
            }
            else
            {
                bytes.Add(Helpers.GetLowByte(slave));
            }
        }
        private static bool RespondingDeviceCheck(byte[] request, byte[] response, bool tcp)
        {
            int requestedDevice;
            int responseDevice;
            int offset = 0;

            // if in tcp mode, just compare the request IDs
            if (tcp)
            {
                if (request[0] == response[0] && request[1] == response[1])
                    return true;
                else
                    return false;
            }

            // RTU mode, compare the slave addresses
            // handle extended addressing
            if (request[offset] == 0xFF)
            {
                requestedDevice = request[offset + 1] << 8 | request[offset + 2];
                responseDevice = response[offset + 1] << 8 | response[offset + 2];
            }
            else
            {
                requestedDevice = request[offset];
                responseDevice = response[offset];
            }

            return (requestedDevice == responseDevice);
        }

        private static bool CRCCheck(byte[] data, bool tcp)
        {
            if (tcp)
                return true;

            ushort responseCRC = Helpers.ToUInt16(data[data.Length - 2], data[data.Length - 1]);

            byte[] msgBytes = new byte[data.Length-2];
            Array.Copy(data,0,msgBytes, 0, data.Length-2);

            ushort calcdCRC = Helpers.CalcCRC16(msgBytes, CRCInitial, CRCPoly, CRCSwapOutputBytes);

            return (responseCRC == calcdCRC);
        }


        // generate a read request (Opcodes 1,2,3,4,7)
        //       private static List<DataRequest> GenerateReadRequest(string reqID, byte group, byte unit,byte hostGroup,byte hostUnit, TLP[] TLPs,List<FDADataPointDefinitionStructure> tagsconfigList,List<Tag> tagList,int groupIdx, int maxDataSize, DataRequest.RequestType requestType)

        private static List<DataRequest> GenerateReadRequest(byte opCode, ushort slave, ushort startRegister, ushort numRegisters, int groupIndex, bool tcp,bool enron, int maxDataSize, List<FDADataPointDefinitionStructure> tagconfigs, List<Tag> tagList, DataRequest.RequestType requestType)
        {
            List<DataRequest> subRequestList = new List<DataRequest>();
            List<byte> requestBytes = new List<byte>();
            List<Tag> subRequestTagList = new List<Tag>();
            List <FDADataPointDefinitionStructure> subRequestTagConfigList = new List<FDADataPointDefinitionStructure>();
            try
            {
                // special case opcode 7 - this will never be oversized, so let's just get that out of the way
                if (opCode == 7)
                {
                    if (tcp)
                    {
                        InsertMBAPHeader(requestBytes);

                        // update the 'bytes to follow' field in the MBAP header (3 bytes for normal addressing, extra 2 bytes if using extended addressing)
                        byte byteCount = 3;
                        if (slave > 254) byteCount += 2;
                        requestBytes[4] = 0x00;
                        requestBytes[5] = byteCount;
                    }

                    InsertSlaveAddress(requestBytes, slave);  // slave address

                    requestBytes.Add(0x07); // opcode

                    // crc
                    if (!tcp)
                    { // add the CRC if not using modbustcp
                        ushort CRC = Helpers.CalcCRC16(requestBytes.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);
                        requestBytes.Add(Helpers.GetHighByte(CRC));
                        requestBytes.Add(Helpers.GetLowByte(CRC));
                    }

                    DataRequest request = new DataRequest()
                    {
                        RequestBytes = requestBytes.ToArray(),
                        GroupIdxNumber = groupIndex.ToString(),
                        TagList = tagList,
                        DataPointConfig = tagconfigs,
                        NodeID = slave.ToString()
                    };

                    request.Protocol = "MODBUS";
                    if (tcp)
                        request.Protocol = "MODBUSTCP";

                    if (enron)
                        request.Protocol = "ENRONMODBUS";

                    request.ExpectedResponseSize = request.RequestBytes.Length + 1;

                    subRequestList.Add(request);

                    return subRequestList;
                }



                // find the max number of bytes we can read while keeping the response under the message size limit
                byte responseOverhead = 5;
                if (!tcp && slave < 255) responseOverhead = 5;
                if (!tcp && slave >= 255) responseOverhead = 7;
                if (tcp && slave < 255) responseOverhead = 9;
                if (tcp && slave >= 255) responseOverhead = 11;

                ushort maxResponseDataBytes = (ushort)(maxDataSize - 11); // 11 is maximum possible response overhead (for use when breaking up oversized messages)

                ushort adjStartReg = (ushort)startRegister;
                if (!enron)
                {
                    adjStartReg = (ushort)(startRegister - 1);
                }

                ushort registerIdx = adjStartReg;
                int tagIdx = 0;

                double responseDataByteCount = 0;
                double registerCount = 0;
                double enronTagCount = 0;
                double tagSize; // double allows for fractions of a byte (bits)
                while (tagIdx <= tagList.Count)
                {
                    if (tagIdx < tagList.Count)
                    {
                        if (tagList[tagIdx].ProtocolDataType.Name == "BIN")
                            tagSize = 1 / 8.0; // 1/8 of a byte (a bit)
                        else
                            tagSize = tagList[tagIdx].ProtocolDataType.Size;

                        if (Math.Ceiling(responseDataByteCount + tagSize) < maxDataSize)
                        {
                            responseDataByteCount += tagSize;
                            DataType dataType = (DataType)tagList[tagIdx].ProtocolDataType;
                            if (!(opCode == 1 || opCode == 2))
                                registerCount += dataType.Size / 2.0;
                            else
                                registerCount += 1;
                            subRequestTagList.Add(tagList[tagIdx]);
                            subRequestTagConfigList.Add(tagconfigs[tagIdx]);

                            if (tagList[tagIdx].ProtocolDataType.Name.EndsWith("INT8"))
                                enronTagCount += 0.5;
                            else
                                enronTagCount++;
                            tagIdx++;
                            continue;
                        }
                    }

                    // this is all the datapoints we can include in this subRequest, create the subRequest and reset for the next one

                    // MODBUS TCP only, start the message off with the MBAP header
                    if (tcp)
                        InsertMBAPHeader(requestBytes);

                    // add the slave address (handles CMS Extended Addressing for addresses > 254)
                    InsertSlaveAddress(requestBytes, slave);

                    // add the opCode
                    requestBytes.Add(opCode);

                    // add the start register (high byte first)
                    requestBytes.Add(Helpers.GetHighByte(registerIdx));
                    requestBytes.Add(Helpers.GetLowByte(registerIdx));

                    // num registers (or num tags for enron)
                    if (enron)
                    {
                        requestBytes.Add(Helpers.GetHighByte((ushort)enronTagCount));
                        requestBytes.Add(Helpers.GetLowByte((ushort)enronTagCount));
                    }
                    else
                    {
                        requestBytes.Add(Helpers.GetHighByte((ushort)Math.Ceiling(registerCount)));
                        requestBytes.Add(Helpers.GetLowByte((ushort)Math.Ceiling(registerCount)));
                    }

                    // crc
                    if (!tcp)
                    {
                        ushort CRC = Helpers.CalcCRC16(requestBytes.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);
                        requestBytes.Add(Helpers.GetHighByte(CRC));
                        requestBytes.Add(Helpers.GetLowByte(CRC));
                    }

                    // update the data length in the MBAP header (if using modbus tcp)
                    if (tcp)
                    {
                        ushort msgLength = (ushort)(requestBytes.Count - 6);
                        requestBytes[4] = Helpers.GetHighByte(msgLength);
                        requestBytes[5] = Helpers.GetLowByte(msgLength);
                    }


                    // protocol name for the request object
                    string protocolName = "MODBUS";

                    if (enron)
                    {
                        protocolName = "ENRONMODBUS";
                    }

                    if (tcp)
                    {
                        protocolName = "MODBUSTCP";
                    }


                    string groupIdx;
                    if (subRequestList.Count > 1)
                        groupIdx = groupIndex.ToString() + "." + (subRequestList.Count + 1).ToString();
                    else
                        groupIdx = groupIndex.ToString();

                    if (requestType == DataRequest.RequestType.ReadbackAfterWrite)
                        groupIdx += " (readback)";

                    // generate a new DataRequest object: set the protocol, request bytes, and expected response size

                    DataRequest request = new DataRequest()
                    {
                        RequestBytes = requestBytes.ToArray(),
                        Protocol = protocolName,
                        ExpectedResponseSize = responseOverhead + (int)Math.Ceiling(responseDataByteCount),
                        GroupIdxNumber = groupIndex.ToString() + "." + (subRequestList.Count + 1).ToString(),
                        TagList = subRequestTagList,
                        DataPointConfig = subRequestTagConfigList,
                        MessageType = requestType,
                        NodeID = slave.ToString()
                    };

                    if (requestType == DataRequest.RequestType.ReadbackAfterWrite)
                        request.DBWriteMode = DataRequest.WriteMode.Update;

                    // add the subRequest to the subRequest List
                    subRequestList.Add(request);

                    // if this is the last subRequest we're done, return the list
                    if (tagIdx == tagList.Count)
                        return subRequestList;
                    else
                    {
                        // reset for the next subRequest
                        registerIdx += (ushort)registerCount;
                        responseDataByteCount = 0;
                        registerCount = 0;
                        enronTagCount = 0;
                        requestBytes = new List<byte>();
                        subRequestTagList = new List<Tag>();
                        subRequestTagConfigList = new List<FDADataPointDefinitionStructure>();
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred in Modbus Protocol GenerateReadRequest()");
            }

            // we'll never get here but compiler doesn't know that, so this keeps it happy
            return null;
        }


        public static void IdentifyWrites(RequestGroup requestGroup)
        {
            string[] request;
            string[] header;
            string[] taginfo;
            string[] requests = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals.Split('$');
            string OpcodeStr;
            foreach (string req in requests)
            {
                request = req.Split('|');
                header = request[0].Split(':');
                OpcodeStr = header[1];
                OpcodeStr = OpcodeStr.Replace("+", "");

                
                if (OpcodeStr=="5" || OpcodeStr=="6" || OpcodeStr =="15" || OpcodeStr== "16")
                {                     
                    if (requestGroup.writeLookup == null)
                    {
                        requestGroup.writeLookup = new Dictionary<Guid, double>();
                    }

                    lock (requestGroup.writeLookup)
                    { 
                        // we need to identify the tags that are being written so the values to be written can be looked up from the database
                        for (int tagidx = 1; tagidx < request.Length; tagidx++)
                        {
                            taginfo = request[tagidx].Split(':');
                            if (!taginfo[0].Contains("MT"))
                                requestGroup.writeLookup.Add(Guid.Parse(taginfo[1]), double.NaN);

                        }
                    }
                }
            }
        }

        public static string PreprocessRequestString(string requestString)
        {
            // special case for opcode 15, look for skipped bits and break up the request into multiple requests with contiguous bits
            string[] parsed = requestString.Split('|');
            string header = parsed[0];
            string[] headerParts = header.Split(':');
            string device = headerParts[0];
            string opCode = headerParts[1];
            int register = int.Parse(headerParts[2]);

            if (opCode != "15")
                return requestString;
            string tag;


            StringBuilder sb = new StringBuilder();

            string[] tagdetails;
            bool MTMode = true;
            for (int tagIdx = 1; tagIdx < parsed.Length; tagIdx++)
            {

                tag = parsed[tagIdx];
                tagdetails = tag.Split(':');

                if (tagdetails[0] != "MT1") // regular tag
                {
                    if (MTMode) // moving from an MT tag to an actual tag, begin a new request
                    {
                        if (sb.Length > 0)
                        {
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append("$");
                        }
                        sb.Append(device + ":" + opCode + ":" + register);
                        sb.Append("|");
                    }

                    // add the tag to the request string
                    sb.Append(tag);
                    sb.Append("|");
                    MTMode = false;
                }
                else
                {
                    MTMode = true;         // MT tag, skip it         
                }
                register += 1;
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        private static List<DataRequest> GenerateWriteRequest(byte opCode, ushort slave, ushort startReg, List<FDADataPointDefinitionStructure> TagsConfigList, List<Tag> tagsList, int maxDataBlockSize, int groupIdx, bool tcp, bool enron, Dictionary<Guid, double> valuesToWrite,Guid groupID)
        {
            try
            {
                List<DataRequest> subRequestList = new List<DataRequest>();

                int tagIdx = 0;
                int dataBytecount;
                int subrequestCount = 0;

                ushort adjStartReg = startReg;

                if (!enron)
                    adjStartReg = (ushort)(startReg - 1);

                List<Tag> subRequestTagList;
                List<FDADataPointDefinitionStructure> subRequestTagConfigs;

                ushort registerIdx = adjStartReg;

                while (tagIdx < tagsList.Count)
                {
                    subrequestCount++;
                    subRequestTagList = new List<Tag>();
                    subRequestTagConfigs = new List<FDADataPointDefinitionStructure>();
                    List<byte> requestBytes = new List<byte>();
                    dataBytecount = 0;

                    // build the message header                             
                    if (tcp)
                        InsertMBAPHeader(requestBytes);

                    InsertSlaveAddress(requestBytes, slave);

                    requestBytes.Add(opCode);

                    DataType dataType;
                    while (tagsList[tagIdx].ProtocolDataType.Size + dataBytecount <= maxDataBlockSize)
                    {
                        dataBytecount += tagsList[tagIdx].ProtocolDataType.Size;
                        dataType = (DataType)tagsList[tagIdx].ProtocolDataType;
                        registerIdx += (ushort)dataType.Registers;
                        subRequestTagList.Add(tagsList[tagIdx]);
                        subRequestTagConfigs.Add(TagsConfigList[tagIdx]);
                        tagIdx++;
                        if (tagIdx == tagsList.Count)
                            break;
                    }

                    // protocol name for the request object
                    string protocolName = "MODBUS";

                    if (enron)
                    {
                        protocolName = "ENRONMODBUS";
                    }

                    if (tcp)
                    {
                        protocolName = "MODBUSTCP";
                    }

                    string groupIdxstring;
                    if (subRequestList.Count > 1)
                        groupIdxstring = groupIdx.ToString() + "." + (subRequestList.Count + 1).ToString();
                    else
                        groupIdxstring = groupIdx.ToString();


                    // build a datarequest object
                    DataRequest subRequest = new DataRequest
                    {
                        RequestBytes = requestBytes.ToArray(),                  // this is just the header, the rest needs to be filled in by CompleteWriteRequest laster
                        ProtocolSpecificParams = adjStartReg,
                        Protocol = protocolName,
                        DataPointConfig = subRequestTagConfigs,
                        TagList = subRequestTagList,
                        GroupIdxNumber = groupIdxstring,
                        MessageType = DataRequest.RequestType.Write,
                        NodeID = slave.ToString()
                    };

                    subRequestList.Add(subRequest);
                    adjStartReg = registerIdx;
                }


                foreach (DataRequest writeRequest in subRequestList)
                {
                    // bring in the previously generated header bytes
                    List<byte> requestBytes = new List<byte>();
                    requestBytes.AddRange(writeRequest.RequestBytes);

                    tcp = (writeRequest.Protocol == "MODBUSTCP");
                    enron = (writeRequest.Protocol == "ENRONMODBUS");

                    startReg = (ushort)writeRequest.ProtocolSpecificParams;


                    object value;
                    Tag thisTag;
                    List<byte> dataBytes;

                    switch (requestBytes.Last())
                    {
                        case 16: // opcode 16 (write multiple registers)
                                 //(function code 16): [numregisters High][numregisters low] [value high][value low] ....  [crc high][crc low] (CRC only if not using modbus tcp)

                            // get the bytes for the values
                            ushort numRegisters = 0;
                            dataBytes = new List<byte>();
                            foreach (Tag writeTag in writeRequest.TagList)
                            {
                                value = valuesToWrite[writeTag.TagID];
                                DataType tagtype = (DataType)Convert.ChangeType(writeTag.ProtocolDataType, typeof(DataType));
                                byte[] valueBytes = tagtype.ToBytes(value);

                                if (writeTag.SwapBytes)
                                    valueBytes = Helpers.SwapBytes(valueBytes);

                                if (writeTag.SwapWords)
                                    valueBytes = Helpers.SwapWords(valueBytes);

                                dataBytes.AddRange(valueBytes);
                            }
                            numRegisters += (ushort)Math.Ceiling(dataBytes.Count / 2.0);

                            requestBytes.Add(Helpers.GetHighByte(startReg));
                            requestBytes.Add(Helpers.GetLowByte(startReg));

                            if (enron)
                            {
                                requestBytes.Add(Helpers.GetHighByte((ushort)writeRequest.TagList.Count));
                                requestBytes.Add(Helpers.GetLowByte((ushort)writeRequest.TagList.Count));
                            }
                            else
                            {
                                requestBytes.Add(Helpers.GetHighByte(numRegisters));
                                requestBytes.Add(Helpers.GetLowByte(numRegisters));
                            }

                            requestBytes.Add((byte)dataBytes.Count);

                            requestBytes.AddRange(dataBytes);
                            break;

                        case 6:  //(function code 6) (write single register)
                                 // address of the first register
                            requestBytes.Add(Helpers.GetHighByte(startReg));
                            requestBytes.Add(Helpers.GetLowByte(startReg));

                            if (writeRequest.TagList.Count > 0)
                            {
                                thisTag = writeRequest.TagList[0];
                                value = valuesToWrite[thisTag.TagID];
                                DataType tagtype = (DataType)Convert.ChangeType(thisTag.ProtocolDataType, typeof(DataType));
                                byte[] valueBytes = tagtype.ToBytes(value);

                                if (thisTag.SwapBytes)
                                    valueBytes = Helpers.SwapBytes(valueBytes);

                                if (thisTag.SwapWords)
                                    valueBytes = Helpers.SwapWords(valueBytes);


                                requestBytes.AddRange(valueBytes);
                            }
                            break;
                        case 5: // (function code 5 - write single coil)
                            thisTag = writeRequest.TagList[0];
                            requestBytes.Add(Helpers.GetHighByte(startReg));
                            requestBytes.Add(Helpers.GetLowByte(startReg));
                            value = valuesToWrite[thisTag.TagID];
                            try
                            {
                                byte byteValue = Convert.ToByte(value);
                                if (byteValue == 0)
                                    requestBytes.Add(0x00);
                                else
                                    requestBytes.Add(0xFF);
                                requestBytes.Add(0x00);
                            }
                            catch
                            {

                            }
                            break;
                        case 15: // (function code 15 - write multiple coils)
                            dataBytes = new List<byte>(); // using byte to store 1's and 0's, boolean evaluates to 0 and -1

                            requestBytes.Add(Helpers.GetHighByte(startReg));
                            requestBytes.Add(Helpers.GetLowByte(startReg));
                            byte tagCount = (byte)writeRequest.TagList.Count;
                            requestBytes.Add(Helpers.GetHighByte(tagCount));
                            requestBytes.Add(Helpers.GetLowByte(tagCount));

                            byte tagValue;

                            // make a list of the 1's and 0's "dataBytes" in order of the tags
                            List<byte> bitValues = new List<byte>();
                            foreach (Tag currentTag in writeRequest.TagList)
                            {
                                if (valuesToWrite.ContainsKey(currentTag.TagID))
                                {
                                    value = valuesToWrite[currentTag.TagID];
                                    tagValue = Convert.ToByte(value);
                                    bitValues.Add(tagValue);
                                }
                                else
                                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "Protocol", "Modbus", "The tag " + currentTag.TagID.ToString() + " was not found in the table when attempting to retrive the value to be written to the field device.");
                            }

                            // break the bit list up into groups of 8 and pack them into bytes
                            byte currentByte = 0;
                            byte bitIdx = 0;
                            foreach (byte thisBit in bitValues)
                            {
                                if (bitIdx == 8)
                                {
                                    dataBytes.Add(currentByte);
                                    currentByte = 0;
                                    bitIdx = 0;
                                }
                                if (thisBit == 1)
                                    currentByte += (byte)Math.Pow(2, bitIdx);
                                bitIdx++;
                            }

                            // add the last byte to the datablock                    
                            dataBytes.Add(currentByte);

                            // add the byte count to the message
                            requestBytes.Add((byte)dataBytes.Count);

                            // add the data bytes to the message
                            requestBytes.AddRange(dataBytes);
                            break;
                        default:
                            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "Modbus Protocol", "", "unsupported Opcode " + requestBytes.Last() + " in request " + writeRequest.GroupIdxNumber + " of group " + writeRequest.GroupID);
                            return null;

                    }

                    // add CRC if not using modbus tcp
                    if (!tcp)
                    {
                        ushort CRC = Helpers.CalcCRC16(requestBytes.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);
                        requestBytes.Add(Helpers.GetHighByte(CRC));
                        requestBytes.Add(Helpers.GetLowByte(CRC));
                    }

                    // update the message length in the MBAP header (if using modbus tcp)
                    if (tcp)
                    {
                        ushort msgLength = (ushort)(requestBytes.Count - 6);
                        requestBytes[4] = Helpers.GetHighByte(msgLength);
                        requestBytes[5] = Helpers.GetLowByte(msgLength);
                    }

                    writeRequest.RequestBytes = requestBytes.ToArray();



                    if (tcp)
                        writeRequest.ExpectedResponseSize = 12;
                    else
                    {
                        if (opCode == 15 || opCode == 16)
                            writeRequest.ExpectedResponseSize = 8;
                        else
                            writeRequest.ExpectedResponseSize = writeRequest.RequestBytes.Length;
                    }
                        
                   
                }

                return subRequestList;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error occurred while generating a write request: Group " + groupID + ", request " + groupIdx);
                return null;
            }

            
        }

 

        public sealed class DataType : DataTypeBase
        {
            public static readonly DataType BIN = new DataType("BIN", 1, typeof(byte));
            public static readonly DataType FL = new DataType("FL", 4, typeof(float));
            //public static readonly DataType AC = new DataType("AC",???, typeof(string)); // doesn't have a fixed size....
            public static readonly DataType INT8 = new DataType("INT8", 1, typeof(sbyte));
            public static readonly DataType INT16 = new DataType("INT16", 2, typeof(Int16));
            public static readonly DataType INT32 = new DataType("INT32", 4, typeof(Int32));
            public static readonly DataType UINT8 = new DataType("UINT8", 1, typeof(byte));
            public static readonly DataType UINT16 = new DataType("UINT16", 2, typeof(UInt16));
            public static readonly DataType UINT32 = new DataType("UINT32", 4, typeof(UInt32));
            public static readonly DataType BCD16 = new DataType("BCD16", 2, typeof(UInt16));
            public static readonly DataType BCDP16 = new DataType("BCDP16", 2, typeof(UInt16));
            public static readonly DataType BCD32 = new DataType("BCD32", 4, typeof(UInt32));
            public static readonly DataType BCDP32 = new DataType("BCDP32", 4, typeof(UInt32));
            public static readonly DataType MT1 = new DataType("MT1", 0, typeof(DBNull));
            public static readonly DataType MT16 = new DataType("MT16", 2, typeof(DBNull));
            public static readonly DataType MT32 = new DataType("MT32", 4, typeof(DBNull));
            public double Registers { get { return GetRegisterCount(); } }

            private DataType(string name, byte size, Type hostDataType) : base(name, size, hostDataType)
            {
            }

            private double GetRegisterCount()
            {
                return Size / 2.0;
            }

            public byte[] ToBytes(object value)
            {
                byte[] bytes = null;
                if (this == BIN)
                {
                    bytes = new byte[1] { Convert.ToByte(value) };
                }

                if (this == FL)
                {
                    bytes = BitConverter.GetBytes(Convert.ToSingle(value));
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == INT8)
                {
                    bytes = BitConverter.GetBytes(Convert.ToSByte(Helpers.GetLowByte((ushort)value)));
                }

        

                if (this == INT16)
                {
                    bytes = BitConverter.GetBytes(Convert.ToInt16(value));
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == INT32)
                {
                    bytes = BitConverter.GetBytes(Convert.ToUInt32(value));
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == UINT8)
                {
                    
                    bytes = new byte[1] { Convert.ToByte(value) };
                }

   

                if (this == UINT16)
                {
                    bytes = BitConverter.GetBytes(Convert.ToUInt16(value));
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == UINT32)
                {
                    bytes = BitConverter.GetBytes(Convert.ToUInt32(value));
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }



                // 16 bit unpacked BCD
                if (this.Name == "BCD16")
                {
                    bytes = new byte[2];
                    int intValue = Convert.ToUInt16(value);

                    for (int i = 1; i >= 0; i--)
                    {
                        bytes[i] = (byte)(intValue % 10);
                        intValue /= 10;
                    }
                }

                // 32 bit unpacked BCD
                if (this.Name == "BCD32")
                {
                    long intValue = Convert.ToUInt32(value);
                    bytes = new byte[4];

                    for (int i = 3; i>=0 ;i--)
                    {
                        bytes[i] = (byte)(intValue % 10);
                        intValue /= 10;
                    }
                }

                // 16 bit packed BCD
                if (this.Name == "BCDP16")
                {
                    int intValue = Convert.ToUInt16(value);
                    bytes = new byte[2];

                    // get the individual numerals
                    byte[] numerals = new byte[4];
                    for (int i=3; i>=0; i--)
                    {
                        numerals[i] = (byte)(intValue % 10);
                        intValue /= 10;
                    }

                    bytes[0] = (byte)( (numerals[0] << 4) | numerals[1]);
                    bytes[1] = (byte)( (numerals[2] << 4) | numerals[3]);

                }

                // 32 bit packed BCD
                if (this.Name == "BCDP32")
                {
                    UInt32 intValue = Convert.ToUInt32(value);
                    bytes = new byte[4];

                    // get the individual numerals
                    byte[] numerals = new byte[8];
                    for (int i = 7; i >= 0; i--)
                    {
                        numerals[i] = (byte)(intValue % 10);
                        intValue /= 10;
                    }

                    bytes[0] = (byte)((numerals[0] << 4) | numerals[1]);
                    bytes[1] = (byte)((numerals[2] << 4) | numerals[3]);
                    bytes[2] = (byte)((numerals[4] << 4) | numerals[5]);
                    bytes[3] = (byte)((numerals[6] << 4) | numerals[7]);
                }
                return bytes;
            }

            public object FromBytes(byte[] bytes)
            {
                // wrong number of bytes for the data type, return null
                //if (bytes.Length != this.Size && this.Size != 0)
                //    return null;


                if (this == BIN)
                {
                    return (byte)bytes[0];
                }

                // interpret 4 bytes as a 32 bit floating point value
                if (this.Name == "FL")
                {
                    Byte[] reordered = new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] };
                    return BitConverter.ToSingle(reordered, 0);
                }

 
                if (this.Name == "INT8") 
                {                                   
                    return (SByte)bytes[0];
                }

 
                
                // interpret 2 bytes as a signed integer
                if (this.Name == "INT16")
                {
                    Byte highByte = bytes[0];
                    Byte lowByte = bytes[1];
                    Int16 value = (Int16)((bytes[0] << 8) | lowByte); 
   
                    return value;
                }

                if (this.Name == "INT32")
                {
                    // highest byte A,B,C,D lowest byte  
                    Byte A = bytes[0];
                    Byte B = bytes[1];
                    Byte C = bytes[2];
                    Byte D = bytes[3];

                    Int32 value = (Int32)((A<<24)|(B<<16)|(C<<8)|D);

                    return value;
                }

                // INT8 expects two bytes, it will interpret both of them and return them as an array of length 2. index 0 is the high byte, index 1 is the low byte
                if (this.Name == "UINT8")
                {
                    return (byte)bytes[0];
                }

       
                if (this.Name == "UINT16")
                {
                    Byte highByte = bytes[0];
                    Byte lowByte = bytes[1];

                    UInt16 value = (UInt16)((highByte << 8) | lowByte);

                    return value;
                }


                if (this.Name == "UINT32")
                {
                    Byte A = bytes[0];
                    Byte B = bytes[1];
                    Byte C = bytes[2];
                    Byte D = bytes[3];

                    UInt32 value = (UInt32)((A << 24) | (B << 16) | (C << 8) | D);

                    return value;
                }


                // 16 bit unpacked BCD
                if (this.Name == "BCD16")
                {
                    UInt16 BCD16Value = (UInt16)(Helpers.GetLowNibble(bytes[0]) * 10 + Helpers.GetLowNibble(bytes[1]));
                    return BCD16Value;
                }

                // 32 bit unpacked BCD
                if (this.Name == "BCD32")
                {
                    UInt16 BCD32Value = (UInt16)(Helpers.GetLowNibble(bytes[0]) * 1000);
                    BCD32Value += (UInt16)(Helpers.GetLowNibble(bytes[1]) * 100);
                    BCD32Value += (UInt16)(Helpers.GetLowNibble(bytes[2]) * 10);
                    BCD32Value += (UInt16)(Helpers.GetLowNibble(bytes[3]));

                    return BCD32Value;
                }

                // 16 bit packed BCD
                if (this.Name == "BCDP16")
                {
                    
                    byte[] numerals = new byte[4];

                    numerals[0] = Helpers.GetHighNibble(bytes[0]);
                    numerals[1] = Helpers.GetLowNibble(bytes[0]);

                    numerals[2] = Helpers.GetHighNibble(bytes[1]);
                    numerals[3] = Helpers.GetLowNibble(bytes[1]);

                    return (UInt16)(numerals[0] * 1000 + numerals[1] * 100 + numerals[2] * 10 + numerals[3]);
                }

                // 32 bit packed BCD
                if (this.Name == "BCDP32")
                {

                    byte[] numerals = new byte[8];

                    numerals[0] = Helpers.GetHighNibble(bytes[0]);
                    numerals[1] = Helpers.GetLowNibble(bytes[0]);

                    numerals[2] = Helpers.GetHighNibble(bytes[1]);
                    numerals[3] = Helpers.GetLowNibble(bytes[1]);

                    numerals[4] = Helpers.GetHighNibble(bytes[2]);
                    numerals[5] = Helpers.GetLowNibble(bytes[2]);

                    numerals[6] = Helpers.GetHighNibble(bytes[3]);
                    numerals[7] = Helpers.GetLowNibble(bytes[3]);

                    return (UInt32)(numerals[0] * 10000000 + numerals[1] * 1000000 + numerals[2] * 100000 + numerals[3] * 10000 + numerals[4]*1000 + numerals[5]*100 + numerals[6]*10 + numerals[7]);
                }



                // interpert a variable number of byes as an ASCII string
                /*
                if (this.Name == "AC")
                {
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
                */


            // shouldn't be possible to get here, but all paths must return a value so...return null
            return null;
            }

            public static DataType GetType(string typeName)
            {

                switch (typeName.ToUpper())
                {
                    case "BIN": return BIN;
                    case "FL": return FL;
                    //case "AC": return AC;
                    case "INT8": return INT8;
                    case "INT16": return INT16;
                    case "INT32": return INT32;
                    case "UINT8": return UINT8;
                    case "UINT16": return UINT16;
                    case "UINT32": return UINT32;
                    case "MT1": return MT1;
                    case "MT16": return MT16;
                    case "MT32": return MT32;
                    case "BCD16": return BCD16;
                    case "BCDP16": return BCDP16;
                    case "BCD32": return BCD32;
                    case "BCDP32": return BCDP32;

                    default: return null;
                }
            }

            public static Type GetHostDataType(string typeName)
            {
                switch (typeName.ToUpper())
                {
                    case "BIN": return typeof(byte);
                    case "FL": return typeof(float);
                    //case "AC": return typeof(string);
                    case "INT8": return typeof(sbyte);
                    case "INT16": return typeof(Int16);
                    case "INT32": return typeof(Int32);
                    case "UINT8": return typeof(byte);
                    case "UINT16": return typeof(UInt16);
                    case "UINT32": return typeof(UInt32);
                    case "MT1": return null;
                    case "MT16": return null;
                    case "MT32": return null;
                    case "BCD16": return typeof(UInt16);
                    case "BCDP16": return typeof(UInt16);
                    case "BCD32": return typeof(UInt32);
                    case "BCDP32": return typeof(UInt32);
                    default: return null;
                }
            }

            public static bool IsValidType(string typeName)
            {
                typeName = typeName.ToUpper();

                if (typeName.EndsWith("SBSW") || typeName.EndsWith("SWSB"))
                    typeName = typeName.Substring(0,typeName.Length - 4);

                if (typeName.EndsWith("SB") || typeName.EndsWith("SW"))
                    typeName = typeName.Substring(0,typeName.Length - 2);

                switch (typeName.ToUpper())
                {
                    case "BIN": return true;
                    case "FL": return true;
                    //case "AC": return true;
                    case "INT8": return true;
                    case "INT16": return true;
                    case "INT32": return true;
                    case "UINT8": return true;
                    case "UINT16": return true;
                    case "UINT32": return true;
                    case "MT1": return true;
                    case "MT16": return true;
                    case "MT32": return true;
                    case "BCD16": return true;
                    case "BCDP16": return true;
                    case "BCD32": return true;
                    case "BCDP32": return true;
                    default: return false;
                }
            }
        }
    }

}