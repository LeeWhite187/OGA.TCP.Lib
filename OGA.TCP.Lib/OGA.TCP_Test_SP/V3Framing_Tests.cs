using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP;
using OGA.TCP.Messages;
using OGA.TCP.Server;
using OGA.TCP.Server.Lib_Tests.Helpers;
using OGA.TCP.SessionLayer;
using OGA.TCP.Shared.Encoding;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Testing_CommonHelpers_SP.Helpers;
using WSEndpoint_Tests.HelperClasses;

namespace OGA.TCP_Test_SP
{
    /*  LibVersion 3 (typed-frame) feature tests.

        These tests exercise the v3 wire capabilities end-to-end over a loopback connection:
        a real TCPClient_v1_Impl talking to the TESTINGSRVR fork of the server endpoint.

        // Binary message exchange (KD-04)...
        //  Test_3_1_1  Client sends a small binary payload to the server; server receives it intact, with routing metadata.
        //  Test_3_1_2  Server sends a small binary payload to the client; client receives it intact.

        // Large-message chunking, json inner type (KD-05)...
        //  Test_3_2_1  Client sends a json message larger than one frame; server reassembles and dispatches it intact.
        //  Test_3_2_2  Server sends a json message larger than one frame; client reassembles and dispatches it intact.

        // Large-message chunking, binary inner type (KD-05)...
        //  Test_3_3_1  Client sends a binary payload larger than one frame; server reassembles it intact.
        //  Test_3_3_2  Server sends a binary payload larger than one frame; client reassembles it intact.

        // Byte-true sizing (KD-06)...
        //  Test_3_4_1  A json payload of non-BMP characters (emoji, CJK) roundtrips intact.

        // Receive limits (KD-06)...
        //  Test_3_5_1  A transfer declared over the receiver's MaxTransferSize is rejected at the start
        //              declaration: nothing is dispatched, and the connection survives.

        // Framing-mismatch detection (KD-07)...
        //  Test_3_6_1  A legacy (pre-v3) framed message hits the reserved 0x7B frame-type detector: the
        //              receive loop reports error and fires the went-bad delegate.
        //  Test_3_6_2  An unknown frame-type byte is fatal: the receive loop reports error.

        // Registration prop robustness (OI-29)...
        //  Test_3_7_1  A registration prop value containing colons and quotes survives the trip intact.
    */

    [DoNotParallelize]
    [TestClass]
    public class V3Framing_Tests : OGA.Testing.Lib.Test_Base_abstract
    {
        #region Private Fields

        // Resolved at runtime, so the suite runs on any host (see cTestHost_Helper for the resolution strategy)...
        protected string RemoteHost = OGA.Testing.Helpers.cTestHost_Helper.GetPrimaryIPv4();
        protected int RemotePort = 5019;

        private TESTINGSRVR_Simple_TCPListener _wsl;

        // Server-side binary capture...
        private readonly object _srvbinlock = new object();
        private List<(string messagetype, byte[] payload, string channel, string scope, string corelationid)> _srv_binmsgs;

        // Server-side json capture...
        private readonly object _srvjsonlock = new object();
        private List<(string messagetype, string jsondata, string corelationid)> _srv_jsonmsgs;

        // Client-side binary capture...
        private readonly object _clibinlock = new object();
        private List<byte[]> _cli_binmsgs;

        // Client-side json capture...
        private readonly object _clijsonlock = new object();
        private List<(string messagetype, string jsondata)> _cli_jsonmsgs;

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

            // Reset listener statics...
            TESTINGSRVR_Simple_TCPListener.DoSomethingWith_ConnectionRegistration = false;
            TESTINGSRVR_Simple_TCPListener.DoSomethingWith_ConnectionClosure = false;
            TESTINGSRVR_Simple_TCPListener.AllowQuietClients = true;
            TESTINGSRVR_Simple_TCPListener.WeRequireClients_tobe_Chatty = false;
            TESTINGSRVR_Simple_TCPListener.Keepalive_Timeout = 20;

            // Reset capture buffers...
            this._srv_binmsgs = new List<(string, byte[], string, string, string)>();
            this._srv_jsonmsgs = new List<(string, string, string)>();
            this._cli_binmsgs = new List<byte[]>();
            this._cli_jsonmsgs = new List<(string, string)>();

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
            try
            {
                _wsl?.Stop();
                _wsl?.Dispose();
                _wsl = null;
            }
            catch (Exception) { }

