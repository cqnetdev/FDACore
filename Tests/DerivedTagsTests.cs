using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;

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
            DerivedTag.PLCTags = srcTags;

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
            BitSeriesDerivedTag softTag = (BitSeriesDerivedTag)DerivedTag.Create("12345-678","bitser", goodArgs[0], true);
            Assert.AreEqual(softTag.IsValid, false);
            Assert.AreEqual(softTag.Enabled, false);
            
            // check that bad arguments are detected
            foreach (string badArgAtring in badArgs)
            {
                softTag = (BitSeriesDerivedTag)DerivedTag.Create(srcTag.DPDUID.ToString(), "bitser", badArgAtring, true);
                Assert.AreEqual(softTag.IsValid, false);
                Assert.AreEqual(softTag.Enabled, false);
            }

            // check that good arguments are determined to be valid
            foreach (string goodArgString in goodArgs)
            {
                softTag = (BitSeriesDerivedTag)DerivedTag.Create(srcTag.DPDUID.ToString(), "bitser", goodArgString, true);
                Assert.AreEqual(softTag.IsValid, true);
                Assert.AreEqual(softTag.Enabled, true);
            }
        }

        [TestMethod]
        public void BitSeriesCreationTest()
        {
            // set up a source tag, put it in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag = new FDADataPointDefinitionStructure();
            srcTag.DPDUID = Guid.NewGuid();
            srcTags.Add(srcTag.DPDUID, srcTag);
            DerivedTag.PLCTags = srcTags;

            // these are all good arguments, should all produce a valid an enabled derived tag, with the correct parameters
            Guid softtagID = Guid.NewGuid();
            BitSeriesDerivedTag softtag = (BitSeriesDerivedTag)DerivedTag.Create(softtagID.ToString(), "bitser", srcTag.DPDUID + ":0:1",true);
            Assert.IsTrue(softtag.ID == softtagID);
            Assert.IsTrue(softtag.SourceTag == srcTag.DPDUID);
            Assert.IsTrue(softtag.IsValid == true);
            Assert.IsTrue(softtag.Enabled == true);
            Assert.IsTrue(softtag.StartBit == 0);
            Assert.IsTrue(softtag.NumBits == 1);


            softtag = (BitSeriesDerivedTag)DerivedTag.Create(softtagID.ToString(), "bitser", srcTag.DPDUID + ":0:2", true);
            Assert.IsTrue(softtag.ID == softtagID);
            Assert.IsTrue(softtag.SourceTag == srcTag.DPDUID);
            Assert.IsTrue(softtag.IsValid == true);
            Assert.IsTrue(softtag.Enabled == true);
            Assert.IsTrue(softtag.StartBit == 0);
            Assert.IsTrue(softtag.NumBits == 2);

            softtag = (BitSeriesDerivedTag)DerivedTag.Create(softtagID.ToString(), "bitser", srcTag.DPDUID + ":10:4", true);
            Assert.IsTrue(softtag.ID == softtagID);
            Assert.IsTrue(softtag.SourceTag == srcTag.DPDUID);
            Assert.IsTrue(softtag.IsValid == true);
            Assert.IsTrue(softtag.Enabled == true);
            Assert.IsTrue(softtag.StartBit == 10);
            Assert.IsTrue(softtag.NumBits == 4);

            softtag = (BitSeriesDerivedTag)DerivedTag.Create(softtagID.ToString(), "bitser", srcTag.DPDUID + ":0:32", true);
            Assert.IsTrue(softtag.ID == softtagID);
            Assert.IsTrue(softtag.SourceTag == srcTag.DPDUID);
            Assert.IsTrue(softtag.IsValid == true);
            Assert.IsTrue(softtag.Enabled == true);
            Assert.IsTrue(softtag.StartBit == 0);
            Assert.IsTrue(softtag.NumBits == 32);

            softtag = (BitSeriesDerivedTag)DerivedTag.Create(softtagID.ToString(), "bitser", srcTag.DPDUID + ":31:1", true);
            Assert.IsTrue(softtag.ID == softtagID);
            Assert.IsTrue(softtag.SourceTag == srcTag.DPDUID);
            Assert.IsTrue(softtag.IsValid == true);
            Assert.IsTrue(softtag.Enabled == true);
            Assert.IsTrue(softtag.StartBit == 31);
            Assert.IsTrue(softtag.NumBits == 1);
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
            DerivedTag.PLCTags = srcTags;

            // generate 1000 random unsigned Int32 values for testing
            List<UInt32> testValues = new List<UInt32>();
            Random rnd = new Random();
            byte[] bytes = new byte[4];
            for (int i = 0;i < 100000;i++)
            {
                rnd.NextBytes(bytes);
                testValues.Add(BitConverter.ToUInt32(bytes));
            }

            // extract bit 0 only
            BitSeriesDerivedTag softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(),"bitser",srcTag.DPDUID.ToString() + ":0:1",true);
            UInt32 expectedDerivedVal;
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastReadDataTimestamp,softTag.Timestamp);
                Assert.AreEqual(srcTag.LastReadQuality,softTag.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 1; 
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }


            // extract bit 31 only
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":31:1", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.Timestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.Quality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & (UInt32)Math.Pow(2, 31))>>31;
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }


            // extract bit 20 only
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":20:1", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.Timestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.Quality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & (UInt32)Math.Pow(2, 20)) >> 20;
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }

            // extract bits 0-1
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":0:2", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.Timestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.Quality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & ((UInt32)Math.Pow(2,1) + (UInt32)Math.Pow(2, 0)) >> 0;
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }

            // extract bits 10-13
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":10:4", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.Timestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.Quality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = (testVal & ((UInt32)Math.Pow(2, 10) + (UInt32)Math.Pow(2,11) +  (UInt32)Math.Pow(2,12) + (UInt32)Math.Pow(2,13))) >> 10;
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }

            // "extract" all the bits
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":0:32", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.Timestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.Quality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal;
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }

            // extract the highest 6 bits
            softTag = (BitSeriesDerivedTag)DerivedTag.Create(Guid.NewGuid().ToString(), "bitser", srcTag.DPDUID.ToString() + ":26:6", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(srcTag.LastReadDataTimestamp,softTag.Timestamp);
                Assert.AreEqual(srcTag.LastReadQuality,softTag.Quality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = expectedDerivedVal = (testVal & ((UInt32)Math.Pow(2, 26) + (UInt32)Math.Pow(2, 27) + (UInt32)Math.Pow(2, 28) + (UInt32)Math.Pow(2, 29) + (UInt32)Math.Pow(2,30) + (UInt32)Math.Pow(2,31))) >> 26;

                Assert.AreEqual(expectedDerivedVal, softTag.Value);
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
            DerivedTag.PLCTags = srcTags;

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
            DerivedTag softTag = DerivedTag.Create("12345-678", "bitmask", goodArgs[0], true);
            Assert.AreEqual(softTag.IsValid, false);
            Assert.AreEqual(softTag.Enabled, false);

            // check that bad arguments are detected
            foreach (string badArgAtring in badArgs)
            {
                softTag = DerivedTag.Create(srcTag.DPDUID.ToString(), "bitmask", badArgAtring, true);
                Assert.AreEqual(softTag.IsValid, false);
                Assert.AreEqual(softTag.Enabled, false);
            }

            // check that good arguments are determined to be valid
            foreach (string goodArgString in goodArgs)
            {
                softTag = DerivedTag.Create(srcTag.DPDUID.ToString(), "bitmask", goodArgString, true);
                Assert.AreEqual(true,softTag.IsValid);
                Assert.AreEqual(true,softTag.Enabled);
            }
        }

        [TestMethod]
        public void BitmaskCreationTest()
        {
            // set up a source tag, put it in a dictionary (like in the FDA), and make the dictionary available to the derived tags class
            Dictionary<Guid, FDADataPointDefinitionStructure> srcTags = new Dictionary<Guid, FDADataPointDefinitionStructure>();
            FDADataPointDefinitionStructure srcTag = new FDADataPointDefinitionStructure();
            srcTag.DPDSEnabled = true;
            srcTag.DPDUID = Guid.NewGuid();
            srcTags.Add(srcTag.DPDUID, srcTag);
            DerivedTag.PLCTags = srcTags;

            // these are all good arguments, should all produce a valid an enabled derived tag, with the correct parameters
            Guid softtagID = Guid.NewGuid();
            BitmaskDerivedTag softtag = (BitmaskDerivedTag)DerivedTag.Create(softtagID.ToString(), "bitmask", srcTag.DPDUID + ":1", true);
            Assert.IsTrue(softtag.ID == softtagID);
            Assert.IsTrue(softtag.SourceTag == srcTag.DPDUID);
            Assert.IsTrue(softtag.IsValid == true);
            Assert.IsTrue(softtag.Enabled == true);
            Assert.IsTrue(softtag.Bitmask == 1);

            softtag = (BitmaskDerivedTag)DerivedTag.Create(softtagID.ToString(), "bitmask", srcTag.DPDUID + ":578", true);
            Assert.IsTrue(softtag.ID == softtagID);
            Assert.IsTrue(softtag.SourceTag == srcTag.DPDUID);
            Assert.IsTrue(softtag.IsValid == true);
            Assert.IsTrue(softtag.Enabled == true);
            Assert.IsTrue(softtag.Bitmask == 578);        
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
            DerivedTag.PLCTags = srcTags;

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

            // bitmask 0000 0000 0000 0000 0000 0000 0000 0001
            DerivedTag softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":1", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.Timestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.Quality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 1;
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }

            // bitmask 0000 0000 0000 0000 1100 0000 0000 0001
            softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":49153", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.Timestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.Quality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal & 49153;
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }

            // bitmask 1111 1111 1111 1111 1111 1111 1111 1111
            softTag = DerivedTag.Create(Guid.NewGuid().ToString(), "bitmask", srcTag.DPDUID.ToString() + ":4294967295", true);
            foreach (UInt32 testVal in testValues)
            {
                // update the value in the source tag
                srcTag.LastReadDataValue = testVal;
                srcTag.LastReadQuality = 192;
                srcTag.LastReadDataTimestamp = DateTime.Now;

                // check that the derived tag has the same timestamp and quality as the source tag
                Assert.AreEqual(softTag.Timestamp, srcTag.LastReadDataTimestamp);
                Assert.AreEqual(softTag.Quality, srcTag.LastReadQuality);

                // check that the value of the derived tag is correct
                expectedDerivedVal = testVal;
                Assert.AreEqual(expectedDerivedVal, softTag.Value);
            }

        }

    }
}
