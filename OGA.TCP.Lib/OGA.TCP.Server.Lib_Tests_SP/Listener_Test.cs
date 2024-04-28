using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace OGA.TCP_Test_SP
{
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

        [TestMethod]
        public void Create_without_Callback()
        {
            OGA.TCP.Server.cListener l1 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;

                int res = l1.Start_Listener();

                if (res != -5)
                {
                    Assert.Fail("Failed to start Listener");
                }
            }
            finally
            {
                try
                {
                    l1.CloseDown_Listener();
                }
                catch (Exception e) { }
            }
        }

        [TestMethod]
        public void Create_with_Callback()
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
                {
                    Assert.Fail("Failed to start Listener");
                }
            }
            finally
            {
                try
                {
                    l1.CloseDown_Listener();
                }
                catch (Exception e) { }
            }
        }

        [TestMethod]
        public void Create_and_Close()
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
                {
                    Assert.Fail("Failed to start Listener");
                }
            }
            finally
            {
                try
                {
                    l1.CloseDown_Listener();
                }
                catch (Exception e) { }
            }
        }

        [TestMethod]
        public void Create_two_on_Same_Port()
        {
            OGA.TCP.Server.cListener l1 = null;
            OGA.TCP.Server.cListener l2 = null;
            try
            {
                l1 = new OGA.TCP.Server.cListener();
                l1.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l1.Listening_Port = 1378;
                l1.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                if (l1.Start_Listener() != 1)
                {
                    Assert.Fail("Failed to start Listener");
                }

                l2 = new OGA.TCP.Server.cListener();
                l2.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
                l2.Listening_Port = 1378;
                l2.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

                if (l2.Start_Listener() != -2)
                {
                    Assert.Fail("Didn't receive expected error from listener.");
                }
            }
            finally
            {
                try
                {
                    l1.CloseDown_Listener();
                }
                catch (Exception e) { }
                try
                {
                    l2.CloseDown_Listener();
                }
                catch (Exception e) { }

            }
        }

        [TestMethod]
        public void Connect_and_Receive_Callback()
        {
            OGA.TCP.Server.cListener l = new OGA.TCP.Server.cListener();
            l.Listening_IP = System.Net.IPAddress.Parse("0.0.0.0");
            l.Listening_Port = 1378;
            l.OnNew_Client_Connection = this.CALLBACK_NewConnection_Received;

            try
            {
                int res = l.Start_Listener();

                if (res != 1)
                {
                    Assert.Fail("Failed to start Listener");
                }

                // Fire off a connection attempt.
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
                        {
                            Assert.Fail("Failed to gain connection");
                        }
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

                // wait for a connection to come back.
                System.DateTime expiry = System.DateTime.Now.AddMilliseconds(5000);
                while (System.DateTime.Now.CompareTo(expiry) < 0)
                {
                    // See if we have received the new connection callback.
                    if(this.newconnection_listing.Contains(l.ListenerID))
                    {
                        // Got a callback.
                        // Leave
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
                    l.CloseDown_Listener();
                }
                catch (Exception e) { }
            }
        }

        #endregion


        #region Private Methods

        List<int> newconnection_listing = new List<int>();
        private void CALLBACK_NewConnection_Received(OGA.TCP.Server.cListener listref, TcpClient newclient)
        {
            // A connection was received.
            // Set a value in the connection list so the appropriate test can be notified the connection came back.
            newconnection_listing.Add(listref.ListenerID);
        }

        #endregion
    }
}
