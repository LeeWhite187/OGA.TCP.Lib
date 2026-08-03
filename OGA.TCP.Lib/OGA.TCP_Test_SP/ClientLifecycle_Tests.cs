using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP.SessionLayer;
using Testing_CommonHelpers_SP.Helpers;

namespace OGA.TCP_Test_SP
{
    /*  Unit Tests for: Client instance lifecycle (single-use contract) and pre-connect metrics.
        Description:    These tests exercise the single-use lifecycle of the v1 tcpsocket client:
                        instances are started once; Stop_Async is terminal; Start_Async refuses after a stop or dispose.
                        They also verify a consumer-initiated stop does not raise the connection-lost delegate,
                        and that metrics can be read before any connection exists.
                        A raw, silent loopback listener provides the server side where a connection is needed.


        // Verify metrics are readable before any connection is made...
        //  Test_1_1_1  Create an instance of the v1 tcpsocket client.
        //              Read the Metrics property, without ever starting the client.
        //              Verify a baseline (empty) metrics snapshot is returned, rather than an exception.

        // Verify the single-use lifecycle refusals...
        //  Test_1_2_1  Create an instance of the v1 tcpsocket client.
        //              Start it against the loopback listener, and verify it connects.
        //              Call Start_Async a second time, and verify it is refused.
        //              Stop the client.
        //              Call Start_Async again, and verify it is refused.
        //  Test_1_2_2  Create an instance of the v1 tcpsocket client.
        //              Stop it without ever starting it.
        //              Verify a subsequent Start_Async is refused.
        //  Test_1_2_3  Create an instance of the v1 tcpsocket client.
        //              Dispose it.
        //              Verify a subsequent Start_Async is refused.

        // Verify consumer-initiated stop raises no connection-lost event...
        //  Test_1_3_1  Create an instance of the v1 tcpsocket client, with keepalive disabled.
        //              Hookup the connection lost delegate.
        //              Start it against the loopback listener, and verify it connects.
        //              Stop the client.
        //              Verify the connection lost delegate was never called.
        //              Verify the client reports not connected after the stop.
     */

    [DoNotParallelize]
    [TestClass]
    public class ClientLifecycle_Tests : OGA.Testing.Lib.Test_Base_abstract
    {
        #region Private Fields

        protected string RemoteHost = "127.0.0.1";
        protected int RemotePort = 5097;

        private TESTINGSRVR_cListener _listener;
        private List<TcpClient> _acceptedclients;

        #endregion


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
            base.Setup();
            // Runs before each test. (Optional)

            this._acceptedclients = new List<TcpClient>();

            // Stand up a raw listener that accepts connections, but never sends anything back...
            this._listener = new TESTINGSRVR_cListener();
            this._listener.Listening_IP = IPAddress.Parse(RemoteHost);
            this._listener.Listening_Port = RemotePort;
            this._listener.OnNew_Client_Connection = this.CALLBACK_NewConnection;
            var res = this._listener.Start_Listener();
            if (res != 1)
                Assert.Fail("Failed to start the loopback listener.");
        }

        /// <summary>
        /// Called after each test runs.
        /// Be sure this method exists in your top-level test class, and that it calls the corresponding test cleanup method of the base.
        /// </summary>
        [TestCleanup]
        override public void TearDown()
        {
            // Runs after each test. (Optional)

            try
            {
                this._listener?.CloseDown_Listener();
            }
            catch (Exception) { }
            this._listener = null;

            try
            {
                foreach (var c in this._acceptedclients)
                {
                    try { c?.Close(); } catch (Exception) { }
                }
                this._acceptedclients.Clear();
            }
            catch (Exception) { }

            base.TearDown();
        }

        #endregion


        #region Tests

        //  Test_1_1_1  Read the Metrics property without ever starting the client, and verify a baseline snapshot returns.
        [TestMethod]
        public void Test_1_1_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                // The v3 registration contract requires app identity...
                wss.AppId = "aid-test";
                wss.AppVersion = "1.0.0";

