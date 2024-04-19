using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP.Messages;
using OGA.TCP.Server;
using OGA.TCP.SessionLayer;
using OGA.TCP.Shared.Encoding;
using Testing.WSEndpoint_Tests.HelperClasses;
using WSEndpoint_Tests.HelperClasses;

namespace OGA.TCP_Test_SP
{
    /* TCPClient_v1 Tests

        //  Test_1_1_0  Create a TCPClient_v1 instance.
        //              Verify it exists.
         
    */

    [DoNotParallelize]
    [TestClass]
    public class TCPClient_v1_Tests : Testing_HelperBase
    {
        #region Private Fields

        private List<MessageEnvelope> receivedmsgs = new List<MessageEnvelope>();

        protected string RemoteHost = "192.168.1.127";
        protected int RemotePort = 5003;

        private cListener_Helper _tcplistener;

        #endregion


        #region Setup

        [TestInitialize]
        override public void Setup()
        {
            base.Setup();

            // Runs before each test. (Optional)

            // Enable trace and debug logging...
            Enable_AllLoggingLevels();

            CommonChannel.Callback_Queue = new System.Collections.Concurrent.ConcurrentQueue<string>();

            CommonChannel.CallbackChannel_MessageEnvelope = new System.Collections.Concurrent.ConcurrentQueue<MessageEnvelope>();


            _tcplistener = new cListener_Helper();
            _tcplistener.Listening_IP = System.Net.IPAddress.Parse(RemoteHost);
            _tcplistener.Listening_Port = RemotePort;
            var res = _tcplistener.Start_Listener();
        }

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
                _tcplistener?.CloseDown_Listener();
                _tcplistener = null;
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


        //  Test_1_1_0  Create a TCPClient_v1 instance.
        //              Verify it exists.
        [TestMethod]
        public async Task Test_1_1_0()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            var client = new TCPClient_v1(RemoteHost, RemotePort, logger);
        }

        [TestMethod]
        public async Task Test_1_1_2()
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1 wss = null;

            try
            {
                // Create data for a V1 client...
                var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                // Setup the websocket service...
                wss = new TCPClient_v1(RemoteHost, RemotePort, logger);
                wss.OnConnectionLost = this.Handle_OnConnectionlost;
                wss.OnMessageReceived = this.Handle_OnMessageReceived;


                // Give the ws client the device client data...
                wss.DeviceId = cp.DeviceId;
                wss.UserId = (Guid)cp.UserId;


                // Make sure the client won't timeout...
                wss.Cfg_Disable_KeepAlive = true;

                // Start the web socket client...
                var res = await wss.Start_Async();
                if(res != 1)
                    Assert.Fail("Wrong return");


                //// Wait for it to get established...
                //while(!wss.IsConnected)
                //    await Task.Delay(200);


                //// Verify the websocket is active...
                //if(!wss.IsConnected)
                //    Assert.Fail("Connection Failed");

                //// Check that the server says connected as well...
                //if(!_wsl.ServerSide_WSEndpoint.IsConnected)
                //    Assert.Fail("Connection Failed");
                //if(!_wsl.ServerSide_WSEndpoint.ClientInfo.IsRegistered)
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.AuthLevel != OGA.WSHost_Base.Lib.WSHost.Model.eAuthLevel.NoAuth)
                //    Assert.Fail("Wrong Value");

                //// Verify client data...
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.UserId != cp.UserId)
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                //    Assert.Fail("Wrong Value");

                //// Verify registration defaults for the V1 client...
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.WSLibVersion != "1")
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.Language != "en-us")
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.AppId != cp.AppId)
                //    Assert.Fail("Wrong Value");


                //// Close the client...
                //await wss.Stop_Async();

                //// Wait for it to close...
                //await Task.Delay(1000);

                //// Verify the websocket is closed...
                //if(wss.IsConnected)
                //    Assert.Fail("Connection Failed");


                //// Check that the server says closed as well...
                //if(_wsl.ServerSide_WSEndpoint.IsConnected)
                //    Assert.Fail("Connection Failed");
                //if(!_wsl.ServerSide_WSEndpoint.ClientInfo.IsRegistered)
                //    Assert.Fail("Wrong Value");

                //// Verify client data...
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.UserId != cp.UserId)
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.DeviceId != cp.DeviceId)
                //    Assert.Fail("Wrong Value");

                //// Verify registration defaults for the V1 client...
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.WSLibVersion != "1")
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.Language != "en-us")
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.AppVersion != cp.AppVersion)
                //    Assert.Fail("Wrong Value");
                //if(_wsl.ServerSide_WSEndpoint.ClientInfo.AppId != cp.AppId)
                //    Assert.Fail("Wrong Value");
            }
            finally
            {
                wss?.Dispose();
            }
        }

        #endregion


        #region Private Methods

        private int Handle_OnMessageReceived(Client_Abstract mep, string messagetype, string msg)
        {
            return 1;
        }

        private void Handle_OnConnectionlost(Client_Abstract mep)
        {
            int x = 0;
        }

        #endregion
    }
}
