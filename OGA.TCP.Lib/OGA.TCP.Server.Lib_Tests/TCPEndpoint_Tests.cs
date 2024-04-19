using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP.Messages;
using OGA.TCP.Server;
using OGA.TCP.Server.Lib_Tests.Helpers;
using OGA.TCP.Shared.Encoding;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

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
    [DoNotParallelize]
    [TestClass]
    public class TCPEndpoint_Tests : Testing_HelperBase
    {
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
        }

        [TestCleanup]
        override public void TearDown()
        {
            // Runs after each test. (Optional)

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
    }
}
