using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry;
using Testing.WSEndpoint_Tests.HelperClasses;
using Testing_CommonHelpers_SP.Helpers;
using OGA.TCP;
using OGA.TCP.Messages;
using OGA.TCP.Server;
using OGA.TCP.Server.Lib_Tests.Helpers;
using OGA.TCP.Server.Model;
using OGA.TCP.Shared.Encoding;
using OGA.TCP.SessionLayer;
using WSEndpoint_Tests.HelperClasses;

namespace OGA.TCP_Test_SP
{
    /*  TCPConnMgr_wListener Tests


    THIS IS NOT A FINISHED SET OF TESTS FOR THE TCPConnMgr_wListener.
    MORE TESTS WILL BE NEEDED TO ENSURE COVERAGE.
     
        //  Test_1_0_0  Create a TCPConnMgr_wListener instance.
        //              Start it.
        //              Verify it indicates active.

        // Verify integrated listener function...
        //  Test_1_1_1  Create a TCPConnMgr_wListener instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.

        // Verify multiple connections...
        //  Test_1_1_2  Create a TCPConnMgr_wListener instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Create and connect a second client.
        //              Verify the listener accepts connection and registers the client.
        // Verify connection queries...
        //  Test_1_1_3  Create a TCPConnMgr_wListener instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Create and connect a second client.
        //              Verify the listener accepts connection and registers the client.
        //              Ask the connmgr for the first connection, by its userid.
        //              Verify we retrieved the first connection.
        //  Test_1_1_4  Create a TCPConnMgr_wListener instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Create and connect a second client.
        //              Verify the listener accepts connection and registers the client.
        //              Ask the connmgr for the first connection, by its deviceid.
        //              Verify we retrieved the first connection.

        //  Verify message exchange...
        //  These tests use a derivative of TCPConnMgr_wListener that includes a message handler delegate we can monitor.
        //  Test_1_2_1  Create a TCPConnMgr_Template instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Send a message from the client.
        //              Verify the conn mgr received it.
        //  Test_1_2_2  Create a TCPConnMgr_Template instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Send a message from the conn mgr.
        //              Verify the client received it.

     */

    [DoNotParallelize]
    [TestClass]
    public class TCPConnMgr_wListener_Tests : OGA.Testing.Lib.Test_Base_abstract
    {
        #region Private Fields

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
        }

        /// <summary>
        /// Called after each test runs.
        /// Be sure this method exists in your top-level test class, and that it calls the corresponding test cleanup method of the base.
        /// </summary>
        [TestCleanup]
        override public void TearDown()
        {
            base.TearDown();
        }

        #endregion


        #region Tests

        //  Test_1_0_0  Create a TCPConnMgr_wListener instance.
        //              Start it.
        //              Verify it indicates active.
        [TestMethod]
        public async Task Test_1_0_0()
        {
            TCPConnMgr_wListener cm = null;
            try
            {
                cm = new TCPConnMgr_wListener();
                cm.ListeningPort = 5000;
                cm.ListeningAddress = "localhost";

                // Start the instance...
                var res = cm.Startup();
                if(res != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    cm?.CloseDown();
                }
                catch (Exception e) { }
            }
        }

        // Verify integrated listener function...
        //  Test_1_1_1  Create a TCPConnMgr_wListener instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        [TestMethod]
        public async Task Test_1_1_1()
        {
            TCPConnMgr_wListener cm = null;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client1 = null;
            try
            {
                cm = new TCPConnMgr_wListener();
                cm.ListeningPort = 5000;
                cm.ListeningAddress = "localhost";

                // Start the instance...
                var res = cm.Startup();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Stand up a client connection...
                {
                    // Create data for a v1 client...
                    var cp = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client1 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client1.OnConnectionLost = (Client_v1_Abstract mep) =>
                    {
                        int x = 0;
                    };
                    client1.OnMessageReceived = (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        return 1;
                    };
                    client1.OnStatus_Change = (cl, sts) =>
                    {
                    };

                    // Give the client the device client data...
                    client1.DeviceId = cp.DeviceId;
                    client1.UserId = (Guid)cp.UserId;


                    // Make sure the client won't timeout...
                    client1.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client1.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client1.IsConnected, 400);

                // Verify connection was made...
                if(!client1.IsConnected)
                    Assert.Fail("Wrong Value");

                // Wait for the connection to get listed...
                WaitforCondition(() => cm.QRYGet_CurrentConnections().Count == 1, 400);

                // Verify the connmgr lists our connection...
                var cl1 = cm.QRYGet_CurrentConnections();
                if(cl1 == null || cl1.Count == 0)
                    Assert.Fail("Wrong Value");


                // wait for the connection to get listed...
                WaitforCondition(() => cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId) != null, 400);

                // To verify our connectionId, we need to get the server-side endpoint by deviceId, then look at its connectionId...
                var ssep1 = cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId);
                if(ssep1 == null)
                    Assert.Fail("Wrong Value");

