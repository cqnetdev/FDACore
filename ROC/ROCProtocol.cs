using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Common;
using FDA;
using Support;

namespace ROC
{
    public static class ROCProtocol
    {
        private const ushort CRCInitial = 0x0000;
        private const ushort CRCPoly = 0xA001;
        private const bool CRCSwapOutputBytes = false;
        private readonly static List<string> supportedOpcodes = new(new string[] { "180", "181", "+181", "17", "120", "121", "+121", "122", "+122" });
        private readonly static List<string> OpcodesRequiringTagList = new(new string[] { "180", "181", "+181" });
        private static List<string> validationErrors;
        private class TLP
        {
            readonly byte _type;
            readonly byte _logical;
            readonly byte _param;

            readonly DataType _datatype;

            byte[] _bytes;

            public byte[] Bytes { get => _bytes; set => _bytes = value; }
            public DataType Datatype { get => _datatype; }
            public string TLPString { get { return _type.ToString() + "-" + _logical.ToString() + "-" + _param.ToString(); } }

            public TLP(byte pointType, byte logical, byte param, DataType dataType)
            {
                _type = pointType;
                _logical = logical;
                _param = param;

                _datatype = dataType;

                Bytes = new byte[3];
                Bytes[0] = pointType;
                Bytes[1] = logical;
                Bytes[2] = param;
            }

            public TLP CloneWithDataTypeChange(DataType newDataType)
            {
                return new TLP(_type, _logical, _param, newDataType);
            }
        }


        public static List<RequestGroup> BackfillTag(FDADataPointDefinitionStructure tagInfo, DataRequest request)
        {
            List<RequestGroup> RequestGroups = new();

            // let's gather up all the info we're going to need
            int HPN = tagInfo.backfill_data_ID;
            Guid tagID = tagInfo.DPDUID;
            int step = (int)tagInfo.backfill_data_interval;
            DateTime fillFrom = tagInfo.PreviousTimestamp;
            DateTime fillTo = tagInfo.LastRead.Timestamp;
            string datatypeName = request.TagList.Find(i => i.TagID == tagID).ProtocolDataType.Name;
            Guid connectionID = request.ConnectionID;

            TimeSpan gap = fillTo.Subtract(fillFrom);
            if (gap.TotalHours >= 1)
            {
                fillFrom = fillFrom.AddHours(1).AddMinutes(-1 * fillFrom.Minute).AddSeconds(-1 * fillFrom.Second).AddMilliseconds(-1 * fillFrom.Millisecond);
                fillTo = fillTo.AddMinutes(-1 * fillFrom.Minute).AddSeconds(-1 * fillFrom.Second).AddMilliseconds(-1 * fillFrom.Millisecond);
            }
            else
            {
                fillFrom = fillFrom.AddMinutes(1).AddSeconds(-1 * fillFrom.Second).AddMilliseconds(-1 * fillFrom.Millisecond);
                fillTo = fillTo.AddSeconds(-1 * fillFrom.Second).AddMilliseconds(-1 * fillFrom.Millisecond);
            }


            // create a new group with an HDR config string
            RequestGroup backfillGroup = (RequestGroup)request.ParentTemplateGroup.Clone();
            backfillGroup.RequesterType = Globals.RequesterType.System;
            backfillGroup.RequesterID = Guid.Empty;
            backfillGroup.Priority = 3;
            backfillGroup.ID = Guid.NewGuid();
            backfillGroup.BackfillStartTime = fillFrom;
            backfillGroup.BackfillEndTime = fillTo;
            backfillGroup.BackfillStep = step;
            backfillGroup.RequeueCount = 5;



            string[] originalReqHeader = backfillGroup.DBGroupRequestConfig.DataPointBlockRequestListVals.Split('|')[0].Split(':');
            originalReqHeader[2] = "HDR";
            string newReqHeader = string.Join(":", originalReqHeader);
            string HDRreq = newReqHeader + "|" + HPN + ":" + datatypeName + ":" + tagID.ToString() + ":" + DateTimeToBackfillFormat(fillFrom) + ":" + DateTimeToBackfillFormat(fillTo) + ":" + step;
            backfillGroup.DBGroupRequestConfig.DataPointBlockRequestListVals = HDRreq;

            RequestGroups.Add(backfillGroup);
            return RequestGroups;
        }

      

        // obj is a reference to the DataAcqManager if the requestor is the FDA
        // obj is a reference to the string[] error list if the requestor is the Database Validator tool
        private static void RecordValidationError(object obj,string groupID,string requestor,string error,bool isWarning=false)
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

        // for calling from the Database Validator tool
        public static string[] ValidateRequestString(string request, Dictionary<Guid, FDADataPointDefinitionStructure> tags, Dictionary<Guid, FDADevice> devices,ref HashSet<Guid> referencedTags)
        {
            validationErrors = new List<string>();
            ValidateRequestString(validationErrors, "", "DBValidator", request, tags, devices,referencedTags);
            return validationErrors.ToArray();
        }

        // obj is a reference to the DataAcqManager if the requestor is the FDA
        // obj is a reference to the string[] error list if the requestor is the Database Validator tool
        public static bool ValidateRequestString(object obj, string groupID, string requestor, string request, Dictionary<Guid, FDADataPointDefinitionStructure> pointDefs, Dictionary<Guid, FDADevice> deviceParams,HashSet<Guid> referencedTags = null)
        {
      
            bool valid = true;

            string[] headerAndTagList = request.Split('|');
            string[] header = headerAndTagList[0].Split(':');


            // the header has the correct number of elements
            if (header.Length < 5) // header can be 5 or 6 (with the optional max data size paramenter). less than 5 is invalid, elements 7+ will just be ignored
            {
                //Globals.SystemManager.LogApplicationEvent(obj, "", " "contains an request with an invalid header (should be 'group:device:OpCode:Login:Pass:MaxDataSize(optional)'.", true);
                RecordValidationError(obj,groupID,requestor,"contains a request with an invalid header(should be 'group:device:OpCode:Login:Pass:MaxDataSize(optional)'");
                valid = false;
            }
            else
            {
                // check if a device reference (device specific settings) is present in header element 1
                string deviceRefString = null;
                string groupString = null;
                string[] headerElement1 = header[0].Split('^');
                groupString = headerElement1[0];

                // if a device reference is present, check that the ID is valid and references an existing device
                if (headerElement1.Length > 1)
                {
                    deviceRefString = headerElement1[1];

                    // is a valid GUID
                    if (!Guid.TryParse(deviceRefString, out Guid deviceRefID))
                    {
                        //    Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid device ID '" + deviceRefString + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.", true);
                        RecordValidationError(obj, groupID, requestor, "contains a request with an invalid device ID '" + deviceRefString + ", the correct format is xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.");
                        valid = false;
                    }

                    // if the GUID is valid, check that it references an existing device object
                    if (valid)
                    {
                        // ask the DB manager if this device exists
                        if (!deviceParams.ContainsKey(deviceRefID))
                        {
                            // Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid device reference (the device ID '" + deviceRefString + "' is not found).", true);
                           RecordValidationError(obj,groupID,requestor,"contains a request with an invalid device reference (the device ID '" + deviceRefString + "' is not found).");
                           valid = false;
                        }
                    }
                }



                if (!int.TryParse(groupString, out _))
                {
                    //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid element in the header (Device Group '" + header[0] + "' is not a number).", true);
                    RecordValidationError(obj,groupID,requestor,"contains a request with an invalid element in the header (Device Group '" + header[0] + "' is not a number).");
                    valid = false;
                }

                // header elements 1,2,4 (if present) and 5 (if present) are numeric
                if (!int.TryParse(header[1], out _))
                {
                    //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid element in the header (device '" + header[1] + "' is not a number).", true);
                    RecordValidationError(obj,groupID,requestor, "contains a request with an invalid element in the header (device '" + header[1] + "' is not a number).");
                    valid = false;
                }

                // remove the plus sign from the opcode (if present), before checking if it's a number
                string opCodewithNoPlus = header[2].Replace("+", "");
                bool opCodeIsNumeric = true;
                if (!int.TryParse(opCodewithNoPlus, out _))
                {
                    opCodeIsNumeric = false;
                    // exception for 'opcode' HDR (historical data request), this is valid even though it isn't numeric
                    if (opCodewithNoPlus != "HDR")
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid element in the header (opcode '" + header[2] + "' is not a number. A plus (+) is allowed but no other non-numeric characters).", true);
                        RecordValidationError(obj,groupID,requestor, "contains a request with an invalid element in the header (opcode '" + header[2] + "' is not a number. A plus (+) is allowed but no other non-numeric characters).");
                        valid = false;
                    }
                }

                // ROC username
                if (header[3] != string.Empty)
                {
                    if (header[3].Length != 3)
                    {
                      //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid element in the header (The ROC username '" + header[3] + "' is invalid, must be three characters).", true);
                        RecordValidationError(obj,groupID,requestor, "contains a request with an invalid element in the header (The ROC username '" + header[3] + "' is invalid, must be three characters).");
                        valid = false;
                    }
                }

                // ROC password
                if (header.Length > 4)
                {
                    if (header[4] != string.Empty)
                    {
                        // it's a number?
                        if (!int.TryParse(header[4], out int n))
                        {
                           //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid element in the header (The ROC password '" + header[4] + "' is not a number).", true);
                           RecordValidationError(obj,groupID,requestor, "contains a request with an invalid element in the header (The ROC password '" + header[4] + "' is not a number).");
                            valid = false;
                        }
                        else
                        {
                            // it's a number in the ushort range?
                            if (n < 0 || n > ushort.MaxValue)
                            {
                                //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid element in the header (The ROC password '" + header[4] + "' must be an integer between 0 and 65,535).", true);
                                RecordValidationError(obj,groupID,requestor, "contains a request with an invalid element in the header (The ROC password '" + header[4] + "' must be an integer between 0 and 65,535).");
                                valid = false;
                            }
                        }
                    }
                }

                if (header.Length > 5)
                {
                    if (!int.TryParse(header[5], out _))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid element in the header (MaxDataSize '" + header[5] + "' is not a number).", true);
                        RecordValidationError(obj,groupID,requestor,"contains a request with an invalid element in the header (MaxDataSize '" + header[5] + "' is not a number).");
                        valid = false;
                    }
                }

