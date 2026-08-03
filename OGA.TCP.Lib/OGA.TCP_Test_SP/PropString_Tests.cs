using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OGA.TCP_Test_SP
{
    /*  PropString Tests

        Verifies the registration prop-string composer/parser (OI-29):
        //  Test_4_1_1  Compose output for plain values is byte-identical to the historical hand-concatenation.
        //  Test_4_1_2  Compose escapes quotes and backslashes in values.
        //  Test_4_1_3  Compose treats a null value as an empty string value.
        //  Test_4_2_1  TryParse recovers plain key/value fragments.
        //  Test_4_2_2  TryParse preserves colons inside values.
        //  Test_4_2_3  TryParse honors escaped quotes and backslashes in values.
        //  Test_4_2_4  TryParse handles empty values.
        //  Test_4_2_5  TryParse falls back on malformed (non-json) fragments without value truncation.
        //  Test_4_2_6  TryParse rejects null/empty/keyless fragments.
        //  Test_4_2_7  TryParse tolerates a nonstandard unquoted value token.
        //  Test_4_3_1  Compose-then-TryParse roundtrips arbitrary values intact.
    */

    [DoNotParallelize]
    [TestClass]
    public class PropString_Tests : OGA.Testing.Lib.Test_Base_abstract
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


        #region Compose Tests

        //  Test_4_1_1  Compose output for plain values is byte-identical to the historical hand-concatenation.
        [TestMethod]
        public void Test_4_1_1()
        {
            // The historical composers hand-built fragments like: "runtimeid":"rid-abc"
            // Wire compatibility requires Compose to emit exactly that for plain values...
            if (PropString.Compose("runtimeid", "rid-abc") != "\"runtimeid\":\"rid-abc\"")
                Assert.Fail("Wrong Value");
            if (PropString.Compose("keepalive", "on") != "\"keepalive\":\"on\"")
                Assert.Fail("Wrong Value");
            if (PropString.Compose("pid", "12345") != "\"pid\":\"12345\"")
                Assert.Fail("Wrong Value");
            if (PropString.Compose("tcplibver", "3") != "\"tcplibver\":\"3\"")
                Assert.Fail("Wrong Value");
        }

        //  Test_4_1_2  Compose escapes quotes and backslashes in values.
        [TestMethod]
        public void Test_4_1_2()
        {
            // A value containing a quote must produce a well-formed fragment...
            var frag = PropString.Compose("runtimeid", "va\"lue");
            if (frag != "\"runtimeid\":\"va\\\"lue\"")
                Assert.Fail("Wrong Value");

            // A value containing a backslash must produce a well-formed fragment...
            var frag2 = PropString.Compose("runtimeid", "va\\lue");
            if (frag2 != "\"runtimeid\":\"va\\\\lue\"")
                Assert.Fail("Wrong Value");
        }

        //  Test_4_1_3  Compose treats a null value as an empty string value.
        [TestMethod]
        public void Test_4_1_3()
        {
            if (PropString.Compose("appid", null) != "\"appid\":\"\"")
                Assert.Fail("Wrong Value");
        }

        #endregion


        #region TryParse Tests

        //  Test_4_2_1  TryParse recovers plain key/value fragments.
        [TestMethod]
        public void Test_4_2_1()
        {
            string key;
            string val;
            if (!PropString.TryParse("\"runtimeid\":\"rid-abc\"", out key, out val))
                Assert.Fail("Wrong return");
            if (key != "runtimeid")
                Assert.Fail("Wrong Value");
            if (val != "rid-abc")
                Assert.Fail("Wrong Value");
        }

        //  Test_4_2_2  TryParse preserves colons inside values.
        [TestMethod]
        public void Test_4_2_2()
        {
            // The historical parser split on every colon and truncated this value to "node"...
            string key;
            string val;
            if (!PropString.TryParse("\"runtimeid\":\"node:4222\"", out key, out val))
                Assert.Fail("Wrong return");
            if (key != "runtimeid")
                Assert.Fail("Wrong Value");
            if (val != "node:4222")
                Assert.Fail("Wrong Value");
        }

        //  Test_4_2_3  TryParse honors escaped quotes and backslashes in values.
        [TestMethod]
        public void Test_4_2_3()
        {
            string key;
            string val;
            if (!PropString.TryParse("\"runtimeid\":\"va\\\"lue\"", out key, out val))
                Assert.Fail("Wrong return");
            if (val != "va\"lue")
                Assert.Fail("Wrong Value");

            if (!PropString.TryParse("\"runtimeid\":\"va\\\\lue\"", out key, out val))
                Assert.Fail("Wrong return");
            if (val != "va\\lue")
                Assert.Fail("Wrong Value");
        }

        //  Test_4_2_4  TryParse handles empty values.
        [TestMethod]
        public void Test_4_2_4()
        {
            string key;
            string val;
            if (!PropString.TryParse("\"appid\":\"\"", out key, out val))
                Assert.Fail("Wrong return");
            if (key != "appid")
                Assert.Fail("Wrong Value");
            if (val != "")
                Assert.Fail("Wrong Value");
        }

        //  Test_4_2_5  TryParse falls back on malformed (non-json) fragments without value truncation.
        [TestMethod]
        public void Test_4_2_5()
        {
            // A hand-built fragment with an unescaped quote inside the value is not valid json.
            // The fallback splits on the FIRST colon only, so a colon later in the value survives
            //  (the historical parser would have truncated it)...
            string key;
            string val;
            if (!PropString.TryParse("keepalive:on", out key, out val))
                Assert.Fail("Wrong return");
            if (key != "keepalive")
                Assert.Fail("Wrong Value");
            if (val != "on")
                Assert.Fail("Wrong Value");

            if (!PropString.TryParse("\"runtimeid\":\"node:4222", out key, out val))
                Assert.Fail("Wrong return");
            if (key != "runtimeid")
                Assert.Fail("Wrong Value");
            if (val != "node:4222")
                Assert.Fail("Wrong Value");
        }

        //  Test_4_2_6  TryParse rejects null/empty/keyless fragments.
        [TestMethod]
        public void Test_4_2_6()
        {
            string key;
            string val;
            if (PropString.TryParse(null, out key, out val))
                Assert.Fail("Wrong return");
            if (PropString.TryParse("", out key, out val))
                Assert.Fail("Wrong return");
            if (PropString.TryParse("   ", out key, out val))
                Assert.Fail("Wrong return");
            if (PropString.TryParse("\"\":\"value\"", out key, out val))
                Assert.Fail("Wrong return");
        }

        //  Test_4_2_7  TryParse tolerates a nonstandard unquoted value token.
        [TestMethod]
        public void Test_4_2_7()
        {
            // A nonstandard sender might emit an unquoted number...
            string key;
            string val;
            if (!PropString.TryParse("\"pid\":12345", out key, out val))
                Assert.Fail("Wrong return");
            if (key != "pid")
                Assert.Fail("Wrong Value");
            if (val != "12345")
                Assert.Fail("Wrong Value");
        }

        #endregion


        #region Roundtrip Tests

        //  Test_4_3_1  Compose-then-TryParse roundtrips arbitrary values intact.
        [TestMethod]
        public void Test_4_3_1()
        {
            var values = new List<string>()
            {
                "plain",
                "node:4222",
                "with \"quotes\" inside",
                "back\\slash",
                "colons:and:\"quotes\":mixed",
                "",
                "unicode 😀 中文",
            };

            foreach (var v in values)
            {
                var frag = PropString.Compose("runtimeid", v);

                string key;
                string val;
                if (!PropString.TryParse(frag, out key, out val))
                    Assert.Fail($"Wrong return for value ({v})");
                if (key != "runtimeid")
                    Assert.Fail($"Wrong key for value ({v})");
                if (!string.Equals(val, v, StringComparison.Ordinal))
                    Assert.Fail($"Roundtrip mismatch for value ({v})");
            }
        }

        #endregion
    }
}
