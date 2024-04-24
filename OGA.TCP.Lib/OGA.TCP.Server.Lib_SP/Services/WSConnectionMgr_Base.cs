using OGA.TCP.Server.Model;
using OGA.TCP.Server.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Server
{
    public class WSConnectionMgr_Base : ConnectionMgr_Abstract
    {
        #region ctor / dtor

        static WSConnectionMgr_Base()
        {
            _classname = nameof(WSConnectionMgr_Base);
        }

        public WSConnectionMgr_Base() : base()
        {
        }

        #endregion
    }
}
