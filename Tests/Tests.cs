using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;
using Npgsql;
using System.Threading;
using System.Diagnostics;
using System;

namespace Tests
{
    [TestClass]
    public class PostgreSQLListenerTests
    {
        Guid rowID = new Guid("c3d43f69-bee6-445b-8ba1-ff6172e0427a");
        string currentTest = "";
        FDADataBlockRequestGroup expectedResult;
        FDADataBlockRequestGroup result;
        string resultOperation;

        bool waiting = false;
        int waitlimitms = 3000;

       [TestMethod]
       public void PostgresListener()
        {
            PostgresListenerInsert();

            PostgresListenerUpdate();

            PostgresListenerDelete();
        }

        public void PostgresListenerInsert()
        {
            // create a FDADataBlockrequestGroup
            FDADataBlockRequestGroup original = new FDADataBlockRequestGroup()
            {
                Description = "FDATestsGroup",
                DRGEnabled = false,
                DPSType = "MODBUS",
                DataPointBlockRequestListVals = "20:3:1|INT16:4BB8D0AF-0DC8-437E-A0F3-0B773A7B0083",
                CommsLogEnabled = true,
                DRGUID = rowID,
            };


            // start listening for changes
            PostgreSQLListener<FDADataBlockRequestGroup> listener = new PostgreSQLListener<FDADataBlockRequestGroup>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA; Keepalive = 1;", "fdadatablockrequestgroup");
            listener.Notification += Listener_Notification;
            listener.StartListening();

            currentTest = "INSERT";
            expectedResult = new FDADataBlockRequestGroup()
            {
                Description = "FDATestsGroup",
                DRGEnabled = false,
                DPSType = "MODBUS",
                DataPointBlockRequestListVals = "20:3:1|INT16:4BB8D0AF-0DC8-437E-A0F3-0B773A7B0083",
                CommsLogEnabled = true,
                DRGUID = rowID,
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA;Keepalive=1"))
            {
                conn.Open();
                string query = "insert into fdadatablockrequestgroup (description,drgenabled,commslogenabled,dpstype,datapointblockrequestlistvals,drguid) values ('" + original.Description + "',cast(" + (original.DRGEnabled ? 1 : 0) +  " as bit),cast(" + (original.CommsLogEnabled?1:0) + " as bit),'MODBUS','20:3:1|INT16:4BB8D0AF-0DC8-437E-A0F3-0B773A7B0083',cast('" + original.DRGUID.ToString() + "' as uuid));";
                using (NpgsqlCommand command = new NpgsqlCommand(query, conn))
                {
                    waiting = true;
                    command.ExecuteNonQuery();
                }

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while (waiting && stopwatch.ElapsedMilliseconds < waitlimitms)
                {
                    Thread.Sleep(100);
                }
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds >= waitlimitms)
                {
                    Assert.Fail("Notification not received");
                }
                else
                {
                    if (currentTest != resultOperation)
                        Assert.Fail("Unexpected operation '" + resultOperation + "', expected '" + currentTest + "'");

                    CompareResult(expectedResult, result);
                }

            }

        }


        public void PostgresListenerUpdate()
        {
            // start listening for changes
            PostgreSQLListener<FDADataBlockRequestGroup> listener = new PostgreSQLListener<FDADataBlockRequestGroup>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA; Keepalive = 1;", "fdadatablockrequestgroup");
            listener.Notification += Listener_Notification;
            listener.StartListening();

            currentTest = "UPDATE";
            expectedResult = new FDADataBlockRequestGroup()
            {
                Description = "FDATestsGroup",
                DRGEnabled = true,
                DPSType = "ROC",
                DataPointBlockRequestListVals = "20:4:1|INT16:4BB8D0AF-0DC8-437E-A0F3-0B773A7B0083",
                CommsLogEnabled = false,
                DRGUID = rowID
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA;Keepalive=1"))
            {
                conn.Open();
                string query = "update fdadatablockrequestgroup set description = '" + expectedResult.Description + "',DRGEnabled = cast(" + (expectedResult.DRGEnabled ? 1 : 0) + " as bit),commslogenabled = cast(" + (expectedResult.CommsLogEnabled ? 1 : 0) + " as bit),DPSTYpe='" + expectedResult.DPSType + "',DataPointBlockRequestListVals='" + expectedResult.DataPointBlockRequestListVals + "',drguid= cast('" + expectedResult.DRGUID.ToString() + "' as uuid) where drguid = cast('" + rowID.ToString() + "' as uuid);";
                using (NpgsqlCommand command = new NpgsqlCommand(query, conn))
                {
                    waiting = true;
                    command.ExecuteNonQuery();
                }

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while (waiting && stopwatch.ElapsedMilliseconds < waitlimitms)
                {
                    Thread.Sleep(100);
                }
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds >= waitlimitms)
                {
                    Assert.Fail("Notification not received");
                }
                else
                {
                    if (currentTest != resultOperation)
                        Assert.Fail("Unexpected operation '" + resultOperation + "', expected '" + currentTest + "'");

                    CompareResult(expectedResult, result);
                }

            }

        }


