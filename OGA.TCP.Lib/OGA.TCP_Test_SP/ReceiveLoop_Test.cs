//using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP;
using OGA.TCP.Messages;
using OGA.TCP.Server.Lib_Tests.Helpers;
using OGA.TCP.Shared.Encoding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
//using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OGA.TCP_Test_SP
{
    /*  cReceiveLoop Tests
     
        // This block of tests checks an unstarted receive loop behavior through various combinations of setup and teardown.
        //  Test_1_1_1  Create simple instance and verify its unstarted state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, but don't start it.
        //              Verify the receive loop instance indicates initialized.
        //  Test_1_1_2  Create simple instance and see what happens if we give it a null TcpClient.
        //              Create a receive loop instance with a null TcpClient.
        //              Verify the receive loop constructor throws an exception.
        //  Test_1_1_3  Create simple instance and verify it throws for an unopened TcpClient.
        //              Create a receive loop instance with an unconnected TcpClient.
        //              Verify the receive loop constructor throws an exception.
        //  Test_1_1_4  Create simple instance and verify its unstarted, disposed state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, but don't start it.
        //              Verify the receive loop instance indicates initialized.
        //              Call the dispose method.
        //              Verify the receive loop instance still indicates initialized.
        //  Test_1_1_6  Test that closing an unstarted instance will remain an initialized state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, but don't start it.
        //              Close the instance.
        //              Verify the receive loop instance still indicates initialized.
        //  Test_1_1_7  Test that disposing an unstarted instance, and attempting to start it will return error.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, but don't start it.
        //              Dispose the instance.
        //              Attempt to start the instance.
        //              Verify the start method returns error.
        //              Verify the instance remains initialized.

        // This block of tests checks a newly started receive loop behavior through various combinations of setup and teardown.
        //  Test_1_2_1  Test that an instance can reach the newly open state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Verify the receive loop instance indicates newly opened.
        //  Test_1_2_2  Test that a newly open instance closes on client connection close.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Close the client side.
        //              Verify the conn lost delegate triggered.
        //              Verify the receive loop instance indicates close.
        //  Test_1_2_3  Test that a newly open instance closes on client connection close.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Close the server side.
        //              Verify the conn lost delegate triggered.
        //              Verify the receive loop instance indicates close.
        //  Test_1_2_4  Test that a newly open instance reports close on close.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Call the receive loop close method.
        //              Verify the receive loop instance indicates closed.
        //              Verify the conn lost delegate is NOT triggered.
        //  Test_1_2_5  Test that a newly open instance reports close on dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates closed.
        //              Verify the conn lost delegate is NOT triggered.
        //  Test_1_2_6  Test that a newly open instance closes on connection lost, even after dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Close the client side.
        //              Verify the conn lost delegate triggered.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates closed.
        //  Test_1_2_7  Test that a newly open instance Errors on connection lost, even after dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Close the server side.
        //              Verify the conn lost delegate triggered.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates Error.
        //  Test_1_2_8  Test that start errors on a newly open instance.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Call the start method.
        //              Verify the start method returns error.
        //              Verify the instance remains unaffected.

        // This block of tests checks a started and fully open receive loop behavior through various combinations of setup and teardown.
        //  Test_1_3_1  Test that an instance can reach the fully open state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //  Test_1_3_2  Test that a fully open instance errors on connection lost.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Close the client side.
        //              Verify the conn lost delegate triggered.
        //              Verify the receive loop instance indicates lost.
        //  Test_1_3_3  Test that a fully open instance errors on connection lost.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Close the server side.
        //              Verify the conn lost delegate triggered.
        //              Verify the receive loop instance indicates lost.
        //  Test_1_3_4  Test that a fully open instance reports close on close.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Call the receive loop close method.
        //              Verify the conn lost delegate is NOT triggered.
        //              Verify the receive loop instance indicates closed.
        //  Test_1_3_5  Test that a fully open instance reports close on dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Call the receive loop dispose.
        //              Verify the conn lost delegate is NOT triggered.
        //              Verify the receive loop instance indicates closed.
        //  Test_1_3_6  Test that a fully open instance errors on connection lost, even after dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Close the client side.
        //              Verify the conn lost delegate triggered.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates lost.
        //  Test_1_3_7  Test that a fully open instance errors on connection lost, even after dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Close the server side.
        //              Verify the conn lost delegate triggered.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates lost.
        //  Test_1_3_8  Test that start errors on a fully open instance.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Call the start method.
        //              Verify the start method returns error.
        //              Verify the instance remains unaffected.
        //  Test_1_3_9  Test that disposing a fully open instance has no effect on the underlying TcpClient, and the client can still send messages, but the receive loop cannot receive them.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Call the dispose method on the receive loop.
        //              Verify the conn lost delegate did NOT trigger.
        //              Verify the receive loop instance indicates closed.
        //              Verify the TcpClient instances still indicate connected.

        // Verify Message Content Tests...
        //  Test_1_4_1  Test that the received message matches what was sent.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a client message.
        //              Verify the received message matches.
        //  Test_1_4_2  Test that an instance can will error for not fully receiving a message after a timeout.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send part of a client message, but not all of it.
        //              Verify the receive loop times out and changes to error.

        //  Metric Update Tests...
        //  Test_1_5_1  Test that receiving a quick-ping (zero byte) message will update last received and message counter.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a quick ping from the client.
        //              Verify the last receive time and receive counter update.
        //  Test_1_5_2  Test the last received time and counter update for a received message.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a regular message from the client.
        //              Verify the last receive time and receive counter update.
        //  Test_1_5_3  Test the last received time and counter update for several received messages.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send several messages from the client.
        //              Verify the last receive time and receive counter updates for each message.
        //  Test_1_5_4  Test the last received time and counter do NOT update for a received malformed message.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send part of a client message, but not all of it.
        //              Verify the receive loop times out and changes to error.
        //              Verify the last receive time and receive counter do NOT update.
        //  Test_1_5_5  Test the last received time and counter do NOT update for an insanely large message.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send an insanely large client message.
        //              Verify the last receive time and receive counter do NOT update.

     */

    [DoNotParallelize]
    [TestClass]
    public class ReceiveLoop_Test : OGA.Testing.Lib.Test_Base_abstract
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

            Reset_StatusCallbackData();
            Reset_LostConnectionData();
            Reset_MessageCallbackData();
        }

        /// <summary>
        /// Called after each test runs.
        /// Be sure this method exists in your top-level test class, and that it calls the corresponding test cleanup method of the base.
        /// </summary>
        [TestCleanup]
        override public void TearDown()
        {
            // Runs after each test. (Optional)

            base.TearDown();
        }

        #endregion


        // This block of tests checks an unstarted receive loop behavior through various combinations of setup and teardown.
        //  Test_1_1_1  Create simple instance and verify its unstarted state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, but don't start it.
        //              Verify the receive loop instance indicates initialized.
        [TestMethod]
        public async Task Test_1_1_1()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Verify the receiver loop is not started...
                if(rl.State != eLoop_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");

                // Close it down...
                if (rl.CloseDown() != 1)
                {
                    // Failed to close receive loop.
                    Assert.Fail("Failed to close receive loop.");
                }
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_1_2  Create simple instance and see what happens if we give it a null TcpClient.
        //              Create a receive loop instance with a null TcpClient.
        //              Verify the receive loop constructor throws an exception.
        [TestMethod]
        public async Task Test_1_1_2()
        {
            cReceiveLoop rl = null;
            try
            {
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(null, logger);

                Assert.Fail("Should have thrown.");
            }
            catch(Exception e)
            {

            }
        }

        //  Test_1_1_3  Create simple instance and verify it throws for an unopened TcpClient.
        //              Create a receive loop instance with an unconnected TcpClient.
        //              Verify the receive loop constructor throws an exception.
        [TestMethod]
        public async Task Test_1_1_3()
        {
            cReceiveLoop rl = null;
            try
            {
                TcpClient cl = new TcpClient();

                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cl, logger);

                Assert.Fail("Should have thrown.");
            }
            catch(Exception e)
            {

            }
        }
        
        //  Test_1_1_4  Create simple instance and verify its unstarted, disposed state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, but don't start it.
        //              Verify the receive loop instance indicates initialized.
        //              Call the dispose method.
        //              Verify the receive loop instance still indicates initialized.
        [TestMethod]
        public async Task Test_1_1_4()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = CALLBACK_Status_Change;

                // Verify the receiver loop is not started...
                if(rl.State != eLoop_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");

                // Call dispose...
                rl.Dispose();

                // Verify that zero status callbacks occurred...
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify the receiver loop state didn't change...
                if(rl.State != eLoop_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_1_6  Test that closing an unstarted instance will remain an initialized state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, but don't start it.
        //              Close the instance.
        //              Verify the receive loop instance still indicates initialized.
        [TestMethod]
        public async Task Test_1_1_6()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Verify the receiver loop is not started...
                if(rl.State != eLoop_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");

                // Call close...
                rl.CloseDown();

                // Verify that zero status callbacks occurred...
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify the receiver loop state didn't change...
                if(rl.State != eLoop_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_1_7  Test that disposing an unstarted instance, and attempting to start it will return error.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, but don't start it.
        //              Dispose the instance.
        //              Attempt to start the instance.
        //              Verify the start method returns error.
        //              Verify the instance remains initialized.
        [TestMethod]
        public async Task Test_1_1_7()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Verify the receiver loop is not started...
                if(rl.State != eLoop_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");

                // Call dispose...
                rl.Dispose();

                // Verify that zero status callbacks occurred...
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify the receiver loop state didn't change...
                if(rl.State != eLoop_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");

                // Attempt to start it after dispose...
                var res = rl.Begin_Comms();
                if(res == 1)
                    Assert.Fail("Wrong Value");

                // Verify that zero status callbacks occurred...
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify the receiver loop state didn't change...
                if(rl.State != eLoop_ConnectionStatus.Initialized)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }


        // This block of tests checks a newly started receive loop behavior through various combinations of setup and teardown.
        //  Test_1_2_1  Test that an instance can reach the newly open state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Verify the receive loop instance indicates newly opened.
        [TestMethod]
        public async Task Test_1_2_1()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");

                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_2_2  Test that a newly open instance closes on client connection close.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Close the client side.
        //              Verify the conn lost delegate triggered.
        //              Verify the receive loop instance indicates close.
        [TestMethod]
        public async Task Test_1_2_2()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the client side...
                cth.Clientside_Connection.Close();

                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 4000);

                // Verify the receive loop closes...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                WaitforCondition(() => lostconnection_counter == 1, 1000);

                // Verify the lost connection callback occurred...
                if(lostconnection_counter != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_2_3  Test that a newly open instance Errors on server connection close.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Close the server side.
        //              Verify the conn lost delegate triggered.
        //              Verify the receive loop instance indicates Error.
        [TestMethod]
        public async Task Test_1_2_3()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the server side...
                cth.Serverside_Connection.Close();

                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Error, 500);

                // Verify the receive loop Error...
                if(rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify the lost connection callback occurred...
                if(lostconnection_counter != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_2_4  Test that a newly open instance reports close on close.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Call the receive loop close method.
        //              Verify the receive loop instance indicates closed.
        //              Verify the conn lost delegate is NOT triggered.
        [TestMethod]
        public async Task Test_1_2_4()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the receiver...
                rl.CloseDown();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 200);

                // Verify the receive loop loses connection...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Verify no lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_2_5  Test that a newly open instance reports close on dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates closed.
        //              Verify the conn lost delegate is NOT triggered.
        [TestMethod]
        public async Task Test_1_2_5()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Dispose the receiver...
                rl.Dispose();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 200);

                // Verify the receive loop loses connection...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Verify no lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_2_6  Test that a newly open instance closes on connection lost, even after dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Close the client side.
        //              Verify the conn lost delegate triggered.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates closed.
        [TestMethod]
        public async Task Test_1_2_6()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the client...
                cth.Clientside_Connection.Close();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 200);

                // Verify the receive loop indicates closed...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify that two status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Wait for the lost status message...
                WaitforCondition(() => lostconnection_counter == 1, 1000);

                // Verify a lost connection callback occurred...
                if(lostconnection_counter != 1)
                    Assert.Fail("Wrong Value");

                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");

                // Dispose the receiver...
                rl.Dispose();

                // Verify the receive loop indicates closed...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify zero status callbacks occurred...
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify zero lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_2_7  Test that a newly open instance Errors on connection lost, even after dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Close the server side.
        //              Verify the conn lost delegate triggered.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates Error.
        [TestMethod]
        public async Task Test_1_2_7()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the server...
                cth.Serverside_Connection.Close();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Error, 400);

                // Verify the receive loop indicates Error...
                if(rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");

                // Verify that one status callback occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify a lost connection callback occurred...
                if(lostconnection_counter != 1)
                    Assert.Fail("Wrong Value");

                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");

                // Dispose the receiver...
                rl.Dispose();

                // Verify the receive loop indicates Error...
                if(rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");

                // Verify zero status callbacks occurred...
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify zero lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_2_8  Test that start errors on a newly open instance.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Call the start method.
        //              Verify the start method returns error.
        //              Verify the instance remains unaffected.
        [TestMethod]
        public async Task Test_1_2_8()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Attempt to start the receiver again...
                var res2 = rl.Begin_Comms();
                if(res2 != -1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }


        // This block of tests checks a started and fully open receive loop behavior through various combinations of setup and teardown.
        //  Test_1_3_1  Test that an instance can reach the fully open state.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        [TestMethod]
        public async Task Test_1_3_1()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");

                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");

                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
                Reset_MessageCallbackData();
                if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Open, 500);

                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");

                WaitforCondition(() => statuschange_listing.Count == 1, 500);

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");

                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_3_2  Test that a fully open instance errors on connection lost.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Close the client side.
        //              Verify the conn lost delegate triggered.
        //              Verify the receive loop instance indicates lost.
        [TestMethod]
        public async Task Test_1_3_2()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the client side...
                cth.Clientside_Connection.Close();

                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 500);

                // Verify the receive loop closes...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                WaitforCondition(() => statuschange_listing.Count == 2, 500);

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Verify the lost connection callback occurred...
                if(lostconnection_counter != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_3_3  Test that a fully open instance errors on connection lost.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Close the server side.
        //              Verify the conn lost delegate triggered.
        //              Verify the receive loop instance indicates lost.
        [TestMethod]
        public async Task Test_1_3_3()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the server side...
                cth.Serverside_Connection.Close();

                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Error, 500);

                // Verify the receive loop Error...
                if(rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify the lost connection callback occurred...
                if(lostconnection_counter != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_3_4  Test that a fully open instance reports close on close.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Call the receive loop close method.
        //              Verify the conn lost delegate is NOT triggered.
        //              Verify the receive loop instance indicates closed.
        [TestMethod]
        public async Task Test_1_3_4()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the receiver...
                rl.CloseDown();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 200);

                // Verify the receive loop loses connection...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Verify no lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_3_5  Test that a fully open instance reports close on dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Call the receive loop dispose.
        //              Verify the conn lost delegate is NOT triggered.
        //              Verify the receive loop instance indicates closed.
        [TestMethod]
        public async Task Test_1_3_5()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Dispose the receiver...
                rl.Dispose();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 520000);

                // Verify the receive loop loses connection...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Verify no lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_3_6  Test that a fully open instance errors on connection lost, even after dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Close the client side.
        //              Verify the conn lost delegate triggered.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates lost.
        [TestMethod]
        public async Task Test_1_3_6()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the client...
                cth.Clientside_Connection.Close();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 200);

                // Verify the receive loop indicates closed...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify that two status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Verify a lost connection callback occurred...
                if(lostconnection_counter != 1)
                    Assert.Fail("Wrong Value");

                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");

                // Dispose the receiver...
                rl.Dispose();

                // Verify the receive loop indicates closed...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify zero status callbacks occurred...
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify zero lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_3_7  Test that a fully open instance errors on connection lost, even after dispose.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Close the server side.
        //              Verify the conn lost delegate triggered.
        //              Call the receive loop dispose.
        //              Verify the receive loop instance indicates lost.
        [TestMethod]
        public async Task Test_1_3_7()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Close the server...
                cth.Serverside_Connection.Close();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Error, 400);

                // Verify the receive loop indicates Error...
                if(rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");

                // Verify that one status callback occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify a lost connection callback occurred...
                if(lostconnection_counter != 1)
                    Assert.Fail("Wrong Value");

                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");

                // Dispose the receiver...
                rl.Dispose();

                // Verify the receive loop indicates Error...
                if(rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");

                // Verify zero status callbacks occurred...
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify zero lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_3_8  Test that start errors on a fully open instance.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Call the start method.
        //              Verify the start method returns error.
        //              Verify the instance remains unaffected.
        [TestMethod]
        public async Task Test_1_3_8()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Attempt to start the receiver again...
                var res2 = rl.Begin_Comms();
                if(res2 != -1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_3_9  Test that disposing a fully open instance has no effect on the underlying TcpClient, and the client can still send messages, but the receive loop cannot receive them.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a message from the client.
        //              Verify the receive loop instance indicates Open.
        //              Call the dispose method on the receive loop.
        //              Verify the conn lost delegate did NOT trigger.
        //              Verify the receive loop instance indicates closed.
        //              Verify the TcpClient instances still indicate connected.
        [TestMethod]
        public async Task Test_1_3_9()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Dispose the receiver...
                rl.Dispose();


                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Closed, 500);

                // Verify the receive loop indicates Error...
                if(rl.State != eLoop_ConnectionStatus.Closed)
                    Assert.Fail("Wrong Value");

                // Verify that one status callback occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Verify no lost connection callback occurred...
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");

                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
                Reset_MessageCallbackData();
                if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");


                // Verify the endpoints are still open...
                if(!cth.Clientside_Connection.Connected)
                    Assert.Fail("Wrong Value");
                if(!cth.Serverside_Connection.Connected)
                    Assert.Fail("Wrong Value");


                // Send another message from the client...
                // This one should not be handled by the receiver, since it is disposed.
                MessageEnvelope me2 = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me2 = new MessageEnvelope();
                    me2.SentTimeUTC = DateTime.UtcNow;
                    me2.Data = json;
                    me2.MessageType = "TestMessage";
                    me2.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me2);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                System.Threading.Thread.Sleep(500);

                // Verify the endpoints are still open...
                if(!cth.Clientside_Connection.Connected)
                    Assert.Fail("Wrong Value");
                if(!cth.Serverside_Connection.Connected)
                    Assert.Fail("Wrong Value");

                // Verify zero message callbacks occurred...
                if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }


        // Verify Message Content Tests...
        //  Test_1_4_1  Test that the received message matches what was sent.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a client message.
        //              Verify the received message matches.
        [TestMethod]
        public async Task Test_1_4_1()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 500);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Get the received message...
                if(!receivedmessage_listing.TryGetValue(1, out string meb2))
                {
                    // Expected a message, but it wasn't in the queue.
                    Assert.Fail("Expected a message, but it wasn't in the queue.");
                }
                // If here, we have a message.

                // Deserialize it...
                var me2 = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageEnvelope>(meb2);
                if(me2 == null)
                    Assert.Fail("Wrong Value");

                if(me2.MessageType != me.MessageType)
                    Assert.Fail("Wrong Value");
                if(me2.Data != me.Data)
                    Assert.Fail("Wrong Value");
                if(me2.SentTimeUTC != me.SentTimeUTC)
                    Assert.Fail("Wrong Value");
                if(me2.MsgId != me.MsgId)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_4_2  Test that an instance can will error for not fully receiving a message after a timeout.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send part of a client message, but not all of it.
        //              Verify the receive loop times out and changes to error.
        [TestMethod]
        public async Task Test_1_4_2()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                // But, only send part of the actual message data.
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);

			        int Result = 0;
			        int bytes_pushed_into_buffer = 0;
			        byte[] frame;

                    // We received a positive length message.
                    // Process it as normal.

                    // Compose the raw buffer that will be pushed down the network stack.
                    // We do this because we must send the data as well as a length, prepending it, so the receiving end can know how much data is in the message.
                    // We push both the size and the data into a single buffer so it's a single network call.
                    // Two array copies (size and data into a single buffer) and one network write are faster than two network writes (for separate size and data).
				    bytes_pushed_into_buffer = bytes.Length + cCustom_Serializer.size_of_Int32;
				    frame = new byte[bytes_pushed_into_buffer];


                    // Add in the frame length at its beginning.
                    // Set a frame size that is larger than the message...
                    int toobigval = 200;
                    int serializesize = cCustom_Serializer.Serialize_Integer32(toobigval, ref frame, 0);
                    if (serializesize != 4)
                        Assert.Fail("Failed to serialize size of the frame.");

			        // Copy over the data.
			        Array.Copy(bytes, 0, frame, 4, bytes.Length);

                    // Send it over the wire...
                    await cth.Clientside_Connection.GetStream().WriteAsync(frame, 0, frame.Length);
                }
                // The above message should trigger the read timeout of the receiver.

                // Wait for status...
                System.Threading.Thread.Sleep(500);

                // We will check that it is still waiting, here...
                // Verify no message was received yet...
                if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                // Verify the receiver is open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");
                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");
                Reset_MessageCallbackData();
                if(receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");


                // Now, wait for the timeout to occur...
                // This would be indicated by our connection being lost...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Lost, 5000);


                // Verify the message was NOT received...
                if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify the receiver promoted to Lost ...
                if(rl.State != eLoop_ConnectionStatus.Lost)
                    Assert.Fail("Wrong Value");

                WaitforCondition(() => statuschange_listing.Count == 1, 1000);

                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }


        //  Metric Update Tests...
        //  Test_1_5_1  Test that receiving a quick-ping (zero byte) message will update last received and message counter.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a quick ping from the client.
        //              Verify the last receive time and receive counter update.
        [TestMethod]
        public async Task Test_1_5_1()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a zero-length message as a ping...
                MessageEnvelope me = null;
                {
			        int bytes_pushed_into_buffer = 0;
			        byte[] frame;

				    bytes_pushed_into_buffer = cCustom_Serializer.size_of_Int32;
				    frame = new byte[bytes_pushed_into_buffer];

                    // Add in the frame length at its beginning.
                    int serializesize = cCustom_Serializer.Serialize_Integer32(0, ref frame, 0);
                    if (serializesize != 4)
                        Assert.Fail("Failed to serialize size of the frame.");

                    // Send it over the wire...
                    await cth.Clientside_Connection.GetStream().WriteAsync(frame, 0, frame.Length);
                }

                // Wait for status...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Open, 5000);

                // Verify the receiver is open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify no message was received yet...
               if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_5_2  Test the last received time and counter update for a received message.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send a regular message from the client.
        //              Verify the last receive time and receive counter update.
        [TestMethod]
        public async Task Test_1_5_2()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => this.receivedmessage_listing.Count == 1, 5000);

                // Verify the message was received...
                if(this.receivedmessage_listing.Count != 1)
                    Assert.Fail("Wrong Value");

                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");

                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_5_3  Test the last received time and counter update for several received messages.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send several messages from the client.
        //              Verify the last receive time and receive counter updates for each message.
        [TestMethod]
        public async Task Test_1_5_3()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => rl.Metrics.Received_Message_Count == 1, 5000);

                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");

                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 1)
                    Assert.Fail("Wrong Value");

                // Wait for status...
                System.Threading.Thread.Sleep(600);

                // Send a message from the client...
                MessageEnvelope me2 = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me2 = new MessageEnvelope();
                    me2.SentTimeUTC = DateTime.UtcNow;
                    me2.Data = json;
                    me2.MessageType = "TestMessage";
                    me2.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me2);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                WaitforCondition(() => rl.Metrics.Received_Message_Count == 2, 5000);

                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");

                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 2)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                System.Threading.Thread.Sleep(600);

                // Send a message from the client...
                MessageEnvelope me3 = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me3 = new MessageEnvelope();
                    me3.SentTimeUTC = DateTime.UtcNow;
                    me3.Data = json;
                    me3.MessageType = "TestMessage";
                    me3.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me3);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);
                    await RawTransportSend(cth.Clientside_Connection.GetStream(), bytes);
                }

                // Wait for status...
                System.Threading.Thread.Sleep(500);

                // Verify the receiver promoted to open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");

                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 3)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_5_4  Test the last received time and counter do NOT update for a received malformed message.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send part of a client message, but not all of it.
        //              Verify the receive loop times out and changes to error.
        //              Verify the last receive time and receive counter do NOT update.
        [TestMethod]
        public async Task Test_1_5_4()
        {
            int localsatuscounter = 0;
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = (cReceiveLoop mep, string statusupdate) =>
                {
                    localsatuscounter++;
                    // A status change was received.
                };

                // Shorten the frame read timeout, so we don't have to wait so long...
                rl.FrameReadTimeout = 4000;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => localsatuscounter == 1, 4000);


                // Verify that one status callbacks occurred...
                if(localsatuscounter != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_LostConnectionData();
                localsatuscounter = 0;
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                // But, only send part of the actual message data.
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);

			        int Result = 0;
			        int bytes_pushed_into_buffer = 0;
			        byte[] frame;

                    // We received a positive length message.
                    // Process it as normal.

                    // Compose the raw buffer that will be pushed down the network stack.
                    // We do this because we must send the data as well as a length, prepending it, so the receiving end can know how much data is in the message.
                    // We push both the size and the data into a single buffer so it's a single network call.
                    // Two array copies (size and data into a single buffer) and one network write are faster than two network writes (for separate size and data).
				    bytes_pushed_into_buffer = bytes.Length + cCustom_Serializer.size_of_Int32;
				    frame = new byte[bytes_pushed_into_buffer];


                    // Add in the frame length at its beginning.
                    // Set a frame size that is larger than the message...
                    int toobigval = 200;
                    int serializesize = cCustom_Serializer.Serialize_Integer32(toobigval, ref frame, 0);
                    if (serializesize != 4)
                        Assert.Fail("Failed to serialize size of the frame.");

			        // Copy over the data.
			        Array.Copy(bytes, 0, frame, 4, bytes.Length);

                    // Send it over the wire...
                    await cth.Clientside_Connection.GetStream().WriteAsync(frame, 0, frame.Length);
                }
                // The above message should trigger the read timeout of the receiver.

                // Store the expected timestamp of the last received message...
                var expected_lastmessagetime = DateTime.UtcNow;

                // Clear the status counter...
                localsatuscounter = 0;

                // Wait for status...
                System.Threading.Thread.Sleep(500);

                // We will check that it is still waiting, here...
                // Verify no message was received yet...
                if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                // Verify the receiver is open...
                if(rl.State != eLoop_ConnectionStatus.Open)
                    Assert.Fail("Wrong Value");

                // Clear the status counter...
                localsatuscounter = 0;

                // Verify that no status callback occurred yet...
                if(localsatuscounter != 0)
                    Assert.Fail("Wrong Value");


                // Now, wait for the timeout to occur...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Lost, 5000);


                // Verify the message was NOT received...
                if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");

                // Verify the receiver promoted to Lost ...
                if(rl.State != eLoop_ConnectionStatus.Lost)
                    Assert.Fail("Wrong Value");

                // Verify that the status callback occurred...
                if(localsatuscounter == 0)
                    Assert.Fail("Wrong Value");


                // Verify the last received timestamp matches what we expect it to be...
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, expected_lastmessagetime, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }

        //  Test_1_5_5  Test the last received time and counter do NOT update for an insanely large message.
        //              Create a connected pair of TcpClients.
        //              Create a receive loop instance with the server client, and start it.
        //              Send an insanely large client message.
        //              Verify the last receive time and receive counter do NOT update.
        [TestMethod]
        public async Task Test_1_5_5()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if(cth.Generate_Connected_TcpClient_Pair(1278) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");
            // We have a pair of tcpclients to work with.

            cReceiveLoop rl = null;
            try
            {
                // Create a receive loop to accept data.
                NLog.ILogger logger = OGA.SharedKernel.Logging_Base.Logger_Ref;
                rl = new cReceiveLoop(cth.Serverside_Connection, logger);
                rl.OnConnection_Went_Bad = this.CALLBACK_ConnectionWentBad;
                rl.OnMessage_Received = this.CALLBACK_Message_Received;
                rl.OnStatus_Change = this.CALLBACK_Status_Change;

                // Start it...
                var res = rl.Begin_Comms();
                if(res != 1)
                    Assert.Fail("Wrong Value");


                // Wait for status...
                WaitforCondition(() => statuschange_listing.Count == 1, 4000);


                // Verify that one status callbacks occurred...
                if(statuschange_listing.Count != 1)
                    Assert.Fail("Wrong Value");


                // Verify the receiver loop is started...
                if(rl.State != eLoop_ConnectionStatus.Newly_Opened)
                    Assert.Fail("Wrong Value");
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");


                // Clear the callback counters...
                Reset_StatusCallbackData();
                Reset_LostConnectionData();
                if(statuschange_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                if(lostconnection_counter != 0)
                    Assert.Fail("Wrong Value");


                // Send a message from the client...
                // But, give it an insanely large size.
                MessageEnvelope me = null;
                {
                    string messagetosend = "Hello";
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(messagetosend);
                
                    me = new MessageEnvelope();
                    me.SentTimeUTC = DateTime.UtcNow;
                    me.Data = json;
                    me.MessageType = "TestMessage";
                    me.MsgId = Guid.NewGuid().ToString();
                    //msg.Props;
                    //msg.Channel = "";
                    //msg.Scope = "";

                    // Serialize the envelope...
                    var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(me);
                    var bytes = Encoding.UTF8.GetBytes(msgjson);

			        int Result = 0;
			        int bytes_pushed_into_buffer = 0;
			        byte[] frame;

                    // We received a positive length message.
                    // Process it as normal.

                    // Compose the raw buffer that will be pushed down the network stack.
                    // We do this because we must send the data as well as a length, prepending it, so the receiving end can know how much data is in the message.
                    // We push both the size and the data into a single buffer so it's a single network call.
                    // Two array copies (size and data into a single buffer) and one network write are faster than two network writes (for separate size and data).
				    bytes_pushed_into_buffer = bytes.Length + cCustom_Serializer.size_of_Int32;
				    frame = new byte[bytes_pushed_into_buffer];


                    // Add in the frame length at its beginning.
                    // Set a frame size that is larger than the message...
                    int toobigval = OGA.TCP.Constants.CONST_MAX_MessageSize + 1;
                    int serializesize = cCustom_Serializer.Serialize_Integer32(toobigval, ref frame, 0);
                    if (serializesize != 4)
                        Assert.Fail("Failed to serialize size of the frame.");

			        // Copy over the data.
			        Array.Copy(bytes, 0, frame, 4, bytes.Length);

                    // Send it over the wire...
                    await cth.Clientside_Connection.GetStream().WriteAsync(frame, 0, frame.Length);
                }


                // Wait for status...
                System.Threading.Thread.Sleep(500);

                // We will check that it is still waiting, here...
                // Verify no message was received yet...
                if(this.receivedmessage_listing.Count != 0)
                    Assert.Fail("Wrong Value");
                // Verify the receiver is error...
                if(rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");
                // Verify that two status callbacks occurred...
                if(statuschange_listing.Count != 2)
                    Assert.Fail("Wrong Value");

                // Verify the last received timestamp matches what we expect it to be...
                if(!this.AreDatesClose(rl.Last_Received_TimestampUTC, DateTime.UtcNow, 1))
                    Assert.Fail("Wrong Value");
                if(rl.Metrics.Received_Message_Count != 0)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try
                {
                    rl.Dispose();
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Clientside_Connection.Close();
#else
                    cth.Clientside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
                try
                {
#if (NET452)
                    cth.Serverside_Connection.Close();
#else
                    cth.Serverside_Connection.Dispose();
#endif
                }
                catch (Exception e) { }
            }
        }



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

        
        private void Reset_StatusCallbackData()
        {
            this.statuscounter = 0;
            this.statuschange_listing = new Dictionary<int, string>();
        }

        Dictionary<int, string> statuschange_listing = new Dictionary<int, string>();
        int statuscounter = 0;
        private void CALLBACK_Status_Change(cReceiveLoop mep, string statusupdate)
        {
            statuscounter++;
            statuschange_listing.Add(statuscounter, statusupdate);
            // A status change was received.
        }

        private void Reset_MessageCallbackData()
        {
            this.receivedmessage_listing = new Dictionary<int, string>();
        }
        Dictionary<int, string> receivedmessage_listing = new Dictionary<int, string>();
        private void CALLBACK_Message_Received(cReceiveLoop mep, string rawmsg)
        {
            // A message was received.
            receivedmessage_listing.Add(receivedmessage_listing.Keys.Count + 1, rawmsg);
        }

        private void Reset_LostConnectionData()
        {
            this.lostconnection_counter = 0;
        }
        int lostconnection_counter = 0;
        private void CALLBACK_ConnectionWentBad(cReceiveLoop mep)
        {
            // A connection went bad.
            lostconnection_counter++;
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
