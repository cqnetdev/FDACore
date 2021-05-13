using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;
using Npgsql;
using System.Threading;
using System.Diagnostics;
using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;

namespace Tests
{
    [TestClass]
    public class DataBaseTests
    {
  
        [TestMethod]
        public void PostgresQuery()
        {
            DataTable result = new DataTable();
            //result = FDA.PG_DBManager.ExecuteQuery("select * from FDASourceConnections",connectionstring);
            Assert.AreEqual(result.Rows.Count, 13, "wrong number of rows (" + result.Rows.Count + " in results, expected 13");
        }
    }

    [TestClass]
    public class PostgreSQLListenerTests
    {
        Guid rowID = new Guid("c3d43f69-bee6-445b-8ba1-ff6172e0427a");
        string currentTest = "";
        FDADataBlockRequestGroup expectedRequestGroupResult;
        FDADataBlockRequestGroup requestGroupResult;
        FDATask expectedTask;
        FDATask resultingTask;
        RocEventFormats expectedREF;
        RocEventFormats resultingREF;
        string resultOperation;

        bool waiting = false;
        readonly int waitlimitms = 3000;

        /***************************************************   RequestGroup **********************************************/
        #region RequestGroup
        [TestMethod]
        public void FDADataBlockRequestGroup()
        {
            RequestGroupInsert();

            RequestGroupUpdate();

            RequestGroupDelete();
        }

        public void RequestGroupInsert()
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
            listener.Notification += Listener_NotificationRequestGroup;
            listener.StartListening();

            currentTest = "INSERT";
            expectedRequestGroupResult = new FDADataBlockRequestGroup()
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