                // Wait for the server side status to agree with client...
                WaitforCondition(() => client1.ConnectionId == ssep1.ClientInfo.ConnectionId, 400);

                if(client1.ConnectionId != ssep1.ClientInfo.ConnectionId)
                    Assert.Fail("Wrong Value");


                // Verify the connmgr lists our connection...
                var cl1a = cm.QRYGet_CurrentConnections();
                if(cl1a == null || cl1a.Count == 0)
                    Assert.Fail("Wrong Value");

                // Get the first connection entry data...
                var ce1 = cl1a.Where(n => n.DeviceId == client1.DeviceId).FirstOrDefault();
                if(ce1 == null)
                    Assert.Fail("Wrong Value");
                // Verify client properties between connection manager and client...
                if(ce1.DeviceId != client1.DeviceId)
                    Assert.Fail("Wrong Value");
                if(ce1.Hostname != cm.ConnHost_Name)
                    Assert.Fail("Wrong Value");
                if(ce1.Host_Port != cm.ListeningPort)
                    Assert.Fail("Wrong Value");
                if(ce1.LibVersion != client1.LibVersion)
                    Assert.Fail("Wrong Value");
                if(ce1.UserId != client1.UserId)
                    Assert.Fail("Wrong Value");


                // Close the connection...
                client1.Dispose();

                WaitforCondition(() => !client1.IsConnected, 500);

                // Verify connection is closed...
                if(client1.IsConnected)
                    Assert.Fail("Wrong Value");

                // Wait for the connection to be open for business...
                WaitforCondition(() => cm.QRYGet_CurrentConnections().Count == 0, 400);

