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
    /*  Unit Tests for: Client-side keepalive (ping-pong) logic.
        Description:    These tests exercise the client-side keepalive logic of the Client_v1_Abstract session layer, using the concrete TCPClient_v1_Impl.
                        These tests use a raw, silent tcp listener for the server side (instead of the TESTINGSRVR endpoint classes),
                        so that no registration reply, pong, or any other traffic is ever returned to the client.
                        This isolates the client's dead-connection declaration logic from server-side keepalive behavior.
        NOTE:           The server-side dead-client timeout (a silent client dropped by the server) is covered by tests in TCPClient_v1_Tests.
                        These tests cover the reverse direction: a silent server declared dead by the client.


        // Verify the client recycles its connection when a keepalive ping receives no reply...
        //  Test_1_1_1  Stand up a raw tcp listener that accepts connections, but never sends anything back.
        //              Create an instance of the v1 tcpsocket client.
        //              Configure the client to not wait on a registration reply, so the silent server does not trip the registration timeout.
        //              Shorten the client's keepalive interval, to keep the test brief.
        //              Hookup the connection lost delegate.
        //              Connect the client to the silent listener.
        //              Verify the client indicates connected.
        //              Wait long enough for a ping to be sent, and for its reply deadline to lapse.
        //              (The reply deadline is floored at two connection-loop ticks after the ping went out.)
        //              Verify the connection lost delegate was called, indicating the client declared the dead connection and recycled it.

     */

    [DoNotParallelize]
    [TestClass]
    public class KeepAlive_Tests : OGA.Testing.Lib.Test_Base_abstract
    {
        #region Private Fields

        protected string RemoteHost = "127.0.0.1";
        protected int RemotePort = 5098;

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
                Assert.Fail("Failed to start the silent listener.");
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

        // Verify the client recycles its connection when a keepalive ping receives no reply...
        //  Test_1_1_1  Stand up a raw tcp listener that accepts connections, but never sends anything back.
        //              Create an instance of the v1 tcpsocket client.
        //              Configure the client to not wait on a registration reply, so the silent server does not trip the registration timeout.
        //              Shorten the client's keepalive interval, to keep the test brief.
        //              Hookup the connection lost delegate.
        //              Connect the client to the silent listener.
        //              Verify the client indicates connected.
        //              Wait long enough for a ping to be sent, and for its reply deadline to lapse.
        //              (The reply deadline is floored at two connection-loop ticks after the ping went out.)
        //              Verify the connection lost delegate was called, indicating the client declared the dead connection and recycled it.
        [TestMethod]
        public async Task Test_1_1_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                int losscounter = 0;

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                // The v3 registration contract requires app identity...
                wss.AppId = "aid-test";
                wss.AppVersion = "1.0.0";
                // Assign a local lambda that will increment a counter each time it triggers...
                wss.OnConnectionLost = ((locws) =>
                {
                    losscounter++;
                });

                // The silent server will never send a registration reply, so tell the client not to wait on one.
                // Otherwise, the registration reply timeout would recycle the connection, and mask the keepalive logic under test...
                wss.Cfg_ConnectionWaitsforRegistrationReply = false;

                // Shorten the keepalive interval, to keep the test brief...
                // NOTE: This also lowers the connection inner-loop delay to 3 seconds.
                wss.Cfg_KeepAliveInterval = 3;

                // Start the tcpsocket client...
                var res = await wss.Start_Async();
                if (res != 1)
                    Assert.Fail("Wrong return");

                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);

                // Verify the connection is active...
                if (!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Verify the loss counter is zero...
                if (losscounter != 0)
                    Assert.Fail("Wrong Value");

                // The client will send a ping once the connection has been quiet for the keepalive interval.
                // The silent server never answers it.
                // The client is expected to declare the connection dead once the ping's reply deadline lapses.
                // The deadline is floored at two connection-loop ticks (2 x 3 seconds, here) after the ping went out.
                // So, we allow a generous wait for: ping send (~3s) + reply floor (~6s) + a few loop ticks of slack...
                WaitforCondition(() => losscounter >= 1, 20000);

                // Verify the client declared the connection lost, and recycled it...
                if (losscounter < 1)
                    Assert.Fail("Client failed to declare a dead connection after an unanswered keepalive ping.");
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
