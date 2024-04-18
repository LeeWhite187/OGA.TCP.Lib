using OGA.TCP.Server.Model;
using OGA.TCP.Server.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Server
{
    public class TCPConnectionMgr_Base : ConnectionMgr_Abstract
    {
        #region ctor / dtor

        static TCPConnectionMgr_Base()
        {
            _classname = nameof(TCPConnectionMgr_Base);
        }

        public TCPConnectionMgr_Base() : base()
        {
        }

        #endregion
    }
}
