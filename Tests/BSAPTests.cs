using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;

namespace Tests
{
    [TestClass]
    class BSAPTests
    {
        private static Dictionary<Guid, FDADataPointDefinitionStructure> tags;
        private static Dictionary<Guid, FDADevice> devices;


        static BSAPTests()
        {
            tags = new();
            Guid tagid = Guid.Parse("A6745A0F-2551-4E77-A072-0341C136E991");
            tags.Add(tagid, new FDADataPointDefinitionStructure());

            devices = new();
            Guid deviceID = Guid.NewGuid();
            devices.Add(deviceID, new FDADevice());


        }

        [TestMethod]
        public void GoodRequestStringValidation()
        {
            // valid UDP request string
            string request = "0:0:127.0.0.1:1234:READ|LOWWATER:BIN:A6745A0F-2551-4E77-A072-0341C136E991";
            bool result = BSAP.BSAPProtocol.ValidateRequestString(new object(), Guid.NewGuid().ToString(),"demand",request, tags, devices,true);
            Assert.IsTrue(result);
        }

    }
}
