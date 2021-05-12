using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;
using Modbus;

namespace Tests
{

    [TestClass]
    public class DerivedTagsTests
    {
        [TestMethod]
        public void BitSeriesArgumentValidityChecks()
        {
            // set up a source tag, put it in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag = new FDADataPointDefinitionStructure();
            srcTag.DPDUID = Guid.NewGuid();
            srcTags.Add(srcTag.DPDUID, srcTag);
            DerivedTag.Tags = srcTags;

            // good arguments, should all produce a valid and enabled derived tag
            List<string> goodArgs = new List<string>();
            goodArgs.Add(srcTag.DPDUID.ToString() + ":0:2");
            goodArgs.Add(srcTag.DPDUID.ToString() + ":10:4");
            goodArgs.Add(srcTag.DPDUID.ToString() + ":0:32");
            goodArgs.Add(srcTag.DPDUID.ToString() + ":31:1");

            // bad arguments, should all produce an invalid and disabled derived tag
            List<string> badArgs = new List<string>();
            badArgs.Add(null);                                 // null argument
            badArgs.Add("");                                   // empty argument string
            badArgs.Add(Guid.NewGuid().ToString());            // not enough args (1)
            badArgs.Add(Guid.NewGuid().ToString()+":0");       // not enough args (2)

            badArgs.Add("123456-789:0:2");                     // invalid SrcID
            badArgs.Add(Guid.NewGuid().ToString() + ":0:2");   // SrcID not found 

            badArgs.Add(srcTag.DPDUID.ToString() + ":a:2");     // start bit not a number
            badArgs.Add(srcTag.DPDUID.ToString() + ":1.2:2");   // start bit not an integer
            badArgs.Add(srcTag.DPDUID.ToString() + ":50:2");    // start bit too high
            badArgs.Add(srcTag.DPDUID.ToString() + ":-2:2");    // start bit too low

            badArgs.Add(srcTag.DPDUID.ToString() + ":0:a");     // bit count not a number
            badArgs.Add(srcTag.DPDUID.ToString() + ":32:-5");   // negative bit count
            badArgs.Add(srcTag.DPDUID.ToString() + ":32:5.5");  // bit count not an integer
            badArgs.Add(srcTag.DPDUID.ToString() + ":0:33");    // bit count too high
            badArgs.Add(srcTag.DPDUID.ToString() + ":1:32");    // start bit + bit count too high;
            badArgs.Add(srcTag.DPDUID.ToString() + ":10:25");   // start bit + bit count too high;
            badArgs.Add(srcTag.DPDUID.ToString() + ":31:2");    // start bit + bit count too high;


            // check that bad softtag id is detected
            BitSeriesDerivedTag softTag = (BitSeriesDerivedTag)DerivedTag.Create("12345-678","bitser", goodArgs[0]);
            softTag.Initialize();

            Assert.AreEqual(softTag.IsValid, false);

            
            // check that bad arguments are detected
            foreach (string badArgString in badArgs)
            {
                softTag = (BitSeriesDerivedTag)DerivedTag.Create(srcTag.DPDUID.ToString(), "bitser", badArgString);
                softTag.Initialize();
                Assert.AreEqual(softTag.IsValid, false);
            }

            // check that good arguments are determined to be valid
            foreach (string goodArgString in goodArgs)
            {
                softTag = (BitSeriesDerivedTag)DerivedTag.Create(srcTag.DPDUID.ToString(), "bitser", goodArgString);
                softTag.Initialize();
                Assert.AreEqual(softTag.IsValid, true);
            }
        }



        [TestMethod]
        public void BitSeriesFunctionalTest()
        {
            // set up a source tag, put it in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag = new FDADataPointDefinitionStructure();
            srcTag.DPDUID = Guid.NewGuid();
            srcTag.DPDSEnabled = true;
            srcTags.Add(srcTag.DPDUID, srcTag);
            DerivedTag.Tags = srcTags;

            // generate 1000 random unsigned Int32 values for testing
            List<UInt32> testValues = new List<UInt32>();
            Random rnd = new Random();
            byte[] bytes = new byte[4];
            for (int i = 0;i < 100000;i++)
            {
                rnd.NextBytes(bytes);
                testValues.Add(BitConverter.ToUInt32(bytes));
            }

            DataTypeBase datatype = Modbus.ModbusProtocol.DataType.UINT32;

            // extract bit 0 only
            BitSeriesDerivedTag softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(),"bitser",srcTag.DPDUID.ToString() + ":0:1");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            UInt32 expectedDerivedVal;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataType = datatype;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastReadDataTimestamp,softTag.LastReadDataTimestamp);
                Assert.AreEqual(srcTag.LastReadQuality,softTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 1; 
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }


            // extract bit 31 only
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":31:1");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastReadDataTimestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.LastReadQuality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & (UInt32)Math.Pow(2, 31))>>31;
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }


            // extract bit 20 only
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":20:1");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastReadDataTimestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.LastReadQuality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & (UInt32)Math.Pow(2, 20)) >> 20;
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }

            // extract bits 0-1
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":0:2");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastReadDataTimestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.LastReadQuality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & ((UInt32)Math.Pow(2,1) + (UInt32)Math.Pow(2, 0)) >> 0;
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }

            // extract bits 10-13
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":10:4");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastReadDataTimestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.LastReadQuality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & ((UInt32)Math.Pow(2, 10) + (UInt32)Math.Pow(2,11) +  (UInt32)Math.Pow(2,12) + (UInt32)Math.Pow(2,13))) >> 10;
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }

            // "extract" all the bits
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":0:32");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastReadDataTimestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.LastReadQuality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal;
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }

            // extract the highest 6 bits
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":26:6");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastReadDataTimestamp,softTag.LastReadDataTimestamp);
                Assert.AreEqual(srcTag.LastReadQuality,softTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = expectedDerivedVal = (testVal & ((UInt32)Math.Pow(2, 26) + (UInt32)Math.Pow(2, 27) + (UInt32)Math.Pow(2, 28) + (UInt32)Math.Pow(2, 29) + (UInt32)Math.Pow(2,30) + (UInt32)Math.Pow(2,31))) >> 26;

                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }


        }

        [TestMethod]
        public void BitmaskArgumentValidityChecks()
        {
            // set up a source tag, put it in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag = new FDADataPointDefinitionStructure();
            srcTag.DPDSEnabled = true;
            srcTag.DPDUID = Guid.NewGuid();
            srcTags.Add(srcTag.DPDUID, srcTag);
            DerivedTag.Tags = srcTags;

            // good arguments, should all produce a valid and enabled derived tag
            List<string> goodArgs = new List<string>();
            goodArgs.Add(srcTag.DPDUID.ToString() + ":1"); // 0000
            goodArgs.Add(srcTag.DPDUID.ToString() + ":2"); // 0010
            goodArgs.Add(srcTag.DPDUID.ToString() + ":4"); // 0100
            goodArgs.Add(srcTag.DPDUID.ToString() + ":8"); // 1000
            goodArgs.Add(srcTag.DPDUID.ToString() + ":12");// 1100
            goodArgs.Add(srcTag.DPDUID.ToString() + ":2147483648"); // 1000 0000 0000 0000 0000 0000 0000 0000
            goodArgs.Add(srcTag.DPDUID.ToString() + ":2147483649"); // 1000 0000 0000 0000 0000 0000 0000 0001


            // bad arguments, should all produce an invalid and disabled derived tag
            List<string> badArgs = new List<string>();
            badArgs.Add(null);                                 // null argument
            badArgs.Add("");                                   // empty argument string
            badArgs.Add(Guid.NewGuid().ToString());            // not enough args (1)

            badArgs.Add("123456-789:0:2");                     // invalid SrcID
            badArgs.Add(Guid.NewGuid().ToString() + ":0:2");   // SrcID not found 

            badArgs.Add(srcTag.DPDUID.ToString() + ":a");       // bit mask not a number
            badArgs.Add(srcTag.DPDUID.ToString() + ":1.2");     // bit mask not an integer
            badArgs.Add(srcTag.DPDUID.ToString() + ":4294967296");    // bit mask too high
            badArgs.Add(srcTag.DPDUID.ToString() + ":-2");    // bit mask too low


            // check that bad softtag id is detected
            DerivedTag softTag = DerivedTag.Create("12345-678", "bitmask", goodArgs[0]);
            Assert.AreEqual(softTag.IsValid, false);
            Assert.AreEqual(softTag.DPDSEnabled, false);

            // check that bad arguments are detected
            foreach (string badArgString in badArgs)
            {
                softTag = DerivedTag.Create(srcTag.DPDUID.ToString(), "bitmask", badArgString);
                softTag.Initialize();
                Assert.AreEqual(softTag.IsValid, false);
                Assert.AreEqual(softTag.DPDSEnabled, false);
            }

            // check that good arguments are determined to be valid
            foreach (string goodArgString in goodArgs)
            {
                softTag = DerivedTag.Create(srcTag.DPDUID.ToString(), "bitmask", goodArgString);
                softTag.Initialize();
                Assert.AreEqual(true,softTag.IsValid);
            }
        }

     

        [TestMethod]
        public void BitmaskFunctionalTest()
        {
            // set up a source tag, put it in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag = new FDADataPointDefinitionStructure();
            srcTag.DPDUID = Guid.NewGuid();
            srcTag.DPDSEnabled = true;
            srcTags.Add(srcTag.DPDUID, srcTag);
            DerivedTag.Tags = srcTags;

            // generate 100,000 random unsigned Int32 values for testing
            List<UInt32> testValues = new List<UInt32>();
            Random rnd = new Random();
            byte[] bytes = new byte[4];
            for (int i = 0; i < 100000; i++)
            {
                rnd.NextBytes(bytes);
                testValues.Add(BitConverter.ToUInt32(bytes));
            }

            UInt32 expectedDerivedVal;
            DataTypeBase datatype = Modbus.ModbusProtocol.DataType.UINT32;
            

            // bitmask 0000 0000 0000 0000 0000 0000 0000 0001
            DerivedTag softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":1");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataType = datatype;
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastReadDataTimestamp,softTag.LastReadDataTimestamp);
                Assert.AreEqual(srcTag.LastReadQuality,softTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 1;
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }

            // bitmask 0000 0000 0000 0000 1100 0000 0000 0001
            softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":49153");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastReadDataTimestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.LastReadQuality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 49153;
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }

            // bitmask 1111 1111 1111 1111 1111 1111 1111 1111
            softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":4294967295");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastReadDataTimestamp,softTag.LastReadDataTimestamp);
                Assert.AreEqual(srcTag.LastReadQuality,softTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal;
                Assert.AreEqual(expectedDerivedVal, softTag.LastReadDataValue);
            }

        }