                // opcode is supported (skip this test if the opcode has already failed the 'is a number' test)
                if (opCodeIsNumeric)
                {
                    if (!supportedOpcodes.Contains(header[2]))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with an invalid element in the header (Opcode '" + header[2] + "' is not supported).", true);
                        RecordValidationError(obj,groupID,requestor,"contains a request with an invalid element in the header (Opcode '" + header[2] + "' is not supported).");
                        valid = false;
                    }
                }
            }



            // for those opcodes that require a TagList (information after the |)
            // string contains at least 1 header / tagList (separated by '|')
            if (valid && OpcodesRequiringTagList.Contains(header[2]))
            {
                if (headerAndTagList.Length < 2)
                {
                    //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an invalid request (requires at least 1 | character)'.", true);
                    RecordValidationError(obj,groupID,requestor, "contains an invalid request (requires at least 1 | character)'.");
                    valid = false;
                    return valid;
                }

                // each tag contains at least 5 elements
                // exception to the 5 elements rule for opcodes 120,121, and 122 (alarm and event retrieval doesn't require any tag information)
                string[] tagInfo;
                for (int i = 1; i < headerAndTagList.Length; i++)
                {
                    tagInfo = headerAndTagList[i].Split(':');
                    if (tagInfo.Length < 5)
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with invalid tag information, should be 'Type:Logical:Parameter:DataType:DataPointID'.", true);
                        RecordValidationError(obj,groupID,requestor, "contains a request with invalid tag information, should be 'Type:Logical:Parameter:DataType:DataPointID'.");
                        valid = false;

                    }
                    else
                    {
                        // elements 0-2 are numeric
                        if (!int.TryParse(tagInfo[0], out _))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid tag information (point type '" + tagInfo[0] + "' is not a number).", true);
                            RecordValidationError(obj,groupID,requestor, "contains a request with  invalid tag information (point type '" + tagInfo[0] + "' is not a number).");
                            valid = false;
                        }

                        if (!int.TryParse(tagInfo[1], out _))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid tag information (logical number '" + tagInfo[1] + "' is not a number).", true);
                           RecordValidationError(obj,groupID,requestor, "contains a request with  invalid tag information (logical number '" + tagInfo[1] + "' is not a number).");
                           valid = false;
                        }

                        if (!int.TryParse(tagInfo[2], out _))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid tag information (parameter '" + tagInfo[2] + "' is not a number).", true);
                            RecordValidationError(obj,groupID,requestor, "contains a request with  invalid tag information (parameter '" + tagInfo[2] + "' is not a number).");
                            valid = false;
                        }

                        // element 3 is a valid data type
                        string dataType = tagInfo[3].ToUpper();
                        if (!DataType.IsValidType(dataType))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with invalid tag information (the data type '" + tagInfo[3] + "' is not recognized).", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with invalid tag information (the data type '" + tagInfo[3] + "' is not recognized).");
                            valid = false;
                        }

                        // elements 4+ are valid guids and reference existing tags (there can be references to multiple tags if using the BIN type)
                        for (int counter = 4; counter < tagInfo.Length; counter++)
                        {
                            if (!ValidationHelpers.IsValidGuid(tagInfo[counter]))
                            {
                                if (!(dataType == "BIN" && tagInfo[counter] == "0")) // exception for guid = 0 in the case of BIN data type
                                {
                                    //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with invalid tag information (the tag ID '" + tagInfo[counter] + "' is not a valid UID, should be in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).", true);
                                    RecordValidationError(obj,groupID,requestor,"contains a request with invalid tag information (the tag ID '" + tagInfo[counter] + "' is not a valid UID, should be in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).");
                                    valid = false;
                                }
                            }
                            else  // the valid guid references an existing tag
                            {
                                if (!pointDefs.ContainsKey(Guid.Parse(tagInfo[counter])))
                                {
                                    //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid tag information (the tag ID '" + tagInfo[counter] + "' is not found).", true);
                                    RecordValidationError(obj, groupID, requestor, "contains a request with  invalid tag information (the tag ID '" + tagInfo[counter] + "' is not found).");
                                    valid = false;
                                }
                                else
                                {
                                    referencedTags?.Add(Guid.Parse(tagInfo[counter]));

                                    // the DPSType of the tag matches the DPStype of the RequestGroup
                                    FDADataPointDefinitionStructure tag = pointDefs[Guid.Parse(tagInfo[counter])];
                                    string tagDPSType = tag.DPSType.ToUpper();
                                    if (tagDPSType != "ROC")
                                    {
                                        RecordValidationError(obj, groupID, requestor, "contains a reference to tag '" + tagInfo[counter] + "' with an incorrect DPSType '" + tagDPSType + "'. The tag DPSType must match the RequestGroup DPSType");
                                    }
                                    // the tag is enabled
                                    if (!tag.DPDSEnabled)
                                    {
                                        RecordValidationError(obj, groupID, requestor, "contains a reference to a tag'" + tagInfo[counter] + "' that is disabled", true);

                                    }
                                }
                            }
                        }
                    }
                }
            }

            // special handling for backfill requests 
            if (valid && header[2]=="HDR")
            {
                string[] tagInfo;
                string[] fromTimeStr;
                string[] toTimeStr;
                for (int i = 1; i < headerAndTagList.Length; i++)
                {
                    
                    tagInfo = headerAndTagList[i].Split(':');

                    // ********************* minumum number of fields are present ********************
                    if (tagInfo.Length < 5)
                    { 
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with invalid information. Historical Data Requests (HDR) format is 'HPN:DataType:DPDUID:FromTime:ToTime:step(optional)'.", true);
                        RecordValidationError(obj,groupID,requestor, "contains a request with invalid information. Historical Data Requests (HDR) format is 'HPN:DataType:DPDUID:FromTime:ToTime:step(optional)'.");
                        valid = false;
                    }


                    //********************* numeric elements are numeric *********************************

                    // Element 0 (HPN) 
                    if (!int.TryParse(tagInfo[0], out _))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information (The value for HPN of '" + tagInfo[0] + "' is not a number).", true);
                       RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information (The value for HPN of '" + tagInfo[0] + "' is not a number).");
                        valid = false;
                    }

                    // Element 5 (step) if present, is numeric
                    if (tagInfo.Length > 5)
                    {
                        if (!int.TryParse(tagInfo[5],out _))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information (The Step value of '" + tagInfo[5] + "' is not a number).", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with  invalid information (The Step value of '" + tagInfo[5] + "' is not a number).");
                            valid = false;
                        }
                    }

                    // HPN is in range
                    int.TryParse(tagInfo[0], out int n);
                    if (n < 1 || n > 255)
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information (The HPN value of '" + tagInfo[5] + "' is out of range, valid values are 0 - 254).", true);
                        RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information (The HPN value of '" + tagInfo[5] + "' is out of range, valid values are 0 - 254).");
                        valid = false;
                    }

                    //***************************** datetime entries are valid date/time values ********************************
                    fromTimeStr = tagInfo[3].Split('-');
                    toTimeStr = tagInfo[4].Split('-');


                    // Element 3 (fromTime)
                    // fromTime has the right number of elements
                    if (fromTimeStr.Length != 5)
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[3] + "' is not a valid Date/Time).", true);
                        RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[3] + "' is not a valid Date/Time).");
                        valid = false;
                    }

                    // fromTime elements are numeric
                    foreach (string element in fromTimeStr)
                    {
                        if (!int.TryParse(element, out _))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[3] + "' is not a valid Date/Time).", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with  invalid information ('" + tagInfo[3] + "' is not a valid Date/Time).");
                            valid = false;
                        }
                    }

                    // each fromTime element is in range
                    if (valid)
                    {
                        // year
                        n = int.Parse(fromTimeStr[0]);
                        if (n < 0 || n > 99)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[3] + "' has a invalid year).", true);
                            RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[3] + "' has a invalid year).");
                            valid = false;
                        }

                        // month
                        int month = int.Parse(fromTimeStr[1]);
                        if (month < 1 || month > 12)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[3] + "' has an invalid month).", true);
                            RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[3] + "' has an invalid month).");
                            valid = false;
                        }

                        //day
                        int day = int.Parse(fromTimeStr[2]);
                        if (!DateTimeHelpers.IsValidDayOfMonth(day,month))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[3] + "' has an invalid day).", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with  invalid information ('" + tagInfo[3] + "' has an invalid day).");
                            valid = false;
                        }

                        // hour
                        n = int.Parse(fromTimeStr[3]);
                        if (n < 0 || n > 23)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[3] + "' has an invalid hour).", true);
                            RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[3] + "' has an invalid hour).");
                            valid = false;
                        }

                        // minute
                        n = int.Parse(fromTimeStr[4]);
                        if (n<0 || n > 59)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[3] + "' has an invalid minute).", true);
                            RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[3] + "' has an invalid minute).");
                            valid = false;
                        }



                        // Element 4 (toTime)
                        // toTime has the right number of elements
                        if (toTimeStr.Length != 5)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[4] + "' is not a valid Date/Time).", true);
                           RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[4] + "' is not a valid Date/Time).");
                            valid = false;
                        }

                        // toTime elements are numeric
                        foreach (string element in toTimeStr)
                        {
                            if (!int.TryParse(element, out n))
                            {
                                //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[4] + "' is not a valid Date/Time).", true);
                                RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[4] + "' is not a valid Date/Time).");
                                valid = false;
                            }
                        }

                        // each toTime element is in range

                        // year
                        n = int.Parse(toTimeStr[0]);
                        if (n < 0 || n > 99)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[4] + "' has an invalid year).", true);
                            RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[4] + "' has an invalid year).");
                            valid = false;
                        }

                        // month
                        month = int.Parse(toTimeStr[1]);
                        if (month < 1 ||month > 12)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[4] + "' has an invalid month).", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with  invalid information ('" + tagInfo[4] + "' has an invalid month).");
                            valid = false;
                        }

                        //day
                        day = int.Parse(toTimeStr[2]);
                        if (!DateTimeHelpers.IsValidDayOfMonth(day, month))
                        {
//                            Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[4] + "' has an invalid day).", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with  invalid information ('" + tagInfo[4] + "' has an invalid day).");
                            valid = false;
                        }

                        // hour
                        n = int.Parse(toTimeStr[3]);
                        if (n < 0 || n > 23)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[4] + "' has an invalid hour).", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with  invalid information ('" + tagInfo[4] + "' has an invalid hour).");
                            valid = false;
                        }

                        // minute
                        n = int.Parse(toTimeStr[4]);
                        if (n < 0 || n > 59)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information ('" + tagInfo[4] + "' has an invalid minute).", true);
                            RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information ('" + tagInfo[4] + "' has an invalid minute).");
                            valid = false;
                        }
                    }

                    if (valid)
                    {
                        // datetime values are valid, is the ToTime > the FromTime?
                        DateTime fromTime = BackfillToDateTime(tagInfo[3]);// DateTime.Parse(fromTimeStr[0] + "/" + fromTimeStr[1] + "/" + fromTimeStr[2] + " " + fromTimeStr[3] + ":00:0.000");
                        DateTime toTime = BackfillToDateTime(tagInfo[4]);  // DateTime.Parse(toTimeStr[0] + "/" + toTimeStr[1] + "/" + toTimeStr[2] + " " + toTimeStr[3] + ":00:0.000");

                        if (toTime < fromTime)
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information (the 'to' time " + tagInfo[4] + " is before the 'from' time" + tagInfo[3] + ").", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with  invalid information (the 'to' time " + tagInfo[4] + " is before the 'from' time" + tagInfo[3] + ").");
                            valid = false;
                        }

                    }


                    //element 5 (step)
                    if (tagInfo.Length > 5)
                    {
                        if (!int.TryParse(tagInfo[5], out _))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid information (the inteval value '" + tagInfo[5] + "' is not a number).", true);
                           RecordValidationError(obj,groupID,requestor, "contains a request with  invalid information (the inteval value '" + tagInfo[5] + "' is not a number).");
                            valid = false;
                        }                      
                    }

                  
                    // element 1 is a valid roc data type
                    string dataType = tagInfo[1].ToUpper();
                    if (!DataType.IsValidType(dataType))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with invalid tag information (the data type '" + tagInfo[1] + "' is not recognized).", true);
                        RecordValidationError(obj,groupID,requestor, "contains a request with invalid tag information (the data type '" + tagInfo[1] + "' is not recognized).");
                        valid = false;
                    }

                    //element 2 is a valid UID
                    if (!ValidationHelpers.IsValidGuid(tagInfo[2]))
                    {
                        if (!(dataType == "BIN" && tagInfo[2] == "0")) // exception for guid = 0 in the case of BIN data type
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with invalid tag information (the tag ID '" + tagInfo[2] + "' is not a valid UID, should be in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).", true);
                           RecordValidationError(obj,groupID,requestor, "contains a request with invalid tag information (the tag ID '" + tagInfo[2] + "' is not a valid UID, should be in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).");
                            valid = false;
                        }
                    }
                    else  // the valid UID references an existing tag
                    {
                        if (!pointDefs.ContainsKey(Guid.Parse(tagInfo[2])))
                        {
                            //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an request with  invalid tag information (the tag ID '" + tagInfo[2] + "' is not found.", true);
                            RecordValidationError(obj,groupID,requestor,"contains a request with  invalid tag information (the tag ID '" + tagInfo[2] + "' is not found.");
                            valid = false;
                        }
                    }
                }
            }

            // Opcode 121/122 with specified pointers: two values are specified, they're numeric and in range
            if (headerAndTagList.Length > 1 && (header[2] == "121" || header[2] == "122"))
            {
                string[] pointerInfo = headerAndTagList[1].Split(':');
                if (pointerInfo.Length < 2)
                {
                    //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an Alarms/Events request with invalid pointers specified.", true);
                   RecordValidationError(obj,groupID,requestor, "contains an Alarms/Events request with invalid pointers specified.");
                   valid = false;
                }
                else
                {

                    if (!int.TryParse(pointerInfo[0], out int start))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an Alarms/Events request with invalid pointers specified.", true);
                        RecordValidationError(obj,groupID,requestor,"contains an Alarms/Events request with invalid pointers specified.");
                        valid = false;
                    }

                    if (!int.TryParse(pointerInfo[1], out int end))
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an Alarms/Events request with invalid pointers specified.", true);
                        RecordValidationError(obj,groupID,requestor, "contains an Alarms/Events request with invalid pointers specified.");
                        valid = false;
                    }

                    if (start < 0 || start > 239 || end < 0 || end > 239)
                    {
                        //Globals.SystemManager.LogApplicationEvent(obj, "", "contains an Alarms/Events request with invalid pointers specified.", true);
                        RecordValidationError(obj,groupID,requestor,"contains an Alarms/Events request with invalid pointers specified.");
                        valid = false;
                    }
                }
            }

            return valid;
        }

        public static void IdentifyWrites(RequestGroup requestGroup)
        {
            //2:1:180:LOI:|
            //    10:1:3:FL:77048344-0506-49E0-A032-6E6591C2E4FD|
            //    10:1:4:FL:6D8BA8CA-93F9-418E-950C-F2A199484085
            string[] request;
            string[] header;
            string[] taginfo;
            string[] requests = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals.Split('$');
            foreach (string req in requests)
            {
                request = req.Split('|');
                header = request[0].Split(':');
                if (header[2].Contains("181"))
                {
                    // this is a write request
                    // we need to identify the tags that are being written so the values to be written can be looked up from the database
                    if (requestGroup.writeLookup == null)
                        requestGroup.writeLookup = new Dictionary<Guid, double>();

                    lock (requestGroup.writeLookup)
                    {
                        //tags = request[1].Split('|');
                        for (int tagidx = 1; tagidx < request.Length; tagidx++)
                        {
                            taginfo = request[tagidx].Split(':');

                            if (taginfo[4] != "0")
                            {
                                requestGroup.writeLookup.Add(Guid.Parse(taginfo[4]), double.NaN);
                            }

                            // extra tags (this can happen with the binary data type)
                            if (taginfo.Length > 5)
                            {
                                for (int i = 5; i < taginfo.Length; i++)
                                {
                                    if (taginfo[i] != "0")
                                        requestGroup.writeLookup.Add(Guid.Parse(taginfo[i]), double.NaN);
                                }
                            }
                        }
                    }
                }
            }
 
        }

        public static void CreateRequests(RequestGroup requestGroup, object protocolOptions = null)
        {

            //  dbRequest definition example  (two requests to two different devices, each with two tags)
            // 2:1:180:LOI:|
            //    10:1:3:FL:77048344-0506-49E0-A032-6E6591C2E4FD|  (T:L:P:type:ID)
            //    10:1:4:FL:6D8BA8CA-93F9-418E-950C-F2A199484085
            // $
            // 2:2:180:LOI:|
            //     10:1:3:FL: 32BB35C7-160F-479B-A0A5-6D04DDB3E1EB|
            //     10:1:4:FL: B1DD8A1F-1C2F-4A5A-8B45-7CAE357A4D6A
            List<Tag> PreReadValues = null;
            try
            {
                if (protocolOptions != null)
                {
                    PreReadValues = (List<Tag>)protocolOptions;
                }

                string requestString = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals;

                string[] requests;           // delimited by $
                string[] HeaderAndBody;      // delimited by |
                string[] Header;             // delimited by :
                string[] requestBody;        // delimited by |
                string[] TagData = null;     // delimited by : (within a Tags element)

                DataType type;
                List<TLP> tlpList;
                List<Tag> tagList;
                List<FDADataPointDefinitionStructure> tagConfigsList;
                Tag newTag;
                TLP newTLP;
                Guid TagID;
                
                string login;
                short pwd;
                string opCode;
                byte groupNum;
                byte devNum;
                FDADevice deviceSettings = null;


                // check for null or empty group definition, exit if found
                if (requestString == null || requestString == string.Empty)
                    return;

                requests = requestString.Split('$');

                string request;
                int maxDataSize = 240; // default to protocol maximum 240
                bool containsBIN = false;
                int arraylength = 0;
                for (int requestIdx = 0; requestIdx < requests.Length; requestIdx++)
                {
                    request = requests[requestIdx];
                    // example request
                    //2:1:180:LOI:|
                    //    10:1:3:FL:77048344-0506-49E0-A032-6E6591C2E4FD|
                    //    10:1:4:FL:6D8BA8CA-93F9-418E-950C-F2A199484085

                    try
                    {
                        HeaderAndBody = request.Split('|');
                        Header = HeaderAndBody[0].Split(':');

                        arraylength = HeaderAndBody.Length;
                        requestBody = new string[arraylength - 1];
                        Array.Copy(HeaderAndBody, 1, requestBody, 0, arraylength - 1);

                        // check for a device reference in the first element of the header, pull it out if present
                        string[] headerElement0 = Header[0].Split('^');
                        {
                            if (headerElement0.Length > 1)
                            {

                                Guid deviceRef = Guid.Parse(headerElement0[1]);
                                deviceSettings = ((DBManager)Globals.DBManager).GetDevice(deviceRef);
                            }
                        }


                        groupNum = Byte.Parse(headerElement0[0]);   // device group number
                        devNum = Byte.Parse(Header[1]);     // device unit number
                        opCode = Header[2];
                        login = Header[3];
                        if (Header[4] != "")
                            pwd = short.Parse(Header[4]);
                        else
                            pwd = -1;

                        if (Header.Length > 5)
                            maxDataSize = int.Parse(Header[5]);

                        if (maxDataSize > 240 || maxDataSize < 20)
                        {

                            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "Protocol", "ROC", "Request group " + requestGroup.ID + " maximum message size " + maxDataSize + " is invalid. using max data block size of 240 bytes");
                            maxDataSize = 240;
                        }

                    }
                    catch (Exception ex)
                    {
                        // something about the definition of this request group caused an error while parsing it, log the error and move to the next request
                        Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "FDA.ROCProtocol: Error while parsing the header of request group ID " + requestGroup.ID);
                        continue;
                    }


                    // historical data requests
                    if (opCode == "HDR")
                    {
                        string[] HDRparams = requestBody[0].Split(':');
                        string fromTimeStr = HDRparams[3];
                        string toTimeStr = HDRparams[4];

                        string requestStr = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals;
                        string[] parsed = requestStr.Split('|');
                        string[] body = parsed[1].Split(':');



                        int interval = 1;
                        if (HDRparams.Length > 5)
                            interval = int.Parse(HDRparams[5]);
                        else
                        {
                            Guid tagID = Guid.Parse(HDRparams[2]);
                            interval = (int)requestGroup.TagsRef[tagID].backfill_data_interval;
                        }

                        DateTime fromTime = BackfillToDateTime(fromTimeStr);
                        DateTime toTime = BackfillToDateTime(toTimeStr);
                        DateTime currentTime = Globals.FDANow();


                        TimeSpan gap = toTime.Subtract(fromTime);


                        if (currentTime.Subtract(toTime).TotalHours > 840)
                        {
                            // entire range requested is too old, doesn't exist in the ROC. nothing to do
                            return;
                        }

                        if (currentTime.Subtract(fromTime).TotalHours < 0)
                        {
                            // entire range is in the future, nothing to do
                            return;
                        }


                        if (currentTime.Subtract(fromTime).TotalHours > 840)
                        {
                            // the requested starting point is too old, adjust it to the oldest available data
                            fromTime = currentTime.AddHours(-840);
                        }


                        if (currentTime.Subtract(toTime).TotalHours < 0)
                        {
                            // the request ending point is in the future, limit to current system time
                            toTime = currentTime;
                        }

                        // set the Backfill parameters for the group to the entire period to be filled
                        requestGroup.BackfillStartTime = fromTime;
                        requestGroup.BackfillEndTime = toTime;
                        requestGroup.BackfillStep = interval;

                        // is minute data available and requested?
                        if (currentTime.Subtract(toTime).TotalHours < 1 && interval < 60)
                        {
                            // yes, start with a request for minute data
                            DateTime minuteDataFrom = fromTime;
                            DateTime minuteDataTo = toTime;

                            // adjust the from time to the oldest available minute data
                            if (currentTime.Subtract(minuteDataFrom).TotalHours > 1)
                                minuteDataFrom = currentTime.AddHours(-1);

                            int gapSize = (int)minuteDataTo.Subtract(minuteDataFrom).TotalMinutes + 1;

                            int startIdx = (int)currentTime.Subtract(minuteDataTo).TotalMinutes;




                            // update the request string with these values




                            // update the body of the request with the startIdx and Count values
                            body[3] = startIdx.ToString();
                            body[4] = gapSize.ToString();

                            string[] updatedBody = new string[] { string.Join(":", body) };

                            // now we can go to GenerateOpcode126Request() to get the actual request built
                            requestGroup.ProtocolRequestList = GenerateOpcode126Request(requestGroup.ID, requestGroup.ConnectionID, groupNum, devNum, 1, 1, login, pwd, updatedBody);

                            foreach (DataRequest req in requestGroup.ProtocolRequestList)
                                req.ParentTemplateGroup = requestGroup;

                             return;
                        }

                        // minute data not required, request hourly data

                        // if the timeframe is under and hour and doesn't cover an hour boundary, shift it to include the nearest hour boundary
                        if (requestGroup.BackfillEndTime.Subtract(requestGroup.BackfillStartTime).TotalHours < 1 && requestGroup.BackfillEndTime.Hour == requestGroup.BackfillStartTime.Hour)
                        {
                            int minutesFromPreviousHour = requestGroup.BackfillStartTime.Minute;
                            int minutesToNextHour = 60 - requestGroup.BackfillEndTime.Minute;

                            if (minutesFromPreviousHour < minutesToNextHour)
                            {
                                // adjust the timeframe backwards to include the previous hour
                                requestGroup.BackfillStartTime = requestGroup.BackfillStartTime.AddMinutes(-30);
                                requestGroup.BackfillEndTime = requestGroup.BackfillEndTime.AddMinutes(-30);
                            }
                            else
                            {
                                // adjust the timeframe forwards to include the next hour
                                requestGroup.BackfillStartTime = requestGroup.BackfillStartTime.AddMinutes(30);
                                requestGroup.BackfillEndTime = requestGroup.BackfillEndTime.AddMinutes(30);
                            }
                        }




                        requestGroup.ProtocolRequestList = BackfillHourData(requestGroup);

                         return;
                    }   


                    if (opCode == "128")
                    {
                        List<DataRequest> historyRequestList = GenerateOpcode128Request(requestGroup.ID, requestGroup.ConnectionID, groupNum, devNum, 1, 1, opCode, login, pwd, requestBody);
                        requestGroup.ProtocolRequestList.AddRange(historyRequestList);
                        return;
                    }

                    if (opCode == "130")
                    {
                        List<DataRequest> historyRequestList;
                        if (requestGroup.BackfillStartTime != DateTime.MinValue) // this is an auto backfill
                        {
                            historyRequestList = BackfillHourData(requestGroup);
                            requestGroup.RequeueCount = 5;
                        }
                        else // this is a manually trigged opcode 130 request
                            historyRequestList = GenerateOpcode130Request(requestGroup.ID, requestGroup.ConnectionID, groupNum, devNum, 1, 1, login, pwd, requestBody);

                        requestGroup.ProtocolRequestList.AddRange(historyRequestList);
                         return;
                    }
                    
 
                                      

                    tlpList = new List<TLP>();
                    tagList = new List<Tag>();
                    tagConfigsList = new List<FDADataPointDefinitionStructure>();

                    byte logical;
                    byte pointType;
                    byte parameter;
                    bool tagError = false;
                    int tagidx = 1;

                    foreach (string tagString in requestBody)
                    {
                        if (tagString == "") continue;

                        if (tagError)
                            continue;
                        // example tag
                        //  1:10:3:FL:77048344-0506-49E0-A032-6E6591C2E4FD
                        try
                        {
                            TagData = tagString.Split(':');
                            if (TagData.Length < 4)
                                continue;
                            pointType = Byte.Parse(TagData[0]);
                            logical = Byte.Parse(TagData[1]);
                            parameter = Byte.Parse(TagData[2]);
                            type = DataType.GetType(TagData[3]);


                            if (!Guid.TryParse(TagData[4], out TagID))  // check for bad ID
                            {
                                if (type == DataType.BIN && TagData[4] == "0")  // an ID of 0 gets recognized as a bad ID, but it's ok if the data type is BIN
                                    TagID = Guid.Empty;                         // insert a placeholder in this case
                            }


                            tagidx++;
                        }
                        catch (Exception ex)
                        {
                            // something about the definition of this tag caused an error while parsing it, log the error and move on to the next tag
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "FDA.ROCProtocol: Error parsing request group ID " + requestGroup.ID + ", request " + (requestIdx + 1).ToString() + ", tag " + tagidx + ". Skipping this request.");
                            tagError = true;
                            continue;
                        }

                        // ignore ASCII types for now (the database can't handle them)
                        if (type == DataType.AC10 || type == DataType.AC20 || type == DataType.AC30)
                            continue;

                        
                        try
                        {
                            if (type == DataType.BIN)    // BIN data type requires special handling
                            {
                                containsBIN = true;
                                for (int i = 4; i < 12; i++)
                                {
                                    if (i < TagData.Length)
                                    {
                                        if (TagData[i] == "0")
                                        {
                                            newTag = new Tag(Guid.Empty);
                                            newTLP = new TLP(pointType, logical, parameter, type);
                                            newTag.DeviceTagAddress = newTLP.TLPString;

                                            tagList.Add(newTag);
                                            tlpList.Add(newTLP);
                                            tagConfigsList.Add(new FDADataPointDefinitionStructure());
                                        }
                                        else
                                        {
                                            TagID = Guid.Parse(TagData[i]);
                                            newTag = new Tag(TagID);
                                            newTLP = new TLP(pointType, logical, parameter, type);
                                            newTag.DeviceTagAddress = newTLP.TLPString;

                                            tagList.Add(newTag);
                                            tlpList.Add(newTLP);
                                            tagConfigsList.Add(requestGroup.TagsRef[TagID]);
                                        }
                                    }
                                    else
                                    {   // there aren't 8 bits defined in the input string, add placeholder(s) for the undefined bit(s)
                                        newTag = new Tag(Guid.Empty);
                                        newTLP = new TLP(pointType, logical, parameter, type);
                                        newTag.DeviceTagAddress = newTLP.TLPString;
                                        tagList.Add(newTag);
                                        tlpList.Add(newTLP);
                                        tagConfigsList.Add(new FDADataPointDefinitionStructure());
                                    }
                                }
                            }
                            else
                            {   // regular (non-BIN) tags
                                newTLP = new TLP(pointType, logical, parameter, type);
                                tlpList.Add(newTLP);

                                newTag = new Tag(TagID)
                                {
                                    DeviceTagAddress = newTLP.TLPString
                                };
                                tagList.Add(newTag);
                                tagConfigsList.Add(requestGroup.TagsRef[TagID]);
                            }
                        }
                        catch (Exception ex)
                        {
                            Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "FDA.ROCPRotocol: Error while processing group " + requestGroup.ID.ToString() + " : Data point definition ID not found (" + TagID.ToString() + ")");
                        }
                        
                    }

                    // check if a OpcodePlus (readback or pre-read, depending on the opcode) is indicated, and strip the + from the opcode field if it is present
                    bool OpcodePlus = (opCode[0] == '+');

                    if (OpcodePlus)
                        opCode = opCode.Substring(1, opCode.Length - 1);

                    // GenerateReadRequest now returns a list of DataRequests, 
                    //in the event that the definition would result in an oversized response message and needs to be broken up into multiple requests 
                    List<DataRequest> subRequestList = new();
                    byte[] ptrs;
                    ushort lastRead;
                    ushort current;

                    switch (opCode)
                    {
                        // current pointers request
                        case "120":
                            subRequestList = GenerateOpcode120Request(requestGroup.ID, "", groupNum, devNum, 1, 1, requestIdx + 1,false,DataRequest.RequestType.AlarmEventPointers);
                            break;
                        // alarms request
                        case "121":
                            if (OpcodePlus)
                            {
                                // Generate an Opcode 120 request, the Opcode 121 will be run when the 120 request is complete
                                subRequestList = GenerateOpcode120Request(requestGroup.ID, "", groupNum, devNum, 1, 1, requestIdx + 1, true,DataRequest.RequestType.AlarmEventPointers);                                
                                break;
                            }

                            if (TagData == null) // no pointers were specified in the request string, get them from the database table
                            {
                                ptrs = ((DBManager)(Globals.DBManager)).GetAlmEvtPtrs(requestGroup.ConnectionID, groupNum + ":" + devNum, "Alarms");
                                if (ptrs != null)
                                {
                                    lastRead = ptrs[0];
                                    current = ptrs[1];

                                    subRequestList = GenerateAlarmEventsRequests(requestGroup.ID, "", groupNum, devNum, 1, 1, 121, lastRead, current,false, (requestIdx + 1).ToString());
                                }
                                else
                                {
                                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROC Protocol", "Failed to retrieve the last read and current alarm pointers from the database, alarm retrieval cancelled");
                                    return;
                                }
                                break;
                            }

                            // use the pointers that were specified in the request string
                            if (TagData.Length >= 2)
                            {
                                int start;
                                int end;
                                int.TryParse(TagData[0], out start);
                                int.TryParse(TagData[1], out end);

                                // adjust the supplied pointers to a 0 based system
                                start = MiscHelpers.AddCircular(start, -1, 0, 239);
                                end = MiscHelpers.AddCircular(end, -1, 0, 239);

                                // when specifying pointers, we need to include the first and last record in the results (the last read and current records are normally excluded), so we set "inclusive" to true
                                subRequestList = GenerateAlarmEventsRequests(requestGroup.ID, "", groupNum, devNum, 1, 1, 121,Convert.ToUInt16(start),Convert.ToUInt16(end),true,(requestIdx + 1).ToString());
                            }
                            else
                            {
                                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROC Protocol", "Opcode 121 request with specified pointers requires two values (First Pointer:Last Pointer)");
                                return;
                            }
                            
                            break; 

                        //events request
                        case "122":
                            if (OpcodePlus)
                            {
                                // Generate an Opcode 120 request, the Opcode 121 will be run when the 120 request is complete
                                subRequestList = GenerateOpcode120Request(requestGroup.ID, "", groupNum, devNum, 1, 1, requestIdx + 1, true,DataRequest.RequestType.AlarmEventPointers);
                                break;
                            }

                            if (TagData == null) // no pointers were specified in the request string, get them from the database table
                            {
                                ptrs = ((DBManager)(Globals.DBManager)).GetAlmEvtPtrs(requestGroup.ConnectionID, groupNum + ":" + devNum, "Events");
                                if (ptrs != null)
                                {
                                    lastRead = ptrs[0];
                                    current = ptrs[1];
                                    if (MiscHelpers.AddCircular(lastRead,1,1,240)==current)
                                    {
                                        Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "", "Alarms request for device " + groupNum + ":" + devNum + " on connection " + requestGroup.ConnectionID + " cancelled, because there are no new records");
                                        break;
                                    }
                                    subRequestList = GenerateAlarmEventsRequests(requestGroup.ID, "", groupNum, devNum, 1, 1, 122, lastRead, current,false, (requestIdx + 1).ToString());
                                }
                                else
                                {
                                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROC Protocol", "Failed to retrieve the last read and current event pointers from the database, alarm retrieval cancelled");
                                    return;
                                }
                                break;
                            }

                            // use the pointers that were specified in the request string
                            if (TagData.Length >= 2)
                            {
                                int start;
                                int end;
                                int.TryParse(TagData[0], out start);
                                int.TryParse(TagData[1], out end);

                                // convert to zero based
                                start = MiscHelpers.AddCircular(start, -1, 0, 239);
                                end = MiscHelpers.AddCircular(end, -1, 0, 239);

                                subRequestList = GenerateAlarmEventsRequests(requestGroup.ID, "", groupNum, devNum, 1, 1, 122, Convert.ToUInt16(start),Convert.ToUInt16(end), true, (requestIdx + 1).ToString());
                            }
                            else
                            {
                                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROC Protocol", "Opcode 122 request with specified pointers requires two values (First Pointer:Last Pointer)");
                                return;
                            }
                            break;
                        // Read Param(s)
                        case "180": subRequestList = GenerateReadRequest(requestGroup.ID, "", groupNum, devNum, 1, 1, tlpList.ToArray(), tagConfigsList, tagList, requestIdx + 1, maxDataSize, DataRequest.RequestType.Read); break;

                        // Write Param(s)
                        // opcode 181, write data request. Generate a new DataRequest object for it, identify it as a write, and include the list of tags. 
                        // DataAcqManager will update it with the values to be written and send it back to the protocol to be completed using CompleteWriteRequests functions
                        case "181":
                            if (containsBIN && requestGroup.RequesterType != Globals.RequesterType.System)  // do a read of the current value of binary tag(s) before writing, if any binary tags are found in the list
                            {
                                // convert the request to a pre-write read instead of a write
                                subRequestList = GeneratePreWriteRead(requestGroup.ID, "", groupNum, devNum, 1, 1, tlpList.ToArray(), tagConfigsList, tagList, requestIdx + 1, maxDataSize);

                                foreach (DataRequest req in subRequestList)
                                    req.ValuesToWrite = requestGroup.writeLookup;
                                
                                // cancel the readback if it was requested
                                OpcodePlus = false;

                            }
                            else
                            { 
                                subRequestList = GenerateWriteRequest("", groupNum, devNum, 1, 1, tlpList.ToArray(), tagConfigsList, requestGroup.writeLookup,PreReadValues,requestIdx + 1, maxDataSize);
                            }
                            break;

                        // unsupported Opcode
                        default:
                            Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "FDA.ROCProtocol", "CreateRequests()", "unsupported opcode " + opCode + " found in group '" + requestGroup.Description + "'");
                            break;
                    }

                    foreach (DataRequest subRequest in subRequestList)
                    {
                        subRequest.DeviceLogin = login;
                        subRequest.DevicePass = pwd;
                        subRequest.ConnectionID = requestGroup.ConnectionID;
                        subRequest.GroupID = requestGroup.ID;
                        subRequest.GroupSize = requests.Length;
                        subRequest.NodeID = groupNum + ":" + devNum;
                        subRequest.RequesterType = requestGroup.RequesterType;
                        subRequest.RequesterID = requestGroup.RequesterID;
                        subRequest.ParentTemplateGroup = requestGroup;
                        subRequest.DeviceSettings = deviceSettings;
                    }
                    requestGroup.ProtocolRequestList.AddRange(subRequestList);

                    List<DataRequest> readbackSubRequestList = new();
                    if (OpcodePlus)
                    {
                        readbackSubRequestList = GenerateReadRequest(requestGroup.ID, "", groupNum, devNum, 1, 1, tlpList.ToArray(), tagConfigsList, tagList, requestIdx + 1, maxDataSize, DataRequest.RequestType.ReadbackAfterWrite);
                        foreach (DataRequest readbackSubRequest in readbackSubRequestList)
                        {
                            readbackSubRequest.DeviceLogin = login;
                            readbackSubRequest.DevicePass = pwd;
                            readbackSubRequest.ConnectionID = requestGroup.ConnectionID;
                            readbackSubRequest.GroupID = requestGroup.ID;
                            readbackSubRequest.GroupSize = requests.Length;
                            readbackSubRequest.NodeID = groupNum + ":" + devNum;
                            readbackSubRequest.RequesterType = requestGroup.RequesterType;
                            readbackSubRequest.RequesterID = requestGroup.RequesterID;
                            readbackSubRequest.ParentTemplateGroup = requestGroup;
                            readbackSubRequest.DeviceSettings = deviceSettings;
                        }
                        requestGroup.ProtocolRequestList.AddRange(readbackSubRequestList);
                      
                    }
                }

                foreach (DataRequest req in requestGroup.ProtocolRequestList)
                    req.ParentGroup = requestGroup;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Roc Protocol CreateRequests() general error");
            }

        }

        public static void CancelDeviceRequests(DataRequest failedReq)
        {
            if (failedReq.RequestBytes.Length < 2)
                return;

            byte thisDeviceUnit = failedReq.RequestBytes[0];
            byte thisDeviceGroup = failedReq.RequestBytes[1];

            if (failedReq.ParentGroup != null)
            {
                foreach (DataRequest req in failedReq.ParentGroup.ProtocolRequestList)
                {
                    if (req.RequestBytes.Length < 2)
                        continue;

                    if (req.RequestBytes[0] == thisDeviceUnit && req.RequestBytes[1] == thisDeviceGroup)
                    {
                        req.SkipRequestFlag = true;
                    }
                }
            }
        }

        private static List<DataRequest> BackfillHourData(RequestGroup requestGroup,int currentPtr=-1)
        {
            string requestStr = requestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals;
            string[] parsed = requestStr.Split('|');
            string[] body = parsed[1].Split(':');
            string[] header = parsed[0].Split(':');

            byte deviceGroup = byte.Parse(header[0]);
            byte deviceNum = byte.Parse(header[1]);
            string login = header[3];


            if (!short.TryParse(header[4], out short pwd))
                pwd = -1;

            if (currentPtr == -1)
            {
                // we need to find where the current hour history pointer is, return an Opcode 120 request for that
                List<DataRequest> ptrRetrievalReq = GenerateOpcode120Request(requestGroup.ID, "", deviceGroup, deviceNum, 1, 1, 1, true, DataRequest.RequestType.HourHistoryPtr);

                // link the opcode 120 request with this group, so the group can be re-run once we have the ptr value
                foreach (DataRequest req in ptrRetrievalReq)
                {
                    req.ParentTemplateGroup = requestGroup;
                    req.RequesterID = requestGroup.RequesterID;
                    req.RequesterType = requestGroup.RequesterType;
                    req.DeviceLogin = login;
                    req.DevicePass = pwd;
                }

                return ptrRetrievalReq;
            }
            else
            {
                // OpCode 120 request is done, we know the current ptr, so generate the Opcode 130 request(s)
                DateTime currentTime = Globals.FDANow();
                DateTime fillFrom = requestGroup.BackfillStartTime;
                DateTime fillTo = requestGroup.BackfillEndTime;
                int step = requestGroup.BackfillStep;
 
                // we don't need hourly data that is within 1 hour from the current time, that will be covered by minute data
                if (fillTo > currentTime.Subtract(new TimeSpan(1, 0, 0)))
                    fillTo = currentTime.Subtract(new TimeSpan(1, 0, 0));

                // we only have 840 hours of data in the device, if the request start time is before that, cut it off at 840 hours ago
                if (fillFrom < currentTime.AddHours(-840))
                    fillFrom = currentTime.AddHours(-840);

                requestGroup.BackfillStartTime = fillFrom;
                //requestGroup.BackfillEndTime = fillTo;

                // how many hour values to we need to request to fill in the gap, at the specified interval?
                // we've already limited the maximum gap for a single request to 60 hours, so the gapSize won't be more than 60 -- note! this not true anymore
                int fullGapSize = (int)(fillTo.Subtract(fillFrom).TotalHours)+1;

                
                int thisReqCount = fullGapSize;

                if (thisReqCount > 60)
                {
                    // limit this request to 60 hours
                    thisReqCount = 60;
                }

                // we need to figure out the offset and count required to fill in the gap, and update the request string with that

                // how many hours between the current time and the backfill end time?
                // our start index is the current pointer minus that number
                int startIdx = currentPtr - (int)currentTime.Subtract(fillTo).TotalHours; // + 1?
    
                // index to start at is (end of the gap - size of the gap)
                startIdx -= (int)fullGapSize;

                // if the startIdx is less than 0 after all the adjustments, we need to wrap it around
                if (startIdx < 0)
                    startIdx = 839 + startIdx + 1;


                // generate an Opcode 130 request
                string[] opCode130Body;
                if (body.Length > 5)
                {
                    opCode130Body = new string[7];
                    opCode130Body[6] = body[5];
                }
                else
                    opCode130Body = new string[6];


                // update the body of the request with startIdx and Count values
                opCode130Body[0] = body[0];
                opCode130Body[1] = body[1];
                opCode130Body[2] = body[2];
                opCode130Body[3] = "0"; // history point type = hourly
                opCode130Body[4] = startIdx.ToString();
                opCode130Body[5] = thisReqCount.ToString();
                // now we can go to GenerateOpcode130Request() to get the actual request(s) built
                List<DataRequest> requests = null;
                requests = GenerateOpcode130Request(requestGroup.ID, requestGroup.ConnectionID, deviceGroup, deviceNum, 1, 1, login, pwd, opCode130Body,step);

                foreach (DataRequest req in requests)
                {
                    req.ParentTemplateGroup = requestGroup;
                    req.RequesterID = requestGroup.RequesterID;
                    req.RequesterType = requestGroup.RequesterType;
                }
                return requests;
            }
        }

        private static string DateTimeToBackfillFormat(DateTime datetime)
        {
            return datetime.Year - 2000 + "-" + datetime.Month + "-" + datetime.Day + "-" + datetime.Hour + "-" + datetime.Minute;
        }

        private static DateTime BackfillToDateTime(string bfDateTime)
        {
            try
            {
                string[] DTStr = bfDateTime.Split('-');

                int year = int.Parse(DTStr[0]) + 2000;
                int month = int.Parse(DTStr[1]);
                int day = int.Parse(DTStr[2]);
                int hour = int.Parse(DTStr[3]);
                int minute = int.Parse(DTStr[4]);

                DateTime dt = new(year, month, day, hour, minute,0);
                return dt;

            } catch(Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while converting backfill datetime string '" + bfDateTime + "' to a DateTime object");
                return DateTime.MinValue;
            }


        }


        /// <summary>
        /// Generates a Datarequest object, which requests the current alarm and event pointers from the specified device
        /// </summary>
        /// <param name="RequestGroup">The ID of the group which contains this request</param>
        /// <param name="reqID">unused, put an empty string</param>
        /// <param name="group">The group number of the target device</param>
        /// <param name="unit">The unit number of the target device</param>
        /// <param name="hostGroup">The group number of the host</param>
        /// <param name="hostUnit">The unit number of the host</param>
        /// <param name="groupIdx">The index number of this request within the group</param>
        /// <param name="systemRequest">mark the request as system generated (not directly triggered by a schedule or demand)</param>
        /// <returns></returns>
        private static List<DataRequest> GenerateOpcode120Request(Guid RequestGroup, string reqID, byte group, byte unit, byte hostGroup, byte hostUnit, int groupIdx,bool systemRequest,DataRequest.RequestType reqType)
        {
            DataRequest OC120Request = new();

            List<byte> requestBytes = new() { unit, group, hostUnit, hostGroup, 120,0};

            // calculate the checksum
            ushort CRC16 = CRCHelpers.CalcCRC16(requestBytes.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);

            // add the checksum bytes to the end of the request
            requestBytes.Add(IntHelpers.GetLowByte(CRC16));         // CRC Low Byte
            requestBytes.Add(IntHelpers.GetHighByte(CRC16));        // CRC High Byte


            DataRequest request = new()
            {
                RequestBytes = requestBytes.ToArray(),
                GroupIdxNumber = groupIdx.ToString(),
                ExpectedResponseSize = 34,
                Protocol = "ROC",
                MessageType = reqType // DataRequest.RequestType.AlarmEventPointers
            };

            if (systemRequest)
            {
                request.RequesterType = Globals.RequesterType.System;
                request.RequesterID = Guid.Empty;
            }


            List<DataRequest> requestList = new()
            {
                request
            };
            return requestList;
        }


        public static List<DataRequest> GenerateAlarmEventsRequests(Guid requestGroupID,string requestID,byte groupNum,byte devNum,byte hostGroupNum, byte HostDevNum,byte opCode, ushort lastReadRecord,ushort currentPointer,bool inclusive,string requestIdx)
        {
            List<DataRequest> requestList = new();

            if (lastReadRecord < 0 || lastReadRecord > 239 || currentPointer < 0 || currentPointer > 239)
            {
                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROC Protocol", "GenerateAlarmsEventsRequests()", "Request group " + requestGroupID + ", request " + requestIdx + " received invalid pointer value(s) (" + lastReadRecord + "," + currentPointer + "). Canceling the request", true);
                return requestList;
            }


            

            // the requests for alarms or events are identical except for the opcode, so we'll just generate the request(s) and plug in the approriate opcode
            ushort firstRecordToRead = lastReadRecord;

            if (!inclusive)
            {
                firstRecordToRead += 1;
                if (firstRecordToRead == 240)
                    firstRecordToRead = 0;
            }

            ushort recordIdx = firstRecordToRead;
            byte numRecordsToRead;

            if (currentPointer > lastReadRecord)
                numRecordsToRead = (byte)(currentPointer - firstRecordToRead);
            else
                numRecordsToRead = (byte)(currentPointer + (240 - firstRecordToRead));

            if (inclusive)
            {
                numRecordsToRead += 1;
            }
         
            byte recordCount = 0;
            byte thisRequestRecordCount;
            DataRequest thisRequest;
            List<byte> requestBytes;
            byte requestCount = 1;
            while (recordCount < numRecordsToRead)
            {
                if (numRecordsToRead - recordCount >= 10)

                    thisRequestRecordCount = 10;
                else
                    thisRequestRecordCount = (byte)(numRecordsToRead - recordCount);

                // generate a request starting at recordIdx, for thisRequestRecordCount records
                thisRequest = new DataRequest()
                {
                    GroupIdxNumber = requestIdx.ToString() + "." + requestCount.ToString(),
                    Protocol = "ROC",
                    ProtocolSpecificParams = inclusive  // ProtocolSpecificParams will carry a boolean value that indicates whether the request was manual with override
                };

                if (opCode == 121)
                    thisRequest.MessageType = DataRequest.RequestType.Alarms;

                if (opCode == 122)
                    thisRequest.MessageType = DataRequest.RequestType.Events;

                thisRequest.TagList.Add(new Tag(Guid.NewGuid()) { Value = lastReadRecord });
                if (inclusive)
                    thisRequest.TagList.Add(new Tag(Guid.NewGuid()) { Value = -1 });
                else
                    thisRequest.TagList.Add(new Tag(Guid.NewGuid()) { Value = currentPointer });
                requestBytes = new List<byte>();
                requestBytes.AddRange(new byte[] { devNum, groupNum, 1, 1 }); 
                requestBytes.Add(opCode);
                requestBytes.Add(3); // 3 bytes of data
                requestBytes.Add(thisRequestRecordCount); // number of records requested
                requestBytes.AddRange(DataType.INT16.ToBytes(recordIdx)); // first record number

                ushort CRC = CRCHelpers.CalcCRC16(requestBytes.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);
                requestBytes.Add(IntHelpers.GetLowByte(CRC));
                requestBytes.Add(IntHelpers.GetHighByte(CRC));

                thisRequest.RequestBytes = requestBytes.ToArray();
                thisRequest.ExpectedResponseSize = 11 + 22 * thisRequestRecordCount + 2;  // header + (22 * the number of records) + 2 bytes for the CRC

                // add it to the request group
                requestList.Add(thisRequest);

                recordIdx += thisRequestRecordCount;
                if (recordIdx > 239)
                    recordIdx = (ushort)(recordIdx - 240);
                recordCount += thisRequestRecordCount;
                requestCount++;
            }

            return requestList;
        }

        public static RequestGroup GenerateAlarmEventsRequests(DataRequest OpCode120Request)
        {
            // get the original group template
            RequestGroup AlarmEventsRequestGroup = (RequestGroup)OpCode120Request.ParentTemplateGroup.Clone();

            ushort currentPointer = 0;
            ushort lastReadRecord = 0;

            AlarmEventsRequestGroup.RequesterType = Globals.RequesterType.System;
            AlarmEventsRequestGroup.RequesterID = Guid.Empty;

            string requestString = AlarmEventsRequestGroup.DBGroupRequestConfig.DataPointBlockRequestListVals;

            string[] header = requestString.Split(':');
            string nodeID = OpCode120Request.RequestBytes[1].ToString() + ":" + OpCode120Request.ResponseBytes[0].ToString();
            DataRequest.RequestType requestType = DataRequest.RequestType.Read;

            byte opCode = 0;
            string opcodeString = header[2].Replace("+", "");
            if (opcodeString == "121") // it's an alarms request
            {
                opCode = 121;
                currentPointer = (ushort)((ushort)OpCode120Request.TagList[0].Value); // current alarm pointer, according to the Opcode 120 request

                byte[] ptrs = ((DBManager)Globals.DBManager).GetAlmEvtPtrs(OpCode120Request.ConnectionID, OpCode120Request.NodeID, "Alarms"); // last read record, according to the database table
                lastReadRecord = ptrs[0];
                requestType = DataRequest.RequestType.Alarms;
            }

            if (opcodeString == "122") // it's an events request
            {
                opCode = 122;
                currentPointer = (ushort)((ushort)OpCode120Request.TagList[1].Value); // current event pointer, according to the Opcode 120 request

                byte[] ptrs = ((DBManager)Globals.DBManager).GetAlmEvtPtrs(OpCode120Request.ConnectionID, OpCode120Request.NodeID, "Events"); // last read record, according to the database table
                lastReadRecord = ptrs[0];
                requestType = DataRequest.RequestType.Events;
            }

            if (MiscHelpers.AddCircular(currentPointer,-1,1,240) == lastReadRecord)
            {
                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "", requestType.ToString() + " request for device " + nodeID + " on connection " + OpCode120Request.ConnectionID + " cancelled, because there are no new records");
                return AlarmEventsRequestGroup;
            }


            AlarmEventsRequestGroup.ProtocolRequestList = GenerateAlarmEventsRequests(AlarmEventsRequestGroup.ID, "", OpCode120Request.RequestBytes[1], OpCode120Request.RequestBytes[0], 1, 1,opCode, lastReadRecord, currentPointer,false, OpCode120Request.GroupIdxNumber);
            AlarmEventsRequestGroup.ConnectionID = OpCode120Request.ConnectionID;
            foreach (DataRequest request in AlarmEventsRequestGroup.ProtocolRequestList)
            {
                request.ConnectionID = AlarmEventsRequestGroup.ConnectionID;
            }
            return AlarmEventsRequestGroup;
        }
 

        private static List<DataRequest> GeneratePreWriteRead(Guid RequestGroupID, string reqID, byte group, byte unit, byte hostGroup, byte hostUnit, TLP[] TLPs, List<FDADataPointDefinitionStructure> tagsconfigList, List<Tag> tagList, int groupIdx, int maxDataSize)
        {
            List<DataRequest> subRequestList = new();

            // find unique BIN TLPs, create a tag/config/TLP entry for each one
            List<string> uniquifier = new();

            // strip out any non BIN tags for the pre-write read
            List<TLP> BinaryTLPs = new();
            List<Tag> BinaryTags = new();
            List<FDADataPointDefinitionStructure> BinaryTagConfigs = new();


            // go through the original TLP list, pull out unique binary TLPs, convert them to UINT8's and generate new lists for the pre-write read
            for (int i = 0; i < TLPs.Length; i++)
            {
                if (TLPs[i].Datatype == DataType.BIN)
                {
                    if (!uniquifier.Contains(TLPs[i].TLPString))
                    {
                        uniquifier.Add(TLPs[i].TLPString);
                        BinaryTLPs.Add(TLPs[i].CloneWithDataTypeChange(DataType.UINT8));
                        BinaryTags.Add(tagList[i]);
                        BinaryTagConfigs.Add(tagsconfigList[i]);
                    }
                }
            }
            //------------------------------------------------------------------------------------------------------------------------------------------

            // call the regular GenerateReadRequest function to create a read request for just these binary tags (now UINT8 tags)
            subRequestList = GenerateReadRequest(RequestGroupID, "", group, unit, hostGroup, hostUnit, BinaryTLPs.ToArray(), BinaryTagConfigs, BinaryTags, groupIdx, maxDataSize, DataRequest.RequestType.PreWriteRead);
            foreach (DataRequest req in subRequestList)
            {
                req.GroupIdxNumber += " (pre-write read)";

            }

            return subRequestList;
        }

  
        public static RequestGroup GenerateWriteRequestAfterPreWriteRead(DataRequest completedPreWriteRead)
        {
            RequestGroup writeGroup = (RequestGroup)completedPreWriteRead.ParentTemplateGroup.Clone();
            writeGroup.RequesterType = Globals.RequesterType.System;
            writeGroup.RequesterID = Guid.Empty;
            writeGroup.writeLookup = completedPreWriteRead.ValuesToWrite;

            CreateRequests(writeGroup, completedPreWriteRead.TagList);

            return writeGroup;
        }

        private static List<DataRequest> GenerateOpcode128Request(Guid requestGroup, Guid connectionID, byte groupNum, byte devNum, byte hostGroup, byte hostDev, string opCode, string login, short pwd, string[] historyPointRequests)
        {
            List<DataRequest> subRequestList = new();
            string[] historyRequestData;
            byte historyPointNumber;
            byte requestedDay;
            byte requestedMonth;
            byte hoursRequested = 24; // number of hours to use from the returned data (counting from most recent)
            byte startHour = 255;      // hour of the day to begin with, default of 255 means start at the beginning and get all 24 hours
            byte hoursStep = 1;
            Guid tagID;

            byte opCodeByte = byte.Parse(opCode);

            for (int i = 0; i < historyPointRequests.Length; i++)
            {
                historyRequestData = historyPointRequests[i].Split(':');

                if (historyRequestData.Length < 5)
                {
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "", "Error while parsing group " + requestGroup + " item + " + (i + 1).ToString() + " (Missing parameter), skipping this item");
                    continue;
                }

                historyPointNumber = byte.Parse(historyRequestData[0]);
                historyPointNumber -= 1; // zero based history point number
                requestedDay = byte.Parse(historyRequestData[3]);
                requestedMonth = byte.Parse(historyRequestData[4]);
                DataType ROCDataType = DataType.GetType(historyRequestData[1]);
                tagID = Guid.Parse(historyRequestData[2]);

                if (historyRequestData.Length > 5)
                {
                    if (historyRequestData[5] != "")
                        startHour = byte.Parse(historyRequestData[5]);
                }

                if (historyRequestData.Length > 6)
                {
                    if (historyRequestData[6] != "")
                    {
                        hoursRequested = byte.Parse(historyRequestData[6]);
                        if (hoursRequested > 60)
                            hoursRequested = 60;
                        if (hoursRequested < 1)
                            hoursRequested = 1;
                    }
                }

                if (historyRequestData.Length > 7)
                {
                    if (historyRequestData[7] != "")
                        hoursStep = byte.Parse(historyRequestData[7]);
                }


                List<byte> requestByteList = new()
                {
                    devNum,
                    groupNum,
                    hostDev,
                    hostGroup,
                    opCodeByte,
                    3, // data size
                    historyPointNumber,
                    requestedDay,
                    requestedMonth,
                };


                ushort CRC = CRCHelpers.CalcCRC16(requestByteList.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);

                requestByteList.Add(IntHelpers.GetLowByte(CRC));
                requestByteList.Add(IntHelpers.GetHighByte(CRC));

                DataRequest thisRequest = new()
                {
                    DeviceLogin = login,
                    DevicePass = pwd,
                    ConnectionID = connectionID,
                    GroupID = requestGroup,
                    GroupIdxNumber = (i + 1).ToString(),
                    Protocol = "ROC",
                    TagList = new List<Tag>(),
                    ProtocolSpecificParams = startHour << 16 | hoursRequested << 8 | hoursStep
                };

                // add the required # of tags to the tag list 
                int tagCount = 24;

                for (int idx = 0; idx < tagCount; idx++)
                {
                    Tag tag = new(tagID) { ProtocolDataType = ROCDataType };
                    thisRequest.TagList.Add(tag);
                }
                thisRequest.RequestBytes = requestByteList.ToArray();
                thisRequest.ExpectedResponseSize = 142;
                thisRequest.MessageType = DataRequest.RequestType.Backfill;
                subRequestList.Add(thisRequest);
            }

            foreach (DataRequest request in subRequestList)
            {
                request.GroupSize = subRequestList.Count;
            }


            return subRequestList;
        }

        private static List<DataRequest> GenerateOpcode130Request(Guid requestGroup, Guid connectionID, byte groupNum, byte devNum, byte hostGroup, byte hostDev, string login, short pwd, string[] historyPointRequest,double recordStep = 60)
        {
            List<DataRequest> subRequestList = new();
            byte historyPointNumber = 0;
            byte historyType = 0;
            ushort startIdx = 1;
            byte recordCount = 1;

            recordStep /= 60; // step is in minutes and we're working with hours here
        
            Guid tagID;

            byte opCodeByte = 130;

            //historyRequestData = historyPointRequest[i].Split(':');

            /* the number of parameters has already been verified, we don't have to do it again here
            if (historyRequestData.Length < 5)
            {
                Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "", "Error while parsing group " + requestGroup + " item + " + (i + 1).ToString() + " (Missing parameter), skipping this item");
                continue;
            }
            */

            historyPointNumber = byte.Parse(historyPointRequest[0]);
            historyPointNumber -= 1; // zero based HPN
            DataType ROCDataType = DataType.GetType(historyPointRequest[1]);
            tagID = Guid.Parse(historyPointRequest[2]);
            historyType = byte.Parse(historyPointRequest[3]);
            startIdx = ushort.Parse(historyPointRequest[4]);
            recordCount = byte.Parse(historyPointRequest[5]);
    
            List<byte> requestByteList = new()
            {
                devNum,
                groupNum,
                hostDev,
                hostGroup,
                opCodeByte,
                5, // data size
                historyType,
                historyPointNumber,
                recordCount,
                IntHelpers.GetLowByte(startIdx),   
                IntHelpers.GetHighByte(startIdx)
                    
            };


            ushort CRC = CRCHelpers.CalcCRC16(requestByteList.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);

            requestByteList.Add(IntHelpers.GetLowByte(CRC));
            requestByteList.Add(IntHelpers.GetHighByte(CRC));
            Opcode130params OC130Params = new() { StartIdx = startIdx, RecordCount = recordCount, Interval = recordStep };

            DataRequest thisRequest = new()
            {
                DeviceLogin = login,
                DevicePass = pwd,
                ConnectionID = connectionID,
                GroupID = requestGroup,
                GroupIdxNumber = "1",
                Protocol = "ROC",
                TagList = new List<Tag>(),
                ProtocolSpecificParams = OC130Params
            };

            // add the required # of tags to the tag list 
            for (int idx = 0; idx < recordCount; idx += 1)
            {
                Tag tag = new(tagID) { ProtocolDataType = ROCDataType };
                thisRequest.TagList.Add(tag);
            }
            thisRequest.RequestBytes = requestByteList.ToArray();
            thisRequest.ExpectedResponseSize = 9 + (recordCount*4) + 2;
            thisRequest.MessageType = DataRequest.RequestType.Backfill;
            subRequestList.Add(thisRequest);
            

            foreach (DataRequest request in subRequestList)
            {
                request.GroupSize = subRequestList.Count;
            }


            return subRequestList;
        }

        private struct Opcode130params
        {
            public int StartIdx;
            public int RecordCount;
            public double Interval;
        }


        private static List<DataRequest> GenerateOpcode126Request(Guid requestGroup, Guid connectionID, byte groupNum, byte devNum, byte hostGroup, byte hostDev,string login, short pwd, string[] historyPointRequests)
        {
            // the history for each point results in a max length response, so each history point requested gets it's own request
            List<DataRequest> subRequestList = new();
            string[] historyRequestData;
            byte historyPointNumber;
            byte minutesRequested = 60;
            byte startIdx = 1;
            byte minutesStep = 1;
            Guid tagID;


            for (int i = 0; i < historyPointRequests.Length; i++)
            {
                historyRequestData = historyPointRequests[i].Split(':');

                if (historyRequestData.Length < 3)
                {
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "", "Error while parsing group " + requestGroup + " item + " + (i + 1).ToString() + " (Missing parameter), skipping this item");
                    continue;
                }

                historyPointNumber = byte.Parse(historyRequestData[0]);
                historyPointNumber -= 1; // zero based HPN

                if (historyRequestData.Length > 3)
                {
                    if (historyRequestData[3] != "")
                        startIdx = byte.Parse(historyRequestData[3]);
                }

                if (historyRequestData.Length > 4)
                {
                    if (historyRequestData[4] != "")
                    {
                        minutesRequested = byte.Parse(historyRequestData[4]);
                        if (minutesRequested > 60)
                            minutesRequested = 60;
                        if (minutesRequested < 1)
                            minutesRequested = 1;
                    }
                }

                if (historyRequestData.Length > 5)
                {
                    if (historyRequestData[5] != "")
                        minutesStep = byte.Parse(historyRequestData[5]);
                }

                List<byte> requestByteList = new()
                {
                    devNum,
                    groupNum,
                    hostDev,
                    hostGroup,
                    126,
                    1, // data size
                    historyPointNumber
                };


                ushort CRC = CRCHelpers.CalcCRC16(requestByteList.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);

                requestByteList.Add(IntHelpers.GetLowByte(CRC));
                requestByteList.Add(IntHelpers.GetHighByte(CRC));


                DataType ROCDataType = DataType.GetType(historyRequestData[1]);
                tagID = Guid.Parse(historyRequestData[2]);



                DataRequest thisRequest = new()
                {
                    DeviceLogin = login,
                    DevicePass = pwd,
                    ConnectionID = connectionID,
                    GroupID = requestGroup,
                    GroupIdxNumber = (i + 1).ToString(),
                    Protocol = "ROC",
                    TagList = new List<Tag>(),
                    ProtocolSpecificParams = startIdx << 16 | minutesRequested << 8 | minutesStep,
                };

                // create 60 tags to handle the full response from the device
                for (int idx = 0; idx < 60; idx++)
                {
                    Tag tag = new(tagID) { ProtocolDataType = ROCDataType };
                    thisRequest.TagList.Add(tag);
                }
                thisRequest.RequestBytes = requestByteList.ToArray();
                thisRequest.ExpectedResponseSize = 250;
                thisRequest.MessageType = DataRequest.RequestType.Backfill;
                subRequestList.Add(thisRequest);
            }

            foreach (DataRequest request in subRequestList)
            {
                request.GroupSize = subRequestList.Count;
            }


            return subRequestList;
        }

        private static List<DataRequest> GenerateWriteRequest(string reqID, byte group, byte unit, byte hostGroup, byte hostUnit, object protocolParams, List<FDADataPointDefinitionStructure> tagsconfigList,Dictionary<Guid,double> writeValues,List<Tag> preReadValues, int groupIdx, int maxDataSize)
        {
            List<DataRequest> requestList = new();

            try
            {
                // generate the data block
                // build the data block of the message

                TLP[] TLPs = (TLP[])protocolParams;

                byte dataSize = 1;

                List<byte> dataBlock = new();

                Guid pointID;
                byte parameterCount = 0;
                Dictionary<string, BitArray> binaryValues = null;

                // create a lookup table for the preReadValues (if exists)
                if (preReadValues != null)
                {
                    binaryValues = new Dictionary<string, BitArray>();
                    foreach (Tag tag in preReadValues)
                    {
                        binaryValues.Add(tag.DeviceTagAddress, new BitArray(new byte[] { (byte)tag.Value }));
                    }
                }

                TLP currentTLP = null;
                for (int i = 0; i < TLPs.Length; i++)
                {
                    if (TLPs[i].Datatype == DataType.BIN)
                    {
                        currentTLP = TLPs[i];
                        bool bitValueToWrite;
                        int bitcounter = 0;
                        while (i < TLPs.Length)
                        {
                            pointID = tagsconfigList[i].DPDUID;
                            if (pointID != Guid.Empty)
                            {
                                bitValueToWrite = writeValues[pointID] == 1;

                                binaryValues[currentTLP.TLPString].Set(bitcounter, bitValueToWrite);
                            }

                            if (TLPs.Length > i + 1)     // if we're not at the end of the TLP list
                            {
                                if (currentTLP.TLPString != TLPs[i + 1].TLPString)  // if the type of the next tag has a different address than the current one, we're done with this tag
                                {
                                    //i++;
                                    break;
                                }
                            }
                            i++;
                            bitcounter++;
                        }

                        // convert the updated bits to a byte and add the write to the request bytes
                        byte[] updatedByte = new byte[1];
                        binaryValues[currentTLP.TLPString].CopyTo(updatedByte, 0);
                        dataBlock.AddRange(currentTLP.Bytes);
                        dataBlock.Add(updatedByte[0]);
                        dataSize += 4;
                        parameterCount += 1;
                    }
                    else
                    {
                        pointID = tagsconfigList[i].DPDUID;

                        // get the value to be written (if found), convert to bytes and add it to the data block
                        object value;
                        if (writeValues.ContainsKey(pointID))
                        {
                            // add the three bytes identifying the point
                            dataBlock.AddRange(TLPs[i].Bytes);
                            value = writeValues[pointID];
                            byte[] valueBytes = TLPs[i].Datatype.ToBytes(value);
                            dataBlock.AddRange(valueBytes);
                            dataSize += 3;
                            dataSize += (byte)valueBytes.Length;
                            parameterCount += 1;
                        }
                    }
                }

                // put the whole message together
                
                List<byte> message = new()
                {
                    unit,                        // Device Unit    (1 byte)
                    group,                       // Device Group   (1 byte)
                    hostUnit,                    // Host unit  (1 byte)
                    hostGroup,                   // Host group (1 byte)
                    0xB5,                        // Operation Code - 181  (1 byte)
                    dataSize,                     
                    parameterCount                     
                };

                message.AddRange(dataBlock);

                // calculate the checksum
                ushort CRC16 = CRCHelpers.CalcCRC16((byte[])message.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);

                // add the checksum bytes to the end of the request
                message.Add(IntHelpers.GetLowByte(CRC16));         // CRC Low Byte
                message.Add(IntHelpers.GetHighByte(CRC16));        // CRC High Byte

                // convert from an ArrayList to a simple array of bytes 
                byte[] requestBytes = (Byte[])message.ToArray();

                // create a request object
                DataRequest request = new()
                {
                    RequestBytes = requestBytes,
                    ExpectedResponseSize = 8,
                    ProtocolSpecificParams = TLPs.ToArray(),
                    Protocol = "ROC",
                    RequestID = reqID,
                    DataPointConfig = tagsconfigList,
                    GroupIdxNumber = groupIdx.ToString(),
                    MessageType = DataRequest.RequestType.Write
                };
               
                requestList.Add(request);

                return requestList;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Roc Protocol GenerateWriteRequest() general error");
                return requestList;
            }
        }


        // Standard read request (OpCode 180)
        private static List<DataRequest> GenerateReadRequest(Guid RequestGroup, string reqID, byte group, byte unit, byte hostGroup, byte hostUnit, TLP[] TLPs, List<FDADataPointDefinitionStructure> tagsconfigList, List<Tag> tagList, int groupIdx, int maxDataSize, DataRequest.RequestType requestType)
        {
            List<DataRequest> subRequestList = new();


            try
            {
                int tagIdx = 0;
                List<TLP> subRequestTLPList;
                List<Tag> subRequestTags;
                List<FDADataPointDefinitionStructure> subRequestTagConfigs;


                while (tagIdx < tagList.Count)
                {
                    if (TLPs[tagIdx].Datatype == null)
                    {
                        Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "", "Invalid data type in Request group " + RequestGroup.ToString());
                        tagIdx++;
                        continue;
                    }
                    byte subRequestParamCount = 0;
                    byte responseDataSize = 1; // first byte of the response data block is the number of parameters, so include that byte in the count
                    subRequestTLPList = new List<TLP>();
                    subRequestTags = new List<Tag>();
                    subRequestTagConfigs = new List<FDADataPointDefinitionStructure>();



                    // build message header (minus the data block size, we don't know that yet)
                    List<byte> message = new()
                    {
                        unit,                         // Device Unit    (1 byte)
                        group,                        // Device Group   (1 byte)
                        hostUnit,                   // Host unit  (1 byte)
                        hostGroup,                   // Host group (1 byte)
                        (byte)0xB4,                   // Operation Code (1 byte)
                    };

                    List<byte> datablock = new();


                    // ---------------------------------- add each parameter (until we reach the max response size)
                    while (responseDataSize + TLPs[tagIdx].Datatype.Size < maxDataSize)
                    {
                        datablock.AddRange(TLPs[tagIdx].Bytes);     // add the 3 identifying bytes (TLP) per parameter
                        responseDataSize += (byte)(3 + TLPs[tagIdx].Datatype.Size);  // update the responseDataSize
                        subRequestParamCount++;
                        subRequestTLPList.Add(TLPs[tagIdx]);
                        subRequestTags.Add(tagList[tagIdx]);
                        subRequestTagConfigs.Add(tagsconfigList[tagIdx]);


                        // for the binary data type, there will always be 8 tags defined (some many be placeholders, but there will laways be 8 elements)
                        // we only need to read the byte once because the bit values for the next 7 tags are contained in the byte 
                        // so add 7 more tags to the list without adding the TLPs to the data request  
                        if (TLPs[tagIdx].Datatype == DataType.BIN)
                        {
                            for (int i=0; i<7; i++)
                            {
                                tagIdx++;                               
                                subRequestTags.Add(tagList[tagIdx]);
                            }
                            
                            
                        }

                        tagIdx++;
                        if (tagIdx >= TLPs.Length)
                            break;

                    }

                    message.Add((byte)(1 + 3 * subRequestParamCount));   // length of data block (1 byte for data size, 1 byte for # params + 3 bytes per param)
                    message.Add(subRequestParamCount);                   // number of parameters requesting (1 byte)
                    message.AddRange(datablock);                         // add the data block

                    // calculate the checksum
                    ushort CRC16 = CRCHelpers.CalcCRC16(message.ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);

                    // add the checksum bytes to the end of the request
                    message.Add(IntHelpers.GetLowByte(CRC16));         // CRC Low Byte
                    message.Add(IntHelpers.GetHighByte(CRC16));        // CRC High Byte

                    // convert from an List to a simple array of bytes 
                    byte[] requestBytes = message.ToArray();

                    // find the total Expected Response Size (header + data + CRC)
                    int expectedResponseSize = 8 + responseDataSize;

                    // build a datarequest object
                    DataRequest request = new()
                    {
                        RequestBytes = requestBytes,
                        ExpectedResponseSize = expectedResponseSize,
                        ProtocolSpecificParams = subRequestTLPList.ToArray(),
                        Protocol = "ROC",
                        RequestID = reqID,
                        TagList = subRequestTags,
                        DataPointConfig = subRequestTagConfigs,
                        MessageType = requestType
                    };

                    if (requestType == DataRequest.RequestType.ReadbackAfterWrite)
                        request.DBWriteMode = DataRequest.WriteMode.Update;

                    // add it to the subRequest List
                    subRequestList.Add(request);
                }

                if (subRequestList.Count > 0)
                {
                    if (subRequestList.Count > 1)
                    {
                        for (int i = 1; i <= subRequestList.Count; i++)
                        {
                            subRequestList[i - 1].GroupIdxNumber = groupIdx.ToString() + "." + i.ToString();
                            if (requestType == DataRequest.RequestType.ReadbackAfterWrite)
                                subRequestList[i - 1].GroupIdxNumber += " (readback)";
                        }
                    }
                    else
                    {
                        subRequestList[0].GroupIdxNumber = groupIdx.ToString();
                        if (subRequestList[0].MessageType == DataRequest.RequestType.ReadbackAfterWrite)
                            subRequestList[0].GroupIdxNumber += " (readback)";
                    }
                }

                return subRequestList;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Roc Protocol CreateRequests() general error");
                return subRequestList;
            }
        }


        private static DataRequest GenerateLoginRequest(byte group, byte unit, string user, short pass)
        {
            try
            {
                // user must be three chars. if the user is too short, pad with spaces
                if (user == null)
                    return null;

                if (user == string.Empty)
                    return null;

                if (user.Length < 3)
                {
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "The ROC Login name '" + user + "' is too short, expected three characters. Padding with spaces to create a 3 character login", true);
                    user = user.PadRight(3);
                }

                // if the user name is too long, truncate it
                if (user.Length > 3)
                {
                    Globals.SystemManager.LogApplicationEvent(Globals.FDANow(),"ROCProtocol", "The ROC Login name '" + user + "' is too long, expected three characters. Truncating to '" + user.Substring(0, 3) + "'",true);
                    user = user.Substring(0, 3);               
                }

                byte[] ID = System.Text.Encoding.ASCII.GetBytes(user);


                ArrayList message;

                // version 1, username with no password
                if (pass == -1)
                {
                    message = new ArrayList
                {
                    unit,
                    group,
                    (byte)0x01,
                    (byte)0x01,
                    (byte)0x11,
                    (byte)0x03,
                    ID[0],
                    ID[1],
                    ID[2]
                };
                }
                else
                // version 2, password supplied
                {
                    message = new ArrayList
                    {
                        unit,
                        group,
                        (byte)0x01,
                        (byte)0x01,
                        (byte)0x11,
                        (byte)0x05,
                        ID[0],
                        ID[1],
                        ID[2],
                        IntHelpers.GetHighByte(pass),
                        IntHelpers.GetLowByte(pass)
                    };
                }

                // calculate and add the checksum
                ushort CRC16 = CRCHelpers.CalcCRC16((byte[])message.ToArray(typeof(byte)), CRCInitial, CRCPoly, CRCSwapOutputBytes);

                message.Add(IntHelpers.GetLowByte(CRC16));
                message.Add(IntHelpers.GetHighByte(CRC16));

                // build a request object
                DataRequest request = new()
                {
                    RequestBytes = (Byte[])message.ToArray(typeof(Byte)),
                    ExpectedResponseSize = 8,
                    Protocol = "ROC"
                };
                return request;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Roc Protocol GenerateLoginRequest() general error");
                return null;
            }

        }

        // for use by opcode 126 (history collection - minute data)
        private static DateTime GetHistoryBaseTime(DataRequest req)
        {
            DateTime respTime = req.ResponseTimestamp;
            DateTime baseTime = new(respTime.Year, respTime.Month, respTime.Day, respTime.Hour - 1, respTime.Minute, 0);
            baseTime = baseTime.AddMinutes(1);

            return baseTime;
        }

        public static void InterpretResponse(ref DataRequest request)
        {
            try
            {
                byte[] response = request.ResponseBytes;
                byte opCode = request.ResponseBytes[4];
                byte[] recordBytes;
                string NodeID = request.ResponseBytes[3].ToString() + ":" + request.ResponseBytes[2].ToString();
                int numRecords;
                int firstRecord;
                int recordPos;
                DateTime historyTime; // for opCodes 126 and 128
                List<AlarmEventRecord> genericAlmEvtList;
                bool manualMode;

                switch (opCode)
                {
                    case 120:
                        if (request.MessageType == DataRequest.RequestType.AlarmEventPointers)
                        {
                            // alarm and events pointers positions
                            //request.TagList = new List<Tag>();
                            ushort AlarmLogPointer = (ushort)DataType.UINT16.FromBytes(request.ResponseBytes.Skip(6).Take(2).ToArray());
                            ushort EventLogPointer = (ushort)DataType.UINT16.FromBytes(request.ResponseBytes.Skip(8).Take(2).ToArray());
                            ushort HourLogPointer = (ushort)DataType.UINT16.FromBytes(request.ResponseBytes.Skip(10).Take(2).ToArray());

                            request.TagList.Add(new Tag(Guid.NewGuid()) { Value = AlarmLogPointer, Timestamp = request.ResponseTimestamp, ProtocolDataType = DataType.UINT16 });
                            request.TagList.Add(new Tag(Guid.NewGuid()) { Value = EventLogPointer, Timestamp = request.ResponseTimestamp, ProtocolDataType = DataType.UINT16 });
                            request.TagList.Add(new Tag(Guid.NewGuid()) { Value = HourLogPointer, Timestamp = request.ResponseTimestamp, ProtocolDataType = DataType.UINT16 });
                        }

                        if (request.MessageType == DataRequest.RequestType.HourHistoryPtr)
                        {
                            byte[] ptrBytes = new byte[2];
                            Array.Copy(request.ResponseBytes, 10, ptrBytes, 0, 2);
                            ushort hourPtr = (ushort)DataType.UINT16.FromBytes(ptrBytes);
                            List<DataRequest> backfillReqs = BackfillHourData(request.ParentTemplateGroup, hourPtr);
                            request.ParentTemplateGroup.ProtocolRequestList = backfillReqs;
                            request.RequesterType = Globals.RequesterType.System;
                            request.SuperPriorityRequestGroup = request.ParentTemplateGroup;
                        }
                        break;
                    case 121: // alarm records
                        manualMode = (bool)request.ProtocolSpecificParams;

                        // break out the 22 bytes for each alarm record and generate a AlarmRecord objects                       
                        numRecords = request.ResponseBytes[6];
                        firstRecord = request.ResponseBytes[7];
                        List<RocAlarmRecord> almRecordList = new();
 
                        for(int recordIdx = 0; recordIdx < numRecords; recordIdx++)
                        {
                            recordBytes = request.ResponseBytes.Skip(11 + recordIdx * 22).Take(22).ToArray();
                            recordPos = firstRecord + recordIdx;
                            if (recordPos > 239) recordPos = recordPos - 240;
                            RocAlarmRecord record = new(request.ResponseTimestamp, request.ConnectionID, NodeID,recordPos, Convert.ToInt16(request.TagList[1].Value), recordBytes,request.Destination, manualMode);
                            //if (record.Valid)
                            almRecordList.Add(record);
                        }

                        // convert to the base class, so that datacq and dbmananger can handle it
                        genericAlmEvtList = almRecordList.ConvertAll(record => (AlarmEventRecord)record);

                        request.TagList.Add(new Tag(Guid.NewGuid()) { Value = genericAlmEvtList });
                        break;
                    case 122: // event records
                        manualMode = (bool)request.ProtocolSpecificParams;

                        // break out the 22 bytes for each event record and generate a AlarmRecord objects 
                        numRecords = request.ResponseBytes[6];
                        firstRecord = request.ResponseBytes[7];
                        List<RocEventRecord> evtRecordList = new();
                        for (int recordIdx = 0; recordIdx < numRecords; recordIdx++)
                        {
                            recordBytes = request.ResponseBytes.Skip(11 + recordIdx * 22).Take(22).ToArray();
                            recordPos = firstRecord + recordIdx;
                            if (recordPos > 239) recordPos = recordPos - 240;
                            RocEventRecord record = new(request.ResponseTimestamp, request.ConnectionID, NodeID, recordPos, Convert.ToInt16(request.TagList[1].Value), recordBytes,request.Destination,manualMode);
                            //if (record.Valid)
                            evtRecordList.Add(record);
                        }

                        // convert to the base class, so that datacq and dbmananger can handle it
                        genericAlmEvtList = evtRecordList.ConvertAll(record => (AlarmEventRecord)record);
                        request.TagList.Add(new Tag(Guid.NewGuid()) { Value = genericAlmEvtList });
                        break;

                    case 126:
                        // optional parameters are stored in the protocolspecificparams
                        int protocolParams;
                        protocolParams = (int)request.ProtocolSpecificParams;
                        byte startIdx = (byte)((protocolParams & 0xFF0000) >> 16);
                        byte minutesRequested = (byte)((protocolParams & 0xFF00) >> 8);
                        byte minutesStep = (byte)(protocolParams & 0xFF);

                        // get the base time (1 hour back from when the response was received, with the seconds set to zero)
                        historyTime = GetHistoryBaseTime(request);

                        // counts the minutes offset from the base time
                        byte minutesCounter = 0;

                        // pointer to the current value in the array returned by the device
                        byte ptr = (byte)(request.ResponseBytes[7] + 1);


                        // byteIdx is the first byte of the next value (increments by 4)
                        int byteIdx;
                        byte[] valueBytes = new byte[4];
                        for (minutesCounter = 0; minutesCounter < 60; minutesCounter++)
                        {
                            byteIdx = 8 + 4 * ptr;

                            Array.Copy(request.ResponseBytes, byteIdx, valueBytes, 0, 4);

                            request.TagList[minutesCounter].Value = DataType.FL.FromBytes(valueBytes);
                            
                            request.TagList[minutesCounter].Quality = 196;
                            request.TagList[minutesCounter].Timestamp = historyTime;

                            if (request.TagList[minutesCounter].Timestamp < request.ParentTemplateGroup.BackfillStartTime || request.TagList[minutesCounter].Timestamp > request.ParentTemplateGroup.BackfillEndTime)
                            {
                                request.TagList[minutesCounter].TagID = Guid.Empty;
                            }

                            historyTime = historyTime.AddMinutes(1);

                            ptr++;
                            if (ptr == 60)
                                ptr = 0;
                        }

                        // filter the full response from the device for only those values needed
                        List<Tag> filtered = new();



                        // from the startIdx (offset from the *end* of the array, going backwards), add each tag to the filtered list (applying the skip parameter)
                        // wrap around the array if necessary

                        // until we've covered the 'minutesCount' number of minutes, or we've wrapped around the whole array
                        int idx = 59 - startIdx;
                        int tagCount = 0;
                        bool wrapped = false;
                        int minutesCovered = 0;
                        while (minutesCovered < minutesRequested)
                        {
                            filtered.Add(request.TagList[idx]);
                            idx -= minutesStep;
                            tagCount++;
                            minutesCovered += minutesStep;

                            //wrap around the array
                            if (idx < 0)
                            {
                                idx = request.TagList.Count + idx; 
                                wrapped = true;
                            }
                            if (idx <= startIdx && wrapped)
                            {
                                break;
                            }
                        }

                        request.TagList = filtered;

                        
                        // minute data is complete, do we need some hourly data too?
                        DateTime earliestMinuteData = filtered[filtered.Count - 1].Timestamp;
                        if (earliestMinuteData.Subtract(request.ParentTemplateGroup.BackfillStartTime).TotalMinutes >= request.ParentTemplateGroup.BackfillStep)
                        {
                            DateTime HourDataTo = earliestMinuteData.AddMinutes(-1*request.ParentTemplateGroup.BackfillStep);
                            DateTime HourDataFrom = request.ParentTemplateGroup.BackfillStartTime;

                            RequestGroup nextGroup = (RequestGroup)request.ParentTemplateGroup.Clone();
                            nextGroup.RequesterType = Globals.RequesterType.System;
                            nextGroup.ID = Guid.Empty;
                            nextGroup.BackfillStartTime = HourDataFrom;
                            nextGroup.BackfillEndTime = HourDataTo;
                            nextGroup.BackfillStep = request.ParentTemplateGroup.BackfillStep;

                            string[] original = nextGroup.DBGroupRequestConfig.DataPointBlockRequestListVals.Split('|');
                            string[] body = original[1].Split(':');
                            body[3] = DateTimeToBackfillFormat(HourDataFrom);
                            body[4] = DateTimeToBackfillFormat(HourDataTo);
                            nextGroup.DBGroupRequestConfig.DataPointBlockRequestListVals = original[0] + "|" + string.Join(":", body);

                            request.NextGroup = nextGroup;
                        }

                        break;
                    case 128:
                        // get the timestamp of the first hour (month, day, hour minute, starting at byte 7)
                        int month = request.ResponseBytes[7];
                        int day = request.ResponseBytes[8];
                        int hour = request.ResponseBytes[9];
                        int minute = request.ResponseBytes[10];
                        int year = Globals.FDANow().Year; // assume the current year                       
                        if (month > Globals.FDANow().Month) // unless the month is higher than the current month, then assume the previous year
                            year -= 1;
                        historyTime = new DateTime(year, month, day, hour, minute, 0);


                        protocolParams = (int)request.ProtocolSpecificParams;
                        byte startHour = (byte)((protocolParams & 0xFF0000) >> 16);
                        byte hoursRequested = (byte)((protocolParams & 0xFF00) >> 8);
                        byte hoursStep = (byte)(protocolParams & 0xFF);

                        int bytePointer;
                        for (int hourIdx = 0; hourIdx < 24; hourIdx++)
                        {
                            bytePointer = 13 + hourIdx * 4;
                            request.TagList[hourIdx].Value = DataType.FL.FromBytes(request.ResponseBytes.Skip(bytePointer).Take(4).ToArray());
                            request.TagList[hourIdx].Quality = 196; 
                            request.TagList[hourIdx].Timestamp = historyTime;
                            historyTime = historyTime.AddHours(1);
                        }

                        

                        // filter the full response from the device for only those values needed to fill the hole in the recorded data
                        List<Tag> filteredList = new();


                        // go through the tags, find the index that corresponds with the start hour
                        idx = 0;
                        startIdx = 0;
                        for (idx = 0; idx < request.TagList.Count; idx++)
                        {
                            if (request.TagList[idx].Timestamp.Hour == startHour)
                            {
                                startIdx = (byte)idx;
                                break;
                            }
                        }

                        //if hourStart is not specified, return the whole array
                        if (startHour == 255) return;

                        // from that point, add each tag to the filtered list (applying the skip parameter)
                        // until we've added the 'hoursCount' number of tags to the filtered list, or we've wrapped around the whole array
                        tagCount = 0;
                        while (tagCount < hoursRequested && idx < request.TagList.Count)
                        {
                            filteredList.Add(request.TagList[idx]);
                            idx += hoursStep;
                            tagCount++;
                        }

                        request.TagList = filteredList;

                        break;

                    case 130:
                        int historyType = request.ResponseBytes[6];
                        int HPN = request.ResponseBytes[7];
                        int recordsReturned = request.ResponseBytes[8];

                        Opcode130params paramsstruct = (Opcode130params)request.ProtocolSpecificParams;
                        int startIndex = paramsstruct.StartIdx; //(byte)((protocolParams & 0xFF0000) >> 16);
                        int recordCount = paramsstruct.RecordCount; // (byte)((protocolParams & 0xFF00) >> 8);
                        double recordStep = paramsstruct.Interval;// (byte)(protocolParams & 0xFF);

                        if (recordStep < 1)
                            recordStep = 1;

                        // first record is the hour change after the backfill start time
                        DateTime gapStart = request.ParentTemplateGroup.BackfillStartTime;
                        if (gapStart.Minute != 0)
                            gapStart = gapStart.AddHours(1);
                        historyTime = new DateTime(gapStart.Year, gapStart.Month, gapStart.Day, gapStart.Hour, 0, 0);
                        int bytePtr = 9;

                        // offset according to the startIdx
                        //historyTime = historyTime.AddHours(-1 * startIndex);
   

                        
                        int tagPtr = 0;
                        byte[] recordValue = new byte[4];
                        for (int recordPtr=0; recordPtr < recordsReturned; recordPtr += 1)
                        {
                            DataType dataType = (DataType)request.TagList[tagPtr].ProtocolDataType;
                            Array.Copy(request.ResponseBytes, bytePtr, recordValue, 0, 4);

                            // identify any datapoints that are outside the requested timeframe and mark them as invalid so they don't get logged
                            if (historyTime < request.ParentTemplateGroup.BackfillStartTime || historyTime > request.ParentTemplateGroup.BackfillEndTime)
                            {
                                request.TagList[tagPtr].TagID = Guid.Empty;
                            }
                            request.TagList[tagPtr].Value = dataType.FromBytes(recordValue);
                            request.TagList[tagPtr].Quality = 196;
                            request.TagList[tagPtr].Timestamp = historyTime;
                            historyTime = historyTime.AddHours(1);
                            tagPtr++;
                            bytePtr += 4;
                        }
                        // filter the list according to the interval
                        filtered = new List<Tag>();
                        int intIdx = request.TagList.Count-1;
                        double dblIdx = intIdx;
                        while (dblIdx >= 0)
                        {
                            filtered.Add(request.TagList[intIdx]);
                            dblIdx -= recordStep;
                            intIdx = (int)(dblIdx+0.5);
                        }
                        request.TagList = filtered;


                        //historyTime = historyTime.AddMinutes(-59);

                        // if we haven't reached the start of the backfill time (we're going backwards through time), create a new request group for the remining time
                        // repeat as needed
                        if (request.ParentTemplateGroup.BackfillEndTime.Subtract(historyTime).TotalHours >= 2) // >=2
                        {
                            RequestGroup nextGroup = (RequestGroup)request.ParentTemplateGroup.Clone();
                            nextGroup.RequesterType = Globals.RequesterType.System;
                            nextGroup.ID = Guid.Empty;
                            nextGroup.BackfillStartTime = historyTime;
                            nextGroup.BackfillEndTime = request.ParentTemplateGroup.BackfillEndTime;
                            nextGroup.BackfillStep = request.ParentTemplateGroup.BackfillStep;
                            nextGroup.RequeueCount = 5;

                            string[] original = nextGroup.DBGroupRequestConfig.DataPointBlockRequestListVals.Split('|');
                            string[] body = original[1].Split(':');
                            body[3] = DateTimeToBackfillFormat(historyTime);
                            nextGroup.DBGroupRequestConfig.DataPointBlockRequestListVals = original[0] + "|" + string.Join(":", body);

                            request.NextGroup = nextGroup;
                        }
                        break;
                        
                    case 255: // handle ROC error responses (opCode 255)

                        Byte errorCode = request.ResponseBytes[6];
                        Byte errorOpCode = request.ResponseBytes[7];
                        string ErrorMessage = ROCError.GetErrorMessage(errorOpCode, errorCode);
                        request.SetStatus(DataRequest.RequestStatus.Error);
                        request.ErrorMessage = ErrorMessage;
                        break;

                    case 181: // handle Opcode 181 responses (opcode 181 just gets an ack back)
                        request.SetStatus(DataRequest.RequestStatus.Success);
                        return;

                    case 180: // handle Opcode 180 responses
                              // responses to OPCode 180 : host unit, host group, roc unit, roc group, opcode (180), data length, data, CRC
                              // data is in format  : num params requested,[type,logical,param,value byte1,value byte 2, value byte3, value byte 4] (repeated)

                        TLP[] TLPs = (TLP[])request.ProtocolSpecificParams;

                        int datasize = request.ResponseBytes[5];
                        if (datasize == 0)
                        {
                            request.SetStatus(DataRequest.RequestStatus.Error);
                            request.ErrorMessage = "No Data";
                            return;
                        }

                        // extract the data bytes from the response
                        byte[] dataBytes = new byte[datasize];
                        Array.Copy(request.ResponseBytes, 6, dataBytes, 0, datasize);


                        //temporary structures to hold the values as they're converted from bytes
                        List<object> values = new();
                        object value = null;

                        // first data byte is the number of parameters in the data
                        int numParams = dataBytes[0];
                        int paramIdx = 0;
                        int byteIndex = 1;
                        int tagIdx = 0;
                        DataType ROCDataType;
                        Type hostDataType;

                        int quality = 192;
                        byte[] valuebytes;
                        byte dataSize;
                        string floatCheck;
                        while (paramIdx < numParams)
                        {
                            // get the data type for this parameter
                            ROCDataType = TLPs[paramIdx].Datatype;
                            hostDataType = ROCDataType.HostDataType;
                            dataSize = ROCDataType.Size;

                            // convert the bytes for this returned value into whatever it should be (float, int, ASCII, etc)
                            // value is a variant

                            // this replaced with more efficient code below
                            //value = ROCDataType.FromBytes(dataBytes.Skip(byteIndex + 3).Take(ROCDataType.Size).ToArray());

                            valuebytes = new byte[dataSize];
                            Array.Copy(dataBytes, byteIndex + 3, valuebytes,0,dataSize);
                            value = ROCDataType.FromBytes(valuebytes);

                            // if the bytes could be converted into the required type, do it, and add the resulting value to the values list
                            if (value != null)
                            {
                                Convert.ChangeType(value, hostDataType);

                                //if it's a float, check for NaN or +/- infinity
                                floatCheck = "good";
                                if (ROCDataType == DataType.FL)
                                {
                                    if (float.IsNegativeInfinity((float)value))
                                        floatCheck = "-infinity";

                                    if (float.IsPositiveInfinity((float)value))
                                        floatCheck = "+infinity";

                                    if (float.IsNaN((float)value))
                                        floatCheck = "NaN";
                                    
                                   
                                    if (floatCheck != "good")
                                    {
                                        // oops, it's NaN or +/- infinity. Look up the last value and use that instead and set the quality to indicate stale value
                                        value = request.DataPointConfig[paramIdx].LastRead.Value;
                                        if (request.DataPointConfig[paramIdx].LastRead.Quality == 192)
                                            quality = 121;
                                        else
                                            quality = 122;

                                        Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "","The tag " + request.DataPointConfig[paramIdx].DPDUID + " received an invalid value (" + floatCheck + ")");
                                    }
                                }

                                // scaling now handled by DataAcqManager in TransactionCompleteHandler()
 /*                               // if the tag has scaling configured, apply it
                                // check for RawHigh = Raw low (causes divide by zero)
                                // check for numeric value
                                FDADataPointDefinitionStructure config = request.DataPointConfig[paramIdx];

                                if (config.read_scaling && config.read_scale_raw_high != config.read_scale_raw_low)
                                {

                                    if (Double.TryParse(Convert.ToString(value), out double numericValue))
                                        value = (object)(((numericValue - config.read_scale_raw_low) * (config.read_scale_eu_high - config.read_scale_eu_low) / (config.read_scale_raw_high - config.read_scale_raw_low)) + config.read_scale_eu_low);
                                }
*/
                                if (ROCDataType == DataType.BIN)
                                {
                                    Tag thisTag;
                                    //Tag nextTag;
                                    byte[] bits = BitHelpers.ByteToBits((byte)value);
                                    int i;
                                    for (i = 0; i < 8; i++)
                                    {
                                       if (tagIdx + i < request.TagList.Count)
                                       {
                                            thisTag = (Tag)request.TagList[tagIdx + i];
                                            if (thisTag.TagID != Guid.Empty)
                                            {
                                                thisTag.ProtocolDataType = ROCDataType;
                                                thisTag.Value = bits[i];
                                                thisTag.Timestamp = request.ResponseTimestamp;
                                                thisTag.Quality = quality;
                                            }

                                            //byteIndex += 4;

                                            /*
                                            if (paramIndex + i + 1 < request.TagList.Count)
                                            {
                                                nextTag = request.TagList[paramIndex + i + 1];
                                                if (nextTag.DeviceAddress != thisTag.DeviceAddress)
                                                    break;
                                            }
                                            
                                            if (paramIndex + i >= request.TagList.Count)
                                                break;
                                            */
                                        }
                                    }
                                    tagIdx += 8;
                                    byteIndex += 4; // move ahead 4 bytes in the response (the 3 byte TLP + the 1 byte value), to get to the next TLP
                                }
                                else
                                {
                                    // add the value to the response values list
                                    Tag thisTag = (Tag)request.TagList[tagIdx];
                                    thisTag.ProtocolDataType = ROCDataType;
                                    thisTag.Value = value;
                                    thisTag.Timestamp = request.ResponseTimestamp;
                                    thisTag.Quality = quality; // for now (need to figure out how I'm supposed to calculate the Quality)

                                    // increment the byte index (3 bytes for the TLP + whatever the data size was for the last parameter)
                                    byteIndex += 3 + ROCDataType.Size;

                                    tagIdx++;
                                }
                            }



                            // increment the tag index
                            paramIdx++;
                        }

                        request.SetStatus(DataRequest.RequestStatus.Success);


                        // special case, this reponse is for a pre-write read
                        // put the original requestgroup at the top of the highest priority queue for processing immediatly after the current request group is complete
                        if (request.MessageType == DataRequest.RequestType.PreWriteRead)
                        {
                            RequestGroup writeGroup = GenerateWriteRequestAfterPreWriteRead(request);
                            writeGroup.Priority = 0;
                            request.SuperPriorityRequestGroup = writeGroup;
                        }
                        break;


                    default:
                        Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "InterpretResponse()", "Unsupport opcode " + opCode + " found in device response");
                        break;

                }
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Roc Protocol InterpretResponse() general error while processing response to group " + request.GroupID + ", index " + request.GroupIdxNumber);
                request.SetStatus(DataRequest.RequestStatus.Error);
            }

        }

        private static class ROCError
        {
            static readonly Dictionary<string, string> opCodeSpecificErrors;
            static readonly Dictionary<byte,string> generalErrors;

            public static string GetErrorMessage(byte opCode, byte errorCode)
            {
                // check opcode specific errors first
                string key = opCode + ":" + errorCode;
                if (opCodeSpecificErrors.ContainsKey(key))
                    return opCodeSpecificErrors[key];

                // then check general errors
                if (generalErrors.ContainsKey(errorCode))
                {
                    return generalErrors[errorCode];
                }

                return "unknown error";
            }

            static ROCError()
            {
                generalErrors = new Dictionary<byte, string>
                {
                    {1,"Invalid Opcode request"},
                    {2,"Invalid Parameter Number"},
                    {3,"Invalid Logical Number/ Point Number"},
                    {4, "Invalid Point Type"},
                    {5, "Received too many data bytes"},
                    {6, "Received too few data bytes"},
                    {7, "Did not receive 1 data byte"},
                    {8, "Did not receive 2 data byte"},
                    {9, "Did not receive 3 data byte"},
                    {10, "Did not receive 4 data byte"},
                    {11, "Did not receive 5 data byte"},
                    {12, "Did not receive 16 data byte"},
                    {13, "Outside valid address range"},
                    {14, "Invalid History Request"},
                    {15, "Invalid FST Request"},
                    {16, "Invalid Event Entry"},
                    {17, "Requested too many alarms"},
                    {18, "Requested too many events"},
                    {19, "Write to read only parameter" },
                    {20, "Security Error"},
                    {21, "Invalid Security Logon"},
                    {22, "Invalid store and forward path" },
                    {23, "Flash programming Error"},
                    {24, "History configuration in progress"},
                    {63, "Requested security level too high" }                    
                };

                opCodeSpecificErrors = new Dictionary<string, string>
                {
                    { "17:6", "Too little data" },
                    { "17:8", "Too much data" },
                    { "17:251", "Industry Canada audit log full" },
                    { "126:59", "Too many data base (expected 2) OR Invalid Point Number OR Invalid RAM area number" },
                    { "128:60", "Undefined history point number OR data block more or less than 3 bytes OR invalid RAM area number" },
                    { "128:61", "Invalid day or month specified OR Specified day and/or month requested does not match the day and/or month in the time stamp associated with the first history value for the day" },
                    { "128:63", "Invalid history point number"}
                };


                // opcode 180 errors
      //          for (int i = 0; i < 100; i++)
      //              opCodeSpecificErrors.Add("180:" + i, "ROC error. Possible causes: The device requires a login (FDA will attempt a login automatically), an unconfigured point was requested, or the response would be too long");
            }
        }

                
        public static void ValidateResponse(DataRequest request,bool FinalAttempt)
        {
            try
            {
                byte[] response = request.ResponseBytes;

                if (response.Length == 0)
                {
                    request.SetStatus(DataRequest.RequestStatus.Error);
                    request.ErrorMessage = "No Response";
                    goto BadResponse;
                }


                // check for minimum response length
                if (response.Length > 0 && response.Length < 8)
                {
                    request.SetStatus(DataRequest.RequestStatus.Error);
                    request.ErrorMessage = "Invalid Response (less than minimum message length)";
                    goto BadResponse;
                }


                byte retOpCode = request.ResponseBytes[4];

                // check for unexpected return response size (with exception for ROC error response opcode 255, and security error opcode 17)         
                if (request.ActualResponseSize != request.ExpectedResponseSize && retOpCode != 255)
                {
                    bool valid = false;
                    if (request.ActualResponseSize > request.ExpectedResponseSize)
                    {
                        // check if the response starts with a valid message, but has trailing garbage

                        // LINQ style (slow)
                        //byte[] initialResponseBytes = request.ResponseBytes.Take(request.ExpectedResponseSize).ToArray();

                        // faster
                        byte[] initialResponseBytes = new byte[request.ExpectedResponseSize];
                        Array.Copy(request.ResponseBytes, 0, initialResponseBytes, 0, request.ExpectedResponseSize);

                        if (CRCCheck(initialResponseBytes) && RespondingDeviceCheck(request.RequestBytes, initialResponseBytes))
                        {
                            // it's from the right device, and passes the CRC check, so it looks like a good message. remove the trailing garbage and continue
                            response = initialResponseBytes;
                            Globals.SystemManager.LogCommsEvent(request.ConnectionID, Globals.FDANow(), "ROCProtocol: The first " + request.ExpectedResponseSize + " bytes of an unexpectedly long response form a valid message.Using this as the device response and ignoring the remaining bytes");
                            valid = true;
                        }
                    }

                    if (!valid)
                    {
                        request.SetStatus(DataRequest.RequestStatus.Error);
                        request.ErrorMessage = "Unexpected response length (expected " + request.ExpectedResponseSize + " bytes)";
                        goto BadResponse;
                    }
                }

                // check for maximum response length
                if (response.Length > 255)
                {
                    request.SetStatus(DataRequest.RequestStatus.Error);
                    request.ErrorMessage = "Invalid Response (over maximum message length at " + response.Length + " bytes)";
                    goto BadResponse;
                }

                // check the CRC       
                if (!CRCCheck(response))
                {
                    request.SetStatus(DataRequest.RequestStatus.Error);
                    request.ErrorMessage = "CRC Fail";
                    goto BadResponse;
                }

                // check that response is from the right device
                if (!RespondingDeviceCheck(request.RequestBytes, response))
                {
                    request.SetStatus(DataRequest.RequestStatus.Error);
                    request.ErrorMessage = "Response from unexpected device";
                    goto BadResponse;
                }

                // login ack response
                if (retOpCode == 17)
                {
                    request.SetStatus(DataRequest.RequestStatus.Success);
                    request.ErrorMessage = "";
                    return;
                }

                // write value(s) ack response
                if (retOpCode == 181)
                {
                    request.SetStatus(DataRequest.RequestStatus.Success);
                    request.ErrorMessage = "";
                    return;
                }

                // ROC error response (Opcode 255)
                if (retOpCode == 255 && !request.ErrorCorrectionAttempted)
                {
                    request.SetStatus(DataRequest.RequestStatus.Error);

                    Byte errorCode = request.ResponseBytes[6];
                    Byte errorOpCode = request.ResponseBytes[7];

                    string errorMsg = ROCError.GetErrorMessage(errorOpCode, errorCode);

                    request.ErrorMessage = "The device returned an error message: " + errorMsg;   

                    if (errorCode == 20 && request.DeviceLogin != string.Empty)
                    {
                        request.ErrorMessage = ". Attempting to log in to the device";

                        // try a login
                        request.ErrorCorrectionRequest = GenerateLoginRequest(request.RequestBytes[1], request.RequestBytes[0], request.DeviceLogin, request.DevicePass);
                        if (request.ErrorCorrectionRequest != null)
                            request.ErrorCorrectionAttempted = true;
                        return;
                    }
                }

                // looks like it's a valid response, set the status to success (unless an error code was returned)
                if (retOpCode != 255)
                {
                    request.SetStatus(DataRequest.RequestStatus.Success);
                    request.ErrorMessage = "";
                }


                // do interpretation of the returned data
                InterpretResponse(ref request);
                return;

                // handle any additional operations required after a bad response, which depend on the message type
                BadResponse:
                if ((request.MessageType == DataRequest.RequestType.Alarms || request.MessageType == DataRequest.RequestType.Events) && FinalAttempt)
                {
                    int firstRecord = request.RequestBytes[7];
                    int numRecords = request.RequestBytes[6];
                    int lastRecord = firstRecord + numRecords;

                    byte transCode = 0;
                    if (request.MessageType == DataRequest.RequestType.Alarms) transCode = 31;
                    if (request.MessageType == DataRequest.RequestType.Events) transCode = 32;

                    Globals.SystemManager.LogCommsEvent(request.ConnectionID, request.ResponseTimestamp.AddMilliseconds(1), "Unable to retrieve " + request.MessageType.ToString() + " for device " + request.RequestBytes[1] + ":" + request.RequestBytes[0] + " from index " + firstRecord + " to " + lastRecord,transCode);
                }
                return;
            }
            catch (Exception ex)
            {
                Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Roc Protocol ValidateResponse() general error");
                return;
            }
        }

        private static bool RespondingDeviceCheck(byte[] request, byte[] response)
        {
            byte requestedUnit = request[0];
            byte requestedGroup = request[1];
            byte responseUnit = response[2];
            byte responseGroup = response[3];
            return (requestedUnit == responseUnit && requestedGroup == responseGroup);
        }

        private static bool CRCCheck(byte[] data)
        {
            ushort responseCRC = IntHelpers.ToUInt16(data[data.Length - 2], data[data.Length - 1]);

            //ushort calcdCRC = CRCHelpers.CalcCRC16(data.Take(data.Length - 2).ToArray(), CRCInitial, CRCPoly, CRCSwapOutputBytes);

            // this is more efficient
            byte[] data_noCRC = new byte[data.Length - 2];
            Array.Copy(data, data_noCRC, data.Length - 2);


            ushort calcdCRC = CRCHelpers.CalcCRC16(data_noCRC, CRCInitial, CRCPoly, CRCSwapOutputBytes);
            calcdCRC = IntHelpers.SwapBytes(calcdCRC);

            return (responseCRC == calcdCRC);
        }

        public sealed class DataType : DataTypeBase
        {
            public static readonly DataType AC10 = new("AC10", 10, typeof(string));
            public static readonly DataType AC20 = new("AC20", 20, typeof(string));
            public static readonly DataType AC30 = new("AC30", 30, typeof(string));
            public static readonly DataType BIN = new("BIN", 1, typeof(byte));
            public static readonly DataType INT8 = new("INT8", 1, typeof(SByte));
            public static readonly DataType INT16 = new("INT16", 2, typeof(Int16));
            public static readonly DataType INT32 = new("INT32", 4, typeof(Int32));
            public static readonly DataType UINT8 = new("UINT8", 1, typeof(Int16));
            public static readonly DataType UINT16 = new("UINT16", 2, typeof(UInt16));
            public static readonly DataType UINT32 = new("UINT32", 4, typeof(UInt32));
            public static readonly DataType FL = new("FL", 4, typeof(float));
            public static readonly DataType TLP = new("TLP", 3, typeof(byte[]));
            public static readonly DataType DT = new("DT", 4, typeof(float));
            private static readonly List<string> TypeList = new(new string[] { "AC10","AC20","AC30","BIN","INT8","INT32","UINT8","UINT16","UINT32","FL","TLP","DT"});

            private DataType(string name, byte size, Type hostDataType) : base(name, size, hostDataType)
            {
   
             }

            public static bool IsValidType(string type)
            {
                return TypeList.Contains(type);
                /*
                if (type == AC10.Name) { return true; }
                if (type == AC20.Name) { return true; }
                if (type == AC30.Name) { return true; }
                if (type == BIN.Name) { return true; }
                if (type == INT8.Name) { return true; }
                if (type == INT16.Name) { return true; }
                if (type == INT32.Name) { return true; }
                if (type == UINT8.Name) { return true; }
                if (type == UINT16.Name) { return true; }
                if (type == UINT32.Name) { return true; }
                if (type == FL.Name) { return true; }
                if (type == TLP.Name) { return true; }

                return false;
                */
            }

            public byte[] ToBytes(object value)
            {
                byte[] bytes = null;

                if (this == FL)
                {
                    bytes = BitConverter.GetBytes(Convert.ToSingle(value));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == INT8)
                {
                    bytes = BitConverter.GetBytes(Convert.ToSByte(value));
                }

                if (this == INT16)
                {
                    bytes = BitConverter.GetBytes(Convert.ToInt16(value));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == INT32)
                {
                    bytes = BitConverter.GetBytes((Int32)value);
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == UINT8)
                {
                    bytes = new byte[1] { Convert.ToByte(value) };
                }

                if (this == UINT16)
                {
                    bytes = BitConverter.GetBytes(Convert.ToUInt16(value));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == UINT32)
                {
                    bytes = BitConverter.GetBytes(Convert.ToUInt32(value));
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                }

                if (this == AC10 || this == AC20 || this == AC30)
                {
                    bytes = System.Text.Encoding.ASCII.GetBytes((string)(value));
                }

                if (this == BIN)
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

                if (this == AC10 || this == AC20 || this == AC30)
                {
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }

                if (this == BIN)
                {
                    return (byte)bytes[0];
                }

                if (this == INT8)
                {
                    return (SByte)bytes[0];
                }

                if (this == INT16)
                {
                    //ROC Integer format: LSB (7-0) MSB (15-8)
                    Byte highByte = bytes[1];
                    Byte lowByte = bytes[0];

                    Int16 value = (Int16)((highByte << 8) | (lowByte));

                    return value;
                }


                if (this == INT32 || this == UINT32)
                {
                    // ROC byte Order LSB,LSB+1,MSB-1,MSB                   
                    Byte A = bytes[0];
                    Byte B = bytes[1];
                    Byte C = bytes[2];
                    Byte D = bytes[3];

                    Int32 value = (D << 24 | C << 16 | B << 8 | A);
                    if (this == UINT32)
                        return (UInt32)value;
                    else
                        return value;
                }
 
                if (this == UINT8)
                {
                    return bytes[0];
                }


                if (this == UINT16)
                {
                    Byte highByte = bytes[1];
                    Byte lowByte = bytes[0];

                    UInt16 value = (UInt16)((highByte << 8) | (lowByte));

                    return value;
                }

                // 32 bit floating point
                if (this == FL)
                {
                    // re-order the bytes from the ROC order to what's expected by the BitConverter class
                    //byte[] reordered = new byte[4] { bytes[1], bytes[0], bytes[2], bytes[3] };

                    //Byte[] reordered = new byte[4] { bytes[3], bytes[2], bytes[1], bytes[0] };

                    // convert to a 32 bit float and return
                    return BitConverter.ToSingle(bytes, 0);
                }

                if (this == TLP)
                {
                    return bytes;
                }

                if (this == DT)
                {
                    byte minute = bytes[0];
                    byte hour = bytes[1];
                    byte day = bytes[2];
                    byte month = bytes[3];

                    return month * 1000000 + day * 10000 + hour * 100 + minute;

                }

                // shouldn't be possible to get here, but all paths must return a value so...return null
                return null;
            }

            public static DataType GetType(string typeName)
            {

                switch (typeName.ToUpper())
                {
                    case "AC10": return AC10;
                    case "AC20": return AC20;
                    case "AC30": return AC30;
                    case "BIN": return BIN;
                    case "INT8": return INT8;
                    case "INT16": return INT16;
                    case "INT32": return INT32;
                    case "UINT8": return UINT8;
                    case "UINT16": return UINT16;
                    case "UINT32": return UINT32;
                    case "FL": return FL;
                    case "TLP": return TLP;
                    case "DT": return DT;
                    default: return null;
                }
            }
        }

        //*********************************************   EVENTS   ****************************************************
        public class RocEventRecord : AlarmEventRecord
        {
            public readonly Guid ConnectionID;    // set by constructor
            public readonly string NodeDetails;   // set by constructor
            public readonly string SourceData;    // raw event record data as a hex string
            public int Format;           // retrieved from database lookup table
            public byte PointType;       // from record bytes
            public byte ParamFSTCal;     // from record bytes  - this was previously 'param' code will need to be updated to use the same variable for FST or Cal Type
            public readonly int EventPtrPosition;  // set by constructor
            public readonly int EventPtrPosDisplay; // EventPtr position for display and storage purposes (1 based)
            public readonly int DevicePtrPosition; // set by constructor
            public readonly DateTime FDATimestamp; // set by constructor
            public DateTime EventTimestamp1 = DateTime.MinValue;  // from record bytes
            public DateTime EventTimestamp2 = DateTime.MinValue;  // from record bytes
            public byte PtNum=0;                // from record bytes
            public string Operator="";           // from record bytes

            public string EventText="";          // from record bytes   
            public bool ValuesConverted = false;      // set by FormatReader function, depending on whether it was able to convert the values
            public float EventValue1;             // set by the FormatReader function, if it was able to convert Device value1
            public float EventValue2;              // set by the FormatReader function, if it was able to convert Device value1
            public string EventDesc = "";          // compiled from PointType description, ParamFSTCal, parameter description (point type and parameter type descriptions come from Tables 'RocEventFormats' and 'RocDataTypes'   in this format "AGA Flow Parameters (2): Latitude"
            public string DestTable;

            public bool updatePointers = true;

            // private properties
            private byte CalibrationType;
            private string CalibrationTypeString;
            private byte CalibrationPointType;
            private string CalibrationPointTypeString;
            private byte MVSInput;
            private string MVSInputString;

            // internal lookup tables
            private static Dictionary<byte, string> CalibrationTypes;
            private static Dictionary<byte, string> MVSInputs;
            /// <summary>
            /// 
            /// </summary>
            /// <param name="timestamp">The date/Time when this record was retrieved from the device by the FDA</param>
            /// <param name="connID">ID of the connection</param>
            /// <param name="node">string identifying the device "group:unit"</param>
            /// <param name="eventPtrPos">This record position in the device</param>
            /// <param name="devPtrPos">Current event record</param>
            /// <param name="eventData">bytes that form a single event record</param>
            public RocEventRecord(DateTime timestamp,Guid connID,string node,int eventPtrPos,int devPtrPos, byte[] eventData,string destTable,bool manual) // 22 bytes representing one event record in the device response to opcode 121)
            {               
                try
                {                                  
                    // set the properties with the values that were passed in to the constructor
                    FDATimestamp = timestamp;
                    ConnectionID = connID;
                    NodeDetails = node;
                    EventPtrPosition = eventPtrPos;
                    EventPtrPosDisplay = MiscHelpers.AddCircular(EventPtrPosition, 1, 1, 240);
                    DevicePtrPosition = devPtrPos;
                    DestTable = destTable;
                    SourceData = BitConverter.ToString(eventData);
                    updatePointers = !manual;

                    // get the rest of the properties from the event record bytes
                    PointType = eventData[0];
                    int seconds = eventData[2];
                    int minutes = eventData[3];
                    int hour = eventData[4];
                    int day = eventData[5];
                    int month = eventData[6];
                    int year = eventData[7];
                    try
                    {
                        EventTimestamp1 = new DateTime(year + 2000, month, day, hour, minutes, seconds);
                    }
                    catch
                    {
                        //Globals.SystemManager.LogApplicationEvent(Globals.GetOffsetUTC(), "ROCProtocol", "", "Events Request (connection " + ConnectionID.ToString() + ", Device " + NodeDetails + "). Record " + EventPtrPosDisplay + " contains invalid record data");
                        Format = 101; // invalid record data (couldn't form a valid timestamp from event data bytes 2-7);
                        Valid = false;
                        return;
                    }
                    
                    // default the EventTimestamp2 to the min date (only used in format 4), if EventTimestamp2 is min date when the record arrives at the DBMananger, a null will be entered in the table
                    EventTimestamp2 = DateTime.MinValue;
                    ParamFSTCal = eventData[1];
  
                    RocEventFormats eventFormat = Globals.SystemManager.GetRocEventFormat(PointType);
                    
                    if (eventFormat == null)
                    {
                        //Globals.SystemManager.LogApplicationEvent(Globals.GetOffsetUTC(), "ROCProtocol", "Unable to determine the event format to use for point type " + PointType + " (not found in RocEventFormats table)");
                        Format = 201; // no record for this point type in RocEventFormats table;
                        Valid = false;
                        return;
                    }
                    

                    Format = eventFormat.FORMAT;

                    ValuesConverted = true;

                    RocDataTypes dataType = null;
                    EventText = "";
                    EventDesc = "";
                    switch (eventFormat.FORMAT)
                    {
                        case 1: ReadFormat1(eventData); break;
                        case 2:
                            dataType = Globals.SystemManager.GetRocDataType(PointType, ParamFSTCal);
                            if (dataType == null)
                            {
                                //Globals.SystemManager.LogApplicationEvent(Globals.GetOffsetUTC(), "ROCProtocol", "Point type " + PointType + ", Parameter " + ParamFSTCal + " not found in RocEventFormats table");
                                Format = 301;
                            }
                            ValuesConverted = (dataType != null);
                            ReadFormat2(eventData, dataType);
                            break;
                        case 3: ReadFormat3(eventData); break;
                        case 4: ReadFormat4(eventData); break;
                        case 5: ReadFormat5(eventData); break;
                        case 6: ReadFormat6(eventData); break;
                        default:
                            //Globals.SystemManager.LogApplicationEvent(Globals.GetOffsetUTC(), "ROCProtocol", "Invalid event format for point type " + PointType + " in RocEventFormats table.");
                            Format = 202; // invalid format for this point type in RocEventFormats table 
                            Valid = false;
                            return;
                    }

                    if (EventDesc == "") // if the ReadFormat() function didn't set the Event Decription, do it here. Format 6 is more complex, the function handles the event description for this format
                    {
                        if (eventFormat == null)
                            EventDesc = "(DescShort = undefined)";
                        else
                        {
                            if (eventFormat.DescShort == "")
                                EventDesc = " (DescShort = undefined)";
                            else
                                EventDesc = eventFormat.DescShort;
                        }                  
                        EventDesc += " (" + PtNum + ")";

                        if (dataType == null)
                            EventDesc += " (DescShort = undefined)";
                        else
                        {
                            if (dataType.DescShort == "")
                                EventDesc += " (DescShort = undefined)";
                            else
                                EventDesc += " " + dataType.DescShort;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while processing device event record (raw data = " + BitConverter.ToString(eventData));
                    Valid = false;
                }
            }

            private void ReadFormat1(byte[] eventData)
            {
                PointType = eventData[0];
                PtNum = eventData[8];
                Operator = System.Text.Encoding.ASCII.GetString(eventData.Skip(9).Take(3).ToArray());
                EventText = System.Text.Encoding.ASCII.GetString(eventData.Skip(12).Take(10).ToArray());

                // EventDesc = ;
                //return "Operator ID '" + Operator + "' Point " + PointType + ":" + PtNum + ":" + ParamFSTCal + ", " + EventText;
            }

            private void ReadFormat2(byte[] eventData,RocDataTypes rocDataTypeEntry)
            {
                PointType = eventData[0];
                PtNum = eventData[8];
                
                Operator = System.Text.Encoding.ASCII.GetString(eventData.Skip(9).Take(3).ToArray());

                DataType ROCDatatype = null;
                if (rocDataTypeEntry != null)
                {
                    ROCDatatype = DataType.GetType(rocDataTypeEntry.DataType);
                    if (ROCDatatype == null)
                    {
                       // Globals.SystemManager.LogApplicationEvent(this, "", "Unrecognized data type '" + rocDataTypeEntry.DataType + "' in the RocDataTypes table for Point type " + PointType + ", parameter " + PtNum, true);
                        ValuesConverted = false;
                        Format = 302;
                        return;
                    }
                    Type HostDataType = ROCDatatype.HostDataType;

                    object eventVal1;
                    object eventVal2;

                    eventVal1 = ROCDatatype.FromBytes(eventData.Skip(12).Take(ROCDatatype.Size).ToArray());
                    eventVal2 = ROCDatatype.FromBytes(eventData.Skip(16).Take(ROCDatatype.Size).ToArray());

                    Convert.ChangeType(eventVal1, HostDataType);
                    Convert.ChangeType(eventVal2, HostDataType);

                    EventValue1 = Convert.ToSingle(eventVal1);
                    EventValue2 = Convert.ToSingle(eventVal2);
                }              
            }

            private void ReadFormat3(byte[] eventData)
            {
                PointType = eventData[0];

                EventText = System.Text.Encoding.ASCII.GetString(eventData.Skip(8).Take(10).ToArray());
 
                //EventDesc = ;

                //return "FST #" + ParamFSTCal + ", " + EventText + ", value = " + DeviceValue2;
            }

            private void ReadFormat4(byte[] eventData)
            {
                int seconds = eventData[8];
                int minutes = eventData[9];
                int hour = eventData[10];
                int day = eventData[11];
                int month = eventData[12];
                int year = eventData[13];
                EventTimestamp2 = new DateTime(year + 2000, month, day, hour, minutes, seconds);

                //EventDesc = ;

                // return "All power removed (event timestamp: " + EventTimestamp2 + ")";
            }

            private void ReadFormat5(byte[] eventData)
            {
                //EventDesc = ;
                //return ""; 
            }

            private void ReadFormat6(byte[] eventData)
            {
                if (CalibrationTypes == null)
                {
                    CalibrationTypes = new Dictionary<byte, string>
                    {
                        { 0, "Set Zero" },
                        { 1, "Set Span" },
                        { 2, "Set Midpoint 1" },
                        { 3, "Set Mid-point 2" },
                        { 4, "Set Mid-point 3" },
                        { 5, "Calibration verified" },
                        { 10, "Set Zero Shift/Static Pressure Offset/RTD Bias" },
                        { 29, "Calibration Cancelled" }
                    };
                }
                if (MVSInputs == null)
                {
                    MVSInputs = new Dictionary<byte, string>
                    {
                        { 1, "Differential Pressure Input" },
                        { 2, "Static Pressure Input" },
                        { 3, "Temperature Input" },
                        { 4, "Low DP Input" }
                    };
                }

                CalibrationType = eventData[1];
                CalibrationTypeString = CalibrationTypes[CalibrationType];
                PtNum = eventData[8];
                Operator = System.Text.Encoding.ASCII.GetString(eventData.Skip(9).Take(3).ToArray());

                CalibrationPointTypeString = "";
                CalibrationPointType = eventData[20];
                if (CalibrationPointType == 40)
                {
                    CalibrationPointTypeString = "MVS";
                    MVSInput = eventData[21];
                    MVSInputString = MVSInputs[MVSInput];
                }
                else
                    if (CalibrationPointType == 3) CalibrationPointTypeString = "AI";
                else
                    CalibrationPointTypeString = "invalid calibration point type (" + eventData[20] + ")";

                

                string returnString = CalibrationPointType + " #" + PtNum;

                if (CalibrationPointTypeString == "MVS")
                    returnString += " " + MVSInputString;

                returnString += " calibration : " + CalibrationType;
                
               
                EventValue1 = (float)(DataType.FL.FromBytes(eventData.Skip(12).Take(4).ToArray()));
                EventValue2 = (float)(DataType.FL.FromBytes(eventData.Skip(16).Take(4).ToArray()));


                EventDesc = returnString;
            }

            public override string GetUpdateLastRecordSQL()
            {
                if (!updatePointers)
                    return "";

                string sql =  "Update " + Globals.SystemManager.GetTableName("FDAHistoricReferences") + " set EventsLastPtrReadPosition = " + EventPtrPosDisplay + ", EventsLastPtrReadTimestamp = '" + DateTimeHelpers.FormatDateTime(FDATimestamp) + "' ";
                sql += "where ConnectionUID = '" + ConnectionID.ToString() + "' and NodeDetails = '" + NodeDetails + "';";

                return sql;
            }

            public override string GetWriteSQL()
            {
  
                string[] destTables = DestTable.Split(':');

                string sql = "insert into " + destTables[0] + " (ConnectionUID,NodeDetails,Format,PointType,ParmFSTCal,EventPtrPosition,DevicePtrPosition,FDATimestamp,";
                sql += "EventTimestamp1,EventTimestamp2,PtNum,Operator,EventText,ValuesConverted,EventValue1,EventValue2,EventDesc,SourceData) values (";
                sql += "'" + ConnectionID.ToString() + "','" + NodeDetails + "'," + Format + "," + PointType + "," + ParamFSTCal + "," + EventPtrPosDisplay + "," + DevicePtrPosition + ",'" + DateTimeHelpers.FormatDateTime(FDATimestamp) + "',";
                sql += "'" + DateTimeHelpers.FormatDateTime(EventTimestamp1) + "',";
                if (EventTimestamp2 == DateTime.MinValue)
                    sql += "NULL,";
                else
                    sql += "'" + DateTimeHelpers.FormatDateTime(EventTimestamp2) + "',";
                
                sql += PtNum + ",'" + Operator + "','" + EventText + "'";

                if (ValuesConverted)
                    sql += ",'True'" + "," + EventValue1 + "," + EventValue2 + ",";
                else
                    sql += ",'False',NULL,NULL,";

                sql += "'" + EventDesc + "','" + SourceData + "');";

                return sql;
            }

        }


        //*********************************************   ALARMS   ****************************************************
        public class RocAlarmRecord : AlarmEventRecord
        {
            public readonly Guid ConnectionID;    // set by constructor
            public readonly string NodeDetails;   // set by constructor
            public readonly DateTime FDATimestamp; // set by constructor
            public readonly int AlarmPtrPostion;   // position of this record
            public readonly int AlarmPtrPosDisplay; // the alarm pointer position for display purposes (1 based)
            public readonly int DevicePtrPostion;  // set by constructor
            public readonly byte AlarmType;        // from alarm record bytes   
            public readonly byte AlarmCode;        // from alarm record bytes
            public readonly DateTime AlarmTimestamp;  // from alarm record bytes
            public string AlarmTag;                     // from alarm record bytes    
            public UInt32 DeviceValue;              // from alarm record bytes
            public float AlarmValue;                // from alarm reccord bytes
            public string AlarmDesc;                 // string compiled from AlarmType/AlarmCode (eg "DP - High Alarm Set")  
            public string DestTable;
            public bool UpdatePointers = true;
            private readonly byte AlarmTypeHighNibble;
            private readonly byte AlarmTypeLowNibble;
            private static Dictionary<byte, string> AlarmTypeHighNibbleLookup;
            private static Dictionary<byte, string> AlarmTypeLowNibbleLookup;  // alarm type low nibble lookup
            private static Dictionary<byte,Dictionary<byte,string>> AlarmCodeLookup;   // <alarm type high nibble,<Alarmcode,alarmText>>
 
            /// <summary>
            /// 
            /// </summary>
            /// <param name="timestamp">The date/Time when this record was retrieved from the device by the FDA</param>
            /// <param name="connID">ID of the connection</param>
            /// <param name="node">Protocol specific string identifying the device</param>
            /// <param name="alarmPtrPos">Position of this record</param>
            /// <param name="devPtrPos">Current alarm record</param>
            /// <param name="alarmData">bytes that form a single alarm record</param>
            public RocAlarmRecord(DateTime timestamp,Guid connID,string node,int alarmPtrPos, int devPtrPos,byte[] alarmData,string destTable,bool manual) // 22 bytes representing one Alarm record in the device response to opcode 121)
            {
                try
                {
                    UpdatePointers = !manual;
                    FDATimestamp = timestamp;
                    ConnectionID = connID;
                    NodeDetails = node;
                    FDATimestamp = timestamp;
                    AlarmPtrPostion = alarmPtrPos;
                    AlarmPtrPosDisplay = MiscHelpers.AddCircular(AlarmPtrPostion, 1, 1, 240);
                    DevicePtrPostion = devPtrPos;
                    DestTable = destTable;
                    AlarmType = alarmData[0];

                    AlarmTypeHighNibble = (byte)(AlarmType >> 4);
                    AlarmTypeLowNibble = (byte)(AlarmType & 0x0F);

                    AlarmCode = alarmData[1];

                    int seconds = alarmData[2];
                    int minutes = alarmData[3];
                    int hour = alarmData[4];
                    int day = alarmData[5];
                    int month = alarmData[6];
                    int year = alarmData[7];

                    try
                    {
                        AlarmTimestamp = new DateTime(year + 2000, month, day, hour, minutes, seconds);

                        AlarmTag = System.Text.Encoding.ASCII.GetString(alarmData.Skip(8).Take(10).ToArray());
                        DeviceValue = (UInt32)DataType.UINT32.FromBytes(alarmData.Skip(18).Take(4).ToArray());
                        AlarmValue = (float)DataType.FL.FromBytes(alarmData.Skip(18).Take(4).ToArray());
                    }
                    catch
                    {
                        Globals.SystemManager.LogApplicationEvent(Globals.FDANow(), "ROCProtocol", "", "Alarms Request (connection " + ConnectionID.ToString() + ", Device " + NodeDetails + ") invalid record data. Raw alarm record data: " + BitConverter.ToString(alarmData));
                        Valid = false;
                        return;
                    }

                    // set up static lookups the first time an alarm Record is created, to be used by subsequent alarm objects
                    if (AlarmTypeHighNibbleLookup == null)
                    {
                        AlarmTypeHighNibbleLookup = new Dictionary<byte, string>
                        {
                            { 1, "DP" },
                            { 2, "AP" },
                            { 3, "PT" },
                            { 5, "I/O" },
                            { 6, "AGA" },
                            { 7, "User Text" },
                            { 8, "User Value" },
                            { 9, "MVS Sensor" },
                            { 10, "SM" },
                            { 15, "FST" }
                        };
                    }

                    if (AlarmTypeLowNibbleLookup == null)
                    {
                        AlarmTypeLowNibbleLookup = new Dictionary<byte, string>
                        {
                            { 0, "Alarm Clear" },
                            { 1, "Alarm Set" },
                            { 2, "Pulse Input Alarm Clear" },
                            { 3, "Pulse Input Alarm Set" },
                            { 4, "SRBX Alarm Clear" },
                            { 5, "SRBX Alarm Set" }
                        };
                    }

                    if (AlarmCodeLookup == null)
                    {
                        AlarmCodeLookup = new Dictionary<byte, Dictionary<byte, string>>();

                        Dictionary<byte, string> AlarmCode = new()
                        {
                            { 0, "Low Alarm" },
                            { 1, "Lo Lo Alarm" },
                            { 2, "High Alarm" },
                            { 3, "Hi Hi Alarm" },
                            { 4, "Rate Alarm" },
                            { 5, "Status Change" },
                            { 6, "A/D Failure" },
                            { 7, "Manual Mode" }
                        };

                        AlarmCodeLookup.Add(1, AlarmCode);
                        AlarmCodeLookup.Add(2, AlarmCode);
                        AlarmCodeLookup.Add(3, AlarmCode);
                        AlarmCodeLookup.Add(5, AlarmCode);

                        AlarmCode = new Dictionary<byte, string>
                        {
                            { 0, "Low Alarm" },
                            { 2, "High Alarm" },
                            { 4, "Redundant Total Count Alarm" },
                            { 5, "Redundant Flow Alarm" },
                            { 6, "No Flow Alarm" },
                            { 7, "Manual Mode" }
                        };
                        AlarmCodeLookup.Add(6, AlarmCode);

                        AlarmCode = new Dictionary<byte, string>
                        {
                            { 0, "Logic Alarm" }
                        };
                        AlarmCodeLookup.Add(8, AlarmCode);

                        AlarmCode = new Dictionary<byte, string>
                        {
                            { 4, "Input Freeze Mode" },
                            { 5, "EIA-485 Fail Alarm" },
                            { 6, "Sensor Communications Fail Alarm" },
                            { 7, "Off Scan Mode" }
                        };
                        AlarmCodeLookup.Add(9, AlarmCode);

                        AlarmCode = new Dictionary<byte, string>
                        {
                            { 0, "Sequence Out of Order Alarm" },
                            { 1, "Phase Discrepancy Detected Alarm" },
                            { 2, "Inconsistent Pulse Count Alarm" },
                            { 3, "Frequency Discrepency Alarm" },
                            { 4, "Channel A Failure Alarm" },
                            { 5, "Channel B Failure Alarm" }
                        };
                    }

                    AlarmDesc = AlarmTypeHighNibbleLookup[AlarmTypeHighNibble] + " - " + AlarmCodeLookup[AlarmTypeHighNibble][AlarmCode] + " " + AlarmTypeLowNibbleLookup[AlarmTypeLowNibble];
                }
                catch (Exception ex)
                {
                    Globals.SystemManager.LogApplicationError(Globals.FDANow(), ex, "Error while processing device alarm record (raw data = " + BitConverter.ToString(alarmData));
                    Valid = false;
                }
            }

            public override string GetWriteSQL()
            {
                string[] destTables = DestTable.Split(':');

                string sql = "insert into " + destTables[0] + "(ConnectionUID,NodeDetails,FDATimestamp,AlarmPtrPosition,DevicePtrPosition,AlarmType,AlarmCode,AlarmTimestamp";
                sql += ",AlarmTag,DeviceValue,AlarmValue,AlarmDesc) values (";

                sql += "'" + ConnectionID.ToString() + "','" + NodeDetails + "','" + DateTimeHelpers.FormatDateTime(FDATimestamp) + "'," + AlarmPtrPosDisplay + "," + DevicePtrPostion + "," + AlarmType + "," + AlarmCode;
                sql += ",'" + DateTimeHelpers.FormatDateTime(AlarmTimestamp) + "','" + AlarmTag + "'," + DeviceValue + "," + AlarmValue + ",'" + AlarmDesc + "');";
                return sql;
            }

            public override string GetUpdateLastRecordSQL()
            {
                if (!UpdatePointers)
                    return "";

                string sql = "Update " + Globals.SystemManager.GetTableName("FDAHistoricReferences") + " set AlarmsLastPtrReadPosition = " + AlarmPtrPosDisplay + ", AlarmsLastPtrReadTimestamp = '" + DateTimeHelpers.FormatDateTime(FDATimestamp) + "' ";
                sql += "where ConnectionUID = '" + ConnectionID.ToString() + "' and NodeDetails = '" + NodeDetails + "';";

                return sql;
            }


        }


    }
}
