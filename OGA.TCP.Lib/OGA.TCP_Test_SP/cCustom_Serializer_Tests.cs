using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.TCP.Shared.Encoding;

namespace OGA.TCP_Test_SP
{
    [DoNotParallelize]
    [TestClass]
    public class cCustom_Serializer_Tests : OGA.Testing.Lib.Test_Base_abstract
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
        }

        /// <summary>
        /// Called after each test runs.
        /// Be sure this method exists in your top-level test class, and that it calls the corresponding test cleanup method of the base.
        /// </summary>
        [TestCleanup]
        override public void TearDown()
        {
            // Runs after each test. (Optional)
        }

        #endregion


        #region Tests


        //  Test_1_1_1  Test that we can serialize a string array.
        [TestMethod]
        public async Task Test_1_1_1()
        {
            var string1 = Guid.NewGuid().ToString();
            var string2 = Guid.NewGuid().ToString();
            var string3 = Guid.NewGuid().ToString();
            string[] stringarray = new string[] { string1, string2, string3 };


            int expectedsize =
                // Add the string array as the member count value and the string length and string data of each member.
                cCustom_Serializer.size_of_Int32;

            // Add the length of each element in the array.
            foreach(var s in stringarray)
            {
                expectedsize += cCustom_Serializer.size_of_Int32 + s.Length;
            }

            // Create an array of the needed size...
            byte[] bytes = new byte[expectedsize];

            // Serialize it...
            var res = cCustom_Serializer.Serialize_StringArray(ref stringarray, ref bytes, 0);

            // Now, call the deserialize method into a new string array...
            var res2 = cCustom_Serializer.DeSerialize_StringArray(ref bytes, 0, out var stringarray2);




            if (false == true)
                Assert.Fail("Wrong value");
        }

        #endregion
    }
}
