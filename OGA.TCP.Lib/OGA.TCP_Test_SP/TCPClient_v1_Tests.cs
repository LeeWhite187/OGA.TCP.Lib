using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP.Messages;
using OGA.TCP.Server;
using OGA.TCP.Server.Model;
using OGA.TCP.SessionLayer;
using OGA.TCP.Shared.Encoding;
using Testing.WSEndpoint_Tests.HelperClasses;
using Testing_CommonHelpers_SP.Helpers;
using WSEndpoint_Tests.HelperClasses;

namespace OGA.TCP_Test_SP
{
    /*  Unit Tests for: TCPClient_v1
        Description:    These unit tests exercise the logic of the TCPClient_v1 concrete class.
                        This test class uses copies of the actual server-side, TCPEndpoint class (and dependencies) for the server-side connection.
                        This ensures compatibility with client code and existing server codebases.
        NOTE:           The reason these are copies of the server-side classes, is that the server project is in NET7.
                        So, we have copied the working set, and decorated the class names to avoid confusion with the live versions.
        NOTE:           For setting up each server-side endpoint, these tests use a simple tcp listener, and not the actual TCP connection manager.
                        This simplifies testing as we are concentrating on tcpclient and tcpendpoint integration.


        // Verify happy path of opening a client, and the client closing the connection...
        //  Test_1_1_1  Create an instance of the v1 tcpsocket client.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Close the client connection.
        //              Verify the client indicates closed.
        //              Verify the server endpoint indicates closed.
                        
        // Verify happy path of opening a client, and the server closing the connection...
        //  Test_1_1_2  Create an instance of the v1 tcpsocket client.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Close the server endpoint connection.
        //              Verify the client indicates closed.
        //              Verify the server endpoint indicates closed.


        // Verify that the closed connection delegate triggers only once on a client-side closure...
        //  Test_1_1_3  Create an instance of the v1 tcpsocket client.
        //              Hookup the connection lost delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Close the client connection.
        //              Verify the client indicates closed.
        //              Verify the server endpoint indicates closed.


        // Verify that the closed connection delegate triggers only once on a server-side closure...
        //  Test_1_1_4  Create an instance of the v1 tcpsocket client.
        //              Hookup the connection lost delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Close the server-side connection.
        //              Verify the connection closed delegate is called.
        //              Verify the client closes and reopens a new connection.
        //              Verify the server endpoint indicates open after the client has recycled its connection.

        // Verify client without channels receives a non-channel message...
        //  Test_1_1_5  Create an instance of the v1 tcpsocket client.
        //              Hookup the non-channel message delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              From the server-side, send a message without a channel to the client.
        //              Verify the client receives the message on the non-channel delegate.

        // Verify client ping functionality...
        //  Test_1_1_6  Create an instance of the v1 tcpsocket client.
        //              Shorten the client's ping duration.
        //              Connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Wait long enough for the ping message to have been sent.
        //              Verify the client and server both indicate a message exchanged.

        // Verify client gets closed after a failed keepalive...
        //  Test_1_1_7  Create an instance of the v1 tcpsocket client.
        //              Set the client to use a very long keepalive interval.
        //              Hookup the connection lost delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Set the server to use a shorter interval.
        //              Wait long enough for the server to timeout the connection for lack of keepalive or traffic.
        //              Verify the connection closed delegate is called.
        //              Verify the client closes and reopens a new connection.
        //              Verify the server endpoint indicates open after the client has recycled its connection.


        // Verify client can received a channel message...
        //  Test_1_2_1  Create an instance of the v1 tcpsocket client.
        //              Hookup a channel message delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              From the server-side, send the client a message assigned to the registered channel.
        //              Verify the client receives the message on the channel delegate.

        // Verify registered loopack echoes all messages...
        //  Test_1_2_2  Create an instance of the v1 tcpsocket client.
        //              Hookup a channel message delegate.
        //              Tell the client to request message loopback.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Have the client send a channel message to the server.
        //              Verify the client receives the message on the channel delegate.

        // Verify client can send message without a channel, and is received...
        //  Test_1_2_3  Create an instance of the v1 tcpsocket client.
        //              Hookup a non-channel message delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Send a non-channel message to the server.
        //              Verify the server receives the message on the non-channel delegate.

        // Verify client can send message on a channel, and it is received...
        //  Test_1_2_4  Create an instance of the v1 tcpsocket client.
        //              Hookup a channel message delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Send a channel message to the server.
        //              Verify the server receives the message on the channel delegate.


        // Verify the client can request to disable keepalive, and is not closed after timeout...
        //  Test_1_3_1  Create an instance of the v1 tcpsocket client.
        //              Set the client to request keepalive to be disabled.
        //              Hookup the connection lost delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Set the server to use a short keepalive interval.
        //              Wait long enough for the server to normally timeout the connection for lack of keepalive or traffic.
        //              Verify the connection remains open.
     

        // Verify client with an InternetAvailable override will spin-wait while internet is not available, and then attempt connection when available....
        //  Test_1_4_1  Create an instance of the v1 tcpsocket client that lets us inject network state.
        //              Begin our testing with the simulated network state as offline.
        //              Start the tcpsocket client.
        //              Wait a duration long enough that the client would have attempted connection.
        //              Verify the client did not attempt connection, by checking that the connection attempt counter stays at zero.
        //              Inject an online network state, so the client will attempt connection.
        //              Verify client and server both agree as connected.
        //              Inject an offline network state, so the client will realize the network is down, and close the connection.
        //              Verify client shows disconnected.


        // Start a client that overrides the DispatchConnected method, and verify it is called when the connection is made...
        //  Test_1_5_1  Create an instance of the v1 tcpsocket client that gives us notification of connection events.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Verify the client's connected method was called.

        // Start a client that overrides the DispatchConnected method, and verify it is called when the connection is made, and that the Connection Lost delegate is fired when closed...
        //  Test_1_5_2  Create an instance of the v1 tcpsocket client that gives us notification of connection events.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Verify the client's connected method was called.
        //              Close the client connection.
        //              Verify the client indicates closed.
        //              Verify the server endpoint indicates closed.
        //              Verify the connection lost delegate was called.
        
        // Verify client sets AllowSend after registering...
        //  Test_1_6_1  Create an instance of the v1 tcpsocket client.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Wait for the client to indicate AllowSend == true.
        //              Verify the server shows the client is registered.
        //              Close the client.
        //              Verify the AllowSend flag drops.

     */


