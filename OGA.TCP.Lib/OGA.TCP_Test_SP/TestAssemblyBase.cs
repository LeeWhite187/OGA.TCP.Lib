using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP_Test_SP
{
    [TestClass]
    public abstract class TestAssemblyBase : OGA.Testing.Lib.TestAssembly_Base
    {
        #region Test Assembly Setup / Teardown

        /// <summary>
        /// This initializer calls the base assembly initializer.
        /// </summary>
        /// <param name="context"></param>
        [AssemblyInitialize]
        static public void TestAssembly_Initialize(TestContext context)
        {
            TestAssemblyBase_Initialize(context);
        }

        /// <summary>
        /// This cleanup method calls the base assembly cleanup.
        /// </summary>
        [AssemblyCleanup]
        static public void TestAssembly_Cleanup()
        {
            TestAssemblyBase_Cleanup();
        }

        #endregion
    }
}
