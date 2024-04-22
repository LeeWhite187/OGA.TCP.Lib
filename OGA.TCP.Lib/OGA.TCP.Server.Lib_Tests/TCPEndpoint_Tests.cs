using Microsoft.AspNetCore.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP;
using OGA.TCP.Messages;
using OGA.TCP.Server;
using OGA.TCP.Server.Lib_Tests.Helpers;
using OGA.TCP.Server.Model;
using OGA.TCP.Shared.Encoding;
using OpenTelemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Testing.WSEndpoint_Tests.HelperClasses;
using Testing_CommonHelpers_SP.Helpers;
using WSEndpoint_Tests.HelperClasses;

namespace OGA.TCP_Test_SP
{
    /*  TCPEndpoint Tests
     
        //  Test_1_1_0  Create server endpoint instance with unopened TcpClient.
        //              Verify endpoint indicates closed and initialized.
        //  Test_1_1_1  Create connected TcpClient pair.
        //              Create server endpoint instance with opened TcpClient.
        //              Verify both endpoints are connected.
        //  Test_1_1_2  Create connected TcpClient pair.
        //              Create server endpoint instance with opened TcpClient.
        //              Verify both endpoints are connected.
        //              Send a simple message from the client.
        //              Verify the server endpoint promotes to Open.
        //              Verify the endpoint client info indicates unregistered and is otherwise blank.

     
     */

    /*  Unit Tests for: TCPEndpoint (a derivative of OGA.WSClient.WSEndpoint), with clients connecting as WSLibVersion=2
    /*  Unit Tests for: WSEndpoint, with clients connecting as WSLibVersion=2
        // Test 01  -   Removed.
        // Test 02  -   Do simple websocket client call.
        //              Verify we get a websocket connection.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        //              Dispose of the websocket client.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        // Test 03  -   Do simple websocket client call.
        //              Send a ping signal.
        //              Verify we get a pong back.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        // Test 04  -   Do simple websocket client call.
        //              Send a message with scope set to "loopback=rawmsg".
        //              Verify we get the same message replied back.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        // Test 05  -   Do simple websocket client call.
        //              Send an empty message that should be disregarded, but not fail the connection.
        //              Send a ping after that.
        //              Verify we get a pong message back.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        // Test 06  -   Do simple websocket client call.
        //              Send a message with an unknown type that should be disregarded, but not fail the connection.
        //              Send a ping after that.
        //              Verify we get a pong message back.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        // Test 07  -   Do simple websocket client call.
        //              Send a close message.
        //              Verify we get a closing message back, and the connection is closed.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        // Test 08  -   Do simple websocket client call.
        //              Send a connection registration message.
        //              Verify the connection registration was received.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        // Test 09  -   Do simple websocket client call.
        //              Enable loopback for all messages via connection registration message.
        //              Test that all messages get echoed back.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        // Test 10  -   Do simple websocket client call.
        //              Disable loopback of all messages via connection registration message.
        //              Test that no more messages get echoed back.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        // Test 11  -   Open a websocket client call.
        //              Disable server keepalive via client message.
        //              Wait the timeout period.
        //              Check that the connection remains open.
        //              Attempt to send a ping after that.
        //              Verify a pong message is replied.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        // Test 12  -   Configure a short server keepalive.
        //              Open a websocket client call.
        //              Enable keepalive.
        //              Wait the timeout period.
        //              Check that the connection is closed.
        //              Attempt to send a ping on the closed connection.
        //              Verify the send failed.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        //              Verify the server-side WSEndpoint has a connectionId and DeviceId.
        // Test 13  -   Open a websocket connection.
        //              Send a connection registration message that is missing a DeviceId.
        //              Verify the connection closes.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        // Test 14  -   Open a websocket connection.
        //              Send a connection registration message that is missing a Connectionid.
        //              Verify the connection closes.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        // Test 15  -   Open a websocket connection.
        //              Send a connection registration message that is missing a UserId.
        //              Verify the connection stays open.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.


        // Test that a second registration message with different connid or deviceid will abort the connection..
        // Test 15.1 -  Open a websocket connection.
        //              Send a full connection registration message.
        //              Verify the connection stays open.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        //              Then, send a connection registration message with a different ConnectionId.
        //              Verify the connection closes.
        //              Verify the server-side WSEndpoint remains in a registered state.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        // Test 15.2 -  Open a websocket connection.
        //              Send a full connection registration message.
        //              Verify the connection stays open.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        //              Then, send a connection registration message with a different ConnectionId.
        //              Verify the connection closes.
        //              Verify the server-side WSEndpoint remains in a registered state.
        //              Verify the server-side WSEndpoint indicates a closed connection.


        //  Test that the server-side WSEndpoint Connection Time changes appropriately.
        //  It should update on connection, and remain constant, regardless of state.
        // Test 16a  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint connection time agrees.
        // Test 16b  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint connection time agrees.
        //              Close the client connection.
        //              Verify the server-side WSEndpoint connection time remains the same.
        // Test 16c  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint connection time agrees.
        //              Dispose of the client connection.
        //              Verify the server-side WSEndpoint connection time remains the same.


        //  Test that the server-side WSEndpoint UnregisteredAge changes appropriately.
        // Test 17a  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint UnRegisteredAge is non-zero and agrees with the connection time.
        // Test 17b  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint UnRegisteredAge is non-zero and agrees with the connection time.
        //              Send a connection registration message.
        //              Verify the server-side WSEndpoint UnRegisteredAge resets to zero.
        // Test 17c  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint UnRegisteredAge is non-zero and agrees with the connection time.
        //              Send a bad connection registration message (one missing a connectionId).
        //              Verify the server-side WSEndpoint UnRegisteredAge is still non-zero and agrees with the connection time.


        //  Test that the server-side WSEndpoint Last Received Timestamp updates for any received message.
        // Test 18a  -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        // Test 18b -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a chat message (representing any non-internal message type).
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        // Test 18c -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a connection registration message.
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        // Test 18d -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a message with an unknown type (one that would be received, but discarded).
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        // Test 18e -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send an empty message, just an empty string... "".
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        // Test 18f -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a message that is not a message envelope dto.
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        // Test 18g -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a ping message.
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        // Test 18h -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a local loopback message.
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.


        //  Test that the server-side WSEndpoint instance can provide a Connection Entry instance.
        // Test 19  -   Open a websocket connection.
        //              Send a connection registration message.
        //              Wait a second.
        //              Ask the WSEndpoint to provide a connection entry.
        //              Verify the connection entry matches the WSEndpoint data.


        //  Test that the server-side WSEndpoint instance can trigger a DispatchConnectionClosed when required.
        // Test 20a -   Open a websocket connection.
        //              Without delay, close the client connection.
        //              Wait a second for the connection loop to catch up and fall.
        //              Verify that a DispatchConnectionClosed was called.
        // Test 20b -   Open a websocket connection.
        //              Wait a second for the connection loop to start.
        //              Send a malformed connection registration message.
        //              Wait a second for the message to be processed.
        //              Verify that a DispatchConnectionClosed was called.
        // Test 20c -   Open a websocket connection.
        //              Enable a short keepalive duration.
        //              Wait long enough for the keepalive to trip.
        //              Verify that a DispatchConnectionClosed was called.
        // Test 20d -   Open a websocket connection.
        //              Wait a second for the connection loop to start.
        //              Call the StopAsync on the server-side WSEndpoint instance.
        //              Wait a second for the connection loop to fall.
        //              Verify that a DispatchConnectionClosed was called.
        // Test 20e -   Open a websocket connection.
        //              Wait a second for the connection loop to start.
        //              Close the client-side connection.
        //              Wait a second for the connection loop to fall.
        //              Verify that a DispatchConnectionClosed was called.
        // Test 20f -   Open a websocket connection.
        //              Wait a second for the connection loop to start.
        //              Dispose the client-side connection.
        //              Wait a second for the connection loop to fall.
        //              Verify that a DispatchConnectionClosed was called.


        //  Test server-side message handling of channel assigned messages.
        // Test 21a -   Open a websocket connection.
        //              Add no channel handlers to the server-side WSEndpoint.
        //              Send a message with an empty channel name to the server.
        //              Verify the message is received on the OnMessageReceived delegate.
        // Test 21b -   Open a websocket connection.
        //              Add a channel handler to the server-side WSEndpoint.
        //              Send a message with an empty channel name to the server.
        //              Verify the message is received on the OnMessageReceived delegate.
        // Test 21c -   Open a websocket connection.
        //              Add a couple of channel handlers to the server-side WSEndpoint.
        //              Send a message with a matching channel name to the server.
        //              Verify the message is received by the appropriate channel handler's delegate.
        // Test 21d -   Open a websocket connection.
        //              Add a couple of channel handlers to the server-side WSEndpoint.
        //              Send a message to the server with a channel name that doesn't match any handler.
        //              Verify the message is not handled by any delegate.


        //  Test server-side message sending.
        // Test 22a -   Open a websocket connection.
        //              From the server-side WSEndpoint, send a POCO instance.
        //              Verify the client receives the POCO instance and its correct class name.
        // Test 22b -   Open a websocket connection.
        //              From the server-side WSEndpoint, send a concreted POCO generic instance.
        //              Verify the client receives the concreted POCO generic instance and its correct class name.
        // Test 22c -   Open a websocket connection.
        //              From the server-side WSEndpoint, send a string instance.
        //              Verify the client receives the string and a type of string.
        // Test 22d -   Open a websocket connection.
        //              From the server-side WSEndpoint, send an integer instance.
        //              Verify the client receives the integer and a type of int32.
        // Test 22e -   Open a websocket connection.
        //              From the server-side WSEndpoint, send an empty string.
        //              Verify the client receives the blank string and a type of string.
        // Test 22f -   Open a websocket connection.
        //              From the server-side WSEndpoint, send the json string of a POCO with a type name.
        //              Verify the client receives the json string and the correct type name.


        //  Test that a channel handler name can only be added once.
        // Test 23a -   Open a websocket connection.
        //              From the server-side WSEndpoint, add the same channel handler twice.
        //              Verify the second attempt fails.

        //  Exercise the max message size that can be exchanged.
        // Test 24a -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 1000 byte string.
        //              Verify the server received the entire string.
        // Test 24b -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 2047 byte string.
        //              Verify the server received the entire string.
        // Test 24c -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 2048 byte string.
        //              Verify the server received the entire string.
        // Test 24d -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 2049 byte string.
        //              Verify the server received the entire string.
        // Test 24e -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 10KB string.
        //              Verify the server received the entire string.
        // Test 24f -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 1MB string.
        //              Verify the server received the entire string.
        // Test 24g -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 100MB string.
        //              Verify the server received the entire string.

     */

    [DoNotParallelize]
    [TestClass]
    public class TCPEndpoint_Tests : Testing_HelperBase
    {
        #region Private Fields

        private List<MessageEnvelope> receivedmsgs = new List<MessageEnvelope>();

        private Simple_TCPListener _wsl;
        string tcphost = "localhost";
        int tcpport = 5000;

        private CancellationTokenSource _receive_cts = new CancellationTokenSource();

        private int Received_RawMessage_Size = 0;
        private string Received_RawMessage_Hash = "";

        private cReceiveLoop clientrcvloop;
        
        #endregion


        #region Setup

        [ClassInitialize]
        static public void Setup_Class(TestContext context)
        {
            Setup_Class_Base(context);
        }

        [TestInitialize]
        override public void Setup()
        {
            base.Setup();


            // Runs before each test. (Optional)

            // Reset status...
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = false;

            CommonChannel.Callback_Queue = new System.Collections.Concurrent.ConcurrentQueue<string>();

            CommonChannel.CallbackChannel_MessageEnvelope = new System.Collections.Concurrent.ConcurrentQueue<MessageEnvelope>();

            /// Make sure we only add one of these...
            if(Trace.Listeners.Count == 0)
                Trace.Listeners.Add(new ConsoleTraceListener());
            else
            {
                if(!Trace.Listeners.OfType<ConsoleTraceListener>().Any())
                    Trace.Listeners.Add(new ConsoleTraceListener());
            }

            _wsl = new Simple_TCPListener();
            var res = _wsl.Start();
        }

