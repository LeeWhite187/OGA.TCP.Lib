using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP.Channels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP_Test_SP
{
    /*  Unit Tests for: ChannelDispatcher and the channel adapter substrate.
        Description:    These tests exercise the dispatcher in isolation - registration, removal, routing,
                        kind enforcement, fallbacks, counters, and thread-safety - with no sockets involved.

        // Registration behavior...
        //  Test_1_1_1  Adding a valid adapter succeeds; adding a duplicate channel is refused; the registry counts match.
        //  Test_1_1_2  Adding a null adapter and an adapter with an empty channel id are refused with distinct codes.
        //  Test_1_1_3  Removing a registered channel closes the adapter and empties the registry; removing an unknown channel returns -1.
        //  Test_1_1_4  CloseAll closes every adapter and empties the registry, even when an adapter's Close throws.

        // Routing behavior...
        //  Test_1_2_1  A json message routes to its channel's adapter with all arguments intact.
        //  Test_1_2_2  A json message for an unknown channel is refused with -1.
        //  Test_1_2_3  A channel-less json message goes to the no-channel handler; with no handler assigned, it returns 0.
        //  Test_1_2_4  A json message arriving on a binary-kind channel is refused with -2, and counted as bad.
        //  Test_1_2_5  A binary payload routes to a binary-kind adapter; a binary payload on a json-kind channel is refused with -2.
        //  Test_1_2_6  A handler that throws yields -10, and the dispatcher survives for subsequent dispatches.

        // Counters...
        //  Test_1_3_1  Receive and bad counters accumulate per delivery result, and ResetCounters clears them.

        // Thread safety...
        //  Test_1_4_1  Concurrent registration, removal, and dispatch across many threads complete without exception,
        //              and the registry ends in a consistent state.
     */

    [DoNotParallelize]
    [TestClass]
    public class ChannelDispatcher_Tests : OGA.Testing.Lib.Test_Base_abstract
    {
        #region Test Doubles

        /// <summary>
        /// Minimal messaging host stub, so the dispatcher can be exercised without a socket client.
        /// </summary>
        private class FakeHost : IMessagingHost
        {
            public int InstanceId { get; } = 999;
            public string TransportShortName { get; } = "test";
            public int SendCount = 0;
            public Task<int> SendMessage_toPeer(object payload, string channel = "", string scope = "", string corelationid = "")
            {
                SendCount++;
                return Task.FromResult(1);
            }
        }

        /// <summary>
        /// Recording adapter used to observe deliveries and to simulate handler outcomes.
        /// </summary>
        private class RecordingAdapter : ChannelAdapter_abstract
        {
            public int JsonCalls = 0;
            public int BinaryCalls = 0;
            public string LastMessageType = null;
            public string LastJson = null;
            public byte[] LastPayload = null;
            public string LastCid = null;
            public int ResultToReturn = 1;
            public bool ThrowOnAccept = false;
            public bool ThrowOnClose = false;
            public bool Closed = false;

            public RecordingAdapter(string channelid, eChannelKind kind) : base(channelid, kind, null) { }

            public override int AcceptIncomingMessage(IMessagingHost host, string messagetype, string jsondata, string corelationid)
            {
                if (ThrowOnAccept) throw new InvalidOperationException("handler fault");
                JsonCalls++;
                LastMessageType = messagetype;
                LastJson = jsondata;
                LastCid = corelationid;
                return ResultToReturn;
            }

            public override int AcceptIncomingMessage(IMessagingHost host, string messagetype, byte[] payload, string corelationid)
            {
                if (ThrowOnAccept) throw new InvalidOperationException("handler fault");
                BinaryCalls++;
                LastMessageType = messagetype;
                LastPayload = payload;
                LastCid = corelationid;
                return ResultToReturn;
            }

            public override void Close()
            {
                Closed = true;
                if (ThrowOnClose) throw new InvalidOperationException("close fault");
                base.Close();
            }
        }

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


        #region Registration Tests

        //  Test_1_1_1  Adding a valid adapter succeeds; adding a duplicate channel is refused; the registry counts match.
        [TestMethod]
        public void Test_1_1_1()
        {
            var d = new ChannelDispatcher(1);

            var ca = new RecordingAdapter("chan1", eChannelKind.Json);
            if (d.Add(ca) != 1)
                Assert.Fail("Wrong return");

            if (d.Count != 1)
                Assert.Fail("Wrong Value");

            // A duplicate channel registration must be refused...
            var dup = new RecordingAdapter("chan1", eChannelKind.Json);
            if (d.Add(dup) != -1)
                Assert.Fail("Wrong return");

            if (d.Count != 1)
                Assert.Fail("Wrong Value");

            if (!d.Contains("chan1"))
                Assert.Fail("Wrong Value");
        }

        //  Test_1_1_2  Adding a null adapter and an adapter with an empty channel id are refused with distinct codes.
        [TestMethod]
        public void Test_1_1_2()
        {
            var d = new ChannelDispatcher(1);

            if (d.Add(null) != -1)
                Assert.Fail("Wrong return");

            var noname = new RecordingAdapter("", eChannelKind.Json);
            if (d.Add(noname) != -2)
                Assert.Fail("Wrong return");

            if (d.Count != 0)
                Assert.Fail("Wrong Value");
        }

        //  Test_1_1_3  Removing a registered channel closes the adapter and empties the registry; removing an unknown channel returns -1.
        [TestMethod]
        public void Test_1_1_3()
        {
            var d = new ChannelDispatcher(1);

            var ca = new RecordingAdapter("chan1", eChannelKind.Json);
            d.Add(ca);

            if (d.Remove("chan1") != 1)
                Assert.Fail("Wrong return");

            if (!ca.Closed)
                Assert.Fail("Adapter was not closed on removal.");

            if (d.Count != 0)
                Assert.Fail("Wrong Value");

            if (d.Remove("chan1") != -1)
                Assert.Fail("Wrong return");

            if (d.Remove("") != -1)
                Assert.Fail("Wrong return");
        }

        //  Test_1_1_4  CloseAll closes every adapter and empties the registry, even when an adapter's Close throws.
        [TestMethod]
        public void Test_1_1_4()
        {
            var d = new ChannelDispatcher(1);

            var ca1 = new RecordingAdapter("chan1", eChannelKind.Json);
            var ca2 = new RecordingAdapter("chan2", eChannelKind.Json) { ThrowOnClose = true };
            var ca3 = new RecordingAdapter("chan3", eChannelKind.Binary);
            d.Add(ca1);
            d.Add(ca2);
            d.Add(ca3);

            // This must complete despite ca2 throwing from Close...
            d.CloseAll();

            if (d.Count != 0)
                Assert.Fail("Registry was not emptied.");

            if (!ca1.Closed || !ca2.Closed || !ca3.Closed)
                Assert.Fail("Not all adapters were closed.");
        }

        #endregion


        #region Routing Tests

        //  Test_1_2_1  A json message routes to its channel's adapter with all arguments intact.
        [TestMethod]
        public void Test_1_2_1()
        {
            var d = new ChannelDispatcher(1);
            var host = new FakeHost();

            var ca = new RecordingAdapter("chan1", eChannelKind.Json);
            d.Add(ca);

            var res = d.DispatchJson(host, "chan1", "sometype", "{\"a\":1}", "cid123");
            if (res != 1)
                Assert.Fail("Wrong return");

            if (ca.JsonCalls != 1)
                Assert.Fail("Wrong Value");
            if (ca.LastMessageType != "sometype")
                Assert.Fail("Wrong Value");
            if (ca.LastJson != "{\"a\":1}")
                Assert.Fail("Wrong Value");
            if (ca.LastCid != "cid123")
                Assert.Fail("Wrong Value");
        }

        //  Test_1_2_2  A json message for an unknown channel is refused with -1.
        [TestMethod]
        public void Test_1_2_2()
        {
            var d = new ChannelDispatcher(1);
            var host = new FakeHost();

            var res = d.DispatchJson(host, "nosuchchannel", "sometype", "{}", "");
            if (res != -1)
                Assert.Fail("Wrong return");
        }

        //  Test_1_2_3  A channel-less json message goes to the no-channel handler; with no handler assigned, it returns 0.
        [TestMethod]
        public void Test_1_2_3()
        {
            var d = new ChannelDispatcher(1);
            var host = new FakeHost();

            // No handler assigned: the message is dropped with 0...
            var res1 = d.DispatchJson(host, "", "sometype", "{}", "");
            if (res1 != 0)
                Assert.Fail("Wrong return");

            // Assign a handler, and verify delivery...
            int fallbackcalls = 0;
            d.NoChannelJsonHandler = (h, mt, json, cid) =>
            {
                fallbackcalls++;
                return 1;
            };

            var res2 = d.DispatchJson(host, "", "sometype", "{}", "");
            if (res2 != 1)
                Assert.Fail("Wrong return");
            if (fallbackcalls != 1)
                Assert.Fail("Wrong Value");
        }

        //  Test_1_2_4  A json message arriving on a binary-kind channel is refused with -2, and counted as bad.
        [TestMethod]
        public void Test_1_2_4()
        {
            var d = new ChannelDispatcher(1);
            var host = new FakeHost();

            var ca = new RecordingAdapter("binchan", eChannelKind.Binary);
            d.Add(ca);

            var res = d.DispatchJson(host, "binchan", "sometype", "{}", "");
            if (res != -2)
                Assert.Fail("Wrong return");

            // The adapter must not have been delivered to...
            if (ca.JsonCalls != 0)
                Assert.Fail("Wrong Value");

            // The rejection is counted against the channel...
            if (ca.BadCount != 1)
                Assert.Fail("Wrong Value");
        }

        //  Test_1_2_5  A binary payload routes to a binary-kind adapter; a binary payload on a json-kind channel is refused with -2.
        [TestMethod]
        public void Test_1_2_5()
        {
            var d = new ChannelDispatcher(1);
            var host = new FakeHost();

            var bin = new RecordingAdapter("binchan", eChannelKind.Binary);
            var jsn = new RecordingAdapter("jsonchan", eChannelKind.Json);
            d.Add(bin);
            d.Add(jsn);

            var payload = new byte[] { 1, 2, 3, 4 };

            var res1 = d.DispatchBinary(host, "binchan", "blobtype", payload, "cid9");
            if (res1 != 1)
                Assert.Fail("Wrong return");
            if (bin.BinaryCalls != 1)
                Assert.Fail("Wrong Value");
            if (bin.LastPayload == null || bin.LastPayload.Length != 4)
                Assert.Fail("Wrong Value");

            var res2 = d.DispatchBinary(host, "jsonchan", "blobtype", payload, "");
            if (res2 != -2)
                Assert.Fail("Wrong return");
            if (jsn.BinaryCalls != 0)
                Assert.Fail("Wrong Value");
        }

        //  Test_1_2_6  A handler that throws yields -10, and the dispatcher survives for subsequent dispatches.
        [TestMethod]
        public void Test_1_2_6()
        {
            var d = new ChannelDispatcher(1);
            var host = new FakeHost();

            var ca = new RecordingAdapter("chan1", eChannelKind.Json) { ThrowOnAccept = true };
            d.Add(ca);

            var res1 = d.DispatchJson(host, "chan1", "sometype", "{}", "");
            if (res1 != -10)
                Assert.Fail("Wrong return");

            // The dispatcher must survive the fault, and keep dispatching...
            ca.ThrowOnAccept = false;
            var res2 = d.DispatchJson(host, "chan1", "sometype", "{}", "");
            if (res2 != 1)
                Assert.Fail("Wrong return");
        }

        #endregion


        #region Counter Tests

        //  Test_1_3_1  Receive and bad counters accumulate per delivery result, and ResetCounters clears them.
        [TestMethod]
        public void Test_1_3_1()
        {
            var d = new ChannelDispatcher(1);
            var host = new FakeHost();

            var ca = new RecordingAdapter("chan1", eChannelKind.Json);
            d.Add(ca);

            // Two good deliveries...
            d.DispatchJson(host, "chan1", "t", "{}", "");
            d.DispatchJson(host, "chan1", "t", "{}", "");

            // One failed delivery...
            ca.ResultToReturn = -5;
            d.DispatchJson(host, "chan1", "t", "{}", "");

            if (ca.ReceiveCount != 3)
                Assert.Fail("Wrong Value");
            if (ca.BadCount != 1)
                Assert.Fail("Wrong Value");

            ca.ResetCounters();

            if (ca.ReceiveCount != 0 || ca.BadCount != 0 || ca.SentCount != 0)
                Assert.Fail("Wrong Value");
        }

        #endregion


        #region Thread Safety Tests

        //  Test_1_4_1  Concurrent registration, removal, and dispatch across many threads complete without exception,
        //              and the registry ends in a consistent state.
        [TestMethod]
        public void Test_1_4_1()
        {
            var d = new ChannelDispatcher(1);
            var host = new FakeHost();

            // Pre-register a stable channel that dispatch hammers throughout...
            var stable = new RecordingAdapter("stable", eChannelKind.Json);
            d.Add(stable);

            int iterations = 2000;
            Exception failure = null;

            // One thread dispatches continuously...
            var t1 = new Thread(() =>
            {
                try
                {
                    for (int x = 0; x < iterations; x++)
                        d.DispatchJson(host, "stable", "t", "{}", "");
                }
                catch (Exception e) { failure = e; }
            });

            // A second thread churns registrations of transient channels...
            var t2 = new Thread(() =>
            {
                try
                {
                    for (int x = 0; x < iterations; x++)
                    {
                        var name = "transient" + (x % 10).ToString();
                        var ca = new RecordingAdapter(name, eChannelKind.Json);
                        if (d.Add(ca) == 1)
                            d.Remove(name);
                    }
                }
                catch (Exception e) { failure = e; }
            });

            // A third thread dispatches at the transient channels (hits both found and not-found paths)...
            var t3 = new Thread(() =>
            {
                try
                {
                    for (int x = 0; x < iterations; x++)
                        d.DispatchJson(host, "transient" + (x % 10).ToString(), "t", "{}", "");
                }
                catch (Exception e) { failure = e; }
            });

            t1.Start(); t2.Start(); t3.Start();
            t1.Join(); t2.Join(); t3.Join();

            if (failure != null)
                Assert.Fail($"Concurrent usage threw: {failure.Message}");

            // The stable channel must have received every dispatch...
            if (stable.ReceiveCount != iterations)
                Assert.Fail("Wrong Value");

            // Registry consistency: only the stable channel remains...
            if (!d.Contains("stable"))
                Assert.Fail("Wrong Value");
        }

        #endregion
    }
}