            base.TearDown();
        }

        #endregion


        #region Binary Message Exchange

        //  Test_3_1_1  Client sends a small binary payload to the server; server receives it intact, with routing metadata.
        [TestMethod]
        public async Task Test_3_1_1()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                client = await this.Connect_Client_and_WaitforRegistration();

                // Attach the server-side binary capture...
                _wsl.ServerSide_TCPEndpoint.OnBinaryMessageReceived = this.CALLBACK_Server_BinaryMessageReceived;

                // Compose a distinctive payload...
                var payload = this.Make_RandomBytes(4096, 20260730);
                var cid = Guid.NewGuid().ToString();

                // Send it as a binary message...
                var res = await client.SendBinaryMessage_to_Endpoint(payload, "", "", cid, "testbinmsg");
                if (res != 1)
                    Assert.Fail("Failed to send binary message.");

                // Wait for the server to receive it...
                WaitforCondition(() => { lock (_srvbinlock) { return _srv_binmsgs.Count == 1; } }, 3000);

                (string messagetype, byte[] payload, string channel, string scope, string corelationid) got;
                lock (_srvbinlock) { got = _srv_binmsgs[0]; }

                // Verify the payload arrived byte-identical, with its routing metadata...
                if (got.messagetype != "testbinmsg")
                    Assert.Fail("Wrong Value");
                if (got.corelationid != cid)
                    Assert.Fail("Wrong Value");
                if (!this.AreBytesEqual(payload, got.payload))
                    Assert.Fail("Payload mismatch");
            }
            finally
            {
                client?.Dispose();
            }
        }

        //  Test_3_1_2  Server sends a small binary payload to the client; client receives it intact.
        [TestMethod]
        public async Task Test_3_1_2()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                client = await this.Connect_Client_and_WaitforRegistration();

                // Attach the client-side binary capture...
                client.OnBinaryFrameReceived = this.CALLBACK_Client_BinaryFrameReceived;

                // Compose a distinctive payload...
                var payload = this.Make_RandomBytes(4096, 20260731);

                // Send it from the server endpoint...
                var res = await _wsl.ServerSide_TCPEndpoint.SendBinary_toClient(payload, "", "", "", "testbinmsg");
                if (res != 1)
                    Assert.Fail("Failed to send binary message.");

                // Wait for the client to receive it...
                WaitforCondition(() => { lock (_clibinlock) { return _cli_binmsgs.Count == 1; } }, 3000);

                byte[] got;
                lock (_clibinlock) { got = _cli_binmsgs[0]; }

                // Verify the payload arrived byte-identical...
                if (!this.AreBytesEqual(payload, got))
                    Assert.Fail("Payload mismatch");
            }
            finally
            {
                client?.Dispose();
            }
        }

        #endregion


        #region Large Json Messages

        //  Test_3_2_1  Client sends a json message larger than one frame; server reassembles and dispatches it intact.
        [TestMethod]
        public async Task Test_3_2_1()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                client = await this.Connect_Client_and_WaitforRegistration();

                // Attach the server-side json capture...
                _wsl.ServerSide_TCPEndpoint.OnMessageReceived = this.CALLBACK_Server_MessageReceived;

                // Compose a message payload that cannot fit one frame (default MaxFrameSize is 1 MiB)...
                var lm = new LargeMessage();
                lm.trackingid = Guid.NewGuid().ToString();
                lm.MessageData = this.Make_RandomString((3 * 1024 * 1024) + 137, 1001);
                var senthash = this.ComputeSha256Hash(lm.MessageData);

                // Send it; the library must chunk it transparently...
                var res = await client.SendMessage_to_Endpoint(lm);
                if (res != 1)
                    Assert.Fail("Failed to send large json message.");

                // Wait for the server to reassemble and dispatch it...
                WaitforCondition(() => { lock (_srvjsonlock) { return _srv_jsonmsgs.Count == 1; } }, 15000);

                (string messagetype, string jsondata, string corelationid) got;
                lock (_srvjsonlock) { got = _srv_jsonmsgs[0]; }

                // Verify the payload survived reassembly intact...
                if (got.messagetype.ToLower() != nameof(LargeMessage).ToLower())
                    Assert.Fail("Wrong Value");
                var gotlm = Newtonsoft.Json.JsonConvert.DeserializeObject<LargeMessage>(got.jsondata);
                if (gotlm.trackingid != lm.trackingid)
                    Assert.Fail("Wrong Value");
                if (this.ComputeSha256Hash(gotlm.MessageData) != senthash)
                    Assert.Fail("Payload hash mismatch");
            }
            finally
            {
                client?.Dispose();
            }
        }

        //  Test_3_2_2  Server sends a json message larger than one frame; client reassembles and dispatches it intact.
        [TestMethod]
        public async Task Test_3_2_2()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                client = await this.Connect_Client_and_WaitforRegistration();

                // Attach the client-side json capture...
                client.OnMessageReceived = this.CALLBACK_Client_MessageReceived;

                // Compose a message payload that cannot fit one frame...
                var lm = new LargeMessage();
                lm.trackingid = Guid.NewGuid().ToString();
                lm.MessageData = this.Make_RandomString((3 * 1024 * 1024) + 137, 1002);
                var senthash = this.ComputeSha256Hash(lm.MessageData);

                // Send it from the server endpoint; the library must chunk it transparently...
                var res = await _wsl.ServerSide_TCPEndpoint.SendMessage_toClient(lm);
                if (res != 1)
                    Assert.Fail("Failed to send large json message.");

                // Wait for the client to reassemble and dispatch it...
                WaitforCondition(() => { lock (_clijsonlock) { return _cli_jsonmsgs.Count == 1; } }, 15000);

                (string messagetype, string jsondata) got;
                lock (_clijsonlock) { got = _cli_jsonmsgs[0]; }

                // Verify the payload survived reassembly intact...
                if (got.messagetype.ToLower() != nameof(LargeMessage).ToLower())
                    Assert.Fail("Wrong Value");
                var gotlm = Newtonsoft.Json.JsonConvert.DeserializeObject<LargeMessage>(got.jsondata);
                if (gotlm.trackingid != lm.trackingid)
                    Assert.Fail("Wrong Value");
                if (this.ComputeSha256Hash(gotlm.MessageData) != senthash)
                    Assert.Fail("Payload hash mismatch");
            }
            finally
            {
                client?.Dispose();
            }
        }

        #endregion


        #region Large Binary Messages

        //  Test_3_3_1  Client sends a binary payload larger than one frame; server reassembles it intact.
        [TestMethod]
        public async Task Test_3_3_1()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                client = await this.Connect_Client_and_WaitforRegistration();

                // Attach the server-side binary capture...
                _wsl.ServerSide_TCPEndpoint.OnBinaryMessageReceived = this.CALLBACK_Server_BinaryMessageReceived;

                // Compose a payload that cannot fit one frame...
                var payload = this.Make_RandomBytes((3 * 1024 * 1024) + 137, 2001);
                var senthash = this.ComputeSha256Hash(payload);

                // Send it; the library must chunk it transparently...
                var res = await client.SendBinaryMessage_to_Endpoint(payload, "", "", "", "bigbinmsg");
                if (res != 1)
                    Assert.Fail("Failed to send large binary message.");

                // Wait for the server to reassemble and dispatch it...
                WaitforCondition(() => { lock (_srvbinlock) { return _srv_binmsgs.Count == 1; } }, 15000);

                (string messagetype, byte[] payload, string channel, string scope, string corelationid) got;
                lock (_srvbinlock) { got = _srv_binmsgs[0]; }

                // Verify the payload survived reassembly intact...
                if (got.messagetype != "bigbinmsg")
                    Assert.Fail("Wrong Value");
                if (got.payload.Length != payload.Length)
                    Assert.Fail("Payload length mismatch");
                if (this.ComputeSha256Hash(got.payload) != senthash)
                    Assert.Fail("Payload hash mismatch");
            }
            finally
            {
                client?.Dispose();
            }
        }

        //  Test_3_3_2  Server sends a binary payload larger than one frame; client reassembles it intact.
        [TestMethod]
        public async Task Test_3_3_2()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                client = await this.Connect_Client_and_WaitforRegistration();

                // Attach the client-side binary capture...
                client.OnBinaryFrameReceived = this.CALLBACK_Client_BinaryFrameReceived;

                // Compose a payload that cannot fit one frame...
                var payload = this.Make_RandomBytes((3 * 1024 * 1024) + 137, 2002);
                var senthash = this.ComputeSha256Hash(payload);

                // Send it from the server endpoint; the library must chunk it transparently...
                var res = await _wsl.ServerSide_TCPEndpoint.SendBinary_toClient(payload, "", "", "", "bigbinmsg");
                if (res != 1)
                    Assert.Fail("Failed to send large binary message.");

                // Wait for the client to reassemble and dispatch it...
                WaitforCondition(() => { lock (_clibinlock) { return _cli_binmsgs.Count == 1; } }, 15000);

                byte[] got;
                lock (_clibinlock) { got = _cli_binmsgs[0]; }

                // Verify the payload survived reassembly intact...
                if (got.Length != payload.Length)
                    Assert.Fail("Payload length mismatch");
                if (this.ComputeSha256Hash(got) != senthash)
                    Assert.Fail("Payload hash mismatch");
            }
            finally
            {
                client?.Dispose();
            }
        }

        #endregion


        #region Byte-true Sizing

        //  Test_3_4_1  A json payload of non-BMP characters (emoji, CJK) roundtrips intact.
        //              These characters encode to multiple UTF-8 bytes per char, so this exercises the
        //              byte-true size decisions on a payload whose byte count far exceeds its char count.
        [TestMethod]
        public async Task Test_3_4_1()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                client = await this.Connect_Client_and_WaitforRegistration();

                // Attach the server-side json capture...
                _wsl.ServerSide_TCPEndpoint.OnMessageReceived = this.CALLBACK_Server_MessageReceived;

                // Compose a payload of non-BMP and multi-byte characters...
                var b = new StringBuilder();
                while (b.Length < 200000)
                    b.Append("😀🚀💾中文テスト🎉");
                var lm = new LargeMessage();
                lm.trackingid = Guid.NewGuid().ToString();
                lm.MessageData = b.ToString();

                // Send it...
                var res = await client.SendMessage_to_Endpoint(lm);
                if (res != 1)
                    Assert.Fail("Failed to send non-BMP json message.");

                // Wait for the server to receive it...
                WaitforCondition(() => { lock (_srvjsonlock) { return _srv_jsonmsgs.Count == 1; } }, 10000);

                (string messagetype, string jsondata, string corelationid) got;
                lock (_srvjsonlock) { got = _srv_jsonmsgs[0]; }

                // Verify the payload arrived character-identical...
                var gotlm = Newtonsoft.Json.JsonConvert.DeserializeObject<LargeMessage>(got.jsondata);
                if (gotlm.trackingid != lm.trackingid)
                    Assert.Fail("Wrong Value");
                if (!string.Equals(gotlm.MessageData, lm.MessageData, StringComparison.Ordinal))
                    Assert.Fail("Payload mismatch");
            }
            finally
            {
                client?.Dispose();
            }
        }

        #endregion


        #region Receive Limits

        //  Test_3_5_1  A transfer declared over the receiver's MaxTransferSize is rejected at the start
        //              declaration: nothing is dispatched, and the connection survives.
        [TestMethod]
        public async Task Test_3_5_1()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                client = await this.Connect_Client_and_WaitforRegistration();

                // Attach the server-side binary capture...
                _wsl.ServerSide_TCPEndpoint.OnBinaryMessageReceived = this.CALLBACK_Server_BinaryMessageReceived;

                // Constrain the server's transfer ceiling below what we are about to send...
                _wsl.ServerSide_TCPEndpoint.MaxTransferSize = 200000;

                // Send a payload that needs a transfer bigger than the server allows...
                var payload = this.Make_RandomBytes((3 * 1024 * 1024) + 137, 3001);
                var res = await client.SendBinaryMessage_to_Endpoint(payload, "", "", "", "rejectme");

                // The sender completes its sends (limits are local receiver policy; there are no acks),
                // so no failure surfaces at the call site...
                if (res != 1)
                    Assert.Fail("Send should have completed from the sender's view.");

                // Give the server time to have processed the whole transfer...
                System.Threading.Thread.Sleep(3000);

                // Verify nothing was dispatched...
                lock (_srvbinlock)
                {
                    if (_srv_binmsgs.Count != 0)
                        Assert.Fail("Over-limit transfer must not dispatch.");
                }

                // Verify both ends survived the rejected transfer...
                if (!client.IsConnected)
                    Assert.Fail("Client connection should have survived.");
                if (!_wsl.ServerSide_TCPEndpoint.IsConnected)
                    Assert.Fail("Server connection should have survived.");
            }
            finally
            {
                client?.Dispose();
            }
        }

        #endregion


        #region Framing-mismatch Detection

        //  Test_3_6_1  A legacy (pre-v3) framed message hits the reserved 0x7B frame-type detector: the
        //              receive loop reports error and fires the went-bad delegate.
        [TestMethod]
        public async Task Test_3_6_1()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if (cth.Generate_Connected_TcpClient_Pair(1291) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");

            cReceiveLoop rl = null;
            int wentbad = 0;
            try
            {
                // Stand up a v3 receive loop on the server side...
                rl = new cReceiveLoop(cth.Serverside_Connection, OGA.SharedKernel.Logging_Base.Logger_Ref);
                rl.OnConnection_Went_Bad = (mep) => { wentbad++; };
                rl.OnMessage_Received = (mep, frametype, body) => { };
                rl.OnStatus_Change = (mep, sts) => { };
                if (rl.Begin_Comms() != 1)
                    Assert.Fail("Failed to start receive loop.");

                // Compose a LEGACY-framed message: 4-byte length then the raw json body, NO frame-type byte.
                // The receiver reads the json open brace (0x7B) in the frame-type position - the reserved
                // legacy-framing detector value...
                var me = new MessageEnvelope();
                me.MsgId = "1";
                me.MessageType = "TestMessage";
                me.Data = "\"Hello\"";
                var bytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(me));

                byte[] frame = new byte[bytes.Length + 4];
                cCustom_Serializer.Serialize_Integer32(bytes.Length, ref frame, 0);
                Array.Copy(bytes, 0, frame, 4, bytes.Length);
                await cth.Clientside_Connection.GetStream().WriteAsync(frame, 0, frame.Length);

                // The loop must die on the legacy tell...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Error, 3000);

                if (rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");

                WaitforCondition(() => wentbad == 1, 1000);
                if (wentbad != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try { rl?.Dispose(); } catch (Exception) { }
                try { cth.Dispose(); } catch (Exception) { }
            }
        }

        //  Test_3_6_2  An unknown frame-type byte is fatal: the receive loop reports error.
        [TestMethod]
        public async Task Test_3_6_2()
        {
            // Get a pair of connected tcp client instances that we can work with.
            cClientServer_Test_Helper cth = new cClientServer_Test_Helper();
            if (cth.Generate_Connected_TcpClient_Pair(1292) != 1)
                Assert.Fail("Failed to get a pair of tcpclient references to test with.");

            cReceiveLoop rl = null;
            int wentbad = 0;
            try
            {
                // Stand up a v3 receive loop on the server side...
                rl = new cReceiveLoop(cth.Serverside_Connection, OGA.SharedKernel.Logging_Base.Logger_Ref);
                rl.OnConnection_Went_Bad = (mep) => { wentbad++; };
                rl.OnMessage_Received = (mep, frametype, body) => { };
                rl.OnStatus_Change = (mep, sts) => { };
                if (rl.Begin_Comms() != 1)
                    Assert.Fail("Failed to start receive loop.");

                // Compose a well-formed preamble carrying a frame type the registry does not define...
                byte[] junkbody = Encoding.UTF8.GetBytes("junk");
                byte[] frame = new byte[junkbody.Length + 5];
                cCustom_Serializer.Serialize_Integer32(junkbody.Length, ref frame, 0);
                frame[4] = 77;
                Array.Copy(junkbody, 0, frame, 5, junkbody.Length);
                await cth.Clientside_Connection.GetStream().WriteAsync(frame, 0, frame.Length);

                // The loop must die on the unknown type...
                WaitforCondition(() => rl.State == eLoop_ConnectionStatus.Error, 3000);

                if (rl.State != eLoop_ConnectionStatus.Error)
                    Assert.Fail("Wrong Value");

                WaitforCondition(() => wentbad == 1, 1000);
                if (wentbad != 1)
                    Assert.Fail("Wrong Value");
            }
            finally
            {
                try { rl?.Dispose(); } catch (Exception) { }
                try { cth.Dispose(); } catch (Exception) { }
            }
        }

        #endregion


        #region Registration Prop Robustness

        //  Test_3_7_1  A registration prop value containing colons and quotes survives the trip intact.
        //              The historical prop handling truncated values at the first colon and produced
        //              malformed fragments for quotes (OI-29); the PropString composer/parser fixes both.
        [TestMethod]
        public async Task Test_3_7_1()
        {
            TCPClient_v1_Impl client = null;
            try
            {
                // A RuntimeId that exercises colons and an embedded quote...
                var hostileruntimeid = "node:4222/\"beta\":9";

                client = await this.Connect_Client_and_WaitforRegistration((cl) =>
                {
                    cl.RuntimeId = hostileruntimeid;
                });

                // Verify the server recorded the value character-identical...
                if (!string.Equals(_wsl.ServerSide_TCPEndpoint.ClientInfo.RuntimeId, hostileruntimeid, StringComparison.Ordinal))
                    Assert.Fail("RuntimeId did not survive registration intact.");
            }
            finally
            {
                client?.Dispose();
            }
        }

        #endregion


        #region Helper Methods

        /// <summary>
        /// Stands up a default (v3) client, connects it to the test listener, and waits for its
        ///     registration to land server-side.
        /// An optional configuration callback runs before the client starts, so a test can adjust
        ///     identity values from the defaults.
        /// </summary>
        /// <param name="configure">Optional pre-start configuration hook.</param>
        /// <returns></returns>
        private async Task<TCPClient_v1_Impl> Connect_Client_and_WaitforRegistration(Action<TCPClient_v1_Impl> configure = null)
        {
            var logger = OGA.SharedKernel.Logging_Base.Logger_Ref;

            if (this._wsl.Listener.State != eListenerState.Active)
                Assert.Fail("Listener not active");

            var cp = clientproperties.Create_Random_WSLibV3_ClientData();

            var client = new TCPClient_v1_Impl(RemoteHost, RemotePort, logger);
            client.DeviceId = cp.DeviceId;
            client.UserId = (Guid)cp.UserId;
            client.RuntimeId = cp.RuntimeId;
            client.Pid = cp.Pid;
            client.AppId = cp.AppId;
            client.AppVersion = cp.AppVersion;
            client.Cfg_Disable_KeepAlive = true;

            // Let the test adjust the client before it starts...
            configure?.Invoke(client);

            var res = await client.Start_Async();
            if (res != 1)
                Assert.Fail("Failed to start client.");

            WaitforCondition(() => client.IsConnected, 3000);
            if (!client.IsConnected)
                Assert.Fail("Connection Failed");

            WaitforCondition(() => _wsl.ServerSide_TCPEndpoint?.ClientInfo.IsRegistered ?? false, 3000);
            if (!_wsl.ServerSide_TCPEndpoint.ClientInfo.IsRegistered)
                Assert.Fail("Registration Failed");

            // The server marks the client registered before the client has processed the registration
            // reply that opens its send gate - wait for the client side to be ready to send as well...
            WaitforCondition(() => client.AllowSend, 3000);
            if (!client.AllowSend)
                Assert.Fail("Client send gate did not open");

            return client;
        }

        private int CALLBACK_Server_BinaryMessageReceived(TESTINGSRVR_Endpoint_Abstract mep, string messagetype, byte[] payload, string channel, string scope, string corelationid)
        {
            lock (_srvbinlock)
            {
                _srv_binmsgs.Add((messagetype, payload, channel, scope, corelationid));
            }
            return 1;
        }

        private int CALLBACK_Server_MessageReceived(TESTINGSRVR_Endpoint_Abstract mep, string messagetype, string jsondata, string corelationid)
        {
            lock (_srvjsonlock)
            {
                _srv_jsonmsgs.Add((messagetype, jsondata, corelationid));
            }
            return 1;
        }

        private int CALLBACK_Client_BinaryFrameReceived(Client_v1_Abstract ws, byte[] msg)
        {
            lock (_clibinlock)
            {
                _cli_binmsgs.Add(msg);
            }
            return 1;
        }

        private int CALLBACK_Client_MessageReceived(Client_v1_Abstract mep, string messagetype, string msg)
        {
            lock (_clijsonlock)
            {
                _cli_jsonmsgs.Add((messagetype, msg));
            }
            return 1;
        }

        private byte[] Make_RandomBytes(int length, int seed)
        {
            var rnd = new Random(seed);
            var d = new byte[length];
            rnd.NextBytes(d);
            return d;
        }

        private string Make_RandomString(int length, int seed)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var rnd = new Random(seed);
            var b = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                b.Append(alphabet[rnd.Next(alphabet.Length)]);
            return b.ToString();
        }

        private bool AreBytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null)
                return false;
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        private string ComputeSha256Hash(string rawData)
        {
            return this.ComputeSha256Hash(Encoding.UTF8.GetBytes(rawData));
        }

        private string ComputeSha256Hash(byte[] rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(rawData);
                var builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        #endregion
    }
}