                // Verify the connmgr removed our connection...
                var cl2 = cm.QRYGet_CurrentConnections();
                if(cl2 != null && cl2.Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    client1?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    cm?.CloseDown();
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_1_2  Create a TCPConnMgr_wListener instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Create and connect a second client.
        //              Verify the listener accepts connection and registers the client.
        [TestMethod]
        public async Task Test_1_1_2()
        {
            TCPConnMgr_wListener cm = null;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client1 = null;
            TCPClient_v1_Impl client2 = null;
            try
            {
                cm = new TCPConnMgr_wListener();
                cm.ListeningPort = 5000;
                cm.ListeningAddress = "localhost";

                // Start the instance...
                var res = cm.Startup();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Stand up a client connection...
                {
                    // Create data for a v1 client...
                    var cp1 = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client1 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client1.OnConnectionLost = (Client_v1_Abstract mep) =>
                    {
                        int x = 0;
                    };
                    client1.OnMessageReceived = (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        return 1;
                    };
                    client1.OnStatus_Change = (cl, sts) =>
                    {
                    };

                    // Give the client the device client data...
                    client1.DeviceId = cp1.DeviceId;
                    client1.UserId = (Guid)cp1.UserId;


                    // Make sure the client won't timeout...
                    client1.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client1.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client1.IsConnected, 400);

                // Verify connection was made...
                if(!client1.IsConnected)
                    Assert.Fail("Wrong Value");

                // Wait for the connection to get listed...
                WaitforCondition(() => cm.QRYGet_CurrentConnections().Count == 1, 400);

                // Verify the connmgr lists our connection...
                var cl1 = cm.QRYGet_CurrentConnections();
                if(cl1 == null || cl1.Count == 0)
                    Assert.Fail("Wrong Value");


                // Wait for the server side status to agree with client...
                WaitforCondition(() => cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId) != null, 400);

                // To verify our connectionId, we need to get the server-side endpoint by deviceId, then look at its connectionId...
                var ssep1 = cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId);
                if(ssep1 == null)
                    Assert.Fail("Wrong Value");

                // Wait for the server side status to agree with client...
                WaitforCondition(() => client1.ConnectionId == ssep1.ClientInfo.ConnectionId, 400);

                if(client1.ConnectionId != ssep1.ClientInfo.ConnectionId)
                    Assert.Fail("Wrong Value");


                // Repull the connection entry...
                var cl1a = cm.QRYGet_CurrentConnections();
                if(cl1a == null || cl1a.Count == 0)
                    Assert.Fail("Wrong Value");

                // Get the first connection entry data...
                var ce1 = cl1a.Where(n => n.DeviceId == client1.DeviceId).FirstOrDefault();
                if(ce1 == null)
                    Assert.Fail("Wrong Value");
                // Verify client properties between connection manager and client...
                if(ce1.DeviceId != client1.DeviceId)
                    Assert.Fail("Wrong Value");
                if(ce1.Hostname != cm.ConnHost_Name)
                    Assert.Fail("Wrong Value");
                if(ce1.Host_Port != cm.ListeningPort)
                    Assert.Fail("Wrong Value");
                if(ce1.LibVersion != client1.LibVersion)
                    Assert.Fail("Wrong Value");
                if(ce1.UserId != client1.UserId)
                    Assert.Fail("Wrong Value");


                // Stand up a second client connection...
                {
                    // Create data for a v1 client...
                    var cp2 = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client2 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client2.OnConnectionLost = (Client_v1_Abstract mep) =>
                    {
                        int x = 0;
                    };
                    client2.OnMessageReceived = (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        return 1;
                    };
                    client2.OnStatus_Change = (cl, sts) =>
                    {
                    };

                    // Give the client the device client data...
                    client2.DeviceId = cp2.DeviceId;
                    client2.UserId = (Guid)cp2.UserId;


                    // Make sure the client won't timeout...
                    client2.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client2.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client2.IsConnected, 400);

                // Verify connection was made...
                if(!client2.IsConnected)
                    Assert.Fail("Wrong Value");

                // Verify the connmgr lists our connection...
                var cl2 = cm.QRYGet_CurrentConnections();
                if(cl2 == null || cl2.Count == 0)
                    Assert.Fail("Wrong Value");

                // Wait for the connection to show up...
                WaitforCondition(() => cm.QRYGetConnection_ByClientDeviceId(client2.DeviceId) != null, 400);
                WaitforCondition(() => client2.ConnectionId == cm.QRYGetConnection_ByClientDeviceId(client2.DeviceId).ClientInfo.ConnectionId, 400);

                // To verify our connectionId, we need to get the server-side endpoint by deviceId, then look at its connectionId...
                var ssep2 = cm.QRYGetConnection_ByClientDeviceId(client2.DeviceId);
                if(ssep2 == null)
                    Assert.Fail("Wrong Value");
                if(client2.ConnectionId != ssep2.ClientInfo.ConnectionId)
                    Assert.Fail("Wrong Value");


                // Repull the connection entry...
                var cl2a = cm.QRYGet_CurrentConnections();
                if(cl2a == null || cl2a.Count == 0)
                    Assert.Fail("Wrong Value");

                // Get the second connection entry data...
                var ce2 = cl2a.Where(n => n.DeviceId == client2.DeviceId).FirstOrDefault();
                if(ce2 == null)
                    Assert.Fail("Wrong Value");
                // Verify client properties between connection manager and client...
                if(ce2.DeviceId != client2.DeviceId)
                    Assert.Fail("Wrong Value");
                if(ce2.Hostname != cm.ConnHost_Name)
                    Assert.Fail("Wrong Value");
                if(ce2.Host_Port != cm.ListeningPort)
                    Assert.Fail("Wrong Value");
                if(ce2.LibVersion != client2.LibVersion)
                    Assert.Fail("Wrong Value");
                if(ce2.UserId != client2.UserId)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    client1?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    client2?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    cm?.CloseDown();
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_1_3  Create a TCPConnMgr_wListener instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Create and connect a second client.
        //              Verify the listener accepts connection and registers the client.
        //              Ask the connmgr for the first connection, by its userid.
        //              Verify we retrieved the first connection.
        [TestMethod]
        public async Task Test_1_1_3()
        {
            TCPConnMgr_wListener cm = null;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client1 = null;
            TCPClient_v1_Impl client2 = null;
            try
            {
                cm = new TCPConnMgr_wListener();
                cm.ListeningPort = 5000;
                cm.ListeningAddress = "localhost";

                // Start the instance...
                var res = cm.Startup();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Stand up a client connection...
                {
                    // Create data for a v1 client...
                    var cp1 = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client1 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client1.OnConnectionLost = (Client_v1_Abstract mep) =>
                    {
                        int x = 0;
                    };
                    client1.OnMessageReceived = (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        return 1;
                    };
                    client1.OnStatus_Change = (cl, sts) =>
                    {
                    };

                    // Give the client the device client data...
                    client1.DeviceId = cp1.DeviceId;
                    client1.UserId = (Guid)cp1.UserId;


                    // Make sure the client won't timeout...
                    client1.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client1.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client1.IsConnected, 400);

                // Verify connection was made...
                if(!client1.IsConnected)
                    Assert.Fail("Wrong Value");

                // Wait for the connection to get listed...
                WaitforCondition(() => cm.QRYGet_CurrentConnections().Count == 1, 400);
                
                // Verify the connmgr lists our connection...
                var cl1 = cm.QRYGet_CurrentConnections();
                if(cl1 == null || cl1.Count == 0)
                    Assert.Fail("Wrong Value");


                // Wait for the entry to show up...
                WaitforCondition(() => cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId) != null, 400);

                // To verify our connectionId, we need to get the server-side endpoint by deviceId, then look at its connectionId...
                var ssep1 = cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId);
                if(ssep1 == null)
                    Assert.Fail("Wrong Value");

                // Wait for the server side status to agree with client...
                WaitforCondition(() => client1.ConnectionId == ssep1.ClientInfo.ConnectionId, 400);

                if(client1.ConnectionId != ssep1.ClientInfo.ConnectionId)
                    Assert.Fail("Wrong Value");


                // Repull the the connection...
                var cl1a = cm.QRYGet_CurrentConnections();
                if(cl1a == null || cl1a.Count == 0)
                    Assert.Fail("Wrong Value");

                // Get the first connection entry data...
                var ce1 = cl1a.Where(n => n.DeviceId == client1.DeviceId).FirstOrDefault();
                if(ce1 == null)
                    Assert.Fail("Wrong Value");
                // Verify client properties between connection manager and client...
                if(ce1.DeviceId != client1.DeviceId)
                    Assert.Fail("Wrong Value");
                if(ce1.Hostname != cm.ConnHost_Name)
                    Assert.Fail("Wrong Value");
                if(ce1.Host_Port != cm.ListeningPort)
                    Assert.Fail("Wrong Value");
                if(ce1.LibVersion != client1.LibVersion)
                    Assert.Fail("Wrong Value");
                if(ce1.UserId != client1.UserId)
                    Assert.Fail("Wrong Value");


                // Stand up a second client connection...
                {
                    // Create data for a v1 client...
                    var cp2 = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client2 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client2.OnConnectionLost = (Client_v1_Abstract mep) =>
                    {
                        int x = 0;
                    };
                    client2.OnMessageReceived = (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        return 1;
                    };
                    client2.OnStatus_Change = (cl, sts) =>
                    {
                    };

                    // Give the client the device client data...
                    client2.DeviceId = cp2.DeviceId;
                    client2.UserId = (Guid)cp2.UserId;


                    // Make sure the client won't timeout...
                    client2.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client2.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client2.IsConnected, 400);

                // Verify connection was made...
                if(!client2.IsConnected)
                    Assert.Fail("Wrong Value");

                // Verify the connmgr lists our connection...
                var cl2 = cm.QRYGet_CurrentConnections();
                if(cl2 == null || cl2.Count == 0)
                    Assert.Fail("Wrong Value");

                // Wait for the entry to show up...
                WaitforCondition(() => cm.QRYGetConnection_ByClientDeviceId(client2.DeviceId) != null, 400);
                WaitforCondition(() => client2.ConnectionId == cm.QRYGetConnection_ByClientDeviceId(client2.DeviceId).ClientInfo.ConnectionId, 400);

                // To verify our connectionId, we need to get the server-side endpoint by deviceId, then look at its connectionId...
                var ssep2 = cm.QRYGetConnection_ByClientDeviceId(client2.DeviceId);
                if(ssep2 == null)
                    Assert.Fail("Wrong Value");
                if(client2.ConnectionId != ssep2.ClientInfo.ConnectionId)
                    Assert.Fail("Wrong Value");

                // Repull the connection entry...
                var cl2a = cm.QRYGet_CurrentConnections();
                if(cl2a == null || cl2a.Count == 0)
                    Assert.Fail("Wrong Value");

                // Get the second connection entry data...
                var ce2 = cl2a.Where(n => n.DeviceId == client2.DeviceId).FirstOrDefault();
                if(ce2 == null)
                    Assert.Fail("Wrong Value");
                // Verify client properties between connection manager and client...
                if(ce2.DeviceId != client2.DeviceId)
                    Assert.Fail("Wrong Value");
                if(ce2.Hostname != cm.ConnHost_Name)
                    Assert.Fail("Wrong Value");
                if(ce2.Host_Port != cm.ListeningPort)
                    Assert.Fail("Wrong Value");
                if(ce2.LibVersion != client2.LibVersion)
                    Assert.Fail("Wrong Value");
                if(ce2.UserId != client2.UserId)
                    Assert.Fail("Wrong Value");


                // As the conn mgr for the first connection by its userid...
                var resuid = cm.QRYGetConnections_ByUserId(client1.UserId);
                if(resuid == null || resuid.Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify we received the first connection's data...
                if (resuid[0].ClientInfo.ConnectionId != client1.ConnectionId)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    client1?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    client2?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    cm?.CloseDown();
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_1_4  Create a TCPConnMgr_wListener instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Create and connect a second client.
        //              Verify the listener accepts connection and registers the client.
        //              Ask the connmgr for the first connection, by its deviceid.
        //              Verify we retrieved the first connection.
        [TestMethod]
        public async Task Test_1_1_4()
        {
            TCPConnMgr_wListener cm = null;

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client1 = null;
            TCPClient_v1_Impl client2 = null;
            try
            {
                cm = new TCPConnMgr_wListener();
                cm.ListeningPort = 5000;
                cm.ListeningAddress = "localhost";

                // Start the instance...
                var res = cm.Startup();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Stand up a client connection...
                {
                    // Create data for a v1 client...
                    var cp1 = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client1 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client1.OnConnectionLost = (Client_v1_Abstract mep) =>
                    {
                        int x = 0;
                    };
                    client1.OnMessageReceived = (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        return 1;
                    };
                    client1.OnStatus_Change = (cl, sts) =>
                    {
                    };

                    // Give the client the device client data...
                    client1.DeviceId = cp1.DeviceId;
                    client1.UserId = (Guid)cp1.UserId;


                    // Make sure the client won't timeout...
                    client1.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client1.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client1.IsConnected, 400);

                // Verify connection was made...
                if(!client1.IsConnected)
                    Assert.Fail("Wrong Value");

                // Wait for the connection to get listed...
                WaitforCondition(() => cm.QRYGet_CurrentConnections().Count == 1, 400);

                // Verify the connmgr lists our connection...
                var cl1 = cm.QRYGet_CurrentConnections();
                if(cl1 == null || cl1.Count == 0)
                    Assert.Fail("Wrong Value");

                // Wait for the connection to get listed...
                WaitforCondition(() => cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId) != null, 400);


                // Wait for the server side connectionId to agree with client...
                WaitforCondition(() => client1.ConnectionId == cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId).ClientInfo.ConnectionId, 1000);

                // To verify our connectionId, we need to get the server-side endpoint by deviceId, then look at its connectionId...
                var ssep1 = cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId);
                if(ssep1 == null)
                    Assert.Fail("Wrong Value");

                // Verify the connmgr lists our connection...
                var cl1a = cm.QRYGet_CurrentConnections();
                if(cl1a == null || cl1a.Count == 0)
                    Assert.Fail("Wrong Value");

                // Get the first connection entry data...
                var ce1 = cl1a.Where(n => n.DeviceId == client1.DeviceId).FirstOrDefault();
                if(ce1 == null)
                    Assert.Fail("Wrong Value");
                // Verify client properties between connection manager and client...
                if(ce1.DeviceId != client1.DeviceId)
                    Assert.Fail("Wrong Value");
                if(ce1.Hostname != cm.ConnHost_Name)
                    Assert.Fail("Wrong Value");
                if(ce1.Host_Port != cm.ListeningPort)
                    Assert.Fail("Wrong Value");
                if(ce1.LibVersion != client1.LibVersion)
                    Assert.Fail("Wrong Value");
                if(ce1.UserId != client1.UserId)
                    Assert.Fail("Wrong Value");


                // Stand up a second client connection...
                {
                    // Create data for a v1 client...
                    var cp2 = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client2 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client2.OnConnectionLost = (Client_v1_Abstract mep) =>
                    {
                        int x = 0;
                    };
                    client2.OnMessageReceived = (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        return 1;
                    };
                    client2.OnStatus_Change = (cl, sts) =>
                    {
                    };

                    // Give the client the device client data...
                    client2.DeviceId = cp2.DeviceId;
                    client2.UserId = (Guid)cp2.UserId;


                    // Make sure the client won't timeout...
                    client2.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client2.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client2.IsConnected, 400);

                // Verify connection was made...
                if(!client2.IsConnected)
                    Assert.Fail("Wrong Value");

                // Verify the connmgr lists our connection...
                var cl2 = cm.QRYGet_CurrentConnections();
                if(cl2 == null || cl2.Count == 0)
                    Assert.Fail("Wrong Value");

                // Wait for the connection to be lsited...
                WaitforCondition(() => client2.ConnectionId == cm.QRYGetConnection_ByClientDeviceId(client2.DeviceId)?.ClientInfo.ConnectionId, 400);

                // To verify our connectionId, we need to get the server-side endpoint by deviceId, then look at its connectionId...
                var ssep2 = cm.QRYGetConnection_ByClientDeviceId(client2.DeviceId);
                if(ssep2 == null)
                    Assert.Fail("Wrong Value");
                if(client2.ConnectionId != ssep2.ClientInfo.ConnectionId)
                    Assert.Fail("Wrong Value");

                // Verify the connmgr lists our connection...
                var cl2a = cm.QRYGet_CurrentConnections();
                if(cl2a == null || cl2a.Count == 0)
                    Assert.Fail("Wrong Value");

                // Get the second connection entry data...
                var ce2 = cl2a.Where(n => n.DeviceId == client2.DeviceId).FirstOrDefault();
                if(ce2 == null)
                    Assert.Fail("Wrong Value");
                // Verify client properties between connection manager and client...
                if(ce2.DeviceId != client2.DeviceId)
                    Assert.Fail("Wrong Value");
                if(ce2.Hostname != cm.ConnHost_Name)
                    Assert.Fail("Wrong Value");
                if(ce2.Host_Port != cm.ListeningPort)
                    Assert.Fail("Wrong Value");
                if(ce2.LibVersion != client2.LibVersion)
                    Assert.Fail("Wrong Value");
                if(ce2.UserId != client2.UserId)
                    Assert.Fail("Wrong Value");


                // As the conn mgr for the first connection by its deviceid...
                var resdid = cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId);
                if(resdid == null)
                    Assert.Fail("Wrong Value");

