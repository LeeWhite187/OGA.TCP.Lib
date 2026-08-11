using OGA.TCP.Server.Model;
using OGA.TCP.Server.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Server
{
    /// <summary>
    /// Listener-less TCP connection manager: the base registry and lifecycle machinery for deployments
    ///     where connections are accepted externally and handed in via AddConnection.
    /// Use TCPConnMgr_wListener for the integrated-listener arrangement.
    /// </summary>
    public class TCPConnectionMgr_Base : ConnectionMgr_Abstract
    {
        #region ctor / dtor

        static TCPConnectionMgr_Base()
        {
            _classname = nameof(TCPConnectionMgr_Base);
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TCPConnectionMgr_Base() : base()
        {
        }

        #endregion
    }
}