    [DoNotParallelize]
    [TestClass]
    public class TCPClient_v1_Tests : OGA.Testing.Lib.Test_Base_abstract
    {
        #region Private Fields

        private List<MessageEnvelope> receivedmsgs = new List<MessageEnvelope>();

        protected string RemoteHost = "192.168.1.128";
        protected int RemotePort = 5003;

        private TESTINGSRVR_Simple_TCPListener _wsl;

        private int Received_RawMessage_Size = 0;
        private string Received_RawMessage_Hash = "";

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
            //// Push the TestContext instance that we received at the start of the current test, into the common property of the test base class...
            //Test_Base.TestContext = TestContext;
            base.Setup();
            // Runs before each test. (Optional)

            // Reset status...
            TESTINGSRVR_Simple_TCPListener.DoSomethingWith_ConnectionRegistration = false;
            TESTINGSRVR_Simple_TCPListener.DoSomethingWith_ConnectionClosure = false;
            TESTINGSRVR_Simple_TCPListener.AllowQuietClients = true;
            TESTINGSRVR_Simple_TCPListener.WeRequireClients_tobe_Chatty = true;
            TESTINGSRVR_Simple_TCPListener.Keepalive_Timeout = 20;

            // Reset the received message metrics...
            this.Reset_ReceivedMessageData();

            CommonChannel.Callback_Queue = new System.Collections.Concurrent.ConcurrentQueue<string>();
            CommonChannel.CallbackChannel_MessageEnvelope = new System.Collections.Concurrent.ConcurrentQueue<MessageEnvelope>();

            _wsl = new TESTINGSRVR_Simple_TCPListener();
            _wsl.Port = RemotePort;
            _wsl.Host = RemoteHost;
            var res = _wsl.Start();
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
                while (!CommonChannel.Callback_Queue.IsEmpty)
                    CommonChannel.Callback_Queue.TryDequeue(out var sss);
            }
            catch(Exception ex)
            {
                int x = 0;
            }
            try
            {
                _wsl?.Stop();
                _wsl?.Dispose();
                _wsl = null;
            }
            catch(Exception ex)
            {
                int x = 0;
            }
            try
            {
                while (!CommonChannel.CallbackChannel_MessageEnvelope.IsEmpty)
                    CommonChannel.CallbackChannel_MessageEnvelope.TryDequeue(out var sss);
            }
            catch(Exception ex)
            {
                int x = 0;
            }

