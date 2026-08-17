using Newtonsoft.Json;
using NLog;
using OGA.TCP.Messages;
using OGA.TCP.Shared;
using OGA.TCP.Shared.Encoding;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP.SessionLayer
{
    /// <summary>
    /// This is a minimal TCP client implementation.
    /// It includes both means to set the host/port.
    /// And, it uses the base class's send method.
    /// You can use this, directly, as a minimal TCP client.
    /// Or, inherit from it or TCPClient_v1_Abstract, to compose a richer client surface,
    ///     with maybe domain-specific send methods and auto-registering channel adapters.
    /// </summary>
    public class TCPClient_v1_Impl : TCPClient_v1_Abstract, IDisposable
    {
        #region Public Properties

        /// <summary>
        /// Hostname or IP of remote connection.
        /// </summary>
        public string ConnectionHost
        {
            get => this.tcpconnection_host;
            set
            {
                this.tcpconnection_host = value ?? "";
            }
        }

        /// <summary>
        /// Port of remote connection.
        /// </summary>
        public int ConnectionPort
        {
            get => this.tcpconnection_port;
            set
            {
                this.tcpconnection_port = value;
            }
        }

        #endregion


        #region ctor / dtor

        /// <summary>
        /// Accepts remote host, port, and logger instance.
        /// </summary>
        public TCPClient_v1_Impl(string host, int port, NLog.ILogger logger = null) : base(logger)
        {
            _classname = nameof(TCPClient_v1_Impl);

            this.tcpconnection_host = host;
            this.tcpconnection_port = port;
        }
        /// <summary>
        /// Constructor requires a logger instance.
        /// If you use this constructor, you must set the host/port properties.
        /// </summary>
        public TCPClient_v1_Impl(NLog.ILogger logger = null) : base(logger)
        {
            _classname = nameof(TCPClient_v1_Impl);
        }

        #endregion


        #region Connection Management

        #endregion
    }
}