        public void PostgresListenerDelete()
        {
            // start listening for changes
            PostgreSQLListener<FDADataBlockRequestGroup> listener = new PostgreSQLListener<FDADataBlockRequestGroup>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA; Keepalive = 1;", "fdadatablockrequestgroup");
            listener.Notification += Listener_Notification;
            listener.StartListening();

            currentTest = "DELETE";
            expectedResult = new FDADataBlockRequestGroup()
            {
                Description = "FDATestsGroup",
                DRGEnabled = true,
                DPSType = "ROC",
                DataPointBlockRequestListVals = "20:4:1|INT16:4BB8D0AF-0DC8-437E-A0F3-0B773A7B0083",
                CommsLogEnabled = false,
                DRGUID = rowID
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA;Keepalive=1"))
            {
                conn.Open();
                string query = "delete from fdadatablockrequestgroup where drguid = cast('" + rowID.ToString() + "' as uuid);";
                using (NpgsqlCommand command = new NpgsqlCommand(query, conn))
                {
                    waiting = true;
                    command.ExecuteNonQuery();
                }

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while (waiting && stopwatch.ElapsedMilliseconds < waitlimitms)
                {
                    Thread.Sleep(100);
                }
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds >= waitlimitms)
                {
                    Assert.Fail("Notification not received");
                }
                else
                {
                    if (currentTest != resultOperation)
                        Assert.Fail("Unexpected operation '" + resultOperation + "', expected '" + currentTest + "'");

                    CompareResult(expectedResult, result);
                }

            }

        }

        private void Listener_Notification(object sender, PostgreSQLListener<FDADataBlockRequestGroup>.PostgreSQLNotification notifyEvent)
        {
            resultOperation = notifyEvent.Notification.operation;
            result = notifyEvent.Notification.row;
            waiting = false;   
        }

        private void CompareResult(FDADataBlockRequestGroup expected,FDADataBlockRequestGroup received)
        {
          
            if (expected.Description != received.Description)
            {
                Assert.Fail("Unexpected description. expected " + expected.Description + ", received " + received.Description);
            }

            if (expected.DPSType != received.DPSType)
            {
                Assert.Fail("Unexpected DPSType. expected " + expected.DPSType + ", received " + received.DPSType);

            }
            if (expected.DRGEnabled != received.DRGEnabled)
            {
                Assert.Fail("Unexpected DRGEnabled. expected " + expected.DRGEnabled + ", received " + received.DRGEnabled);
            }

            if (expected.CommsLogEnabled != received.CommsLogEnabled)
            {
                Assert.Fail("Unexpected CommsLogEnabled. expected " + expected.CommsLogEnabled + ", received " + received.CommsLogEnabled);
            }

            if (expected.DataPointBlockRequestListVals != received.DataPointBlockRequestListVals)
            {
                Assert.Fail("Unexpected DataPointBlockRequestListVals. expected " + expected.DataPointBlockRequestListVals + ", received " + received.DataPointBlockRequestListVals);
            }

            if (expected.DRGUID != received.DRGUID)
            {
                Assert.Fail("Unexpected DRGUID. expected " + expected.DRGUID.ToString() + ", received " + received.DRGUID.ToString());
            }

        }

    }
}
