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
    public class cCustom_Serializer_Tests : Testing_HelperBase
    {

        #region Setup

        [TestInitialize]
        override public void Setup()
        {
            base.Setup();


            // Runs before each test. (Optional)
        }

        [TestCleanup]
        override public void TearDown()
        {
            // Runs after each test. (Optional)

            base.TearDown();
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
