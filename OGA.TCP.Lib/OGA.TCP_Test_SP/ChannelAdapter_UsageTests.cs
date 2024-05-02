using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP.ClientAdapters;
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
    /*  Unit Tests for: Derivatives of ChannelAdapter_abstract
        Description:    These unit tests exercise the functionality of a channel adapter class being registered with a Client_v1_Abstract.
                        Channel adapters allow consumers of this library to write simple channel handling logic, without actually creating implementations of Client_v1_Abstract.
        NOTE:           For testing purposes, we are using a TCPClient_v1 implementation.

        // Verify channel adapter registration and removal...
        //  Test_1_1_1  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter with a blank channel name.
        //              Verify the client responds with an error.
        //  Test_1_1_2  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Verify the channel was added.
        //              Attempt to remove the channel by name.
        //              Verify the channel was removed.
        //  Test_1_1_3  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Verify the channel was added.
        //              Attempt to remove a channel with a bogus name.
        //              Verify an error was returned.
        //  Test_1_1_4  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Attempt to add a channel adapter of the same name.
        //              Verify the client responds with an error.
        //  Test_1_1_5  Create an instance of the v1 tcpsocket client.
        //              Attempt to add a null for a channel adapter.
        //              Verify an error was returned.

        // Verify positive comms through a channel adapter...
        //  Test_1_2_1  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Send a message from the channel adapter.
        //              Verify it was received by the server side.
        //  Test_1_2_2  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Send a message from the server side endpoint.
        //              Verify it was received by the channel adapter.

     */


    [DoNotParallelize]
    [TestClass]
    public class ChannelAdapter_UsageTests : OGA.Testing.Lib.Test_Base_abstract
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


        // Verify channel adapter registration and removal...
        //  Test_1_1_1  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter with a blank channel name.
        //              Verify the client responds with an error.
        [TestMethod]
        public async Task Test_1_1_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client = null;

            int receivedmessagecounter = 0;

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

                // The server-side of this test does not send connection registration replies.
                // So, we need to tell the client to not require this...
                client.Cfg_ConnectionWaitsforRegistrationReply = false;

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

                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 2000);

                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Declare the delegate callback that we will pass to the channel adapter...
                OGA.TCP.SessionLayer.Client_v1_Abstract.DelMessageReceived callback = (mep, messagetype, msg) =>
                {
                    receivedmessagecounter++;
                    return 1;
                };


                // Add a channel adapter with a blank channel name...
                var cname = "";
                var somemessagetypename = "";
                var ca = new ChannelAdapter_CustomType(cname, callback, somemessagetypename, logger);
                var resca = client.Add_ChannelAdapter(ca);
                if(resca != -2)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                client?.Dispose();
            }
        }

        //  Test_1_1_2  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Verify the channel was added.
        //              Attempt to remove the channel by name.
        //              Verify the channel was removed.
        [TestMethod]
        public async Task Test_1_1_2()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client = null;

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

                // The server-side of this test does not send connection registration replies.
                // So, we need to tell the client to not require this...
                client.Cfg_ConnectionWaitsforRegistrationReply = false;

                // Make sure the client won't timeout...
                client.Cfg_Disable_KeepAlive = true;


                // Declare the delegate callback that we will pass to the channel adapter...
                OGA.TCP.SessionLayer.Client_v1_Abstract.DelMessageReceived callback = (mep, messagetype, msg) =>
                {
                    return 1;
                };


                // Add a channel adapter with a blank channel name...
                var channelname = Guid.NewGuid().ToString();
                var somemessagetypename = "";
                var ca = new ChannelAdapter_CustomType(channelname, callback, somemessagetypename, logger);
                var resca = client.Add_ChannelAdapter(ca);
                if(resca != 1)
                    Assert.Fail("Wrong Value");


                // Attempt to remove the channel...
                var resca2 = client.Remove_ChannelHandler(channelname);
                if(resca2 != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                client?.Dispose();
            }
        }

        //  Test_1_1_3  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Verify the channel was added.
        //              Attempt to remove a channel with a bogus name.
        //              Verify an error was returned.
        [TestMethod]
        public async Task Test_1_1_3()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client = null;

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

                // The server-side of this test does not send connection registration replies.
                // So, we need to tell the client to not require this...
                client.Cfg_ConnectionWaitsforRegistrationReply = false;

                // Make sure the client won't timeout...
                client.Cfg_Disable_KeepAlive = true;


                // Declare the delegate callback that we will pass to the channel adapter...
                OGA.TCP.SessionLayer.Client_v1_Abstract.DelMessageReceived callback = (mep, messagetype, msg) =>
                {
                    return 1;
                };


                // Add a channel adapter with a blank channel name...
                var channelname = Guid.NewGuid().ToString();
                var somemessagetypename = "";
                var ca = new ChannelAdapter_CustomType(channelname, callback, somemessagetypename, logger);
                var resca = client.Add_ChannelAdapter(ca);
                if(resca != 1)
                    Assert.Fail("Wrong Value");


                // Attempt to remove an unknown channel name...
                var resca2 = client.Remove_ChannelHandler(Guid.NewGuid().ToString());
                if(resca2 != -1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                client?.Dispose();
            }
        }

        //  Test_1_1_4  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Attempt to add a channel adapter of the same name.
        //              Verify the client responds with an error.
        [TestMethod]
        public async Task Test_1_1_4()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client = null;

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

                // The server-side of this test does not send connection registration replies.
                // So, we need to tell the client to not require this...
                client.Cfg_ConnectionWaitsforRegistrationReply = false;

                // Make sure the client won't timeout...
                client.Cfg_Disable_KeepAlive = true;


                // Declare the delegate callback that we will pass to the channel adapter...
                OGA.TCP.SessionLayer.Client_v1_Abstract.DelMessageReceived callback = (mep, messagetype, msg) =>
                {
                    return 1;
                };


                // Add a channel adapter with a blank channel name...
                var channelname = Guid.NewGuid().ToString();
                var somemessagetypename = "";
                var ca = new ChannelAdapter_CustomType(channelname, callback, somemessagetypename, logger);
                var resca = client.Add_ChannelAdapter(ca);
                if(resca != 1)
                    Assert.Fail("Wrong Value");


                // Attempt to add another adapter with the same name...
                var channelname2 = Guid.NewGuid().ToString();
                var somemessagetypename2 = "";
                var ca2 = new ChannelAdapter_CustomType(channelname2, callback, somemessagetypename2, logger);
                var resca2 = client.Remove_ChannelHandler(Guid.NewGuid().ToString());
                if(resca2 != -1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                client?.Dispose();
            }
        }

        //  Test_1_1_5  Create an instance of the v1 tcpsocket client.
        //              Attempt to add a null for a channel adapter.
        //              Verify an error was returned.
        [TestMethod]
        public async Task Test_1_1_5()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client = null;

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

                // The server-side of this test does not send connection registration replies.
                // So, we need to tell the client to not require this...
                client.Cfg_ConnectionWaitsforRegistrationReply = false;

                // Make sure the client won't timeout...
                client.Cfg_Disable_KeepAlive = true;


                // Add a channel adapter with a blank channel name...
                var resca = client.Add_ChannelAdapter(null);
                if(resca != -1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                client?.Dispose();
            }
        }


        // Verify positive comms through a channel adapter...
        //  Test_1_2_1  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Send a message from the channel adapter.
        //              Verify it was received by the server side.
        [TestMethod]
        public async Task Test_1_2_1()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client = null;

            int clientreceivedmessagecounter = 0;
            int serverreceivedmessagecounter = 0;

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


                // The server-side of this test does not send connection registration replies.
                // So, we need to tell the client to not require this...
                client.Cfg_ConnectionWaitsforRegistrationReply = false;

                // Make sure the client won't timeout...
                client.Cfg_Disable_KeepAlive = true;

                // Start the web socket client...
                var res = await client.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                // Wait for it to get established...
                WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.IsConnected ?? false, 2000);

                // Ensure we got connected...
                if(!client.IsConnected)
                    Assert.Fail("Connection Failed");
                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");

                // Wait for the client to be allowed to send...
                WaitforCondition(() => client.AllowSend, 1000);

                // Check that the client now allows messages...
                if(!client.AllowSend)
                    Assert.Fail("Wrong value");

                // Declare the delegate callback that we will pass to the channel adapter...
                OGA.TCP.SessionLayer.Client_v1_Abstract.DelMessageReceived callback = (mep, messagetype, msg1) =>
                {
                    clientreceivedmessagecounter++;
                    return 1;
                };


                // Add a channel adapter with a blank channel name...
                var channelname = Guid.NewGuid().ToString();
                var somemessagetypename = "";
                var ca = new ChannelAdapter_CustomType(channelname, callback, somemessagetypename, logger);
                var resca = client.Add_ChannelAdapter(ca);
                if(resca != 1)
                    Assert.Fail("Wrong Value");


                // Assign a non-channel handler to the server endpoint...
                this._wsl.ServerSide_TCPEndpoint.Add_ChannelHandler(channelname, (ws, messagetype, rcvmsg, corelationid) =>
                {
                    serverreceivedmessagecounter++;

                    return 1;
                });


                // Clear the server's receive counter...
                serverreceivedmessagecounter = 0;


                // Send a channel message to the server...
                var msg = new SimpleGeneric<string>();
                var res1 = await ca.SendMessage(msg);
                if(res1 != 1)
                    Assert.Fail("Wrong Return");

                // Wait for the message to be reach the server and bounce back to us...
                WaitforCondition(() => serverreceivedmessagecounter == 1, 2000);


                // Verify the message was received...
                if(serverreceivedmessagecounter != 1)
                    Assert.Fail("Wrong count.");
            }
            catch(Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    "Exception caught.\r\n" +
                    e.Message);
                Assert.Fail("Exception Caught.");
            }
            finally
            {
                client?.Dispose();
            }
        }


        //  Test_1_2_2  Create an instance of the v1 tcpsocket client.
        //              Create and add a channel adapter for a given channel name.
        //              Send a message from the server side endpoint.
        //              Verify it was received by the channel adapter.
        [TestMethod]
        public async Task Test_1_2_2()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client = null;

            int clientreceivedmessagecounter = 0;
            int serverreceivedmessagecounter = 0;


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

                // The server-side of this test does not send connection registration replies.
                // So, we need to tell the client to not require this...
                client.Cfg_ConnectionWaitsforRegistrationReply = false;

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
                // Check that the server says connected as well...
                if(!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Connection Failed");


                // Declare the delegate callback that we will pass to the channel adapter...
                OGA.TCP.SessionLayer.Client_v1_Abstract.DelMessageReceived callback = (mep, messagetype, msg1) =>
                {
                    clientreceivedmessagecounter++;
                    return 1;
                };


                // Add a channel adapter with a blank channel name...
                var channelname = Guid.NewGuid().ToString();
                var somemessagetypename = "";
                var ca = new ChannelAdapter_CustomType(channelname, callback, somemessagetypename, logger);
                var resca = client.Add_ChannelAdapter(ca);
                if(resca != 1)
                    Assert.Fail("Wrong Value");


                // Assign a non-channel handler to the server endpoint...
                this._wsl.ServerSide_TCPEndpoint.Add_ChannelHandler(channelname, (ws, messagetype, rcvmsg, corelationid) =>
                {
                    serverreceivedmessagecounter++;

                    return 1;
                });


                // Clear the client's receive counter...
                clientreceivedmessagecounter = 0;


                // Send a channel message to the client...
                var msg = new SimpleGeneric<string>();
                var res1 = await _wsl.ServerSide_TCPEndpoint.SendMessage_toClient(msg, channelname);
                if(res1 != 1)
                    Assert.Fail("Wrong Return");

                // Wait for the message to be reach the client...
                WaitforCondition(() => clientreceivedmessagecounter == 1, 2000);

                // Verify the message was received...
                if(clientreceivedmessagecounter != 1)
                    Assert.Fail("Wrong count.");
            }
            finally
            {
                client?.Dispose();
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
