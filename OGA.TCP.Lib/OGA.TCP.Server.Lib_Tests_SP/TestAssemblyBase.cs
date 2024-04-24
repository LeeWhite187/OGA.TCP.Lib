using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP_Test_SP
{
    [TestClass]
    public abstract class TestAssemblyBase
    {
        [AssemblyInitializeAttribute]
        public static void Initialize(TestContext context)
        {
            int x = 0;
            // put your initialize code here
        }
    }
}
