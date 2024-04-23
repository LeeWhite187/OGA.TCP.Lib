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
    /// Provides connectivity to a TCP endpoint, creating an easy abstraction for message exchange.
    /// This variation allows injection of network state for testing.
    /// </summary>
    public class TCPClient_v1_InjectibleNetworkState : TCPClient_v1_Abstract, IDisposable
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
        public TCPClient_v1_InjectibleNetworkState(string host, int port, NLog.ILogger logger = null) : base(logger)
        {
            _classname = nameof(TCPClient_v1_InjectibleNetworkState);

            this.tcpconnection_host = host;
            this.tcpconnection_port = port;
        }
        /// <summary>
        /// Constructor requires a logger instance.
        /// </summary>
        public TCPClient_v1_InjectibleNetworkState(NLog.ILogger logger = null) : base(logger)
        {
            _classname = nameof(TCPClient_v1_InjectibleNetworkState);
        }

        #endregion


        #region Connection Management

        /// <summary>
        /// This is a hook, in the Setup Before Connection logic flow, to provide a call point for determining any dynamic connection info, such as host, port, or url.
        /// This is especially used by websocket clients, whose connection url is determined by server load balancing and region.
        /// For a simple TCP socket client connecting to a static, target server, this method will simply return success (1).
        /// NOTE: This method is called each time the client attempts to connect.
        /// </summary>
        /// <returns></returns>
        override protected async Task<int> Get_ConnectionInfo()
        {
            // NOTE: This method is called each time the client attempts to create and connect a new transport connection.
            // This is called each time, in case your code is connecting to a service that provides dynamic connection info.
            // Such is the case for some websocket implementations, in that multiple WS host services may be available.
            //  But, you must ask a central clearinghouse for which one to connect to, and you will be given connection info based on closest service, or load balancing.

            // Include in your override method, the logic necessary to retrieve, lookup, or ask for, the transport's connection info.
            // For a websocket connection, this would be logic that gets a connection URL, like the below example call to Get_ConnectionUrl().
            // Or. If the connection URL is fixed, maybe there is nothing to do, here.
            // Tcp socket connection info works similar, it's just not a url, but a host and port instead.

            //return await this.Get_ConnectionUrl();
            // For a tcp socket, this may be a call to get the host and port of listening server.
            this._connection_string = (this.tcpconnection_host ?? "") + ":" + this.tcpconnection_port.ToString();

            return 1;
        }

        #endregion


        #region Testing Overrides

        /// <summary>
        /// Testing input property for simulating network as available or not.
        /// </summary>
        public bool TESTING_NetworkIsAvailable { get; set; }

        /// <summary>
        /// This override allows injection of network state into the instance for testing purposes.
        /// </summary>
        /// <returns></returns>
        protected override bool IsInternetAvailable()
        {
            return this.TESTING_NetworkIsAvailable;
        }

        #endregion
    }
}