                // Verify we received the first connection's data...
                if (resdid.ClientInfo.UserId != client1.UserId)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    client1?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    client2?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    cm?.CloseDown();
                }
                catch (Exception e) { }
            }
        }

        //  Verify message exchange...
        //  These tests use a derivative of TCPConnMgr_wListener that includes a message handler delegate we can monitor.
        //  Test_1_2_1  Create a TCPConnMgr_Template instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Send a message from the client.
        //              Verify the conn mgr received it.
        [TestMethod]
        public async Task Test_1_2_1()
        {
            TCPConnMgr_Template cm = null;

            string serverreceivedmessagetext = "";
            string clientreceivedmessagetext = "";

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client1 = null;
            try
            {
                cm = new TCPConnMgr_Template();
                cm.ListeningPort = 5000;
                cm.ListeningAddress = "localhost";
                cm.OnMessageReceived = (Endpoint_Abstract mep, string messagetype, string jsondata, string corelationid) =>
                {
                    var m = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageEnvelope>(jsondata);
                    var m2 = Newtonsoft.Json.JsonConvert.DeserializeObject<String>(m.Data);
                    serverreceivedmessagetext = m2;
                    return 1;
                };

                // Start the instance...
                var res = cm.Startup();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Stand up a client connection...
                {
                    // Create data for a v1 client...
                    var cp1 = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client1 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client1.Add_ChannelHandler(TCPConnMgr_Template.TESTCHANNELNAME, (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        var m2 = Newtonsoft.Json.JsonConvert.DeserializeObject<String>(msg);
                        clientreceivedmessagetext = m2;
                        return 1;
                    });


                    // Give the client the device client data...
                    client1.DeviceId = cp1.DeviceId;
                    client1.UserId = (Guid)cp1.UserId;


                    // Make sure the client won't timeout...
                    client1.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client1.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client1.IsConnected, 400);

                // Verify connection was made...
                if(!client1.IsConnected)
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

                // Wait for the connection to be open for business...
                WaitforCondition(() => client1.AllowSend, 400);

                // Have the client deliver the message...
                var ressend = await client1.SendMessage_to_Endpoint(msg, TCPConnMgr_Template.TESTCHANNELNAME, "", "");
                if(ressend != 1)
                    Assert.Fail("Wrong return");

                // Wait for the server to receive it...
                WaitforCondition(() => messagetosend == serverreceivedmessagetext, 400);

                // Verify the message was received...
                if(messagetosend != serverreceivedmessagetext)
                    Assert.Fail("Wrong return");
            }
            finally
            {
                try
                {
                    client1?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    cm?.CloseDown();
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_2_2  Create a TCPConnMgr_Template instance.
        //              Start the instance.
        //              Create and connect a client to it.
        //              Verify the listener accepts connection and registers the client.
        //              Send a message from the conn mgr.
        //              Verify the client received it.
        [TestMethod]
        public async Task Test_1_2_2()
        {
            TCPConnMgr_Template cm = null;

            string serverreceivedmessagetext = "";
            string clientreceivedmessagetext = "";

            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
            TCPClient_v1_Impl client1 = null;
            try
            {
                cm = new TCPConnMgr_Template();
                cm.ListeningPort = 5000;
                cm.ListeningAddress = "localhost";
                cm.OnMessageReceived = (Endpoint_Abstract mep, string messagetype, string jsondata, string corelationid) =>
                {
                    var m = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageEnvelope>(jsondata);
                    var m2 = Newtonsoft.Json.JsonConvert.DeserializeObject<String>(m.Data);
                    serverreceivedmessagetext = m2;
                    return 1;
                };

                // Start the instance...
                var res = cm.Startup();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Stand up a client connection...
                {
                    // Create data for a v1 client...
                    var cp1 = clientproperties.Create_Random_WSLibV1_ClientData();

                    // Setup the tcpsocket client...
                    client1 = new TCPClient_v1_Impl(cm.ListeningAddress, cm.ListeningPort, logger);
                    client1.Add_ChannelHandler(TCPConnMgr_Template.TESTCHANNELNAME, (Client_v1_Abstract mep, string messagetype, string msg) =>
                    {
                        var m = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageEnvelope>(msg);
                        var m2 = Newtonsoft.Json.JsonConvert.DeserializeObject<String>(m.Data);
                        clientreceivedmessagetext = m2;
                        return 1;
                    });

                    // Give the client the device client data...
                    client1.DeviceId = cp1.DeviceId;
                    client1.UserId = (Guid)cp1.UserId;


                    // Make sure the client won't timeout...
                    client1.Cfg_Disable_KeepAlive = true;

                    // Start the web socket client...
                    var resconnect = await client1.Start_Async();
                    if(resconnect != 1)
                        Assert.Fail("Wrong return");
                }

                // Wait for connection...
                WaitforCondition(() => client1.IsConnected, 400);

                // Verify connection was made...
                if(!client1.IsConnected)
                    Assert.Fail("Wrong Value");


                // Send a simple message from the server...
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

                // Wait for the server side connection to show up...
                WaitforCondition(() => cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId) != null, 1000);

                // To send the message to our client, we have to get the matching connection from the conn mgr...
                var serversideconn1 = cm.QRYGetConnection_ByClientDeviceId(client1.DeviceId);
                if(serversideconn1 == null)
                    Assert.Fail("Wrong return");

                // Send the message from the server side...
                var ressend = await serversideconn1.SendMessage_toClient(msg, TCPConnMgr_Template.TESTCHANNELNAME, "", "");
                if(ressend != 1)
                    Assert.Fail("Wrong return");

                // Wait for the client to receive it...
                WaitforCondition(() => messagetosend == clientreceivedmessagetext, 400);

                // Verify the message was received...
                if(messagetosend != clientreceivedmessagetext)
                    Assert.Fail("Wrong return");
            }
            finally
            {
                try
                {
                    client1?.Dispose();
                }
                catch (Exception e) { }
                try
                {
                    cm?.CloseDown();
                }
                catch (Exception e) { }
            }
        }

        #endregion


        #region Private Methods

        #endregion
    }
}
