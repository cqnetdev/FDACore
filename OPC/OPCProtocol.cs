using Common;
using System;
using System.Collections.Generic;
using System.Text;


namespace OPC
{
    public static class OPCProtocol
    {
        public static bool ValidateRequestString(string requestGroup,Dictionary<Guid,Common.FDADataPointDefinitionStructure> dpds,string groupID,string requestor)
        {
            // Simulation Examples.Functions.Ramp2:5a3f5726-dd52-4224-9584-25254c268a86|Simulation Examples.Functions.Ramp4:DF01DDCD-9F61-485D-9353-4228AA4CFEAB

            // break up the group into individual requests
            string[] requests = requestGroup.Split("|", StringSplitOptions.RemoveEmptyEntries);

            string[] requestParts;
            string error;
            foreach (string request in requests)
            {
                // split the request into ns, path and dpds ref
                requestParts = request.Split(":", StringSplitOptions.RemoveEmptyEntries);

                // make sure the required # of elements are present
                if (requestParts.Length < 3)
                {
                    LogConfigError(groupID, requestor, "Bad format, should be namespace:path:DPDUID");
                    return false;
                }

                // make sure the namespace is an integer
                if (!Int16.TryParse(requestParts[0],out _))
                {
                    LogConfigError(groupID, requestor, "Element 1 (namespace) is not an integer:" + requestParts[0]);
                    return false;
                }

                // make sure the dpds reference is a valud Guid
                Guid guid;
                if (!Guid.TryParse(requestParts[2], out guid))
                {
                    // tag reference is not a valid UID
                    LogConfigError(groupID, requestor, "Element 3 (DPDUID) is not a a valid uid:" + requestParts[3]);
                    return false;
                }

                if (!dpds.ContainsKey(guid))
                {
                    LogConfigError(groupID, requestor, "Element 3 (DPDUID), no datapointdefinition found with this ID:" + requestParts[2]);
                    return false;
                }
            }

            return true;

        }

        private static void LogConfigError(string groupID,string requestor,string error)
        {
            error = "The group '" + groupID + "', requested by " + requestor + ": " + requestor + ". This request group will not be processed";
            Globals.SystemManager.LogApplicationEvent("OPC Protocol", "", error, true);
        }
    }
}