/*
        [TestMethod]
        public void SummationArgumentValidityChecks()
        {
            // set up a few source tags, put them in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag;
            List<string> goodIDs = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                srcTag = new FDADataPointDefinitionStructure();
                srcTag.DPDUID = Guid.NewGuid();
                srcTags.Add(srcTag.DPDUID, srcTag);
                goodIDs.Add(srcTag.DPDUID.ToString());
            }
            DerivedTag.PLCTags = srcTags;

            // good arguments, should all produce a valid and enabled derived tag
            List<string> goodArgs = new List<string>();
            goodArgs.Add(goodIDs[0]);  // just one source tag
            goodArgs.Add(goodIDs[0] + ":" + goodIDs[1]); // two source tags
            goodArgs.Add(goodIDs[0] + ":" + goodIDs[1] + ":" + goodIDs[2]); // three source tags
            goodArgs.Add(goodIDs[0] + ":" + goodIDs[1] + ":" + goodIDs[2] + ":" + goodIDs[3]); // four source tags
            goodArgs.Add(goodIDs[0] + ":" + goodIDs[1] + ":" + goodIDs[2] + ":" + goodIDs[3] + ":" + goodIDs[4]); // 5 source tags 
        

            // bad arguments, should all produce an invalid and disabled derived tag
            List<string> badArgs = new List<string>();
            string nonexistentID = Guid.NewGuid().ToString();
            badArgs.Add(null);                      // null argument
            badArgs.Add("");                        // empty argument string
            badArgs.Add("fdjhkfrahufe");            // not a valid id
            badArgs.Add(Guid.NewGuid().ToString()); // references non-existent source tag
            badArgs.Add(goodIDs[0] + ":" + nonexistentID); //references one good source tag, and one that doesn't exist
            badArgs.Add(goodIDs[0] + ":fdsfehsife"); //references one good source tag, one invalid ID

            // check that bad softtag id is detected
            DerivedTag softTag = DerivedTag.Create("12345-678", "summation", goodArgs[0], true);
            Assert.AreEqual(false,softTag.IsValid);
            Assert.AreEqual(false,softTag.DPDSEnabled);

            // check that bad arguments are detected
            foreach (string badArgString in badArgs)
            {
                softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "summation", badArgString, true);
                Assert.AreEqual(false,softTag.IsValid);
                Assert.AreEqual(false,softTag.DPDSEnabled);
            }

            // check that good arguments are determined to be valid
            foreach (string goodArgString in goodArgs)
            {
                softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "summation", goodArgString, true);
                Assert.AreEqual(true,softTag.IsValid);
                Assert.AreEqual(true,softTag.DPDSEnabled);
            }

        }

        [TestMethod]
        public void SummationFunctionalTest()
        {
            // set up a few source tags, put them in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag;
            List<string> goodIDs = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                srcTag = new FDADataPointDefinitionStructure();
                srcTag.DPDUID = Guid.NewGuid();
                srcTags.Add(srcTag.DPDUID, srcTag);
                goodIDs.Add(srcTag.DPDUID.ToString());
            }
            DerivedTag.PLCTags = srcTags;

            ModbusProtocol.DataType datatypeFloat= ModbusProtocol.DataType.FL;
                       
            List<string> goodArgs = new List<string>();
            goodArgs.Add(goodIDs[0]);  // just one source tag
            goodArgs.Add(goodIDs[0] + ":" + goodIDs[1]); // two source tags
            goodArgs.Add(goodIDs[0] + ":" + goodIDs[1] + ":" + goodIDs[2]); // three source tags
            goodArgs.Add(goodIDs[0] + ":" + goodIDs[1] + ":" + goodIDs[2] + ":" + goodIDs[3]); // four source tags
            goodArgs.Add(goodIDs[0] + ":" + goodIDs[1] + ":" + goodIDs[2] + ":" + goodIDs[3] + ":" + goodIDs[4]); // 5 source tags 

            Double newValue;
            Random rnd = new Random();
            byte[] bytes = new byte[4];
            int selectedSourceTagIdx;
            string selectedSourceIDString;
            Guid selectedSourceIDguid;
            DerivedTag testTag;
            Double expectedResult;
          
            // create a new summation soft tag that sums all 5 source tags and then do 1000 tests
            testTag = DerivedTag.Create(Guid.NewGuid().ToString(), "summation", goodArgs[4], true);

            for (int i = 0; i < 1000; i++)
            {
                // generate a random double and assign it to a random source tag
                newValue = rnd.NextDouble();

                selectedSourceTagIdx = rnd.Next(0, 4);
                selectedSourceIDString = goodIDs[selectedSourceTagIdx];
                selectedSourceIDguid = Guid.Parse(selectedSourceIDString);

                srcTags[selectedSourceIDguid].LastReadDataType = datatypeFloat;
                srcTags[selectedSourceIDguid].LastReadQuality = 192;
                srcTags[selectedSourceIDguid].LastReadDataValue = newValue;
                srcTags[selectedSourceIDguid].LastReadDataTimestamp = DateTime.Now;

                expectedResult = 0;
                foreach (FDADataPointDefinitionStructure sourcetag in srcTags.Values)
                {
                    expectedResult += sourcetag.LastReadDataValue;
                }

                Assert.AreEqual(expectedResult, testTag.LastReadDataValue);
                Assert.AreEqual(srcTags[selectedSourceIDguid].LastReadQuality, testTag.LastReadQuality);
                Assert.AreEqual(srcTags[selectedSourceIDguid].LastReadDataTimestamp, testTag.LastReadDataTimestamp);
            }
        }
    */
    }

}