        [TestCleanup]
        override public void TearDown()
        {
            // Runs after each test. (Optional)

            this.StopReceiveLoop();


            // Clear the received message list...
            this.receivedmsgs.Clear();


            try
            {
                CommonChannel.Callback_Queue.Clear();
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
                CommonChannel.CallbackChannel_MessageEnvelope.Clear();
            }
            catch(Exception ex)
            {
                int x = 0;
            }


            base.TearDown();
        }

        #endregion


        #region Tests

        //  Test_1_1_0  Create server endpoint instance with unopened TcpClient.
        //              Verify endpoint indicates closed and initialized.
        [TestMethod]
        public void Test_1_1_0()
        {
            TcpClient tcl = null;
            TCPEndpoint ep = null;
            try
            {
                tcl = new TcpClient();
                ep = new TCPEndpoint(tcl);

                if (ep.IsConnected)
                    Assert.Fail("Wrong Value");

                if(ep.State != TCP.eEndpoint_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");

                // Verify client info is not yet set...
                if(!string.IsNullOrEmpty(ep.ClientInfo.AppId))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.AppVersion))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.AuthLevel != TCP.Server.Model.eAuthLevel.NoAuth)
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.ClientIP))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.ConnectionId))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.DeviceId))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.Language))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.LibVersion))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.UserId != Guid.Empty)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    tcl?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    ep?.Dispose();
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_1_1  Create connected TcpClient pair.
        //              Create server endpoint instance with opened TcpClient.
        //              Verify both endpoints are connected.
        [TestMethod]
        public void Test_1_1_1()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            TcpClient tcl = null;
            TCPEndpoint ep = null;
            try
            {
                tcl = cth.Clientside_Connection;
                ep = new TCPEndpoint(cth.Serverside_Connection);

                // Start the endpoint, and give it its own thread...
                _= Task.Run(() => ep.Start_Async());

                // Give the endpoint a second to initialize and register as open...
                System.Threading.Thread.Sleep(500);

                if (!ep.IsConnected)
                    Assert.Fail("Wrong Value");

                if(ep.State != TCP.eEndpoint_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");

                // Verify client info is not yet set...
                if(!string.IsNullOrEmpty(ep.ClientInfo.AppId))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.AppVersion))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.AuthLevel != TCP.Server.Model.eAuthLevel.NoAuth)
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.ClientIP))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.ConnectionId))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.DeviceId))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.Language))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.LibVersion))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.UserId != Guid.Empty)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    tcl?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    ep?.Dispose();
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_1_2  Create connected TcpClient pair.
        //              Create server endpoint instance with opened TcpClient.
        //              Verify both endpoints are connected.
        //              Send a simple message from the client.
        //              Verify the server endpoint promotes to Open.
        //              Verify the endpoint client info indicates unregistered and is otherwise blank.
        [TestMethod]
        public async Task Test_1_1_2()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            TcpClient tcl = null;
            TCPEndpoint ep = null;
            try
            {
                tcl = cth.Clientside_Connection;
                ep = new TCPEndpoint(cth.Serverside_Connection);

                // Start the endpoint, and give it its own thread...
                _= Task.Run(() => ep.Start_Async());


                // Give the endpoint a second to initialize and register as open...
                System.Threading.Thread.Sleep(500);


                // Record the starting received counter...
                var rca = ep.Metrics.Received_Message_Count;
                // Ensure no messages have been received...
                if (rca != 0)
                    Assert.Fail("Wrong Value");


                // Verify the server endpoint is newly opened...
                if (!ep.IsConnected)
                    Assert.Fail("Wrong Value");
                if(ep.State != TCP.eEndpoint_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");


                // Send a simple message from the client...
                string messagetosend = "Hello";
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                var msg = new MessageEnvelope();
                msg.SentTimeUTC = DateTime.UtcNow;
                msg.Data = json;
                msg.MessageType = messagetosend.GetType().Name;
                msg.MsgId = Guid.NewGuid().ToString();
                //msg.Props;
                //msg.Channel = "";
                //msg.Scope = "";

                // Serialize the envelope...
                var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                var bytes = Encoding.UTF8.GetBytes(msgjson);
                await RawTransportSend(tcl.GetStream(), bytes);


                // Wait for transit...
                System.Threading.Thread.Sleep(1000);


                // Get the receive counter after transit...
                var rcb = ep.Metrics.Received_Message_Count;
                // Verify the endpoint received our message...
                if((rcb - rca) != 1)
                    Assert.Fail("Wrong Value");


                // Verify the server endpoint state...
                if (!ep.IsConnected)
                    Assert.Fail("Wrong Value");
                if(ep.State != TCP.eEndpoint_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify client info is not yet set...
                if(!string.IsNullOrEmpty(ep.ClientInfo.AppId))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.AppVersion))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.AuthLevel != TCP.Server.Model.eAuthLevel.NoAuth)
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.ClientIP))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.ConnectionId))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.DeviceId))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.IsRegistered)
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.Language))
                    Assert.Fail("Wrong Value");
                if(!string.IsNullOrEmpty(ep.ClientInfo.LibVersion))
                    Assert.Fail("Wrong Value");
                if(ep.ClientInfo.UserId != Guid.Empty)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    tcl?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    ep?.Dispose();
                }
                catch (Exception e) { }
            }
        }


        // Test 02  -   Do simple websocket client call.
        //              Verify we get a websocket connection.
        //              Verify the server-side TCPEndpoint indicates unregistered.
        //              Verify the server-side TCPEndpoint indicates an open connection.
        //              Verify the server-side TCPEndpoint has no connectionId or DeviceId.
        //              Dispose of the websocket client.
        //              Verify the server-side TCPEndpoint indicates a closed connection.
        [TestMethod]
        public async Task Test_02()
        {
            int counter = 0;
            int counter_limit = 1;


            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            await Task.Delay(1000);

            // Verify the server-side TCPEndpoint indicates unregistered.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("TCPEndpoint should claim unregistered.");

            // Verify the server-side TCPEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("TCPEndpoint should indicate open.");

            // Verify the server-side TCPEndpoint has no connectionId or DeviceId.
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId))
                Assert.Fail("TCPEndpoint has a connectionid.");
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId))
                Assert.Fail("TCPEndpoint has a DeviceId.");

            // Close the connection...
            tcp.Dispose();

            System.Threading.Thread.Sleep(1000);

            // Verify the server-side WSEndpoint indicates a closed connection.
            if(this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate closed.");
        }

        // Test 03  -   Do simple websocket client call.
        //              Send a ping signal.
        //              Verify we get a pong back.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        [TestMethod]
        public async Task Test_03()
        {
            int counter = 0;
            int counter_limit = 1;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            await Task.Delay(2000);

            while(tcp.Connected && counter < counter_limit)
            {
                // The websocket is open.

                try
                {
                    int ress = await SendPingMessage(tcp);
                    if(ress != 1)
                    {
                        // Failed to send.
                        Assert.Fail("Failed to send message.");
                    }


                    // Receive the message via our client connection...
                    if(this.receivedmsgs.Count != 1)
                        Assert.Fail("Failed to receive message.");
                    var me2 = this.receivedmsgs[0];
                    // Clear the received message list..
                    this.receivedmsgs.Clear();


                    // Check the return message...
                    if (me2.MessageType.ToLower() != "pong")
                        Assert.Fail("Receive reply has wrong message type");
                }
                finally
                {
                    counter++;
                }
            }
            // If here, the websocket failed to open, or we timed out.
            // Check which it was...
            if(counter >= counter_limit)
            {
                // We finished.
            }
            else
            {
                // The websocket failed to open.
                Assert.Fail("Failed to open websocket.");
            }

            // Verify the server-side WSEndpoint indicates unregistered.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim unregistered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // Verify the server-side WSEndpoint has no connectionId or DeviceId.
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId))
                Assert.Fail("WSEndpoint has a connectionid.");
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId))
                Assert.Fail("WSEndpoint has a DeviceId.");
        }

        // Test 04  -   Do simple websocket client call.
        //              Send a message with scope set to "loopback=rawmsg".
        //              Verify we get the same message replied back.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        [TestMethod]
        public async Task Test_04()
        {
            int counter = 0;
            int counter_limit = 1;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            await Task.Delay(2000);

            while(tcp.Connected && counter < counter_limit)
            {
                // The websocket is open.

                try
                {
                    var ress = await SendLocalEchoMessage(tcp);
                    if(ress.retcode != 1)
                    {
                        // Failed to send.
                        Assert.Fail("Failed to send message.");
                    }


                    // Receive the message via our client connection...
                    if(this.receivedmsgs.Count != 1)
                        Assert.Fail("Failed to receive message.");
                    var me2 = this.receivedmsgs[0];
                    // Clear the received message list..
                    this.receivedmsgs.Clear();


                    // Check the return message...
                    if (me2.MessageType != ress.msg.MessageType)
                        Assert.Fail("Receive reply has wrong message type");

                    if (me2.Data != ress.msg.Data)
                        Assert.Fail("Receive reply has wrong message data");

                    if (me2.MsgId != ress.msg.MsgId)
                        Assert.Fail("Receive reply has wrong MsgId");

                    if (me2.SentTimeUTC != ress.msg.SentTimeUTC)
                        Assert.Fail("Receive reply has wrong message SentTimeUTC");

                    if (me2.Scope != "")
                        Assert.Fail("Receive reply has wrong message scope that should be empty.");

                    if (me2.Channel != ress.msg.Channel)
                        Assert.Fail("Receive reply has wrong message Channel");

                    if (me2.ReplyTo != ress.msg.ReplyTo)
                        Assert.Fail("Receive reply has wrong message ReplyTo");

                    // Check props...
                    if(me2.Props.Length != ress.msg.Props.Length)
                        Assert.Fail("Receive reply has wrong message Props");
                    for(int x = 0; x < me2.Props.Length; x++)
                    {
                        if(me2.Props[x] != ress.msg.Props[x])
                            Assert.Fail("Receive reply has wrong message Props");
                    }
                }
                finally
                {
                    counter++;
                }
            }
            // If here, the websocket failed to open, or we timed out.
            // Check which it was...
            if(counter >= counter_limit)
            {
                // We finished.
            }
            else
            {
                // The websocket failed to open.
                Assert.Fail("Failed to open websocket.");
            }

            // Verify the server-side WSEndpoint indicates unregistered.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim unregistered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // Verify the server-side WSEndpoint has no connectionId or DeviceId.
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId))
                Assert.Fail("WSEndpoint has a connectionid.");
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId))
                Assert.Fail("WSEndpoint has a DeviceId.");
        }

        // Test 05  -   Do simple websocket client call.
        //              Send an empty message that should be disregarded, but not fail the connection.
        //              Send a ping after that.
        //              Verify we get a pong message back.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        [TestMethod]
        public async Task Test_05()
        {
            int counter = 0;
            int counter_limit = 1;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            await Task.Delay(2000);

            while(tcp.Connected && counter < counter_limit)
            {
                // The websocket is open.

                try
                {
                    // Send an empty message that should be disregarded, without a return...
                    var res1 = await SendEmptyMessage(tcp);
                    if(res1 != 1)
                    {
                        // Failed to send.
                        Assert.Fail("Failed to send message.");
                    }

                    // Send a ping, and wait for a pong...
                    var res2 = await SendPingMessage(tcp);
                    if(res2 != 1)
                    {
                        // Failed to send.
                        Assert.Fail("Failed to send message.");
                    }


                    // Receive the message via our client connection...
                    if(this.receivedmsgs.Count != 1)
                        Assert.Fail("Failed to receive message.");
                    var me2 = this.receivedmsgs[0];
                    // Clear the received message list..
                    this.receivedmsgs.Clear();


                    if(me2.MessageType != "pong")
                        Assert.Fail("Failed to receive pong message.");
                }
                finally
                {
                    counter++;
                }
            }
            // If here, the websocket failed to open, or we timed out.
            // Check which it was...
            if(counter >= counter_limit)
            {
                // We finished.
            }
            else
            {
                // The websocket failed to open.
                Assert.Fail("Failed to open websocket.");
            }

            // Verify the server-side WSEndpoint indicates unregistered.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim unregistered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // Verify the server-side WSEndpoint has no connectionId or DeviceId.
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId))
                Assert.Fail("WSEndpoint has a connectionid.");
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId))
                Assert.Fail("WSEndpoint has a DeviceId.");
        }

        // Test 06  -   Do simple websocket client call.
        //              Send a message with an unknown type that should be disregarded, but not fail the connection.
        //              Send a ping after that.
        //              Verify we get a pong message back.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        [TestMethod]
        public async Task Test_06()
        {
            int counter = 0;
            int counter_limit = 1;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            await Task.Delay(2000);

            while(tcp.Connected && counter < counter_limit)
            {
                // The websocket is open.

                try
                {
                    // Send a message of unknown type that should be disregarded, without a return...
                    var res1 = await SendUnknownTypeMessage(tcp);
                    if(res1 != 1)
                    {
                        // Failed to send.
                        Assert.Fail("Failed to send message.");
                    }

                    // Send a ping, and wait for a pong...
                    var res2 = await SendPingMessage(tcp);
                    if(res2 != 1)
                    {
                        // Failed to send.
                        Assert.Fail("Failed to send message.");
                    }


                    // Receive the message via our client connection...
                    if(this.receivedmsgs.Count != 1)
                        Assert.Fail("Failed to receive message.");
                    var me2 = this.receivedmsgs[0];
                    // Clear the received message list..
                    this.receivedmsgs.Clear();


                    if(me2.MessageType != "pong")
                        Assert.Fail("Failed to receive pong message.");
                }
                finally
                {
                    counter++;
                }
            }
            // If here, the websocket failed to open, or we timed out.
            // Check which it was...
            if(counter >= counter_limit)
            {
                // We finished.
            }
            else
            {
                // The websocket failed to open.
                Assert.Fail("Failed to open websocket.");
            }

            // Verify the server-side WSEndpoint indicates unregistered.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim unregistered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // Verify the server-side WSEndpoint has no connectionId or DeviceId.
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId))
                Assert.Fail("WSEndpoint has a connectionid.");
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId))
                Assert.Fail("WSEndpoint has a DeviceId.");
        }

        // Test 07  -   Do simple websocket client call.
        //              Send a close message.
        //              Verify we get a closing message back, and the connection is closed.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        [TestMethod]
        public async Task Test_07()
        {
            int counter = 0;
            int counter_limit = 1;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            await Task.Delay(2000);

            while(tcp.Connected && counter < counter_limit)
            {
                // The websocket is open.

                try
                {
                    // Close the client connection...
                    tcp.Close();

                    // Check the connection state...
                    if(tcp.Connected)
                        Assert.Fail("Failed to send message.");

                    // Send a ping, ensuring it fails...
                    var res2 = await SendPingMessage(tcp);
                    if(res2 != -2)
                    {
                        // Failed to send.
                        Assert.Fail("Failed to send message.");
                    }


                    // Receive the message via our client connection...
                    if(this.receivedmsgs.Count != 1)
                        Assert.Fail("Failed to receive message.");
                    var me2 = this.receivedmsgs[0];
                    // Clear the received message list..
                    this.receivedmsgs.Clear();


                    if(me2 != null)
                        Assert.Fail("Expected null message.");
                }
                finally
                {
                    counter++;
                }
            }
            // If here, the websocket failed to open, or we timed out.
            // Check which it was...
            if(counter >= counter_limit)
            {
                // We finished.
            }
            else
            {
                // The websocket failed to open.
                Assert.Fail("Failed to open websocket.");
            }

            // Verify the server-side WSEndpoint indicates unregistered.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim unregistered.");

            // Verify the server-side WSEndpoint indicates a closed connection.
            if(this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate closed.");

            // Verify the server-side WSEndpoint has no connectionId or DeviceId.
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId))
                Assert.Fail("WSEndpoint has a connectionid.");
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId))
                Assert.Fail("WSEndpoint has a DeviceId.");
        }

        // Test 08  -   Do simple websocket client call.
        //              Send a connection registration message.
        //              Verify the connection registration was received.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        [TestMethod]
        public async Task Test_08()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            int counter = 0;
            int counter_limit = 1;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            await Task.Delay(2000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();

            // Send a connection registration message...
            var res1 = await SendConnectionRegistrationMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Check that the connection registration message was received...
            if(!CommonChannel.Callback_Queue.TryDequeue(out var item))
                Assert.Fail("Message not received.");

            var connentry = Newtonsoft.Json.JsonConvert.DeserializeObject<ConnectionEntry_v1>(item);
            if(connentry == null)
                Assert.Fail("Failed to send message.");

            if(connentry.UserId != crd.UserId)
                Assert.Fail("Connection registration message was garbled.");

            if(connentry.DeviceId != crd.DeviceId)
                Assert.Fail("Connection registration message was garbled.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("Connection registration message was garbled.");

            // Verify the server-side WSEndpoint indicates registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
            if(this._wsl.ServerSide_TCPEndpoint.WSId != connentry.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != connentry.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != connentry.AppId)
                Assert.Fail("WSEndpoint has wrong AppId.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != connentry.AppVersion)
                Assert.Fail("WSEndpoint has wrong AppVersion.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.Language != connentry.Language)
                Assert.Fail("WSEndpoint has wrong Language.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != connentry.LibVersion)
                Assert.Fail("WSEndpoint has wrong WSLibVersion.");
        }

        // Test 09  -   Do simple websocket client call.
        //              Enable loopback for all messages via connection registration message.
        //              Test that all messages get echoed back.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        [TestMethod]
        public async Task Test_09()
        {
            //Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;

            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();


            // Send a loopback all message...
            var res1 = await SendLoopbackAllMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Clear out any received messages...
            Get_ReceivedMessages(out var msgs);

            // Send a random message...
            var res3 = await SendChatMessage(tcp);
            if(res3 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Check that it was echoed back to us...
            var res4 = Get_ReceivedMessages(out msgs);
            if(res4 != 1)
            {
                // Failed to receive.
                Assert.Fail("Failed to retrieve messages.");
            }

            // Disable loopback...
            var res5 = await SendLoopbackOFFMessage(tcp, crd);
            if(res5 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            // Send a random message...
            var res6 = await SendChatMessage(tcp);
            if(res6 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Verify nothing was echoed back to us...
            var res7 = Get_ReceivedMessages(out msgs);
            if(res7 != 0)
            {
                // Expected no received messages.
                Assert.Fail("Failed by receiving messages.");
            }

            // Verify the server-side WSEndpoint indicates registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            int x = 0;
        }

        // Test 10  -   Do simple websocket client call.
        //              Disable loopback of all messages via connection registration message.
        //              Test that no more messages get echoed back.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        [TestMethod]
        public async Task Test_10()
        {
            //Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;

            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();


            // Send a loopback all message...
            var res1 = await SendLoopbackAllMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Clear out any received messages...
            Get_ReceivedMessages(out var msgs);

            // Send a random message...
            var res3 = await SendChatMessage(tcp);
            if(res3 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Check that it was echoed back to us...
            var res4 = Get_ReceivedMessages(out msgs);
            if(res4 != 1)
            {
                // Failed to receive.
                Assert.Fail("Failed to retrieve messages.");
            }

            // Disable loopback...
            var res5 = await SendLoopbackOFFMessage(tcp, crd);
            if(res5 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            // Send a random message...
            var res6 = await SendChatMessage(tcp);
            if(res6 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Verify nothing was echoed back to us...
            var res7 = Get_ReceivedMessages(out msgs);
            if(res7 != 0)
            {
                // Expected no received messages.
                Assert.Fail("Failed by receiving messages.");
            }

            // Verify the server-side WSEndpoint indicates registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");

            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            int x = 0;
        }

        // Test 11  -   Open a websocket client call.
        //              Disable server keepalive via client message.
        //              Wait the timeout period.
        //              Check that the connection remains open.
        //              Attempt to send a ping after that.
        //              Verify a pong message is replied.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        [TestMethod]
        public async Task Test_11()
        {
            //Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;

            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();


            // Send a disable keepalive message...
            var res1 = await SendDisableKeepaliveMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Clear out any received messages...
            Get_ReceivedMessages(out var msgs);

            // Send a random message...
            var res3 = await SendChatMessage(tcp);
            if(res3 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            // Wait longer than the keepalive time should be...
            System.Threading.Thread.Sleep(4000);

            // Check that the socket is still open...
            if(!tcp.Connected)
            {
                // The connection is no longer open.
                Assert.Fail("Connection should still be open.");
            }

            // Send a ping message...
            var res6 = await SendPingMessage(tcp);
            if(res6 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Look for a received pong...
            var res7 = Get_ReceivedMessages(out msgs);
            if(res7 != 1)
            {
                // Expected one received message.
                Assert.Fail("Failed to receive one message.");
            }
            if (msgs[0].MessageType != "pong")
                Assert.Fail("Expected pong message.");

            // Verify the server-side WSEndpoint indicates registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            int x = 0;
        }

        // Test 12  -   Configure a short server keepalive.
        //              Open a websocket client call.
        //              Enable keepalive.
        //              Wait the timeout period.
        //              Check that the connection is closed.
        //              Attempt to send a ping on the closed connection.
        //              Verify the send failed.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        //              Verify the server-side WSEndpoint has a connectionId and DeviceId.
        [TestMethod]
        public async Task Test_12()
        {
            //Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = true;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();


            // Send an enable keepalive message...
            var res1 = await SendEnableKeepaliveMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Clear out any received messages...
            Get_ReceivedMessages(out var msgs);

            // Send a random message...
            var res3 = await SendChatMessage(tcp);
            if(res3 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            // Wait longer than the keepalive time should be...
            System.Threading.Thread.Sleep(10000);

            // Check that the socket closed...
            if(!tcp.Connected)
            {
                // The connection is no longer open.
                Assert.Fail("Connection should be aborted.");
            }

            // Try sending a ping message on the aborted connection...
            var res6 = await SendPingMessage(tcp);
            if(res6 != -2)
            {
                // Connection should have failed.
                Assert.Fail("Should have failed.");
            }

            // Verify the server-side WSEndpoint indicates registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates a closed connection.
            if(this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate closed.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            int x = 0;
        }

        // Test 13  -   Open a websocket connection.
        //              Send a connection registration message that is missing a DeviceId.
        //              Verify the connection closes.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        [TestMethod]
        public async Task Test_13()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;

            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();
            // Leave the device as blank...
            crd.DeviceId = "";


            // Send a malformed connection registration...
            var res1 = await SendConnectionRegistrationMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Check that the socket closed...
            if(!tcp.Connected)
            {
                // The connection is no longer open.
                Assert.Fail("Connection should be aborted.");
            }

            // Try sending a ping message on the aborted connection...
            var res6 = await SendPingMessage(tcp);
            if(res6 != -2)
            {
                // Connection should have failed.
                Assert.Fail("Should have failed.");
            }

            // Verify the server-side WSEndpoint indicates unregistered.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim unregistered.");

            // Verify the server-side WSEndpoint indicates a closed connection.
            if(this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate closed.");

            // Verify the server-side WSEndpoint has no connectionId or DeviceId.
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId))
                Assert.Fail("WSEndpoint connectionid should be blank.");
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId))
                Assert.Fail("WSEndpoint DeviceId should be blank.");

            int x = 0;
        }

        // Test 14  -   Open a websocket connection.
        //              Send a connection registration message that is missing a Connectionid.
        //              Verify the connection closes.
        //              Verify the server-side WSEndpoint indicates unregistered.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        //              Verify the server-side WSEndpoint has no connectionId or DeviceId.
        [TestMethod]
        public async Task Test_14()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;

            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();
            // Leave the connectionId blank...
            crd.ConnectionId = "";


            // Send a malformed connection registration...
            var res1 = await SendConnectionRegistrationMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Check that the socket closed...
            if(!tcp.Connected)
            {
                // The connection is no longer open.
                Assert.Fail("Connection should be aborted.");
            }

            // Try sending a ping message on the aborted connection...
            var res6 = await SendPingMessage(tcp);
            if(res6 != -2)
            {
                // Connection should have failed.
                Assert.Fail("Should have failed.");
            }

            // Verify the server-side WSEndpoint indicates unregistered.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim unregistered.");

            // Verify the server-side WSEndpoint indicates a closed connection.
            if(this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate closed.");

            // Verify the server-side WSEndpoint has no connectionId or DeviceId.
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId))
                Assert.Fail("WSEndpoint connectionid should be blank.");
            if(!string.IsNullOrEmpty(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId))
                Assert.Fail("WSEndpoint DeviceId should be blank.");

            int x = 0;
        }

        // Test 15  -   Open a websocket connection.
        //              Send a connection registration message that is missing a UserId.
        //              Verify the connection stays open.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        [TestMethod]
        public async Task Test_15()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;

            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();
            // Leave the userId blank...
            crd.UserId = Guid.Empty;


            // Send a malformed connection registration...
            var res1 = await SendConnectionRegistrationMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Check that the socket is still open...
            if(!tcp.Connected)
            {
                // The connection is no longer open.
                Assert.Fail("Connection should still be open.");
            }

            // Send a ping message...
            var res6 = await SendPingMessage(tcp);
            if(res6 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Look for a received pong...
            var res7 = Get_ReceivedMessages(out var msgs);
            if(res7 != 1)
            {
                // Expected one received message.
                Assert.Fail("Failed to receive one message.");
            }
            if (msgs[0].MessageType != "pong")
                Assert.Fail("Expected pong message.");

            // Verify the server-side WSEndpoint indicates registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            int x = 0;
        }


        // Test 15.1 -  Open a websocket connection.
        //              Send a full connection registration message.
        //              Verify the connection stays open.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        //              Then, send a connection registration message with a different ConnectionId.
        //              Verify the connection closes.
        //              Verify the server-side WSEndpoint remains in a registered state.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        [TestMethod]
        public async Task Test_15_1()
        {
            //Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;

            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();

            // Send a disable keepalive message...
            var res1 = await SendDisableKeepaliveMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Clear out any received messages...
            Get_ReceivedMessages(out var msgs);

            // Send a random message...
            var res3 = await SendChatMessage(tcp);
            if(res3 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            // Wait longer than the keepalive time should be...
            System.Threading.Thread.Sleep(4000);

            // Check that the socket is still open...
            if(!tcp.Connected)
            {
                // The connection is no longer open.
                Assert.Fail("Connection should still be open.");
            }

            // Send a ping message...
            var res6 = await SendPingMessage(tcp);
            if(res6 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Look for a received pong...
            var res7 = Get_ReceivedMessages(out msgs);
            if(res7 != 1)
            {
                // Expected one received message.
                Assert.Fail("Failed to receive one message.");
            }
            if (msgs[0].MessageType != "pong")
                Assert.Fail("Expected pong message.");

            // Verify the server-side WSEndpoint indicates registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            // Send a new connection registration message with a different ConnectionId...
            var crd2 = new clientproperties();
            crd2.CopyFrom(crd);
            var newconnid = Guid.NewGuid().ToString();
            crd2.ConnectionId = newconnid;
            var res8 = await SendConnectionRegistrationMessage(tcp, crd2);
            if(res8 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Check that the socket closed...
            if(tcp.Connected)
            {
                // The connection is still open.
                Assert.Fail("Connection should have closed.");
            }

            // Verify the server-side WSEndpoint remains registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates a closed connection.
            if(this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate closed.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            // Verify the server still has the first connectionid...
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            int x = 0;
        }

        // Test 15.2 -  Open a websocket connection.
        //              Send a full connection registration message.
        //              Verify the connection stays open.
        //              Verify the server-side WSEndpoint indicates registered.
        //              Verify the server-side WSEndpoint indicates an open connection.
        //              Verify the server-side WSEndpoint has the correct connectionId and DeviceId.
        //              Then, send a connection registration message with a different ConnectionId.
        //              Verify the connection closes.
        //              Verify the server-side WSEndpoint remains in a registered state.
        //              Verify the server-side WSEndpoint indicates a closed connection.
        [TestMethod]
        public async Task Test_15_2()
        {
            //Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;

            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start a receive loop, so we only have to collect waiting messages...
            var rrr = await StartReceiveLoop(tcp);

            await Task.Delay(1000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();


            // Send a disable keepalive message...
            var res1 = await SendDisableKeepaliveMessage(tcp, crd);
            if(res1 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            // Store off the registration data that was used...
            Guid userid = (Guid)crd.UserId;
            string connid = crd.ConnectionId;
            string deviceid = crd.DeviceId;

            System.Threading.Thread.Sleep(1000);

            // Clear out any received messages...
            Get_ReceivedMessages(out var msgs);

            // Send a random message...
            var res3 = await SendChatMessage(tcp);
            if(res3 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            // Wait longer than the keepalive time should be...
            System.Threading.Thread.Sleep(4000);

            // Check that the socket is still open...
            if(!tcp.Connected)
            {
                // The connection is no longer open.
                Assert.Fail("Connection should still be open.");
            }

            // Send a ping message...
            var res6 = await SendPingMessage(tcp);
            if(res6 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Look for a received pong...
            var res7 = Get_ReceivedMessages(out msgs);
            if(res7 != 1)
            {
                // Expected one received message.
                Assert.Fail("Failed to receive one message.");
            }
            if (msgs[0].MessageType != "pong")
                Assert.Fail("Expected pong message.");

            // Verify the server-side WSEndpoint indicates registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates an open connection.
            if(!this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate open.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            // Send a new connection registration message with a different DeviceId...
            var crd2 = new clientproperties();
            crd2.CopyFrom(crd);
            var newdeviceid = Guid.NewGuid().ToString();
            crd2.DeviceId = newdeviceid;
            var res8 = await SendConnectionRegistrationMessage(tcp, crd2);
            if(res8 != 1)
            {
                // Failed to send.
                Assert.Fail("Failed to send message.");
            }

            System.Threading.Thread.Sleep(1000);

            // Check that the socket closed...
            if(tcp.Connected)
            {
                // The connection is still open.
                Assert.Fail("Connection should have closed.");
            }

            // Verify the server-side WSEndpoint remains registered.
            if(!this._wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("WSEndpoint should claim registered.");

            // Verify the server-side WSEndpoint indicates a closed connection.
            if(this._wsl.ServerSide_TCPEndpoint.IsConnected)
                Assert.Fail("WSEndpoint should indicate closed.");

            // The sent connectionid, from our wsclient is different than the tracking id the server goes by.
            // This change has been done to prevent client-forgery of a connectionId.
            // Since this test is intended to verify the submitted data, regardless of its use, server-side, we will still check the
            //  submitted connectionId for accuracy... against what the server has for it.
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionId != crd.ConnectionId)
                Assert.Fail("WSEndpoint has wrong connectionid.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.DeviceId != crd.DeviceId)
                Assert.Fail("WSEndpoint has wrong DeviceId.");

            int x = 0;
        }


        // Test 16a  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint connection time agrees.
        [TestMethod]
        public async Task Test_16a()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side WSEndpoint Connection Time agrees...
            var ssepctime = this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionTimeUTC;
            var deltat = ssepctime.Subtract(conntime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Expected pong message.");

            int x = 0;
        }

        // Test 16b  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint connection time agrees.
        //              Close the client connection.
        //              Verify the server-side WSEndpoint connection time remains the same.
        [TestMethod]
        public async Task Test_16b()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side WSEndpoint Connection Time agrees...
            var ssepctime = this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionTimeUTC;
            var deltat = ssepctime.Subtract(conntime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Expected pong message.");

            // Close the client connection...
            tcp.Close();

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side WSEndpoint Connection Time agrees...
            var ssepctime2 = this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionTimeUTC;
            var deltat2 = ssepctime2.Subtract(conntime);

            if(deltat2.TotalMilliseconds > 100)
                Assert.Fail("Expected pong message.");

            int x = 0;
        }

        // Test 16c  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint connection time agrees.
        //              Dispose of the client connection.
        //              Verify the server-side WSEndpoint connection time remains the same.
        [TestMethod]
        public async Task Test_16c()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side WSEndpoint Connection Time agrees...
            var ssepctime = this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionTimeUTC;
            var deltat = ssepctime.Subtract(conntime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Expected pong message.");

            // Dispose the client connection...
            tcp.Dispose();

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side WSEndpoint Connection Time agrees...
            var ssepctime2 = this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionTimeUTC;
            var deltat2 = ssepctime2.Subtract(conntime);

            if(deltat2.TotalMilliseconds > 100)
                Assert.Fail("Expected pong message.");

            int x = 0;
        }

        // Test 17a  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint UnRegisteredAge is non-zero and agrees with the connection time.
        [TestMethod]
        public async Task Test_17a()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side Unregistered Age agrees...
            var unregage = this._wsl.ServerSide_TCPEndpoint.ClientInfo.UnRegisteredAge;
            var connduration = DateTime.UtcNow.Subtract(conntime);
            var deltat = connduration.Subtract(unregage);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Registration age is too high.");

            int x = 0;
        }

        // Test 17b  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint UnRegisteredAge is non-zero and agrees with the connection time.
        //              Send a connection registration message.
        //              Verify the server-side WSEndpoint UnRegisteredAge resets to zero.
        [TestMethod]
        public async Task Test_17b()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side Unregistered Age agrees...
            var unregage = this._wsl.ServerSide_TCPEndpoint.ClientInfo.UnRegisteredAge;
            var connduration = DateTime.UtcNow.Subtract(conntime);
            var deltat = connduration.Subtract(unregage);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Registration age is too high.");


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();


            // Send a registration message...
            var res = await this.SendConnectionRegistrationMessage(tcp, crd);
            if (res != 1)
                Assert.Fail("Failed to send registration message.");

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the registration age resets...
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.UnRegisteredAge.TotalMilliseconds != 0)
                Assert.Fail("Registration age should be zero.");

            int x = 0;
        }

        // Test 17c  -  Open a websocket connection.
        //              Mark the connection Time.
        //              Verify the server-side WSEndpoint UnRegisteredAge is non-zero and agrees with the connection time.
        //              Send a bad connection registration message (one missing a connectionId).
        //              Verify the server-side WSEndpoint UnRegisteredAge is still non-zero and agrees with the connection time.
        [TestMethod]
        public async Task Test_17c()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side Unregistered Age agrees...
            var unregage = this._wsl.ServerSide_TCPEndpoint.ClientInfo.UnRegisteredAge;
            var connduration = DateTime.UtcNow.Subtract(conntime);
            var deltat = connduration.Subtract(unregage);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Registration age is too high.");


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();
            // Leave the connectionId blank...
            crd.ConnectionId = "";


            // Send a registration message...
            var res = await this.SendConnectionRegistrationMessage(tcp, crd);
            if (res != 1)
                Assert.Fail("Failed to send registration message.");

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Check that the server-side Unregistered Age remains non-zero and larger than before...
            var unregage2 = this._wsl.ServerSide_TCPEndpoint.ClientInfo.UnRegisteredAge;
            var connduration2 = DateTime.UtcNow.Subtract(conntime);
            var deltat2 = connduration.Subtract(unregage);

            if(deltat2.TotalMilliseconds > 100)
                Assert.Fail("Registration age is too high.");

            if(deltat.TotalMilliseconds > deltat2.TotalMilliseconds)
                Assert.Fail("Registration age mismatch.");

            int x = 0;
        }

        // Test 18a  -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        [TestMethod]
        public async Task Test_18a()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(2000);

            // Check that the server-side Last Received time agrees...
            var lrtime = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat = conntime.Subtract(lrtime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Last Receive time is incorrect.");

            int x = 0;
        }

        // Test 18b -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a chat message (representing any non-internal message type).
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        [TestMethod]
        public async Task Test_18b()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(2000);

            // Check that the server-side Last Received time agrees...
            var lrtime = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat = conntime.Subtract(lrtime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Last Receive time is incorrect.");

            // Send a chat message...
            var res = await this.SendChatMessage(tcp);
            if (res != 1)
                Assert.Fail("Failed to send chat message.");

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(100);

            // Check that the server-side Last Received time is very recent...
            var lrtime2 = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat2 = DateTime.UtcNow.Subtract(lrtime2);

            if(deltat2.TotalMilliseconds > 150)
                Assert.Fail("Last Receive time is too old.");

            int x = 0;
        }

        // Test 18c -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a connection registration message.
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        [TestMethod]
        public async Task Test_18c()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(2000);

            // Check that the server-side Last Received time agrees...
            var lrtime = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat = conntime.Subtract(lrtime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Last Receive time is incorrect.");


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();


            // Send a connection request message...
            var res = await this.SendConnectionRegistrationMessage(tcp, crd);
            if (res != 1)
                Assert.Fail("Failed to send connection request message.");

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(100);

            // Check that the server-side Last Received time is very recent...
            var lrtime2 = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat2 = DateTime.UtcNow.Subtract(lrtime2);

            if(deltat2.TotalMilliseconds > 150)
                Assert.Fail("Last Receive time is too old.");

            int x = 0;
        }

        // Test 18d -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a message with an unknown type (one that would be received, but discarded).
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        [TestMethod]
        public async Task Test_18d()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(2000);

            // Check that the server-side Last Received time agrees...
            var lrtime = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat = conntime.Subtract(lrtime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Last Receive time is incorrect.");

            // Send a connection request message...
            var res = await this.SendUnknownTypeMessage(tcp);
            if (res != 1)
                Assert.Fail("Failed to send connection request message.");

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(100);

            // Check that the server-side Last Received time is very recent...
            var lrtime2 = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat2 = DateTime.UtcNow.Subtract(lrtime2);

            if(deltat2.TotalMilliseconds > 150)
                Assert.Fail("Last Receive time is too old.");

            int x = 0;
        }

        // Test 18e -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send an empty message, just an empty string... "".
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        [TestMethod]
        public async Task Test_18e()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(2000);

            // Check that the server-side Last Received time agrees...
            var lrtime = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat = conntime.Subtract(lrtime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Last Receive time is incorrect.");

            // Send a connection request message...
            var res = await this.SendEmptyMessage(tcp);
            if (res != 1)
                Assert.Fail("Failed to send connection request message.");

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(100);

            // Check that the server-side Last Received time is very recent...
            var lrtime2 = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat2 = DateTime.UtcNow.Subtract(lrtime2);

            if(deltat2.TotalMilliseconds > 150)
                Assert.Fail("Last Receive time is too old.");

            int x = 0;
        }

        // Test 18f -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a message that is not a message envelope dto.
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        [TestMethod]
        public async Task Test_18f()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(2000);

            // Check that the server-side Last Received time agrees...
            var lrtime = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat = conntime.Subtract(lrtime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Last Receive time is incorrect.");

            // Send a non-MessageEnvelope message...
            var res = await this.SendNonMessageEnvelopeMessage(tcp);
            if (res != 1)
                Assert.Fail("Failed to send non-messageenvelope message.");

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(100);

            // Check that the server-side Last Received time is very recent...
            var lrtime2 = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat2 = DateTime.UtcNow.Subtract(lrtime2);

            if(deltat2.TotalMilliseconds > 150)
                Assert.Fail("Last Receive time is too old.");

            int x = 0;
        }

        // Test 18g -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a ping message.
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        [TestMethod]
        public async Task Test_18g()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(2000);

            // Check that the server-side Last Received time agrees...
            var lrtime = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat = conntime.Subtract(lrtime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Last Receive time is incorrect.");

            // Send a ping message...
            var res = await this.SendPingMessage(tcp);
            if (res != 1)
                Assert.Fail("Failed to send ping message.");

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(100);

            // Check that the server-side Last Received time is very recent...
            var lrtime2 = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat2 = DateTime.UtcNow.Subtract(lrtime2);

            if(deltat2.TotalMilliseconds > 150)
                Assert.Fail("Last Receive time is too old.");

            int x = 0;
        }

        // Test 18h -   Open a websocket connection.
        //              Mark the connection Time.
        //              Wait a couple seconds.
        //              Verify the server-side WSEndpoint Last Received timestamp agrees with the connection time.
        //              (The last received time should be same or slightly newer than the connection time).
        //              Send a local loopback message.
        //              Verify the server-side WSEndpoint Last Received timestamp updates to near current time.
        [TestMethod]
        public async Task Test_18h()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Mark the connection time...
            DateTime conntime = DateTime.UtcNow;

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(2000);

            // Check that the server-side Last Received time agrees...
            var lrtime = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat = conntime.Subtract(lrtime);

            if(deltat.TotalMilliseconds > 100)
                Assert.Fail("Last Receive time is incorrect.");

            // Send a local loopback message...
            var res = await this.SendLocalEchoMessage(tcp);
            if (res.retcode != 1)
                Assert.Fail("Failed to send local loopback message.");

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(100);

            // Check that the server-side Last Received time is very recent...
            var lrtime2 = this._wsl.ServerSide_TCPEndpoint.LastReceivedTimeUTC;
            var deltat2 = DateTime.UtcNow.Subtract(lrtime2);

            if(deltat2.TotalMilliseconds > 150)
                Assert.Fail("Last Receive time is too old.");

            int x = 0;
        }


        //  Test that the server-side WSEndpoint instance can provide a Connection Entry instance.
        // Test 19  -   Open a websocket connection.
        //              Send a connection registration message.
        //              Wait a second.
        //              Ask the WSEndpoint to provide a connection entry.
        //              Verify the connection entry matches the WSEndpoint data.
        [TestMethod]
        public async Task Test_19()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();


            // Send a connection registration message...
            var res = await this.SendConnectionRegistrationMessage(tcp, crd);
            if (res != 1)
                Assert.Fail("Failed to send connection registration message.");

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(1000);

            // Tell the WSEndpoint to create a connection entry...
            var connentry = new ConnectionEntry_v1();
            this._wsl.ServerSide_TCPEndpoint.Populate_ConnectionEntry(connentry);

            // The server-side now uses the WSId property of a WSEndpoint for the connectionId in registration data.
            // So, we need to verify that, here.
            // Check that the server-side WSEndpoint's WSId matches what the server-side used for registration data...
            if(this._wsl.ServerSide_TCPEndpoint.WSId != connentry.ConnectionId)
                Assert.Fail("Wrong Connection Registration Data.");
            if(crd.DeviceId != connentry.DeviceId)
                Assert.Fail("Wrong Connection Registration Data.");
            if(crd.UserId != connentry.UserId)
                Assert.Fail("Wrong Connection Registration Data.");
            if(connentry.ConnectionTimeUTC != this._wsl.ServerSide_TCPEndpoint.ClientInfo.ConnectionTimeUTC)
                Assert.Fail("Wrong Connection Registration Data.");

            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.AppId != connentry.AppId)
                Assert.Fail("Wrong Connection Registration Data.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.AppVersion != connentry.AppVersion)
                Assert.Fail("Wrong Connection Registration Data.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.Language != connentry.Language)
                Assert.Fail("Wrong Connection Registration Data.");
            if(this._wsl.ServerSide_TCPEndpoint.ClientInfo.LibVersion != connentry.LibVersion)
                Assert.Fail("Wrong Connection Registration Data.");

            int x = 0;
        }


        //  Test that the server-side WSEndpoint instance can trigger a DispatchConnectionClosed when required.
        // Test 20a -   Open a websocket connection.
        //              Without delay, close the client connection.
        //              Wait a second for the connection loop to catch up and fall.
        //              Verify that a DispatchConnectionClosed was called.
        [TestMethod]
        public async Task Test_20a()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Store the current connection closure counter...
            var closurecount_before = this._wsl.ClosureCount;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Quickly close the client connection...
            tcp.Close();

            // Wait a second for the connection loop to detect the fall and close down...
            System.Threading.Thread.Sleep(7000);

            // Get the updated closure count...
            var closurecount_after = this._wsl.ClosureCount;
            var count = closurecount_after - closurecount_before;

            if(count != 1)
                Assert.Fail("Failed to register closure.");

            int x = 0;
        }

        // Test 20b -   Open a websocket connection.
        //              Wait a second for the connection loop to start.
        //              Send a malformed connection registration message.
        //              Wait a second for the message to be processed.
        //              Verify that a DispatchConnectionClosed was called.
        [TestMethod]
        public async Task Test_20b()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Store the current connection closure counter...
            var closurecount_before = this._wsl.ClosureCount;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the connection loop to startup...
            System.Threading.Thread.Sleep(3000);


            // Create some client registration data for a WSLibver=2 client...
            var crd = clientproperties.Create_Random_WSLibV2_ClientData();
            // Leave the connectionid blank...
            crd.ConnectionId = "";


            // Send a malformed connection registration message...
            var res = await this.SendConnectionRegistrationMessage(tcp, crd);
            if(res != 1)
                Assert.Fail("Failed to send connection registration message.");

            // Wait a second for it to process...
            System.Threading.Thread.Sleep(3000);

            // Get the updated closure count...
            var closurecount_after = this._wsl.ClosureCount;
            var count = closurecount_after - closurecount_before;

            if(count != 1)
                Assert.Fail("Failed to register closure.");

            int x = 0;
        }

        // Test 20c -   Open a websocket connection.
        //              Enable a short keepalive duration.
        //              Wait long enough for the keepalive to trip.
        //              Verify that a DispatchConnectionClosed was called.
        [TestMethod]
        public async Task Test_20c()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = true;
            Simple_TCPListener.AllowQuietClients = false;

            // Store the current connection closure counter...
            var closurecount_before = this._wsl.ClosureCount;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait for the keepalive to trip...
            System.Threading.Thread.Sleep(6000);

            // Get the updated closure count...
            var closurecount_after = this._wsl.ClosureCount;
            var count = closurecount_after - closurecount_before;

            if(count != 1)
                Assert.Fail("Failed to register closure.");

            int x = 0;
        }

        // Test 20d -   Open a websocket connection.
        //              Wait a second for the connection loop to start.
        //              Call the StopAsync on the server-side WSEndpoint instance.
        //              Wait a second for the connection loop to fall.
        //              Verify that a DispatchConnectionClosed was called.
        [TestMethod]
        public async Task Test_20d()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Store the current connection closure counter...
            var closurecount_before = this._wsl.ClosureCount;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the connection loop to startup...
            System.Threading.Thread.Sleep(3000);

            // Call the stop async on the server side...
            await this._wsl.ServerSide_TCPEndpoint.Stop_Async();

            // Wait a second for the connection loop to fall...
            System.Threading.Thread.Sleep(4000);

            // Get the updated closure count...
            var closurecount_after = this._wsl.ClosureCount;
            var count = closurecount_after - closurecount_before;

            if(count != 1)
                Assert.Fail("Failed to register closure.");

            int x = 0;
        }

        // Test 20e -   Open a websocket connection.
        //              Wait a second for the connection loop to start.
        //              Close the client-side connection.
        //              Wait a second for the connection loop to fall.
        //              Verify that a DispatchConnectionClosed was called.
        [TestMethod]
        public async Task Test_20e()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Store the current connection closure counter...
            var closurecount_before = this._wsl.ClosureCount;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the connection loop to startup...
            System.Threading.Thread.Sleep(3000);

            // Call the stop async on the client side...
            tcp.Close();

            // Wait a second for the connection loop to fall...
            System.Threading.Thread.Sleep(4000);

            // Get the updated closure count...
            var closurecount_after = this._wsl.ClosureCount;
            var count = closurecount_after - closurecount_before;

            if(count != 1)
                Assert.Fail("Failed to register closure.");

            int x = 0;
        }

        // Test 20f -   Open a websocket connection.
        //              Wait a second for the connection loop to start.
        //              Dispose the client-side connection.
        //              Wait a second for the connection loop to fall.
        //              Verify that a DispatchConnectionClosed was called.
        [TestMethod]
        public async Task Test_20f()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Store the current connection closure counter...
            var closurecount_before = this._wsl.ClosureCount;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the connection loop to startup...
            System.Threading.Thread.Sleep(3000);

            tcp.Dispose();

            // Wait a second for the connection loop to fall...
            System.Threading.Thread.Sleep(4000);

            // Get the updated closure count...
            var closurecount_after = this._wsl.ClosureCount;
            var count = closurecount_after - closurecount_before;

            if(count != 1)
                Assert.Fail("Failed to register closure.");

            int x = 0;
        }


        //  Test server-side message handling of channel assigned messages.
        // Test 21a -   Open a websocket connection.
        //              Add no channel handlers to the server-side WSEndpoint.
        //              Send a message with an empty channel name to the server.
        //              Verify the message is received on the OnMessageReceived delegate.
        [TestMethod]
        public async Task Test_21a()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the connection loop to start...
            System.Threading.Thread.Sleep(2000);

            // Setup a handler to the no-channel delegate...
            this._wsl.ServerSide_TCPEndpoint.OnMessageReceived = this.CALLBACK_OnMessageReceived;

            // Clear the received message list...
            this.receivedmsgs.Clear();

            // Send a message with no channel named...
            var res = await this.SendChatMessage(tcp);
            if(res != 1)
                Assert.Fail("Failed to send message.");

            // Wait for the message to propagate...
            System.Threading.Thread.Sleep(4000);

            // Verify the message is received on the no-channel delegate...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");

            // Get the message...
            var msg = this.receivedmsgs[0];

            int x = 0;
        }

        // Test 21b -   Open a websocket connection.
        //              Add a channel handler to the server-side WSEndpoint.
        //              Send a message with an empty channel name to the server.
        //              Verify the message is received on the OnMessageReceived delegate.
        [TestMethod]
        public async Task Test_21b()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the connection loop to start...
            System.Threading.Thread.Sleep(2000);

            // Add a channel handler to the server-side WSEndpoint...
            var res = this._wsl.ServerSide_TCPEndpoint.Add_ChannelHandler("fff", this.handler_callback);
            if(res != 1)
                Assert.Fail("Failed to send message.");

            // Setup a handler to the no-channel delegate...
            this._wsl.ServerSide_TCPEndpoint.OnMessageReceived = this.CALLBACK_OnMessageReceived;

            // Clear the received message list...
            this.receivedmsgs.Clear();

            // Send a message with no channel named...
            var res2 = await this.SendChatMessage(tcp);
            if(res2 != 1)
                Assert.Fail("Failed to send message.");

            // Wait for the message to propagate...
            System.Threading.Thread.Sleep(4000);

            // Verify the message is received on the no-channel delegate...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");

            // Get the message...
            var msg = this.receivedmsgs[0];

            int x = 0;
        }

        // Test 21c -   Open a websocket connection.
        //              Add a couple of channel handlers to the server-side WSEndpoint.
        //              Send a message with a matching channel name to the server.
        //              Verify the message is received by the appropriate channel handler's delegate.
        [TestMethod]
        public async Task Test_21c()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionClosure = true;
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 10;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the connection loop to start...
            System.Threading.Thread.Sleep(3000);

            // Add a channel handler to the server-side WSEndpoint...
            var res = this._wsl.ServerSide_TCPEndpoint.Add_ChannelHandler("handler1_callback", this.handler1_callback);
            if(res != 1)
                Assert.Fail("Failed to send message.");
            var res2 = this._wsl.ServerSide_TCPEndpoint.Add_ChannelHandler("handler2_callback", this.handler2_callback);
            if(res2 != 1)
                Assert.Fail("Failed to send message.");

            // Setup a handler to the no-channel delegate...
            this._wsl.ServerSide_TCPEndpoint.OnMessageReceived = this.CALLBACK_OnMessageReceived;

            // Clear the received message list...
            this.receivedmsgs.Clear();

            // Send a message on named channel...
            var res3 = await this.SendMessage_onChannel(tcp, "handler1_callback");
            if(res3 != 1)
                Assert.Fail("Failed to send message.");

            // Wait for the message to propagate...
            System.Threading.Thread.Sleep(4000);

            // Verify the message is received on the no-channel delegate...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");

            // Get the message...
            var msg = this.receivedmsgs[0];

            // Check that the message was received on the correct channel...
            if(msg.Channel != "handler1_callback")
                Assert.Fail("Failed to receive message.");

            int x = 0;
        }

        // Test 21d -   Open a websocket connection.
        //              Add a couple of channel handlers to the server-side WSEndpoint.
        //              Send a message to the server with a channel name that doesn't match any handler.
        //              Verify the message is not handled by any delegate.


        // Test 22a -   Open a websocket connection.
        //              From the server-side WSEndpoint, send a POCO instance.
        //              Verify the client receives the POCO instance and its correct class name.
        [TestMethod]
        public async Task Test_22a()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start...
            System.Threading.Thread.Sleep(1000);


            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);


            // Have the server WSEndpoint send a a json string of a POCO with a type name...
            var ob = new SimplePOCO2();
            ob.SignatureB64 = Guid.NewGuid().ToString();
            ob.KeyId = Guid.NewGuid().ToString();
            ob.Algo = Guid.NewGuid().ToString();
            var senttype = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(ob);

            var res1 = await this._wsl.ServerSide_TCPEndpoint.SendMessage_toClient(ob, "", "");
            if (res1 != 1)
                Assert.Fail("Failed to send json object back to client.");


            // Receive the message via our client connection...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");
            var me2 = this.receivedmsgs[0];


            // Check that we received the correct datatype...
            if(me2.MessageType != senttype)
                Assert.Fail("Failed to receive message.");

            // Deserialize the message...
            var ob2 = Newtonsoft.Json.JsonConvert.DeserializeObject<SimplePOCO2>(me2.Data);

            if(ob2.Algo != ob.Algo)
                Assert.Fail("Failed to receive message.");
            if(ob2.KeyId != ob.KeyId)
                Assert.Fail("Failed to receive message.");
            if(ob2.SignatureB64 != ob.SignatureB64)
                Assert.Fail("Failed to receive message.");

            int x = 0;
        }

        // Test 22b -   Open a websocket connection.
        //              From the server-side WSEndpoint, send a concreted POCO generic instance.
        //              Verify the client receives the concreted POCO generic instance and its correct class name.
        [TestMethod]
        public async Task Test_22b()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            // Wait a second for the server connection loop to start...
            System.Threading.Thread.Sleep(1000);

            // Have the server WSEndpoint send a concreted POCO generic instance...
            var strl = new List<string>();
            var ob = new SimpleGeneric<string>();
            ob.Name = Guid.NewGuid().ToString();
            ob.Value = Guid.NewGuid().ToString();

            var senttype = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(ob);

            var res1 = await this._wsl.ServerSide_TCPEndpoint.SendMessage_toClient(ob, "", "");
            if (res1 != 1)
                Assert.Fail("Failed to send json object back to client.");


            // Receive the message via our client connection...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");
            var me2 = this.receivedmsgs[0];


            // Check that we received the correct datatype...
            if(me2.MessageType != senttype)
                Assert.Fail("Failed to receive message.");

            // Deserialize the message...
            var ob2 = Newtonsoft.Json.JsonConvert.DeserializeObject<SimpleGeneric<string>>(me2.Data);

            if(ob2.Name != ob.Name)
                Assert.Fail("Failed to receive message.");
            if(ob2.Value != ob.Value)
                Assert.Fail("Failed to receive message.");

            int x = 0;
        }

        // Test 22c -   Open a websocket connection.
        //              From the server-side WSEndpoint, send a string instance.
        //              Verify the client receives the string and a type of string.
        [TestMethod]
        public async Task Test_22c()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            // Wait a second for the server connection loop to start...
            System.Threading.Thread.Sleep(1000);

            // Have the server WSEndpoint send a string...
            string ob = Nanoid.Nanoid.Generate( size:100);
            var senttype = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(ob);

            var res1 = await this._wsl.ServerSide_TCPEndpoint.SendMessage_toClient(ob, "", "");
            if (res1 != 1)
                Assert.Fail("Failed to send json object back to client.");


            // Receive the message via our client connection...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");
            var me2 = this.receivedmsgs[0];


            // Check that we received the correct datatype...
            if(me2.MessageType != senttype)
                Assert.Fail("Failed to receive message.");

            // Deserialize the message...
            var ob2 = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(me2.Data);

            if(ob2 != ob)
                Assert.Fail("Failed to receive message.");

            int x = 0;
        }

        // Test 22d -   Open a websocket connection.
        //              From the server-side WSEndpoint, send an integer instance.
        //              Verify the client receives the integer and a type of int32.
        [TestMethod]
        public async Task Test_22d()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            // Wait a second for the server connection loop to start...
            System.Threading.Thread.Sleep(1000);

            // Have the server WSEndpoint send an integer...
            int ob = 1234;
            var senttype = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(ob);

            var res1 = await this._wsl.ServerSide_TCPEndpoint.SendMessage_toClient(ob, "", "");
            if (res1 != 1)
                Assert.Fail("Failed to send json object back to client.");


            // Receive the message via our client connection...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");
            var me2 = this.receivedmsgs[0];


            // Check that we received the correct datatype...
            if(me2.MessageType != senttype)
                Assert.Fail("Failed to receive message.");

            // Deserialize the message...
            var ob2 = Newtonsoft.Json.JsonConvert.DeserializeObject<Int32>(me2.Data);

            if(ob2 != ob)
                Assert.Fail("Failed to receive message.");

            int x = 0;
        }

        // Test 22e -   Open a websocket connection.
        //              From the server-side WSEndpoint, send an empty string.
        //              Verify the client receives the blank string and a type of string.
        [TestMethod]
        public async Task Test_22e()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            // Wait a second for the server connection loop to start...
            System.Threading.Thread.Sleep(1000);

            var res1 = await this._wsl.ServerSide_TCPEndpoint.Send_SerializedObject_toClient_Async("string", "", "", "");
            if (res1 != 1)
                Assert.Fail("Failed to send json object back to client.");


            // Receive the message via our client connection...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");
            var me2 = this.receivedmsgs[0];


            // Check that we received the correct datatype...
            if(me2.MessageType != "string")
                Assert.Fail("Failed to receive message.");

            if(me2.Data != "")
                Assert.Fail("Failed to receive message.");

            int x = 0;
        }

        // Test 22f -   Open a websocket connection.
        //              From the server-side WSEndpoint, send the json string of a POCO with a type name.
        //              Verify the client receives the json string and the correct type name.
        [TestMethod]
        public async Task Test_22f()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Start the receiver loop, so we can get messages from the server connection...
            var resrl = this.StartReceiveLoop(tcp);

            // Wait a second for the server connection loop to start...
            System.Threading.Thread.Sleep(1000);

            // Have the server WSEndpoint send a a json string of a POCO with a type name...
            var ob = new SimplePOCO2();
            ob.SignatureB64 = Guid.NewGuid().ToString();
            ob.KeyId = Guid.NewGuid().ToString();
            ob.Algo = Guid.NewGuid().ToString();

            var objjson = Newtonsoft.Json.JsonConvert.SerializeObject(ob);

            var res1 = await this._wsl.ServerSide_TCPEndpoint.Send_SerializedObject_toClient_Async("Signature", objjson, "", "");
            if (res1 != 1)
                Assert.Fail("Failed to send json object back to client.");


            // Receive the message via our client connection...
            if(this.receivedmsgs.Count != 1)
                Assert.Fail("Failed to receive message.");
            var me2 = this.receivedmsgs[0];


            // Check that we received the correct datatype...
            if(me2.MessageType != "Signature")
                Assert.Fail("Failed to receive message.");

            var ob2 = Newtonsoft.Json.JsonConvert.DeserializeObject<SimplePOCO2>(me2.Data);

            if(ob2.Algo != ob.Algo)
                Assert.Fail("Failed to receive message.");
            if(ob2.KeyId != ob.KeyId)
                Assert.Fail("Failed to receive message.");
            if(ob2.SignatureB64 != ob.SignatureB64)
                Assert.Fail("Failed to receive message.");

            int x = 0;
        }

        // Test 23a -   Open a websocket connection.
        //              From the server-side WSEndpoint, add the same channel handler twice.
        //              Verify the second attempt fails.
        [TestMethod]
        public async Task Test_23a()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Add a channel handler to the server-side WSEndpoint instance...
            var res1 = this._wsl.ServerSide_TCPEndpoint.Add_ChannelHandler("somename", this.handler_callback);
            if(res1 != 1)
                Assert.Fail("Failed to add channel handler.");

            // Attempt to add a handler for the same channel again...
            var res2 = this._wsl.ServerSide_TCPEndpoint.Add_ChannelHandler("somename", this.handler_callback);
            if(res2 != -1)
                Assert.Fail("Expected handler to not be allowed.");

            int x = 0;
        }


        // Test 24a -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 1000 byte string.
        //              Verify the server received the entire string.
        [TestMethod]
        public async Task Test_24a()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            int messagesize = 1000;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Assign a raw message handler...
            this._wsl.ServerSide_TCPEndpoint.OnRawMessageReceived = this.CALLBACK_OnRawMessageReceived;

            // Send a raw string message...
            var res = await this.SendRawStringMessage(tcp, messagesize);
            if (res.retcode != 1)
                Assert.Fail("Failed to send raw string message.");

            // Keep the source hash value...
            string senthash = res.hashval;

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(3000);

            // Check how much was received...
            var recsize = this.Get_Received_RawMessage_Size();

            if(recsize != messagesize)
                Assert.Fail("Failed to receive correct message length.");

            if(senthash != this.Received_RawMessage_Hash)
                Assert.Fail("Failed to match hash of sent and received strings.");

            int x = 0;
        }

        // Test 24b -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 2047 byte string.
        //              Verify the server received the entire string.
        [TestMethod]
        public async Task Test_24b()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            int messagesize = 2047;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Assign a raw message handler...
            this._wsl.ServerSide_TCPEndpoint.OnRawMessageReceived = this.CALLBACK_OnRawMessageReceived;

            // Send a raw string message...
            var res = await this.SendRawStringMessage(tcp, messagesize);
            if (res.retcode != 1)
                Assert.Fail("Failed to send raw string message.");

            // Keep the source hash value...
            string senthash = res.hashval;

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(3000);

            // Check how much was received...
            var recsize = this.Get_Received_RawMessage_Size();

            if(recsize != messagesize)
                Assert.Fail("Failed to receive correct message length.");

            if(senthash != this.Received_RawMessage_Hash)
                Assert.Fail("Failed to match hash of sent and received strings.");

            int x = 0;
        }

        // Test 24c -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 2048 byte string.
        //              Verify the server received the entire string.
        [TestMethod]
        public async Task Test_24c()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            int messagesize = 2048;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Assign a raw message handler...
            this._wsl.ServerSide_TCPEndpoint.OnRawMessageReceived = this.CALLBACK_OnRawMessageReceived;

            // Send a raw string message...
            var res = await this.SendRawStringMessage(tcp, messagesize);
            if (res.retcode != 1)
                Assert.Fail("Failed to send raw string message.");

            // Keep the source hash value...
            string senthash = res.hashval;

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(3000);

            // Check how much was received...
            var recsize = this.Get_Received_RawMessage_Size();

            if(recsize != messagesize)
                Assert.Fail("Failed to receive correct message length.");

            if(senthash != this.Received_RawMessage_Hash)
                Assert.Fail("Failed to match hash of sent and received strings.");

            int x = 0;
        }

        // Test 24d -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 2049 byte string.
        //              Verify the server received the entire string.
        [TestMethod]
        public async Task Test_24d()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            int messagesize = 2049;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Assign a raw message handler...
            this._wsl.ServerSide_TCPEndpoint.OnRawMessageReceived = this.CALLBACK_OnRawMessageReceived;

            // Send a raw string message...
            var res = await this.SendRawStringMessage(tcp, messagesize);
            if (res.retcode != 1)
                Assert.Fail("Failed to send raw string message.");

            // Keep the source hash value...
            string senthash = res.hashval;

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(3000);

            // Check how much was received...
            var recsize = this.Get_Received_RawMessage_Size();

            if(recsize != messagesize)
                Assert.Fail("Failed to receive correct message length.");

            if(senthash != this.Received_RawMessage_Hash)
                Assert.Fail("Failed to match hash of sent and received strings.");

            int x = 0;
        }

        // Test 24e -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 10KB string.
        //              Verify the server received the entire string.
        [TestMethod]
        public async Task Test_24e()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            int messagesize = 10 * 1024;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Assign a raw message handler...
            this._wsl.ServerSide_TCPEndpoint.OnRawMessageReceived = this.CALLBACK_OnRawMessageReceived;

            // Send a raw string message...
            var res = await this.SendRawStringMessage(tcp, messagesize);
            if (res.retcode != 1)
                Assert.Fail("Failed to send raw string message.");

            // Keep the source hash value...
            string senthash = res.hashval;

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(3000);

            // Check how much was received...
            var recsize = this.Get_Received_RawMessage_Size();

            if(recsize != messagesize)
                Assert.Fail("Failed to receive correct message length.");

            if(senthash != this.Received_RawMessage_Hash)
                Assert.Fail("Failed to match hash of sent and received strings.");

            int x = 0;
        }

        // Test 24f -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 1MB string.
        //              Verify the server received the entire string.
        [TestMethod]
        public async Task Test_24f()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            int messagesize = 1000 * 1024;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Assign a raw message handler...
            this._wsl.ServerSide_TCPEndpoint.OnRawMessageReceived = this.CALLBACK_OnRawMessageReceived;

            // Send a raw string message...
            var res = await this.SendRawStringMessage(tcp, messagesize);
            if (res.retcode != 1)
                Assert.Fail("Failed to send raw string message.");

            // Keep the source hash value...
            string senthash = res.hashval;

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(1000);

            // Check how much was received...
            var recsize = this.Get_Received_RawMessage_Size();

            if(recsize != messagesize)
                Assert.Fail("Failed to receive correct message length.");

            if(senthash != this.Received_RawMessage_Hash)
                Assert.Fail("Failed to match hash of sent and received strings.");

            int x = 0;
        }

        // Test 24g -   Open a websocket connection.
        //              Attach a handler to the Raw message delegate of the server-side WSEndpoint.
        //              From the client, send a 100MB string.
        //              Verify the server received the entire string.
        [TestMethod]
        public async Task Test_24g()
        {
            Simple_TCPListener.DoSomethingWith_ConnectionRegistration = true;
            Simple_TCPListener.Keepalive_Timeout = 5;
            Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            Simple_TCPListener.AllowQuietClients = true;

            int messagesize = 100 * 1000 * 1024;

            // Create a client socket, and attempt connection...
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(this.tcphost, this.tcpport, CancellationToken.None);

            // Wait a second for the server connection loop to start and mark time...
            System.Threading.Thread.Sleep(1000);

            // Assign a raw message handler...
            this._wsl.ServerSide_TCPEndpoint.OnRawMessageReceived = this.CALLBACK_OnRawMessageReceived;

            // Send a raw string message...
            var res = await this.SendRawStringMessage(tcp, messagesize);
            if (res.retcode != 1)
                Assert.Fail("Failed to send raw string message.");

            // Keep the source hash value...
            string senthash = res.hashval;

            // Wait a second for the server to handle the nessage...
            System.Threading.Thread.Sleep(5000);

            // Check how much was received...
            var recsize = this.Get_Received_RawMessage_Size();

            if(recsize != messagesize)
                Assert.Fail("Failed to receive correct message length.");

            if(senthash != this.Received_RawMessage_Hash)
                Assert.Fail("Failed to match hash of sent and received strings.");

            int x = 0;
        }

        #endregion


        #region Send Helper Methods

        /// <summary>
        /// Override this method with the transport-specific means to send the given array.
        /// No need for any try-catch, as the call to this method is safely wrapped.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected async Task<int> RawTransportSend(NetworkStream stream, byte[] data)
        {
			int Result = 0;
			int bytes_pushed_into_buffer = 0;
			byte[] frame;

			// Handle the special case that the caller send us an empty message.
			// This is usually a zer-bypte ping message, and we will send it as a zero-length and empty data section.
			if(data.Length == 0)
			{
				// We retrieved a zero-length message that we need to send.

				// Create a frame of just the header size.
				bytes_pushed_into_buffer = cCustom_Serializer.size_of_Int32;
				frame = new byte[bytes_pushed_into_buffer];

				// Serialize the size.
				Result = cCustom_Serializer.Serialize_Integer32(0, ref frame, 0);
				if (Result < 0)
				{
					OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
						"Error occurred while forming the empty message.");

					return -2;
				}
				// We serialized the empty message.
			}
			else
			{
				// We received a positive length message.
				// Process it as normal.

				// Compose the raw buffer that will be pushed down the network stack.
				// We do this because we must send the data as well as a length, prepending it, so the receiving end can know how much data is in the message.
				// We push both the size and the data into a single buffer so it's a single network call.
				// Two array copies (size and data into a single buffer) and one network write are faster than two network writes (for separate size and data).
				bytes_pushed_into_buffer = data.Length + cCustom_Serializer.size_of_Int32;
				frame = new byte[bytes_pushed_into_buffer];

				// Serialize the size.
				Result = cCustom_Serializer.Serialize_Integer32(data.Length, ref frame, 0);
				if (Result < 0)
				{
					OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
						"Error occurred while forming the message frame.");

					return -2;
				}
				// We serialized the message size.

			}

			// Copy over the data.
			Array.Copy(data, 0, frame, 4, data.Length);

			// We have a message in the buffer that can be pushed to the wire.

			// Push the buffer to the wire.
			return this.Push_Buffer_to_Wire(stream, frame, 0, bytes_pushed_into_buffer);
        }

		/// <summary>
		/// TCP specific means to push a buffer to the wire.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="start"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		protected int Push_Buffer_to_Wire(NetworkStream stream, byte[] buffer, int start, int length)
		{
            try
            {
                // Send the message buffer to the wire.
                stream.Write(buffer, start, length);

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    "Message buffer was sent over the wire.");

                // Return success to the caller.
                return length;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // IO Exception occurred.
                // We can no longer trust the connection.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(se,
                    "Socket Exception occurred while attempting to send a message to the wire.");

                return -6;
            }
            catch (System.IO.IOException ioe)
            {
                // IO Exception occurred.
                // We can no longer trust the connection.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ioe,
                    "IO Exception occurred while attempting to send a message to the wire.");

                return -3;
            }
            catch (System.ObjectDisposedException ode)
            {
                // Object disposed Exception occurred.
                // We can no longer trust the connection.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ode,
                    "Object Dispose Exception occurred while attempting to send a message to the wire.");

                return -4;
            }
            catch (Exception e)
            {
                // Error occurred.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    "Standard Exception occurred while attempting to send a message to the wire.");

                return -5;
            }
		}

        #endregion


        #region Send Methods

        private async Task<(int retcode, string hashval)> SendRawStringMessage(TcpClient newcws, int messagesize)
        {
            int left = messagesize;
            string hashval = "";

            // Create a string of the desired length...
            StringBuilder b = new StringBuilder();
            while(left > 0)
            {
                if(left >= 100)
                {
                    b.Append(Nanoid.Nanoid.Generate( size: 100 ));
                    left = left - 100;
                }
                else if(left >= 10)
                {
                    b.Append(Nanoid.Nanoid.Generate( size: 10 ));
                    left = left - 10;
                }
                else
                {
                    b.Append(Nanoid.Nanoid.Generate( size: 1 ));
                    left = left - 1;
                }
            }
            
            // Compose the whole string...
            var rawmsg = b.ToString();

            // Calculate its hash...
            hashval = ComputeSha256Hash(rawmsg);

            ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(rawmsg));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return (1, hashval);
        }

        private async Task<int> SendEnableKeepaliveMessage(TcpClient newcws, clientproperties cd)
        {
            // Create the connection registration message...
            var cr = new ConnRegisterDTO();
            cr.UserId = cd.UserId ?? Guid.Empty;
            cr.DeviceId = cd.DeviceId;
            cr.ConnectionId = cd.ConnectionId;

            List<string> props = new List<string>();

            // Add wslibver=2 properties if set...
            if(cd.WSLibVersion > 1)
            {
                props.Add("\"wslibver\":\"" + cd.WSLibVersion.ToString() + "\"");

                if(!string.IsNullOrEmpty(cd.AppId))
                    props.Add("\"appid\":\"" + cd.AppId + "\"");
                if(!string.IsNullOrEmpty(cd.AppVersion))
                    props.Add("\"appver\":\"" + cd.AppVersion + "\"");
                if(!string.IsNullOrEmpty(cd.Language))
                    props.Add("\"language\":\"" + cd.Language + "\"");
            }

            props.Add("\"keepalive\":\"on\"");

            cr.Props = props.ToArray();

            // Wrap the registration message for sending..
            var msg = new MessageEnvelope();
            msg.MsgId = "";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = "";
            msg.Scope = "";
            msg.ReplyTo = "";
            msg.MessageType = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(cr);
            msg.Data = Newtonsoft.Json.JsonConvert.SerializeObject(cr);
            msg.Props = new string[0];

            // Serialize the message...
            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendDisableKeepaliveMessage(TcpClient newcws, clientproperties cd)
        {
            // Create the connection registration message...
            var cr = new ConnRegisterDTO();
            cr.UserId = cd.UserId ?? Guid.Empty;
            cr.DeviceId = cd.DeviceId;
            cr.ConnectionId = cd.ConnectionId;


            List<string> props = new List<string>();

            // Add wslibver=2 properties if set...
            if(cd.WSLibVersion > 1)
            {
                props.Add("\"wslibver\":\"" + cd.WSLibVersion.ToString() + "\"");

                if(!string.IsNullOrEmpty(cd.AppId))
                    props.Add("\"appid\":\"" + cd.AppId + "\"");
                if(!string.IsNullOrEmpty(cd.AppVersion))
                    props.Add("\"appver\":\"" + cd.AppVersion + "\"");
                if(!string.IsNullOrEmpty(cd.Language))
                    props.Add("\"language\":\"" + cd.Language + "\"");
            }

            props.Add("\"keepalive\":\"off\"");

            cr.Props = props.ToArray();

            // Wrap the registration message for sending..
            var msg = new MessageEnvelope();
            msg.MsgId = "";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = "";
            msg.Scope = "";
            msg.ReplyTo = "";
            msg.MessageType = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(cr);
            msg.Data = Newtonsoft.Json.JsonConvert.SerializeObject(cr);
            msg.Props = new string[0];

            // Serialize the message...
            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendConnectionRegistrationMessage(TcpClient newcws, clientproperties cd)
        {
            // Create the connection registration message...
            var cr = new ConnRegisterDTO();
            cr.UserId = cd.UserId ?? Guid.Empty;
            cr.Props = new string[0];
            cr.DeviceId = cd.DeviceId;
            cr.ConnectionId = cd.ConnectionId;

            List<string> props = new List<string>();

            // Add wslibver=2 properties if set...
            if(cd.WSLibVersion > 1)
            {
                props.Add("\"wslibver\":\"" + cd.WSLibVersion.ToString() + "\"");

                if(!string.IsNullOrEmpty(cd.AppId))
                    props.Add("\"appid\":\"" + cd.AppId + "\"");
                if(!string.IsNullOrEmpty(cd.AppVersion))
                    props.Add("\"appver\":\"" + cd.AppVersion + "\"");
                if(!string.IsNullOrEmpty(cd.Language))
                    props.Add("\"language\":\"" + cd.Language + "\"");
            }

            cr.Props = props.ToArray();


            // Wrap the registration message for sending..
            var msg = new MessageEnvelope();
            msg.MsgId = "";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = "";
            msg.Scope = "";
            msg.ReplyTo = "";
            msg.MessageType = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(cr);
            msg.Data = Newtonsoft.Json.JsonConvert.SerializeObject(cr);
            msg.Props = new string[0];

            // Serialize the message...
            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendLoopbackAllMessage(TcpClient newcws, clientproperties cd)
        {
            // Create the connection registration message...
            var cr = new ConnRegisterDTO();
            cr.UserId = cd.UserId ?? Guid.Empty;
            cr.Props = new string[0];
            cr.DeviceId = cd.DeviceId;
            cr.ConnectionId = cd.ConnectionId;


            List<string> props = new List<string>();

            // Add wslibver=2 properties if set...
            if(cd.WSLibVersion > 1)
            {
                props.Add("\"wslibver\":\"" + cd.WSLibVersion.ToString() + "\"");

                if(!string.IsNullOrEmpty(cd.AppId))
                    props.Add("\"appid\":\"" + cd.AppId + "\"");
                if(!string.IsNullOrEmpty(cd.AppVersion))
                    props.Add("\"appver\":\"" + cd.AppVersion + "\"");
                if(!string.IsNullOrEmpty(cd.Language))
                    props.Add("\"language\":\"" + cd.Language + "\"");
            }

            props.Add("\"loopback\":\"rawmsg\"");

            cr.Props = props.ToArray();


            // Wrap the registration message for sending..
            var msg = new MessageEnvelope();
            msg.MsgId = "";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = "";
            msg.Scope = "";
            msg.ReplyTo = "";
            msg.MessageType = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(cr);
            msg.Data = Newtonsoft.Json.JsonConvert.SerializeObject(cr);
            msg.Props = new string[0];

            // Serialize the message...
            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendLoopbackOFFMessage(TcpClient newcws, clientproperties cd)
        {
            // Create the connection registration message...
            var cr = new ConnRegisterDTO();
            cr.UserId = cd.UserId ?? Guid.Empty;
            cr.Props = new string[0];
            cr.DeviceId = cd.DeviceId;
            cr.ConnectionId = cd.ConnectionId;


            List<string> props = new List<string>();

            // Add wslibver=2 properties if set...
            if(cd.WSLibVersion > 1)
            {
                props.Add("\"wslibver\":\"" + cd.WSLibVersion.ToString() + "\"");

                if(!string.IsNullOrEmpty(cd.AppId))
                    props.Add("\"appid\":\"" + cd.AppId + "\"");
                if(!string.IsNullOrEmpty(cd.AppVersion))
                    props.Add("\"appver\":\"" + cd.AppVersion + "\"");
                if(!string.IsNullOrEmpty(cd.Language))
                    props.Add("\"language\":\"" + cd.Language + "\"");
            }

            props.Add("\"loopback\":\"off\"");

            cr.Props = props.ToArray();

            // Wrap the registration message for sending..
            var msg = new MessageEnvelope();
            msg.MsgId = "";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = "";
            msg.Scope = "";
            msg.ReplyTo = "";
            msg.MessageType = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(cr);
            msg.Data = Newtonsoft.Json.JsonConvert.SerializeObject(cr);
            msg.Props = new string[0];

            // Serialize the message...
            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }

        private async Task<(int retcode, MessageEnvelope msg)> SendLocalEchoMessage(TcpClient newcws)
        {
            var msg = new MessageEnvelope();
            msg.MsgId = "1";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = Guid.NewGuid().ToString();
            msg.Scope = "loopback=rawmsg";
            msg.ReplyTo = Guid.NewGuid().ToString();
            msg.MessageType = "EchoMessage";
            msg.Data = Guid.NewGuid().ToString();
            msg.Props = new string[0];

            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);

            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return (1, msg);
        }

        private async Task<int> SendMessage_onChannel(TcpClient newcws, string channel)
        {
            var msg = new MessageEnvelope();
            msg.MsgId = "1";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = channel;
            msg.Scope = "";
            msg.ReplyTo = "";
            msg.MessageType = "random";
            msg.Data = "somedata";
            msg.Props = new string[0];

            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);

            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendChatMessage(TcpClient newcws)
        {
            var msg = new MessageEnvelope();
            msg.MsgId = "1";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = "";
            msg.Scope = "";
            msg.ReplyTo = "";
            msg.MessageType = "random";
            msg.Data = "somedata";
            msg.Props = new string[0];

            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);

            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendUnknownTypeMessage(TcpClient newcws)
        {
            var msg = new MessageEnvelope();
            msg.MsgId = "1";
            msg.SentTimeUTC = DateTime.UtcNow;
            msg.Channel = "";
            msg.Scope = "";
            msg.ReplyTo = "";
            msg.MessageType = Guid.NewGuid().ToString();
            msg.Data = "";
            msg.Props = new string[0];

            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);

            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendNonMessageEnvelopeMessage(TcpClient newcws)
        {
            var msg = new SimplePOCO2();

            var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);

            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendEmptyMessage(TcpClient newcws)
        {
            var jsonstring = "";

            ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
            await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

            return 1;
        }
        private async Task<int> SendPingMessage(TcpClient newcws)
        {
            try
            {
                var msg = new MessageEnvelope();
                msg.MsgId = "1";
                msg.SentTimeUTC = DateTime.UtcNow;
                msg.Channel = "";
                msg.Scope = "";
                msg.ReplyTo = "";
                msg.MessageType = "ping";
                msg.Data = "";
                msg.Props = new string[0];

                var jsonstring = Newtonsoft.Json.JsonConvert.SerializeObject(msg);

                ArraySegment<byte> bytesToSend =new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonstring));
            
                await newcws.GetStream().WriteAsync(bytesToSend, CancellationToken.None);

                return 1;
            }
            catch(Exception e)
            {
                return -2;
            }
        }

        #endregion


        #region Private Methods

        private int handler_callback(Endpoint_Abstract ws, string messagetype, string jsondata, string corelationid)
        {
            return 1;
        }


        private int Get_Received_RawMessage_Size()
        {
            var ff = Received_RawMessage_Size;
            this.Received_RawMessage_Size = 0;

            return ff;
        }

        private string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private int handler1_callback(Endpoint_Abstract ws, string messagetype, string jsondata, string corelationid)
        {
            MessageEnvelope env = new MessageEnvelope();

            env.Data = jsondata;
            env.MessageType = messagetype;
            env.Channel = "handler1_callback";

            this.receivedmsgs.Add(env);

            return 1;
        }
        private int handler2_callback(Endpoint_Abstract ws, string messagetype, string jsondata, string corelationid)
        {
            MessageEnvelope env = new MessageEnvelope();

            env.Data = jsondata;
            env.MessageType = messagetype;
            env.Channel = "handler2_callback";

            this.receivedmsgs.Add(env);

            return 1;
        }

        private int CALLBACK_OnMessageReceived(Endpoint_Abstract ws, string messagetype, string jsondata, string corelationid)
        {
            MessageEnvelope env = new MessageEnvelope();

            env.Data = jsondata;
            env.MessageType = messagetype;

            this.receivedmsgs.Add(env);

            return 1;
        }
        private int CALLBACK_OnRawMessageReceived(Endpoint_Abstract ws, string rawstring)
        {
            var ff = rawstring.Length;

            this.Received_RawMessage_Size = ff;

            this.Received_RawMessage_Hash = ComputeSha256Hash(rawstring);

            return 1;
        }

        private int StopReceiveLoop()
        {
            this._receive_cts?.Cancel();
            System.Threading.Thread.Sleep(200);
            this._receive_cts?.Dispose();
            System.Threading.Thread.Sleep(200);
            this._receive_cts = null;

            this.clientrcvloop.Dispose();
            this.clientrcvloop = null;

            return 1;
        }

        private int Get_ReceivedMessages(out List<MessageEnvelope> msgs)
        {
            msgs = new List<MessageEnvelope>();

            while(receivedmsgs.Count != 0)
            {
                msgs.Add(receivedmsgs[0]);

                receivedmsgs.RemoveAt(0);
            }

            return msgs.Count;
        }


        private async Task<int> StartReceiveLoop(TcpClient newcws)
        {
            this.receivedmsgs = new List<MessageEnvelope>();
            this._receive_cts = new CancellationTokenSource();

            try
            {
                // Start the receive loop...
                clientrcvloop = new cReceiveLoop(newcws);
                clientrcvloop.OnMessage_Received = (mep, json) =>
                {
                    var msg = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageEnvelope>(json);

                    // Stuff the message into a receive buffer...
                    this.receivedmsgs.Add(msg);
                };
                //rl.OnConnection_Went_Bad = sdfsdf;
                //rl.OnStatus_Change = sdfsd;
                var res = clientrcvloop.Begin_Comms();

                // Register a callback that will close the receiver if the cts is cancelled...
                _receive_cts.Token.Register(() =>
                {
                    var res = clientrcvloop.CloseDown();
                });

                return 1;
            }
            catch(Exception e)
            {
                return -2;
            }
        }
        
        #endregion
    }
}
