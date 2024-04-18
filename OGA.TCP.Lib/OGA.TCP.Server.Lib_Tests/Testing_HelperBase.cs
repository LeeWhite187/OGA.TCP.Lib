using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OGA.Testing.Lib;

namespace OGA.TCP_Test_SP
{
    public class Testing_HelperBase : Test_Base
    {
        static public bool Delete_Test_Data_from_Disk = true;
        static private string testfolderpath = "";

        [ClassInitialize]
        static public void Setup_Class_Base(TestContext context)
        {
            Create_Test_Folder();
        }
        [ClassCleanup]
        static public void Teardown_Class()
        {
            if (Delete_Test_Data_from_Disk)
            {
                // Delete the test folder in which testing was performed for this class.
                try
                {
                    System.IO.Directory.Delete(testfolderpath);
                }
                catch (Exception e)
                {

                }
            }
        }

        [TestInitialize()]
        override public void Setup()
        {
            base.Setup();

            // Make the runtime key accessible for framework things...
            OGA.SharedKernel.Process.App_Data_v2.Runtime_Enc_Key = RuntimeEncryptionKey;
                
            // Runs before each test. (Optional)
        }

        [TestCleanup]
        override public void TearDown()
        {
            // Runs after each test. (Optional)

            try
            {
            }
            catch(Exception ex)
            {
                int x = 0;
            }

            base.TearDown();
        }

        #region Private Methods

        static public void Create_Test_Folder()
        {
            testfolderpath = System.IO.Path.GetTempPath() + System.Guid.NewGuid().ToString();

            try
            {
                System.IO.Directory.CreateDirectory(testfolderpath);
            }
            catch (Exception e)
            {
                return;
            }
        }

        #endregion
    }
}
