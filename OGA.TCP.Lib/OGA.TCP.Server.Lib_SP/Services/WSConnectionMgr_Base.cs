using OGA.TCP.Server.Model;
using OGA.TCP.Server.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Server
{
    /// <summary>
    /// WebSocket-flavored listener-less connection manager: connections are accepted by an external HTTP
    ///     request handler and handed in via AddConnection.
    /// NOTE: Part of the WebSocket file set excluded from this library's scope (OI-01) — retained only so
    ///     the projects compile until the owner's backport to OGA.WSClient_Base removes it.
    /// </summary>
    public class WSConnectionMgr_Base : ConnectionMgr_Abstract
    {
        #region ctor / dtor

        static WSConnectionMgr_Base()
        {
            _classname = nameof(WSConnectionMgr_Base);
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public WSConnectionMgr_Base() : base()
        {
        }

        #endregion
    }
}
