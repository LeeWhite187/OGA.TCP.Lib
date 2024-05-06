using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WSEndpoint_Tests.HelperClasses
{
    /// <summary>
    /// Copied from OGA_WSHost_Base.Dev/WSEndpoint_Tests.
    /// </summary>
    public class clientproperties
    {
        public int WSLibVersion { get; set; }
        public string AppId { get; set; }
        public string AppVersion { get; set; }
        public string Language { get; set; }

        public Guid? UserId { get; set; }
        public string DeviceId { get; set; }
        public string ConnectionId { get; set; }
        public string RuntimeId { get; set; }
        public int Pid { get; set; }


        public clientproperties()
        {

        }

        static public clientproperties Create_Random_WSLibV1_ClientData()
        {
            var v = new clientproperties();
            v.WSLibVersion = 1;
            v.AppVersion = "";
            v.AppId = "";
            v.Language = "";
            v.UserId = Guid.NewGuid();
            v.DeviceId = "did-" + Guid.NewGuid().ToString();
            v.ConnectionId = "cid-" + Guid.NewGuid().ToString();
            v.RuntimeId = "rid-" + Guid.NewGuid().ToString();
            v.Pid = OGA.Testing.Helpers.RandomValueGenerators.CreateRandomInt();

            return v;
        }

        static public clientproperties Create_Random_WSLibV2_ClientData()
        {
            var v = new clientproperties();
            v.WSLibVersion = 2;
            v.AppVersion = "aver-" + Guid.NewGuid().ToString();
            v.AppId = "aid-" + Guid.NewGuid().ToString();
            v.Language = "lang-" + Guid.NewGuid().ToString();
            v.UserId = Guid.NewGuid();
            v.DeviceId = "did-" + Guid.NewGuid().ToString();
            v.ConnectionId = "cid-" + Guid.NewGuid().ToString();
            v.RuntimeId = "rid-" + Guid.NewGuid().ToString();
            v.Pid = OGA.Testing.Helpers.RandomValueGenerators.CreateRandomInt();

            return v;
        }

        public void CopyFrom(clientproperties crd)
        {
            this.WSLibVersion = crd.WSLibVersion;
            this.AppVersion = crd.AppVersion;
            this.AppId = crd.AppId;
            this.Language = crd.Language;
            this.UserId = crd.UserId;
            this.DeviceId = crd.DeviceId;
            this.ConnectionId = crd.ConnectionId;
            this.RuntimeId = crd.RuntimeId;
            this.Pid = crd.Pid;
        }
    }
}
