using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP.Server;
using OGA.TCP.Server.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OGA.TCP_Test_SP
{
    /*  ConnectionMgr_Abstract isolation tests (OI-05 gap closure)

        Exercises the manager base without sockets, via the listener-less TCPConnectionMgr_Base.
        (Socketed manager behavior - accept, registration, purge collection - is covered by
        TCPConnMgr_wListener_Tests.)

        //  Test_6_1_1  A listener-less manager starts up and closes down cleanly, twice.
        //  Test_6_1_2  The new-connection gate: disabled refuses (-10), re-enabled resumes evaluation.
        //  Test_6_1_3  A null connection is refused without effect.
        //  Test_6_1_4  Config floors: purge interval and unregistered TTL floor at 1.
        //  Test_6_1_5  The purges run harmlessly on an empty registry, and counts read zero.
    */

    [DoNotParallelize]
    [TestClass]
    public class ConnMgr_Isolation_Tests : OGA.Testing.Lib.Test_Base_abstract
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


        #region Tests

        //  Test_6_1_1  A listener-less manager starts up and closes down cleanly, twice.
        [TestMethod]
        public void Test_6_1_1()
        {
            var cm = new TCPConnectionMgr_Base();

            var res = cm.Startup();
            if (res != 1)
                Assert.Fail("Startup should succeed for a listener-less manager.");

            // Closedown must be clean, and repeatable...
            cm.CloseDown();
            cm.CloseDown();
        }

        //  Test_6_1_2  The new-connection gate: disabled refuses (-10), re-enabled resumes evaluation.
        [TestMethod]
        public void Test_6_1_2()
        {
            var cm = new TCPConnectionMgr_Base();
            try
            {
                // The allow-gate is evaluated before anything else, so a null argument suffices to probe it...
                cm.DisableNewConnections();
                if (cm.AddConnection(null) != -10)
                    Assert.Fail("A disabled manager should refuse new connections with -10.");

                cm.AllowNewConnections();
                if (cm.AddConnection(null) != 0)
                    Assert.Fail("A re-enabled manager should resume evaluating connections (null yields 0).");
            }
            finally
            {
                try { cm.CloseDown(); } catch (Exception) { }
            }
        }

        //  Test_6_1_3  A null connection is refused without effect.
        [TestMethod]
        public void Test_6_1_3()
        {
            var cm = new TCPConnectionMgr_Base();
            try
            {
                if (cm.AddConnection(null) != 0)
                    Assert.Fail("A null connection should be refused with 0.");

                if (cm.ConnectionCount != 0 || cm.UnRegisteredConnectionCount != 0)
                    Assert.Fail("A refused connection should not affect the registry.");
            }
            finally
            {
                try { cm.CloseDown(); } catch (Exception) { }
            }
        }

        //  Test_6_1_4  Config floors: purge interval and unregistered TTL floor at 1.
        [TestMethod]
        public void Test_6_1_4()
        {
            var cm = new TCPConnectionMgr_Base();
            try
            {
                cm.Cfg_ConnectionPurgeInterval = 0;
                if (cm.Cfg_ConnectionPurgeInterval != 1)
                    Assert.Fail("Purge interval should floor at 1.");

                cm.Cfg_ConnectionPurgeInterval = 45;
                if (cm.Cfg_ConnectionPurgeInterval != 45)
                    Assert.Fail("In-range purge interval should be accepted.");

                cm.Cfg_UnregisteredConnectionTTL = 0;
                if (cm.Cfg_UnregisteredConnectionTTL != 1)
                    Assert.Fail("Unregistered TTL should floor at 1.");

                cm.Cfg_UnregisteredConnectionTTL = 90;
                if (cm.Cfg_UnregisteredConnectionTTL != 90)
                    Assert.Fail("In-range unregistered TTL should be accepted.");
            }
            finally
            {
                try { cm.CloseDown(); } catch (Exception) { }
            }
        }

        //  Test_6_1_5  The purges run harmlessly on an empty registry, and counts read zero.
        [TestMethod]
        public void Test_6_1_5()
        {
            var cm = new TCPConnectionMgr_Base();
            try
            {
                cm.Purge_OldUnregisteredConnections();
                cm.Purge_LostConnections();

                if (cm.ConnectionCount != 0 || cm.UnRegisteredConnectionCount != 0)
                    Assert.Fail("An empty manager should report zero connections.");
            }
            finally
            {
                try { cm.CloseDown(); } catch (Exception) { }
            }
        }

        #endregion
    }
}