                    CompareResult(expectedRequestGroupResult, requestGroupResult);
                }

            }

        }

        
        public void RequestGroupUpdate()
        {
            // start listening for changes
            PostgreSQLListener<FDADataBlockRequestGroup> listener = new PostgreSQLListener<FDADataBlockRequestGroup>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA; Keepalive = 1;", "fdadatablockrequestgroup");
            listener.Notification += Listener_NotificationRequestGroup;
            listener.StartListening();

            currentTest = "UPDATE";
            expectedRequestGroupResult = new FDADataBlockRequestGroup()
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
                string query = "update fdadatablockrequestgroup set description = '" + expectedRequestGroupResult.Description + "',DRGEnabled = cast(" + (expectedRequestGroupResult.DRGEnabled ? 1 : 0) + " as bit),commslogenabled = cast(" + (expectedRequestGroupResult.CommsLogEnabled ? 1 : 0) + " as bit),DPSTYpe='" + expectedRequestGroupResult.DPSType + "',DataPointBlockRequestListVals='" + expectedRequestGroupResult.DataPointBlockRequestListVals + "',drguid= cast('" + expectedRequestGroupResult.DRGUID.ToString() + "' as uuid) where drguid = cast('" + rowID.ToString() + "' as uuid);";
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

                    CompareResult(expectedRequestGroupResult, requestGroupResult);
                }

            }

        }


        public void RequestGroupDelete()
        {
            // start listening for changes
            PostgreSQLListener<FDADataBlockRequestGroup> listener = new PostgreSQLListener<FDADataBlockRequestGroup>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA; Keepalive = 1;", "fdadatablockrequestgroup");
            listener.Notification += Listener_NotificationRequestGroup;
            listener.StartListening();

            currentTest = "DELETE";
            expectedRequestGroupResult = new FDADataBlockRequestGroup()
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

                    if (expectedRequestGroupResult.DRGUID != requestGroupResult.DRGUID)
                    {
                        Assert.Fail("Unexpected DRGUID. expected " + expectedRequestGroupResult.DRGUID.ToString() + ", received " + requestGroupResult.DRGUID.ToString());
                    }
                }

            }

        }

        private void Listener_NotificationRequestGroup(object sender, PostgreSQLListener<FDADataBlockRequestGroup>.PostgreSQLNotification notifyEvent)
        {
            resultOperation = notifyEvent.Notification.operation;
            requestGroupResult = notifyEvent.Notification.row;
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
        #endregion


        /************************************************* FDATasks **************************************************/
        #region tasks

        [TestMethod]
        public void FDATasks()
        {
            TaskInsert();

            TaskUpdate();

            TaskDelete();
        }

        public void TaskInsert()
        {
            // create a FDATask
            FDATask original = new FDATask()
            {
                TASK_ID = rowID,
                task_type = "task type",
                task_details = "task details"
            };


            // start listening for changes
            PostgreSQLListener<FDATask> listener = new PostgreSQLListener<FDATask>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA; Keepalive = 1;", "fdatasks");
            listener.Notification += Listener_NotificationTasks;
            listener.StartListening();

            currentTest = "INSERT";

            expectedTask = new FDATask()
            {
                TASK_ID = rowID,
                task_type = "task type",
                task_details = "task details"
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA;Keepalive=1"))
            {
                conn.Open();
                string query = "insert into fdatasks (task_id,task_type,task_details) values (cast('" + original.TASK_ID + "' as uuid),'" + original.task_type + "','" + original.task_details + "');";
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

                    CompareTasks(expectedTask, resultingTask);
                }

            }

        }

        
        public void TaskUpdate()
        {
            // start listening for changes
            PostgreSQLListener<FDATask> listener = new PostgreSQLListener<FDATask>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA; Keepalive = 1;", "fdatasks");
            listener.Notification += Listener_NotificationTasks;
            listener.StartListening();

            currentTest = "UPDATE";
            expectedTask = new FDATask()
            {
                TASK_ID= rowID,
                task_details = "task details changed",
                task_type = "task type changed",
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA;Keepalive=1"))
            {
                conn.Open();
                string query = "update fdatasks set task_details = '" + expectedTask.task_details + "',task_type = '" + expectedTask.task_type +"' where task_id = cast('" + rowID.ToString() + "' as uuid);";
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

                    CompareTasks(expectedTask, resultingTask);
                }

            }

        }


        public void TaskDelete()
        {
            // start listening for changes
            PostgreSQLListener<FDATask> listener = new PostgreSQLListener<FDATask>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA; Keepalive = 1;", "fdatasks");
            listener.Notification += Listener_NotificationTasks;
            listener.StartListening();

            currentTest = "DELETE";
            expectedTask = new FDATask()
            {
                TASK_ID = rowID,
                task_details = null,
                task_type = null
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDA;Keepalive=1"))
            {
                conn.Open();
                string query = "delete from FDATasks where task_id = cast('" + rowID.ToString() + "' as uuid);";
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
                    Assert.Fail("delete notification not received");
                }
                else
                {
                    if (currentTest != resultOperation)
                        Assert.Fail("Unexpected operation '" + resultOperation + "', expected '" + currentTest + "'");

                    if (expectedTask.TASK_ID != resultingTask.TASK_ID)
                    {
                        Assert.Fail("Unexpected DRGUID. expected " + expectedRequestGroupResult.DRGUID.ToString() + ", received " + requestGroupResult.DRGUID.ToString());
                    }
                }

            }

        }

        private void Listener_NotificationTasks(object sender, PostgreSQLListener<FDATask>.PostgreSQLNotification notifyEvent)
        {
            resultOperation = notifyEvent.Notification.operation;
            resultingTask = notifyEvent.Notification.row;
            waiting = false;
        }


        private void CompareTasks(FDATask expected, FDATask received)
        {
          
            if (expected.TASK_ID != received.TASK_ID)
            {
                Assert.Fail("Unexpected task_id. expected " + expected.TASK_ID + ", received " + received.TASK_ID);
            }

            if (expected.task_type != received.task_type)
            {
                Assert.Fail("Unexpected task_type. expected " + expected.task_type + ", received " + received.task_type);

            }
            if (expected.task_details != received.task_details)
            {
                Assert.Fail("Unexpected DRGEnabled. expected " + expected.task_details + ", received " + received.task_details);
            }

        }
        #endregion


        /***************************************************** ROCEventFormats ********************************************/
        #region roceventformats
        [TestMethod]
        public void ROCEventFormats()
        {
            REFInsert();

            REFUpdate();

            REFDelete();
        }

        public void REFInsert()
        {
            // create an REF
            RocEventFormats original = new RocEventFormats()
            {
                POINTTYPE = 1,
                FORMAT = 2,
                DescShort = "FAKE",
                DescLong = "FAKE FORMAT"
            };


            // start listening for changes
            PostgreSQLListener<RocEventFormats> listener = new PostgreSQLListener<RocEventFormats>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDASystem; Keepalive = 1;", "roceventformats");
            listener.Notification += Listener_NotificationREF;
            listener.StartListening();

            currentTest = "INSERT";

            expectedREF = new RocEventFormats()
            {
                POINTTYPE = 1,
                FORMAT = 2,
                DescShort = "FAKE",
                DescLong = "FAKE FORMAT"
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDASystem;Keepalive=1"))
            {
                conn.Open();
                string query = "insert into RocEventFormats (pointtype,format,descshort,desclong) values (1,2,'FAKE','FAKE FORMAT');";
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

                    CompareREFs(expectedREF, resultingREF);
                }

            }

        }


        public void REFUpdate()
        {
            // start listening for changes
            PostgreSQLListener<FDATask> listener = new PostgreSQLListener<FDATask>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDASystem; Keepalive = 1;", "fdatasks");
            listener.Notification += Listener_NotificationTasks;
            listener.StartListening();

            currentTest = "UPDATE";
            expectedREF = new RocEventFormats()
            {
                POINTTYPE = 1,
                FORMAT = 2,
                DescShort = "FAKE-updated",
                DescLong = "FAKE FORMAT-updated"           
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDASystem;Keepalive=1"))
            {
                conn.Open();
                string query = "update roceventformats set descshort = 'FAKE-updated',desclong='FAKE FORMAT-updated' where pointtype = 1 and FORMAT = 2";
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

                    CompareREFs(expectedREF, resultingREF);
                }

            }

        }


        public void REFDelete()
        {
            // start listening for changes
            PostgreSQLListener<FDATask> listener = new PostgreSQLListener<FDATask>("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDASystem; Keepalive = 1;", "roceventformats");
            listener.Notification += Listener_NotificationTasks;
            listener.StartListening();

            currentTest = "DELETE";
            expectedTask = new FDATask()
            {
                TASK_ID = rowID,
                task_details = null,
                task_type = null
            };

            using (NpgsqlConnection conn = new NpgsqlConnection("Server = localhost; Port = 5432; User Id = Intricatesql; Password = Intricate2790!; Database = FDASystem;Keepalive=1"))
            {
                conn.Open();
                string query = "delete from roceventformats where pointtype=1 and format=2;";
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
                    Assert.Fail("delete notification not received");
                }
                else
                {
                    if (currentTest != resultOperation)
                        Assert.Fail("Unexpected operation '" + resultOperation + "', expected '" + currentTest + "'");

                    if (expectedREF.POINTTYPE != resultingREF.POINTTYPE)
                    {
                        Assert.Fail("Unexpected POINTTYPE. expected " + expectedREF.POINTTYPE.ToString() + ", received " + resultingREF.POINTTYPE.ToString());
                    }

                    if (expectedREF.FORMAT != resultingREF.FORMAT)
                    {
                        Assert.Fail("Unexpected FORMAT. expected " + expectedREF.FORMAT.ToString() + ", received " + resultingREF.FORMAT.ToString());
                    }

                    
                }

            }

        }

        private void Listener_NotificationREF(object sender, PostgreSQLListener<RocEventFormats>.PostgreSQLNotification notifyEvent)
        {
            resultOperation = notifyEvent.Notification.operation;
            resultingREF = notifyEvent.Notification.row;
            waiting = false;
        }


        private void CompareREFs(RocEventFormats expected, RocEventFormats received)
        {

            if (expected.POINTTYPE != received.POINTTYPE)
            {
                Assert.Fail("Unexpected POINTTYPE. expected " + expected.POINTTYPE + ", received " + received.POINTTYPE);
            }

            if (expected.FORMAT != received.FORMAT)
            {
                Assert.Fail("Unexpected FORMAT. expected " + expected.FORMAT + ", received " + received.FORMAT);

            }
            if (expected.DescLong != received.DescLong)
            {
                Assert.Fail("Unexpected DescLong. expected " + expected.DescLong + ", received " + received.DescLong);
            }

            if (expected.DescShort != received.DescShort)
            {
                Assert.Fail("Unexpected DescShort. expected " + expected.DescShort + ", received " + received.DescShort);
            }

        }

        #endregion

    }


}
