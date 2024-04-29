using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace OGA.TCP_Test_SP
{
    /*
        Unit tests for: cListener

        //  Verify creation without callback...
        //  Test_1_1_1  Create instance without new connection callback.
        //              Verify start call returns error.
        //  Verify the instance can be started...
        //  Test_1_1_2  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //  Verify the listener can be started and stopped...
        //  Test_1_1_3  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //              Call close method.
        //              Verify no exception occurred.
        //  Verify the listener returns error if port is occupied...
        //  Test_1_1_4  Create instance on a port.
        //              Verify start call returns success.
        //              Attempt to create a second instance on the same port.
        //              Verify start method returns error.
        //  Verify happy path to listen and accept a connection...
        //  Test_1_1_5  Create instance and start it.
        //              Create a client and connect to the listener.
        //              Verify the listener new connection callback returns the new connection.
        //  Verify that dispose works...
        //  Test_1_1_6  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //              Call dispose method.
        //              Verify state changes to closed.
        //  Verify closing an already disposed instance has no effect...
        //  Test_1_1_7  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //              Call dispose method.
        //              Call closedown method.
        //              Verify no error and state remains closed.
        //  Verify a disposed instance cannot be started...
        //  Test_1_1_8  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //              Call dispose method.
        //              Attempt to start the instance back up.
        //              Verify an error was returned.

     */

    [DoNotParallelize]
    [TestClass]
    public class Listener_Test : OGA.Testing.Lib.Test_Base_abstract
    {
        #region Setup

        /// <summary>
        /// This will perform any test setup before the first class tests start.
        /// This exists, because MSTest won't call the class setup method in a base class.
        /// Be sure this method exists in your top-level test class, and that it calls the corresponding test class setup method of the base.
        /// </summary>
        [ClassInitialize]
        static public void TestClass_Setup(TestContext context)
        {
            TestClassBase_Setup(context);
        }

        /// <summary>
        /// This will cleanup resources after all class tests have completed.
        /// This exists, because MSTest won't call the class cleanup method in a base class.
        /// Be sure this method exists in your top-level test class, and that it calls the corresponding test class cleanup method of the base.
        /// </summary>
        [ClassCleanup]
        static public void TestClass_Cleanup()
        {
            TestClassBase_Cleanup();
        }

        /// <summary>
        /// Called before each test runs.
        /// Be sure this method exists in your top-level test class, and that it calls the corresponding test setup method of the base.
        /// </summary>
        [TestInitialize]
        override public void Setup()
        {
            //// Push the TestContext instance that we received at the start of the current test, into the common property of the test base class...
            //Test_Base.TestContext = TestContext;
            base.Setup();
            // Runs before each test. (Optional)

            Reset_ReceivedConnectionList();
        }

        /// <summary>
        /// Called after each test runs.
        /// Be sure this method exists in your top-level test class, and that it calls the corresponding test cleanup method of the base.
        /// </summary>
        [TestCleanup]
        override public void TearDown()
        {
            // Runs after each test. (Optional)
        }

        #endregion
        

        #region Tests

        // Verify creation without callback...
        //  Test_1_1_1  Create instance without new connection callback.
        //              Verify start call returns error.
        [TestMethod]
        public void Test_1_1_1()
        {
            OGA.TCP.Server.cListener l1 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;
                //l1.OnNew_Client_Connection = null;

                int res = l1.Start_Listener();
                if (res != -3)
                    Assert.Fail("Failed to start Listener");
            }
            finally
            {
                try
                {
                    l1.Dispose();
                }
                catch (Exception e) { }
            }
        }

        //  Verify the instance can be started...
        //  Test_1_1_2  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        [TestMethod]
        public void Test_1_1_2()
        {
            OGA.TCP.Server.cListener l1 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;
                l1.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                int res = l1.Start_Listener();

                if (res != 1)
                    Assert.Fail("Failed to start Listener");
            }
            finally
            {
                try
                {
                    l1.Dispose();
                }
                catch (Exception e) { }
            }
        }

        //  Verify the listener can be started and stopped...
        //  Test_1_1_3  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //              Call close method.
        //              Verify no exception occurred.
        [TestMethod]
        public void Test_1_1_3()
        {
            OGA.TCP.Server.cListener l1 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;
                l1.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                int res = l1.Start_Listener();
                if (res != 1)
                    Assert.Fail("Failed to start Listener");

                // Close the listener...
                l1.CloseDown_Listener();
            }
            finally
            {
                try
                {
                    l1.Dispose();
                }
                catch (Exception e) { }
            }
        }

        //  Verify the listener returns error if port is occupied...
        //  Test_1_1_4  Create instance on a port.
        //              Verify start call returns success.
        //              Attempt to create a second instance on the same port.
        //              Verify start method returns error.
        [TestMethod]
        public void Test_1_1_4()
        {
            OGA.TCP.Server.cListener l1 = null;
            OGA.TCP.Server.cListener l2 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;
                l1.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                // Start first listener...
                if (l1.Start_Listener() != 1)
                    Assert.Fail("Failed to start Listener");

                // Create second listener on same port...
                l2 = new OGA.TCP.Server.cListener();
                l2.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l2.Listening_Port = 1378;
                l2.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                // Attempt start of second listener...
                if (l2.Start_Listener() != -2)
                    Assert.Fail("Didn't receive expected error from listener.");
            }
            finally
            {
                try
                {
                    l1.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    l2.Dispose();
                }
                catch (Exception e) { }

            }
        }

        //  Verify happy path to listen and accept a connection...
        //  Test_1_1_5  Create instance and start it.
        //              Create a client and connect to the listener.
        //              Verify the listener new connection callback returns the new connection.
        [TestMethod]
        public void Test_1_1_5()
        {
            List<int> receivedlistenerIds = new List<int>();
            // Stand up a listener...
            OGA.TCP.Server.cListener l = new OGA.TCP.Server.cListener();
            l.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
            l.Listening_Port = 1378;
            l.OnNew_Client_Connection = (OGA.TCP.Server.cListener listref, TcpClient newclient) =>
            {
                receivedlistenerIds.Add(listref.InstanceId);
            };

            try
            {
                // Start the listener...
                int res = l.Start_Listener();
                if (res != 1)
                    Assert.Fail("Failed to start Listener");

                // Reset the received id...
                receivedlistenerIds.Clear();

                // Fire off a connection attempt...
                System.Threading.Thread t = new System.Threading.Thread(() =>
                {
                    System.Threading.Thread.Sleep(1000);
                    System.Net.Sockets.TcpClient tc = null;

                    try
                    {
                        // Open a connection to the endpoint.
                        tc = new TcpClient();
                        tc.Connect("127.0.0.1", 1378);

                        System.Threading.Thread.Sleep(100);
                        if (!tc.Connected)
                            Assert.Fail("Failed to gain connection");
                    }
                    finally
                    {
                        try
                        {
                            tc.Close();
                        }
                        catch(Exception e) { }
                    }
                });
                t.IsBackground = true;
                t.Start();

                // Wait for a connection to come back.
                System.DateTime expiry = System.DateTime.Now.AddMilliseconds(5000);
                while (System.DateTime.Now.CompareTo(expiry) < 0)
                {
                    // See if we have received the new connection callback...
                    if(receivedlistenerIds.Contains(l.InstanceId))
                    {
                        // Got our listener's callback.
                        // Leave...
                        return;
                    }

                    System.Threading.Thread.Sleep(50);
                }
                // if here, we failed to get the callback.

                Assert.Fail("Failed to get callback from Listener");
            }
            finally
            {
                try
                {
                    l.Dispose();
                }
                catch (Exception e) { }
            }
        }

        //  Verify that dispose works...
        //  Test_1_1_6  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //              Call dispose method.
        //              Verify state changes to closed.
        [TestMethod]
        public void Test_1_1_6()
        {
            OGA.TCP.Server.cListener l1 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;
                l1.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                int res = l1.Start_Listener();
                if (res != 1)
                    Assert.Fail("Failed to start Listener");

                // Close the listener...
                l1.Dispose();

                if (l1.State != TCP.Server.eListenerState.Closed)
                    Assert.Fail("Failed to start Listener");
            }
            finally
            {
                try
                {
                    l1.Dispose();
                }
                catch (Exception e) { }
            }
        }

        //  Verify closing an already disposed instance has no effect...
        //  Test_1_1_7  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //              Call dispose method.
        //              Call closedown method.
        //              Verify no error and state remains closed.
        [TestMethod]
        public void Test_1_1_7()
        {
            OGA.TCP.Server.cListener l1 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;
                l1.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                int res = l1.Start_Listener();
                if (res != 1)
                    Assert.Fail("Failed to start Listener");

                // Close the listener...
                l1.Dispose();

                if (l1.State != TCP.Server.eListenerState.Closed)
                    Assert.Fail("Failed to start Listener");

                // Attempt to close the listener...
                l1.CloseDown_Listener();

                if (l1.State != TCP.Server.eListenerState.Closed)
                    Assert.Fail("Failed to start Listener");
            }
            finally
            {
                try
                {
                    l1.Dispose();
                }
                catch (Exception e) { }
            }
        }

        //  Verify a disposed instance cannot be started...
        //  Test_1_1_8  Create instance.
        //              Add new connection callback.
        //              Verify start call returns success.
        //              Call dispose method.
        //              Attempt to start the instance back up.
        //              Verify an error was returned.
        [TestMethod]
        public void Test_1_1_8()
        {
            OGA.TCP.Server.cListener l1 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;
                l1.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                int res = l1.Start_Listener();
                if (res != 1)
                    Assert.Fail("Failed to start Listener");

                // Close the listener...
                l1.Dispose();

                if (l1.State != TCP.Server.eListenerState.Closed)
                    Assert.Fail("Failed to start Listener");

                // Attempt to restar tthe listener...
                var ress1 = l1.Start_Listener();
                if (ress1 != -1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    l1.Dispose();
                }
                catch (Exception e) { }
            }
        }

        #endregion


        #region Private Methods

        private void Reset_ReceivedConnectionList()
        {
            newconnection_listing.Clear();
        }
        List<int> newconnection_listing = new List<int>();
        private void CALLBACK_NewConnection_Received(OGA.TCP.Server.cListener listref, TcpClient newclient)
        {
            // A connection was received.
            // Set a value in the connection list so the appropriate test can be notified the connection came back.
            newconnection_listing.Add(listref.InstanceId);
        }

        #endregion
    }
}