                // Read metrics before any connection exists...
                // This must return a baseline snapshot, not throw.
                var met = wss.Metrics;

                if (met == null)
                    Assert.Fail("Wrong Value");

                if (met.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");
                if (met.Sent_Message_Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }

        //  Test_1_2_1  Verify a running client refuses a second start, and a stopped client refuses restart.
        [TestMethod]
        public async Task Test_1_2_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                // The v3 registration contract requires app identity...
                wss.AppId = "aid-test";
                wss.AppVersion = "1.0.0";

                // The silent server never sends a registration reply, so don't wait on one...
                wss.Cfg_ConnectionWaitsforRegistrationReply = false;
                // Keepalive is irrelevant to this test; keep the connection quiet...
                wss.Cfg_Disable_KeepAlive = true;

                var res = await wss.Start_Async();
                if (res != 1)
                    Assert.Fail("Wrong return");

                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);

                if (!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // A second start on a running instance must be refused...
                var res2 = await wss.Start_Async();
                if (res2 != -1)
                    Assert.Fail("Wrong return");

                // Stop the client...
                await wss.Stop_Async();

                // A start after a stop must be refused: instances are single-use...
                var res3 = await wss.Start_Async();
                if (res3 != -1)
                    Assert.Fail("Wrong return");
            }
            finally
            {
                wss?.Dispose();
            }
        }

        //  Test_1_2_2  Verify a never-started, stopped client refuses a start.
        [TestMethod]
        public async Task Test_1_2_2()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                // The v3 registration contract requires app identity...
                wss.AppId = "aid-test";
                wss.AppVersion = "1.0.0";

                // Stop the client without ever starting it...
                await wss.Stop_Async();

                // A start after a stop must be refused: instances are single-use...
                var res = await wss.Start_Async();
                if (res != -1)
                    Assert.Fail("Wrong return");
            }
            finally
            {
                wss?.Dispose();
            }
        }

        //  Test_1_2_3  Verify a disposed client refuses a start.
        [TestMethod]
        public async Task Test_1_2_3()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;

            var wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);

            // The v3 registration contract requires app identity...

            wss.AppId = "aid-test";

            wss.AppVersion = "1.0.0";
            wss.Dispose();

            var res = await wss.Start_Async();
            if (res != -1)
                Assert.Fail("Wrong return");
        }

        //  Test_1_3_1  Verify a consumer-initiated stop raises no connection-lost event.
        [TestMethod]
        public async Task Test_1_3_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                int losscounter = 0;

                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);

                // The v3 registration contract requires app identity...

                wss.AppId = "aid-test";

                wss.AppVersion = "1.0.0";
                // Assign a local lambda that will increment a counter each time it triggers...
                wss.OnConnectionLost = ((locws) =>
                {
                    losscounter++;
                });

                // The silent server never sends a registration reply, so don't wait on one...
                wss.Cfg_ConnectionWaitsforRegistrationReply = false;
                // Disable keepalive, so the quiet server cannot trip a pong timeout during the test...
                wss.Cfg_Disable_KeepAlive = true;

                var res = await wss.Start_Async();
                if (res != 1)
                    Assert.Fail("Wrong return");

                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);

                if (!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Stop the client: this is a consumer-initiated stop, not a connection loss...
                await wss.Stop_Async();

                // Give any stray teardown callbacks a moment to fire, if they were going to...
                await Task.Delay(500);

                // The connection lost delegate must not have been called...
                if (losscounter != 0)
                    Assert.Fail("Connection-lost delegate fired on a consumer-initiated stop.");

                // And the client must report not connected...
                if (wss.IsConnected)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }

        #endregion


        #region Callbacks

        private void CALLBACK_NewConnection(TESTINGSRVR_cListener l, TcpClient newclient)
        {
            // Keep a reference to each accepted connection, so it stays open (but silent) for the duration of the test...
            this._acceptedclients.Add(newclient);
        }

        #endregion
    }
}
