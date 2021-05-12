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
        public static List<UInt32> TestValues;
        public static DataTypeBase DT; 

        static DerivedTagsTests()
        {
            // generate a ton of random test values
            TestValues = new List<UInt32>();
            Random rnd = new Random();
            byte[] bytes = new byte[4];
            for (int i = 0; i < 100000; i++)
            {
                rnd.NextBytes(bytes);
                TestValues.Add(BitConverter.ToUInt32(bytes));
            }

            DT = Modbus.ModbusProtocol.DataType.UINT32;
        }

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



        [TestMethod, TestCategory("BitSeries")]
        public void BitSeriesFunctionalTest()
        {
            // set up a source tag, put it in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag = new FDADataPointDefinitionStructure();
            srcTag.DPDUID = Guid.NewGuid();
            srcTag.DPDSEnabled = true;
            srcTags.Add(srcTag.DPDUID, srcTag);
            DerivedTag.Tags = srcTags;

            
            DataTypeBase datatype = Modbus.ModbusProtocol.DataType.UINT32;

            // extract bit 0 only
            BitSeriesDerivedTag softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(),"bitser",srcTag.DPDUID.ToString() + ":0:1");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            UInt32 expectedDerivedVal;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    192,
                    DateTime.Now,
                    "",
                    datatype,
                    DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastRead.Timestamp,softTag.LastRead.Timestamp);
                Assert.AreEqual(srcTag.LastRead.Quality,softTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 1; 
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }


            // extract bit 31 only
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":31:1");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                     testVal,
                     192,
                     DateTime.Now,
                     "",
                     datatype,
                     DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastRead.Timestamp, srcTag.LastRead.Timestamp);
                Assert.AreEqual(softTag.LastRead.Quality, srcTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & (UInt32)Math.Pow(2, 31))>>31;
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }


            // extract bit 20 only
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":20:1");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    192,
                    DateTime.Now,
                    "",
                    datatype,
                    DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastRead.Timestamp, srcTag.LastRead.Timestamp);
                Assert.AreEqual(softTag.LastRead.Quality, srcTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & (UInt32)Math.Pow(2, 20)) >> 20;
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }

            // extract bits 0-1
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":0:2");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    192,
                    DateTime.Now,
                    "",
                    datatype,
                    DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastRead.Timestamp, srcTag.LastRead.Timestamp);
                Assert.AreEqual(softTag.LastRead.Quality, srcTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & ((UInt32)Math.Pow(2,1) + (UInt32)Math.Pow(2, 0)) >> 0;
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }

            // extract bits 10-13
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":10:4");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    192,
                    DateTime.Now,
                    "",
                    datatype,
                    DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastRead.Timestamp, srcTag.LastRead.Timestamp);
                Assert.AreEqual(softTag.LastRead.Quality, srcTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & ((UInt32)Math.Pow(2, 10) + (UInt32)Math.Pow(2,11) +  (UInt32)Math.Pow(2,12) + (UInt32)Math.Pow(2,13))) >> 10;
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }

            // "extract" all the bits
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":0:32");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                     testVal,
                     192,
                     DateTime.Now,
                     "",
                     datatype,
                     DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastRead.Timestamp, srcTag.LastRead.Timestamp);
                Assert.AreEqual(softTag.LastRead.Quality, srcTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal;
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }

            // extract the highest 6 bits
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":26:6");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    192,
                    DateTime.Now,
                    "",
                    datatype,
                    DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastRead.Timestamp,softTag.LastRead.Timestamp);
                Assert.AreEqual(srcTag.LastRead.Quality,softTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = expectedDerivedVal = (testVal & ((UInt32)Math.Pow(2, 26) + (UInt32)Math.Pow(2, 27) + (UInt32)Math.Pow(2, 28) + (UInt32)Math.Pow(2, 29) + (UInt32)Math.Pow(2,30) + (UInt32)Math.Pow(2,31))) >> 26;

                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }


            // extract the highest 6 bits, source tag has bad quality
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":26:6");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            FDADataPointDefinitionStructure.Datapoint lastread_before;
            foreach (UInt32 testVal in TestValues)
            {
                lastread_before = softTag.LastRead;
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    0,
                    DateTime.Now,
                    "",
                    datatype,
                    DataRequest.WriteMode.Insert);

                // soft tag should not have updated
                Assert.AreSame(lastread_before, softTag.LastRead);
              
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
           

           

            UInt32 expectedDerivedVal;
            
            

            // bitmask 0000 0000 0000 0000 0000 0000 0000 0001
            DerivedTag softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":1");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    192,
                    DateTime.Now,
                    "",
                    DT,
                    DataRequest.WriteMode.Insert);


                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastRead.Timestamp,softTag.LastRead.Timestamp);
                Assert.AreEqual(srcTag.LastRead.Quality,softTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 1;
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }

            // bitmask 0000 0000 0000 0000 1100 0000 0000 0001
            softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":49153");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    192,
                    DateTime.Now,
                    "",
                    DT,
                    DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.LastRead.Timestamp, srcTag.LastRead.Timestamp);
                Assert.AreEqual(softTag.LastRead.Quality, srcTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 49153;
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }

            // bitmask 1111 1111 1111 1111 1111 1111 1111 1111
            softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":4294967295");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            foreach (UInt32 testVal in TestValues)
            {
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                     testVal,
                     192,
                     DateTime.Now,
                     "",
                     DT,
                     DataRequest.WriteMode.Insert);

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastRead.Timestamp,softTag.LastRead.Timestamp);
                Assert.AreEqual(srcTag.LastRead.Quality,softTag.LastRead.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal;
                Assert.AreEqual(expectedDerivedVal, softTag.LastRead.Value);
            }


            // bitmask 0000 0000 0000 0000 1100 0000 0000 0001, source tag has bad quality
            softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":49153");
            softTag.Initialize();
            softTag.DPDSEnabled = true;
            FDADataPointDefinitionStructure.Datapoint lastRead_before_trigger;
            foreach (UInt32 testVal in TestValues)
            {
                lastRead_before_trigger = softTag.LastRead;
                // update the value in the source tag
                srcTag.LastRead = new FDADataPointDefinitionStructure.Datapoint(
                    testVal,
                    0,   // bad quality
                    DateTime.Now,
                    "",
                    DT,
                    DataRequest.WriteMode.Insert);

                // derived tag should not have updated (LastRead datapoint should not have changed)
                Assert.AreSame(lastRead_before_trigger, softTag.LastRead);              
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
                srcTags[selectedSourceIDguid].LastRead.Quality = 192;
                srcTags[selectedSourceIDguid].LastRead.Value = newValue;
                srcTags[selectedSourceIDguid].LastRead.Timestamp = DateTime.Now;

                expectedResult = 0;
                foreach (FDADataPointDefinitionStructure sourcetag in srcTags.Values)
                {
                    expectedResult += sourcetag.LastRead.Value;
                }

                Assert.AreEqual(expectedResult, testTag.LastRead.Value);
                Assert.AreEqual(srcTags[selectedSourceIDguid].LastRead.Quality, testTag.LastRead.Quality);
                Assert.AreEqual(srcTags[selectedSourceIDguid].LastRead.Timestamp, testTag.LastRead.Timestamp);
            }
        }
    */
    }

}