            base.TearDown();
        }

        #endregion


        #region Tests

        [TestMethod]
        public async Task Test_1_1_0()
        {
            // Verify the simple listener is active...
            if(this._wsl.Listener.State != eListenerState.Active)
                Assert.Fail("Wrong return");
        }

        // Verify happy path of opening a client, and the client closing the connection...
        //  Test_1_1_1  Create an instance of the v1 tcpsocket client.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Close the client connection.
        //              Verify the client indicates closed.
        //              Verify the server endpoint indicates closed.
        [TestMethod]
        public async Task Test_1_1_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client = null;

            TESTINGSRVR_Simple_TCPListener.WeRequireClients_tobe_Chatty = false;

            try
            {
                if(this._wsl.Listener.State != eListenerState.Active)
                    Assert.Fail("Wrong return");

                // Create data for a v1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                client = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                client.OnConnectionLost = this.Handle_OnConnectionlost;
                client.OnMessageReceived = this.Handle_OnMessageReceived;
                client.OnStatus_Change = (cl, sts) =>
                {
                };

                // Give the client the device client data...
                client.DeviceId = cp.DeviceId;
                client.UserId = (Guid)cp.UserId;
                client.RuntimeId = cp.RuntimeId;
                client.Pid = cp.Pid;

                // Make sure the client won't timeout...
                client.Cfg_Disable_KeepAlive = true;

                // Start the web socket client...
                var res = await client.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => client.IsConnected, 2000);

                // Ensure we got connected...
                if(!client.IsConnected)
                    Assert.Fail("Connection Failed");

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.ClientInfo.IsRegistered ?? false, 1000);
                
                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");

                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AuthLevel != eAuthLevel.NoAuth)
                    Assert.Fail("Wrong Value");

                // Verify client data...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.UserId != cp.UserId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                    Assert.Fail("Wrong Value");

                // Verify registration defaults for the V1 client...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != "1")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Language != "en-us")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != cp.AppId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId != cp.RuntimeId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Pid != cp.Pid)
                    Assert.Fail("Wrong Value");


                // Close the client...
                await client.Stop_Async();

                // Wait for it to close...
                WaitforCondition(() => !client.IsConnected, 1000);

                // Verify the websocket is closed...
                if(client.IsConnected)
                    Assert.Fail("Connection Failed");

                // Make sure we wait for server-side connection loss...
                WaitforCondition(() => !_wsl.ServerSide_TCPEndpoint.IsConnected, 6000);

                // Check that the server says closed as well...
                if(_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");

                // Verify client data...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.UserId != cp.UserId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                    Assert.Fail("Wrong Value");

                // Verify registration defaults for the V1 client...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != "1")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Language != "en-us")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != cp.AppId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId != cp.RuntimeId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Pid != cp.Pid)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                client?.Dispose();
            }
        }


        // Verify happy path of opening a client, and the server closing the connection...
        //  Test_1_1_2  Create an instance of the v1 tcpsocket client.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Close the server endpoint connection.
        //              Verify the client indicates closed.
        //              Verify the server endpoint indicates closed.
        [TestMethod]
        public async Task Test_1_1_2()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                wss.OnConnectionLost = this.Handle_OnConnectionlost;
                wss.OnMessageReceived = this.Handle_OnMessageReceived;

                // Give the client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Make sure the client won't timeout...
                wss.Cfg_Disable_KeepAlive = true;

                // Start the tcpsocket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the tcpsocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");


                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.ClientInfo.IsRegistered ?? false, 1000);

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AuthLevel != eAuthLevel.NoAuth)
                    Assert.Fail("Wrong Value");

                // Verify client data...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.UserId != cp.UserId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                    Assert.Fail("Wrong Value");

                // Verify registration defaults for the V1 client...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != "1")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Language != "en-us")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != cp.AppId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId != cp.RuntimeId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Pid != cp.Pid)
                    Assert.Fail("Wrong Value");


                // Tell the server endpoint to close the connection...
                await this._wsl.ServerSide_TCPEndpoint.Stop_Async();

                // Wait for it to get lost...
                WaitforCondition(() => !wss.IsConnected, 4000);

                // Verify the websocket is closed...
                if(wss.IsConnected)
                    Assert.Fail("Connection Failed");


                // Check that the server says closed as well...
                if(_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");

                // Verify client data...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.UserId != cp.UserId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                    Assert.Fail("Wrong Value");

                // Verify registration defaults for the V1 client...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != "1")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Language != "en-us")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != cp.AppId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId != cp.RuntimeId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Pid != cp.Pid)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify that the closed connection delegate triggers only once on a client-side closure...
        //  Test_1_1_3  Create an instance of the v1 tcpsocket client.
        //              Hookup the connection lost delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Close the client connection.
        //              Verify the client indicates closed.
        //              Verify the server endpoint indicates closed.
        [TestMethod]
        public async Task Test_1_1_3()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                int losscounter = 0;

                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                wss.OnMessageReceived = this.Handle_OnMessageReceived;
                // Assign a local lambda that will increment a counter each time it triggers...
                wss.OnConnectionLost = ((locws) =>
                {
                    losscounter++;
                });

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Make sure the client won't timeout...
                wss.Cfg_Disable_KeepAlive = true;

                // Mark the connection start time...
                DateTime startime = DateTime.Now;

                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);

                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Record connected time...
                TimeSpan connectionduration = DateTime.Now.Subtract(startime);

                // Wait for the allow to send...
                WaitforCondition(() => wss.AllowSend, 6000);

                // Record allowed send time...
                TimeSpan allowedsendduration = DateTime.Now.Subtract(startime);

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AuthLevel != eAuthLevel.NoAuth)
                    Assert.Fail("Wrong Value");

                // Verify client data...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.UserId != cp.UserId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                    Assert.Fail("Wrong Value");

                // Verify registration defaults for the V1 client...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != "1")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Language != "en-us")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != cp.AppId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId != cp.RuntimeId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Pid != cp.Pid)
                    Assert.Fail("Wrong Value");

                // wait for us to reach the allowed to send state...
                WaitforCondition(() => wss.AllowSend, 200);

                // Verify allow send was hit...
                if(!wss.AllowSend)
                    Assert.Fail("Connection Failed");

                // Verify the counter is zero...
                if(losscounter != 0)
                    Assert.Fail("Wrong Value");


                // Close the client...
                await wss.Stop_Async();

                // Wait for it to be lost...
                WaitforCondition(() => !wss.IsConnected, 4000);

                // Verify the websocket is closed...
                if(wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Check that the server says closed as well...
                if(_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");

                // Verify client data...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.UserId != cp.UserId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                    Assert.Fail("Wrong Value");

                // Verify registration defaults for the V1 client...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != "1")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Language != "en-us")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != cp.AppId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId != cp.RuntimeId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Pid != cp.Pid)
                    Assert.Fail("Wrong Value");


                // Wait to ensure we got the loss callback...
                WaitforCondition(() => losscounter == 1, 2000);

                // Verify the counter is exactly one...
                if(losscounter != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify that the closed connection delegate triggers only once on a server-side closure...
        //  Test_1_1_4  Create an instance of the v1 tcpsocket client.
        //              Hookup the connection lost delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Close the server-side connection.
        //              Verify the connection closed delegate is called.
        //              Verify the client closes and reopens a new connection.
        //              Verify the server endpoint indicates open after the client has recycled its connection.
        [TestMethod]
        public async Task Test_1_1_4()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                int losscounter = 0;

                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                wss.OnMessageReceived = this.Handle_OnMessageReceived;
                // Assign a local lambda that will increment a counter each time it triggers...
                wss.OnConnectionLost = ((locws) =>
                {
                    losscounter++;
                });

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Make sure the client won't timeout...
                wss.Cfg_Disable_KeepAlive = true;

                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AuthLevel != eAuthLevel.NoAuth)
                    Assert.Fail("Wrong Value");

                // Verify client data...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.UserId != cp.UserId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                    Assert.Fail("Wrong Value");

                // Verify registration defaults for the V1 client...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != "1")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Language != "en-us")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != cp.AppId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId != cp.RuntimeId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Pid != cp.Pid)
                    Assert.Fail("Wrong Value");


                // Verify the counter is zero...
                if(losscounter != 0)
                    Assert.Fail("Wrong Value");

                // Fetch the current attempt counter...
                var attempts_before = wss.ConnAttempt_TotalCounter;


                // Close the server-side connection...
                // This should force the client to close its connection and reopen another.
                await this._wsl.ServerSide_TCPEndpoint.Stop_Async();

                // Wait for things to close and reopen...
                WaitforCondition(() => !wss.IsConnected, 6000);
                WaitforCondition(() => wss.IsConnected, 6000);

                // Fetch the current attempt counter...
                var attempts_after = wss.ConnAttempt_TotalCounter;


                // Verify the client has opened a new connection...
                if((attempts_after - attempts_before) != 1)
                    Assert.Fail("Connection was not recycled.");

                // Verify the websocket is re-opened...
                if(!wss.IsConnected)
                    Assert.Fail("Connection failed to reopen.");


                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.ClientInfo.IsRegistered ?? false, 1000);

                // Check that the server says opened as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");

                // Verify client data...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.UserId != cp.UserId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                    Assert.Fail("Wrong Value");

                // Verify registration defaults for the V1 client...
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != "1")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Language != "en-us")
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != cp.AppId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId != cp.RuntimeId)
                    Assert.Fail("Wrong Value");
                if(_wsl.ServerSide_TCPEndpoint.ClientInfo.Pid != cp.Pid)
                    Assert.Fail("Wrong Value");

                // Verify the counter is exactly one...
                if(losscounter != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify client without channels receives a non-channel message...
        //  Test_1_1_5  Create an instance of the v1 tcpsocket client.
        //              Hookup the non-channel message delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              From the server-side, send a message without a channel to the client.
        //              Verify the client receives the message on the non-channel delegate.
        [TestMethod]
        public async Task Test_1_1_5()
        {
            int receivedcounter = 0;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                wss.OnConnectionLost = this.Handle_OnConnectionlost;
                // Assign a local message handler...
                wss.OnMessageReceived = ((recws, recmessagetype, recmsg) =>
                {
                    receivedcounter++;

                    return 1;
                });

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;

                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");
                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                receivedcounter = 0;

                // Create a message to send to the client...
                var msg = new SimpleGeneric<string>();
                msg.Name = Guid.NewGuid().ToString();
                msg.Value = Guid.NewGuid().ToString();

                // From the server-side, send a message to the client, but not on a channel...
                var res2 = await this._wsl.ServerSide_TCPEndpoint.SendMessage_toClient(msg);
                if(res2 != 1)
                    Assert.Fail("Wrong return");


                // Wait for the message to come in...
                WaitforCondition(() => receivedcounter == 1, 1000);

                // Verify the client received the message...
                if(receivedcounter != 1)
                    Assert.Fail("Nothing Received.");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify client ping functionality...
        //  Test_1_1_6  Create an instance of the v1 tcpsocket client.
        //              Shorten the client's ping duration.
        //              Connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Wait long enough for the ping message to have been sent.
        //              Verify the client and server both indicate a message exchanged.
        [TestMethod]
        public async Task Test_1_1_6()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                // Tell the client to use a short keepalive interval...
                wss.Cfg_KeepAliveInterval = 3;

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;

                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");
                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // There will be messages exchanged as part of normal connection setup and registration.
                // So, we will mark a delta for each end....
                int client_msgcount_before = wss.ReceivedMessage_Counter;
                int server_msgcount_before = this._wsl.ServerSide_TCPEndpoint.ReceivedMessage_Counter;


                // Wait long enough for a keepalive to have been exchanged...
                await Task.Delay(3000);


                // Mark the counters after...
                int client_msgcount_after = wss.ReceivedMessage_Counter;
                int server_msgcount_after = this._wsl.ServerSide_TCPEndpoint.ReceivedMessage_Counter;

                // Verify the message counters reflect a ping pong exchanged...
                if((client_msgcount_after - client_msgcount_before) > 1)
                    Assert.Fail("Wrong Value");
                if((server_msgcount_after - server_msgcount_before) > 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify client gets closed after a failed keepalive...
        //  Test_1_1_7  Create an instance of the v1 tcpsocket client.
        //              Set the client to use a very long keepalive interval.
        //              Hookup the connection lost delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Set the server to use a shorter interval.
        //              Wait long enough for the server to timeout the connection for lack of keepalive or traffic.
        //              Verify the connection closed delegate is called.
        //              Verify the client closes and reopens a new connection.
        //              Verify the server endpoint indicates open after the client has recycled its connection.
        [TestMethod]
        public async Task Test_1_1_7()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                int losscounter = 0;

                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                wss.OnMessageReceived = this.Handle_OnMessageReceived;
                // Assign a local lambda that will increment a counter each time it triggers...
                wss.OnConnectionLost = ((locws) =>
                {
                    losscounter++;
                });
                // Set the client's keepalive very long, so it will never send a ping...
                wss.Cfg_KeepAliveInterval = 100;

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 1000);
                
                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Verify the counter is zero...
                if(losscounter != 0)
                    Assert.Fail("Wrong Value");

                // Fetch the current attempt counter...
                var attempts_before = wss.ConnAttempt_TotalCounter;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
                    $"{nameof(TCPClient_v1_Tests)}:-::{nameof(Test_1_1_7)} - " +
                    $"Attempts_Before = {attempts_before.ToString()}");

                // Tell the server endpoint to expect a short keepalive interval...
                this._wsl.ServerSide_TCPEndpoint.Cfg_DeadClientTimeout = 5;


                // Wait an expected duration for the client to have been dropped for its silence...
                WaitforCondition(() => !wss.IsConnected, 11000);


                // Check that the client is dropped...
                if(wss.IsConnected)
                    Assert.Fail("Connection failed to reopen.");

                // With the client dropped, we need to wait for it to reconnect...
                WaitforCondition(() => wss.IsConnected, 4000);

                // Check that the client has reopened the connection...
                if(!wss.IsConnected)
                    Assert.Fail("Connection failed to reopen.");

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 500);

                // Check that the server says opened as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");

                // Wait for the server to claim the client as registered...
                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered, 500);

                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");


                // Fetch the current attempt counter...
                var attempts_after = wss.ConnAttempt_TotalCounter;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
                    $"{nameof(TCPClient_v1_Tests)}:-::{nameof(Test_1_1_7)} - " +
                    $"Attempts_After = {attempts_after.ToString()}");


                // Verify the client attempted a second connection...
                if((attempts_after - attempts_before) < 1)
                    Assert.Fail("Connection was not recycled.");

                // Verify the counter is exactly one...
                if(losscounter != (wss.ConnAttempt_TotalCounter - 1))
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify client can received a channel message...
        //  Test_1_2_1  Create an instance of the v1 tcpsocket client.
        //              Hookup a channel message delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              From the server-side, send the client a message assigned to the registered channel.
        //              Verify the client receives the message on the channel delegate.
        [TestMethod]
        public async Task Test_1_2_1()
        {
            int receivecounter = 0;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                string channelname = Guid.NewGuid().ToString();

                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);

                // Add a channel handler, to receive server messages...
                wss.Add_ChannelHandler(channelname, (ws, messagetype, rcvmsg) =>
                {
                    receivecounter++;

                    return 1;
                });

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Clear the receive counter...
                receivecounter = 0;


                // Tell the server endpoint to send a message to the client...
                var msg = new SimpleGeneric<string>();
                var res1 = await this._wsl.ServerSide_TCPEndpoint.SendMessage_toClient(msg, channelname);
                if(res1 != 1)
                    Assert.Fail("Wrong Return");

                // Wait for the message to be received...
                WaitforCondition(() => receivecounter == 1, 2000);


                // Verify the message was received...
                if(receivecounter != 1)
                    Assert.Fail("Wrong count.");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify registered loopack echoes all messages...
        //  Test_1_2_2  Create an instance of the v1 tcpsocket client.
        //              Hookup a channel message delegate.
        //              Tell the client to request message loopback.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Have the client send a channel message to the server.
        //              Verify the client receives the message on the channel delegate.
        [TestMethod]
        public async Task Test_1_2_2()
        {
            int receivecounter = 0;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                string channelname = Guid.NewGuid().ToString();

                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);

                // Add a channel handler, to receive server messages...
                wss.Add_ChannelHandler(channelname, (ws, messagetype, rcvmsg) =>
                {
                    receivecounter++;

                    return 1;
                });

                // Tell the client to ask for total loopback...
                wss.Register_with_Loopback_AllMessages = true;

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 2000);
                WaitforCondition(() => wss.AllowSend, 2000);
                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Clear the receive counter...
                receivecounter = 0;


                // Send a channel message to the server (to verify it echo's back)...
                var msg = new SimpleGeneric<string>();
                var res1 = await wss.SendMessage_to_Endpoint(msg, channelname);
                if(res1 != 1)
                    Assert.Fail("Wrong Return");

                // Wait for the message to be reach the server and bounce back to us...
                WaitforCondition(() => receivecounter == 1, 2000);


                // Verify the message was received...
                if(receivecounter != 1)
                    Assert.Fail("Wrong count.");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify client can send message without a channel, and is received...
        //  Test_1_2_3  Create an instance of the v1 tcpsocket client.
        //              Hookup a non-channel message delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Send a non-channel message to the server.
        //              Verify the server receives the message on the non-channel delegate.
        [TestMethod]
        public async Task Test_1_2_3()
        {
            int receivecounter = 0;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                string channelname = Guid.NewGuid().ToString();

                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);

                //// Add a channel handler, to receive server messages...
                //wss.Add_ChannelHandler(channelname, (ws, messagetype, msg) =>
                //{
                //    receivecounter++;

                //    return 1;
                //});

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 1000);

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Assign a non-channel handler to the server endpoint...
                this._wsl.ServerSide_TCPEndpoint.OnMessageReceived = (ws, messagetype, rcvmsg, corelationid) =>
                {
                    receivecounter++;

                    return 1;
                };


                // Clear the receive counter...
                receivecounter = 0;


                // Have the client send a non-channel message to the server...
                var msg = new SimpleGeneric<string>();
                var res1 = await wss.SendMessage_to_Endpoint(msg);
                if(res1 != 1)
                    Assert.Fail("Wrong Return");

                // Wait for the message to be received...
                WaitforCondition(() => receivecounter == 1, 2000);


                // Verify the message was received...
                if(receivecounter != 1)
                    Assert.Fail("Wrong count.");
            }
            finally
            {
                wss?.Dispose();
            }
        }

        // Verify client can send message on a channel, and it is received...
        //  Test_1_2_4  Create an instance of the v1 tcpsocket client.
        //              Hookup a channel message delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Send a channel message to the server.
        //              Verify the server receives the message on the channel delegate.
        [TestMethod]
        public async Task Test_1_2_4()
        {
            int receivecounter = 0;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                string channelname = Guid.NewGuid().ToString();

                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Assign a non-channel handler to the server endpoint...
                this._wsl.ServerSide_TCPEndpoint.Add_ChannelHandler(channelname, (ws, messagetype, rcvmsg, corelationid) =>
                {
                    receivecounter++;

                    return 1;
                });


                // Clear the receive counter...
                receivecounter = 0;

                // Make sure we wait for registration, so we can send messages...
                WaitforCondition(() => wss.AllowSend, 2000);

                // Have the client send a non-channel message to the server...
                var msg = new SimpleGeneric<string>();
                var res1 = await wss.SendMessage_to_Endpoint(msg, channelname);
                if(res1 != 1)
                    Assert.Fail("Wrong Return");

                // Wait for the message to be received...
                WaitforCondition(() => receivecounter == 1, 2000);

                // Verify the message was received...
                if(receivecounter != 1)
                    Assert.Fail("Wrong count.");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify the client can request to disable keepalive, and is not closed after timeout...
        //  Test_1_3_1  Create an instance of the v1 tcpsocket client.
        //              Set the client to request keepalive to be disabled.
        //              Hookup the connection lost delegate.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Set the server to use a short keepalive interval.
        //              Wait long enough for the server to normally timeout the connection for lack of keepalive or traffic.
        //              Verify the connection remains open.
        [TestMethod]
        public async Task Test_1_3_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                int losscounter = 0;

                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                wss.OnMessageReceived = this.Handle_OnMessageReceived;
                // Assign a local lambda that will increment a counter each time it triggers...
                wss.OnConnectionLost = ((locws) =>
                {
                    losscounter++;
                });


                // Have the client request no keepalives for the session...
                wss.Cfg_Disable_KeepAlive = true;

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Verify the counter is zero...
                if(losscounter != 0)
                    Assert.Fail("Wrong Value");

                // Fetch the current attempt counter...
                var attempts_before = wss.ConnAttempt_TotalCounter;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
                    $"{nameof(TCPClient_v1_Tests)}:-::{nameof(Test_1_1_7)} - " +
                    $"Attempts_Before = {attempts_before.ToString()}");

                // Tell the server endpoint to expect a short keepalive interval (as if keepalives would be active)...
                this._wsl.ServerSide_TCPEndpoint.Cfg_DeadClientTimeout = 5;


                // Wait an expected duration for the client to have been dropped for its silence...
                WaitforCondition(() => wss.IsConnected, 15000);

                // Check that the client is still open...
                if(!wss.IsConnected)
                    Assert.Fail("Connection failed to reopen.");


                // Check that the server says opened as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");


                // Fetch the current attempt counter...
                var attempts_after = wss.ConnAttempt_TotalCounter;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
                    $"{nameof(TCPClient_v1_Tests)}:-::{nameof(Test_1_1_7)} - " +
                    $"Attempts_After = {attempts_after.ToString()}");


                // Verify the attempts did not change...
                if((attempts_after - attempts_before) > 0)
                    Assert.Fail("Connection was recycled for some reason.");

                // Verify the counter is zero...
                if(losscounter > 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify client with an InternetAvailable override will spin-wait while internet is not available, and then attempt connection when available....
        //  Test_1_4_1  Create an instance of the v1 tcpsocket client that lets us inject network state.
        //              Begin our testing with the simulated network state as offline.
        //              Start the tcpsocket client.
        //              Wait a duration long enough that the client would have attempted connection.
        //              Verify the client did not attempt connection, by checking that the connection attempt counter stays at zero.
        //              Inject an online network state, so the client will attempt connection.
        //              Verify client and server both agree as connected.
        //              Inject an offline network state, so the client will realize the network is down, and close the connection.
        //              Verify client shows disconnected.
        [TestMethod]
        public async Task Test_1_4_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_InjectibleNetworkState wss = null;

            try
            {
                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_InjectibleNetworkState(RemoteHost, RemotePort, logger);
                wss.OnMessageReceived = this.Handle_OnMessageReceived;


                // Start off the client with a simulated offline network state...
                wss.TESTING_NetworkIsAvailable = false;

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait a tick before checking...
                WaitforCondition(() => !wss.IsConnected, 2000);


                // Verify the websocket remains unconnected...
                if(wss.IsConnected)
                    Assert.Fail("Should not have connected");

                // Check that the server has no endpoint either...
                if(_wsl.ServerSide_TCPEndpoint != null)
                    Assert.Fail("Should have been null");


                // Simulate the network state as online...
                wss.TESTING_NetworkIsAvailable = true;


                // Give the client a little bit to create a connection...
                WaitforCondition(() => wss.IsConnected, 400);


                // Verify the websocket connected...
                if(!wss.IsConnected)
                    Assert.Fail("Wrong state");

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 400);

                // Check that the server has no endpoint either...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Wrong state");


                // Now that the client is connected, take away the network state...
                wss.TESTING_NetworkIsAvailable = false;
                // And, drop the server side....
                await this._wsl.ServerSide_TCPEndpoint.Stop_Async();


                // Wait for things to drop out...
                WaitforCondition(() => !wss.IsConnected, 5000);

                // Save off the attempt counter...
                var attemptbefore = wss.ConnAttempt_TotalCounter;


                // Now, wait to ensure no reconnect is attempted...
                await Task.Delay(1000);


                // Verify the websocket remains unconnected...
                if(wss.IsConnected)
                    Assert.Fail("Should be disconnected");

                // Check that the endpoint is null or disconnected...
                if(_wsl.ServerSide_TCPEndpoint != null && _wsl.ServerSide_TCPEndpoint.IsConnected == true)
                    Assert.Fail("Should be disconnected");

                // Get the attempt counter after...
                var attemptafter = wss.ConnAttempt_TotalCounter;

                // Ensure no change to the attempt counter...
                if((attemptafter - attemptbefore) > 0)
                    Assert.Fail("Should be zero");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Start a client that overrides the DispatchConnected method, and verify it is called when the connection is made...
        //  Test_1_5_1  Create an instance of the v1 tcpsocket client that gives us notification of connection events.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Verify the client's connected method was called.
        [TestMethod]
        public async Task Test_1_5_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_ConnectedDelegate wss = null;

            try
            {
                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_ConnectedDelegate(RemoteHost, RemotePort, logger);

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");


                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 1000);

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");

                WaitforCondition(() => wss.TESTING_ConnectedMethod_CallCounter == 1, 400);

                // Verify the connected method was called...
                if(wss.TESTING_ConnectedMethod_CallCounter != 1)
                    Assert.Fail("Wrong Count");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Start a client that overrides the DispatchConnected method, and verify it is called when the connection is made, and that the Connection Lost delegate is fired when closed...
        //  Test_1_5_2  Create an instance of the v1 tcpsocket client that gives us notification of connection events.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Verify client properties are registered with the server endpoint.
        //              Verify the client's connected method was called.
        //              Close the client connection.
        //              Verify the client indicates closed.
        //              Verify the server endpoint indicates closed.
        //              Verify the connection lost delegate was called.
        [TestMethod]
        public async Task Test_1_5_2()
        {
            int connlosscounter = 0;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_ConnectedDelegate wss = null;

            try
            {
                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_ConnectedDelegate(RemoteHost, RemotePort, logger);

                // Assign a connection lost delegate to the client...
                wss.OnConnectionLost = (x)=>
                {
                    connlosscounter++;
                };

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Make sure the client won't timeout...
                wss.Cfg_Disable_KeepAlive = true;

                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 500);

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");

                WaitforCondition(() => wss.TESTING_ConnectedMethod_CallCounter == 1, 500);

                // Verify the connected method was called...
                if(wss.TESTING_ConnectedMethod_CallCounter != 1)
                    Assert.Fail("Wrong Count");


                // Close the client...
                await wss.Stop_Async();

                // Wait for it to close...
                WaitforCondition(() => !wss.IsConnected, 1000);

                // Verify the websocket is closed...
                if(wss.IsConnected)
                    Assert.Fail("Connection Failed");

                // Check that the server says closed as well...
                if(_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Verify the connection lost delegate was triggered...
                if(connlosscounter != 1)
                    Assert.Fail("Connection Lost Count Incorrect");
            }
            finally
            {
                wss?.Dispose();
            }
        }


        // Verify client sets AllowSend after registering...
        //  Test_1_6_1  Create an instance of the v1 tcpsocket client.
        //              Attempt to connect to the test tcpendpoint.
        //              Verify client and server both agree as connected.
        //              Wait for the client to indicate AllowSend == true.
        //              Verify the server shows the client is registered.
        //              Close the client.
        //              Verify the AllowSend flag drops.
        [TestMethod]
        public async Task Test_1_6_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl wss = null;

            try
            {
                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the tcpsocket client...
                wss = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
                wss.OnConnectionLost = this.Handle_OnConnectionlost;
                wss.OnMessageReceived = this.Handle_OnMessageReceived;

                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;
                wss.RuntimeId = cp.RuntimeId;
                wss.Pid = cp.Pid;


                // Make sure the client won't timeout...
                wss.Cfg_Disable_KeepAlive = true;

                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => wss.IsConnected, 2000);


                // Verify the websocket is active...
                if(!wss.IsConnected)
                    Assert.Fail("Connection Failed");

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.ClientInfo.IsRegistered ?? false, 2000);

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
                if(!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");

                WaitforCondition(() => wss.AllowSend, 400);

                // Check that the client indicates Allow Send...
                if(wss.AllowSend == false)
                    Assert.Fail("Wrong Value");


                // Tell the server endpoint to close the connection...
                await this._wsl.ServerSide_TCPEndpoint.Stop_Async();

                // Wait for it to close...
                WaitforCondition(() => !wss.IsConnected, 1000);

                // Check that the Allow Send has dropped...
                if(wss.AllowSend == true)
                    Assert.Fail("Wrong Value");

                // Verify the websocket is closed...
                if(wss.IsConnected)
                    Assert.Fail("Connection Failed");


                // Check that the server says closed as well...
                if(_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");
            }
            finally
            {
                wss?.Dispose();
            }
        }
        
        
        #endregion


        #region Private Methods

        private bool AreDatesClose(DateTime d1, DateTime d2, int offset)
        {
            if(d1.CompareTo(d2) > 0)
            {
                var diff1 = d1.Subtract(d2);
                return diff1.TotalSeconds < offset;
            }
            if(d1.CompareTo(d2) < 0)
            {
                var diff2 = d2.Subtract(d1);
                return diff2.TotalSeconds < offset;
            }
            else
            {
                return true;
            }
        }

        private void Reset_ReceivedMessageData()
        {
            this.Received_RawMessage_Size = 0;
            this.Received_RawMessage_Hash = "";
        }

        private int Handle_OnMessageReceived(Client_v1_Abstract mep, string messagetype, string msg)
        {
            return 1;
        }
        private void Handle_OnConnectionlost(Client_v1_Abstract mep)
        {
            int x = 0;
        }

        #endregion
    }
}
