using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP;
using OGA.TCP.Shared;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP_Test_SP
{
    /*  Shared-component unit tests (OI-05 gap closure)

        // ExpBackoff_wJitter...
        //  Test_5_1_1  The delay progression grows from the floor and plateaus at the ceiling (AtMax).
        //  Test_5_1_2  A zero-width range (floor == ceiling) is a constant delay, at max immediately.
        //  Test_5_1_3  Reset returns the progression to its floor.
        //  Test_5_1_4  JitterHeight clamps out-of-range values to the nearest bound (OI-37 regression).
        //  Test_5_1_5  A cancelled token makes Delay and DelayAsync return 0 without throwing (OI-37 regression).
        //  Test_5_1_6  With jitter enabled, delays stay within the configured envelope.

        // cBuffer...
        //  Test_5_2_1  First resize allocates; Length reports the allocation.
        //  Test_5_2_2  A growing resize preserves existing content; a smaller request does not shrink.
        //  Test_5_2_3  Dispose releases the buffer; a later resize re-allocates.

        // cEndpoint_Metrics...
        //  Test_5_3_1  Construction baselines counters to zero and timestamps to sentinels.
        //  Test_5_3_2  CopyFrom copies every counter and timestamp.
        //  Test_5_3_3  Reset_Metrics returns to the constructed baseline.
    */

    [DoNotParallelize]
    [TestClass]
    public class SharedComponents_Tests : OGA.Testing.Lib.Test_Base_abstract
    {
        #region Setup

        [ClassInitialize]
        static public void TestClass_Setup(TestContext context)
        {
            TestClassBase_Setup(context);
        }

        [ClassCleanup]
        static public void TestClass_Cleanup()
        {
            TestClassBase_Cleanup();
        }

        [TestInitialize]
        override public void Setup()
        {
            base.Setup();
        }

        [TestCleanup]
        override public void TearDown()
        {
            base.TearDown();
        }

        #endregion


        #region ExpBackoff_wJitter Tests

        //  Test_5_1_1  The delay progression grows from the floor and plateaus at the ceiling (AtMax).
        [TestMethod]
        public void Test_5_1_1()
        {
            var eb = new ExpBackoff_wJitter(0, 100, 2000);
            eb.EnableJitter = false;

            if (eb.AtMax)
                Assert.Fail("Wrong Value");

            // Walk the progression; it must be non-decreasing and reach the ceiling...
            int last = 0;
            int max = 0;
            for (int i = 0; i < 12; i++)
            {
                var d = eb.CalculateDelay();
                if (d < last && !eb.AtMax)
                    Assert.Fail("Delay progression decreased before reaching max.");
                if (d > 2000)
                    Assert.Fail("Delay exceeded the configured ceiling.");
                last = d;
                if (d > max) max = d;
            }

            if (!eb.AtMax)
                Assert.Fail("Progression should have reached max.");
            if (max != 2000)
                Assert.Fail("Progression never produced the ceiling delay.");
        }

        //  Test_5_1_2  A zero-width range (floor == ceiling) is a constant delay, at max immediately.
        [TestMethod]
        public void Test_5_1_2()
        {
            var eb = new ExpBackoff_wJitter(0, 500, 500);
            eb.EnableJitter = false;

            var d1 = eb.CalculateDelay();
            var d2 = eb.CalculateDelay();

            if (d1 != 500 || d2 != 500)
                Assert.Fail("Constant-range delay should always be the configured value.");
            if (!eb.AtMax)
                Assert.Fail("Constant-range delay should report at-max.");
        }

        //  Test_5_1_3  Reset returns the progression to its floor.
        [TestMethod]
        public void Test_5_1_3()
        {
            var eb = new ExpBackoff_wJitter(0, 100, 2000);
            eb.EnableJitter = false;

            // Run to the ceiling...
            for (int i = 0; i < 12; i++)
                eb.CalculateDelay();
            if (!eb.AtMax)
                Assert.Fail("Progression should have reached max.");

            // Reset, and the next delay must be small again...
            eb.Reset();
            if (eb.AtMax)
                Assert.Fail("Reset should clear the at-max state.");

            var d = eb.CalculateDelay();
            if (d >= 2000)
                Assert.Fail("Reset should return the progression to its floor.");
        }

        //  Test_5_1_4  JitterHeight clamps out-of-range values to the nearest bound (OI-37 regression).
        [TestMethod]
        public void Test_5_1_4()
        {
            var eb = new ExpBackoff_wJitter(0, 100, 2000);

            eb.JitterHeight = -0.5f;
            if (eb.JitterHeight != 0.0f)
                Assert.Fail("Negative jitter should clamp to 0.");

            // The historical defect clamped over-1.0 to 0, silently disabling jitter for a caller
            //  asking for maximum...
            eb.JitterHeight = 2.0f;
            if (eb.JitterHeight != 1.0f)
                Assert.Fail("Over-range jitter should clamp to 1.0.");

            eb.JitterHeight = 0.4f;
            if (eb.JitterHeight != 0.4f)
                Assert.Fail("In-range jitter should be accepted as-is.");
        }

        //  Test_5_1_5  A cancelled token makes Delay and DelayAsync return 0 without throwing (OI-37 regression).
        [TestMethod]
        public async Task Test_5_1_5()
        {
            var eb = new ExpBackoff_wJitter(0, 500, 5000);
            eb.EnableJitter = false;

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // The async variant must report the cancellation, not throw and not claim success...
                var resasync = await eb.DelayAsync(cts.Token);
                if (resasync != 0)
                    Assert.Fail("DelayAsync should return 0 for a cancelled token.");

                // The blocking variant likewise...
                var ressync = eb.Delay(cts.Token);
                if (ressync != 0)
                    Assert.Fail("Delay should return 0 for a cancelled token.");
            }
        }

        //  Test_5_1_6  With jitter enabled, delays stay within the configured envelope.
        [TestMethod]
        public void Test_5_1_6()
        {
            var eb = new ExpBackoff_wJitter(0, 1000, 1000);
            eb.EnableJitter = true;
            eb.JitterHeight = 0.5f;

            // Constant 1000ms base with a 0.5 jitter envelope centered on it: every calculated delay
            //  must land within [1000 - 250, 1000 + 250] (the centered envelope), never negative...
            for (int i = 0; i < 50; i++)
            {
                var d = eb.CalculateDelay();
                if (d < 750 || d > 1250)
                    Assert.Fail($"Jittered delay ({d.ToString()}) escaped the configured envelope.");
            }
        }

        #endregion


        #region cBuffer Tests

        //  Test_5_2_1  First resize allocates; Length reports the allocation.
        [TestMethod]
        public void Test_5_2_1()
        {
            var b = new cBuffer();

            if (b.Buffer != null)
                Assert.Fail("Buffer should be unallocated before first use.");

            b.Resize_Buffer_if_Needed(128);

            if (b.Buffer == null || b.Length != 128)
                Assert.Fail("First resize should allocate the requested size.");
        }

        //  Test_5_2_2  A growing resize preserves existing content; a smaller request does not shrink.
        [TestMethod]
        public void Test_5_2_2()
        {
            var b = new cBuffer();
            b.Resize_Buffer_if_Needed(4);

            // Stamp recognizable content...
            b.Buffer[0] = 0x11;
            b.Buffer[1] = 0x22;
            b.Buffer[2] = 0x33;
            b.Buffer[3] = 0x44;

            // Grow, and the prefix must survive...
            b.Resize_Buffer_if_Needed(64);
            if (b.Length != 64)
                Assert.Fail("Buffer should have grown.");
            if (b.Buffer[0] != 0x11 || b.Buffer[3] != 0x44)
                Assert.Fail("Growing resize should preserve existing content.");

            // A smaller request must not shrink (grow-only contract)...
            b.Resize_Buffer_if_Needed(8);
            if (b.Length != 64)
                Assert.Fail("Buffer should not shrink on a smaller request.");
        }

        //  Test_5_2_3  Dispose releases the buffer; a later resize re-allocates.
        [TestMethod]
        public void Test_5_2_3()
        {
            var b = new cBuffer();
            b.Resize_Buffer_if_Needed(16);

            b.Dispose();
            if (b.Buffer != null)
                Assert.Fail("Dispose should release the backing array.");

            // The instance remains usable; the next resize re-allocates...
            b.Resize_Buffer_if_Needed(32);
            if (b.Buffer == null || b.Length != 32)
                Assert.Fail("Resize after dispose should re-allocate.");
        }

        #endregion


        #region cEndpoint_Metrics Tests

        //  Test_5_3_1  Construction baselines counters to zero and timestamps to sentinels.
        [TestMethod]
        public void Test_5_3_1()
        {
            var m = new cEndpoint_Metrics();

            if (m.Received_Message_Count != 0 || m.Sent_Message_Count != 0 ||
                m.Unknown_MessageType_Count != 0 || m.Unknown_ReplyType_Count != 0 ||
                m.Loop_Counter != 0 || m.Loop_Duration != 0)
                Assert.Fail("Counters should baseline to zero.");

            if (m.Last_Received_Message_Time.Year != 1900 || m.Last_Sent_Message_Time.Year != 1900)
                Assert.Fail("Timestamps should baseline to the sentinel value.");
        }

        //  Test_5_3_2  CopyFrom copies every counter and timestamp.
        [TestMethod]
        public void Test_5_3_2()
        {
            var src = new cEndpoint_Metrics();
            src.Loop_Counter = 7;
            src.Loop_Duration = 42;
            src.Loop_EntryTime = new DateTime(2026, 8, 1, 1, 2, 3, DateTimeKind.Utc);
            src.Unknown_MessageType_Count = 1;
            src.Unknown_ReplyType_Count = 2;
            src.Last_Unknown_MessageType_Time = new DateTime(2026, 8, 2);
            src.Last_Unknown_ReplyType_Time = new DateTime(2026, 8, 3);
            src.Received_Message_Count = 100;
            src.Sent_Message_Count = 200;
            src.Last_Received_Message_Time = new DateTime(2026, 8, 4);
            src.Last_Sent_Message_Time = new DateTime(2026, 8, 5);

            var dst = new cEndpoint_Metrics();
            dst.CopyFrom(src);

            if (dst.Loop_Counter != 7 || dst.Loop_Duration != 42 ||
                dst.Loop_EntryTime != src.Loop_EntryTime ||
                dst.Unknown_MessageType_Count != 1 || dst.Unknown_ReplyType_Count != 2 ||
                dst.Last_Unknown_MessageType_Time != src.Last_Unknown_MessageType_Time ||
                dst.Last_Unknown_ReplyType_Time != src.Last_Unknown_ReplyType_Time ||
                dst.Received_Message_Count != 100 || dst.Sent_Message_Count != 200 ||
                dst.Last_Received_Message_Time != src.Last_Received_Message_Time ||
                dst.Last_Sent_Message_Time != src.Last_Sent_Message_Time)
                Assert.Fail("CopyFrom should copy every field.");
        }

        //  Test_5_3_3  Reset_Metrics returns to the constructed baseline.
        [TestMethod]
        public void Test_5_3_3()
        {
            var m = new cEndpoint_Metrics();
            m.Received_Message_Count = 5;
            m.Sent_Message_Count = 6;
            m.Last_Received_Message_Time = DateTime.UtcNow;

            m.Reset_Metrics();

            if (m.Received_Message_Count != 0 || m.Sent_Message_Count != 0)
                Assert.Fail("Reset should zero the counters.");
            if (m.Last_Received_Message_Time.Year != 1900)
                Assert.Fail("Reset should restore the sentinel timestamps.");
        }

        #endregion
    }
}
