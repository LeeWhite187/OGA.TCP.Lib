using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP.Server.Lib_Tests.Helpers
{
    /// <summary>
    /// Small class that is used to initiate a TCP connection with a remote listener,
    ///     and setup the TcpCLient instance for handoff to an endpoint.
    /// This object is considered a client-analog to the server's listener, which hands off a tcp connection to an endpoint.
    /// </summary>
    public class cClient_Helper
    {
        Thread receiveThread;

        public TcpClient client;

        // TcpClient.Connected doesn't check if socket != null, which
        // results in NullReferenceExceptions if connection was closed.
        // -> let's check it manually instead
        public bool Connected => client != null &&
                                 client.Client != null &&
                                 client.Client.Connected;

        // TcpClient has no 'connecting' state to check. We need to keep track
        // of it manually.
        // -> checking 'thread.IsAlive && !Connected' is not enough because the
        //    thread is alive and connected is false for a short moment after
        //    disconnecting, so this would cause race conditions.
        // -> we use a threadsafe bool wrapper so that ThreadFunction can remain
        //    static (it needs a common lock)
        // => Connecting is true from first Connect() call in here, through the
        //    thread start, until TcpClient.Connect() returns. Simple and clear.
        // => bools are atomic according to
        //    https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/variables
        //    made volatile so the compiler does not reorder access to it
        volatile bool _Connecting;
        public bool Connecting => _Connecting;

        // NoDelay disables nagle algorithm. lowers CPU% and latency but
        // increases bandwidth
        public bool NoDelay = true;

        // Send would stall forever if the network is cut off during a send, so
        // we need a timeout (in milliseconds)
        public int SendTimeout = 5000;


        #region Delegates and Handlers

        public delegate void dNew_Client_Connection(cClient_Helper ch, System.Net.Sockets.TcpClient newclient);
        private dNew_Client_Connection _del_new_client_connection;
        /// <summary>
        /// Assign a handler to this delegate if the listener does not create sessions for each connecting client.
        /// </summary>
        public dNew_Client_Connection OnNew_Client_Connection
        {
            set
            {
                this._del_new_client_connection = value;
            }
        }

        public delegate void dConnection_Failed();
        private dConnection_Failed _del_Connection_Failed;
        /// <summary>
        /// Assign a handler to this delegate to receive connection failed status.
        /// </summary>
        public dConnection_Failed OnConnection_Failed
        {
            set
            {
                this._del_Connection_Failed = value;
            }
        }

        #endregion


        /// <summary>
        /// Use this connect method to attempt a synchronous connection.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public int Connect_Blocking(string addr, int port)
        {
            // not if already started
            if (Connecting || Connected)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                        "Can not create connection because an existing connection is connecting or connected.");
                return 0;
            }

            // We are connecting from now until Connect succeeds or fails
            _Connecting = true;

            // create a TcpClient with perfect IPv4, IPv6 and hostname resolving
            // support.
            //
            // * TcpClient(hostname, port): works but would connect (and block)
            //   already
            // * TcpClient(AddressFamily.InterNetworkV6): takes Ipv4 and IPv6
            //   addresses but only connects to IPv6 servers (e.g. Telepathy).
            //   does NOT connect to IPv4 servers (e.g. Mirror Booster), even
            //   with DualMode enabled.
            // * TcpClient(): creates IPv4 socket internally, which would force
            //   Connect() to only use IPv4 sockets.
            //
            // => the trick is to clear the internal IPv4 socket so that Connect
            //    resolves the hostname and creates either an IPv4 or an IPv6
            //    socket as needed (see TcpClient source)
            client = new TcpClient(); // creates IPv4 socket
            client.Client = null; // clear internal IPv4 socket until Connect()

            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // connect (blocking)
                client.Connect(addr, port);
                _Connecting = false;

                // set socket options after the socket was created in Connect()
                // (not after the constructor because we clear the socket there)
                client.NoDelay = NoDelay;
                client.SendTimeout = SendTimeout;

                // Notify the owner that a connection was made.
                return 1;
            }
            catch (SocketException exception)
            {
                // This happens if (for example) the ip address is correct
                // but there is no server running on that ip/port

                // Connect might have failed. thread might have been closed.
                // let's reset connecting state no matter what.
                _Connecting = false;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(exception,
                        "Client Recv: failed to connect to ip=" + addr + " port=" + port.ToString());

                // Notify the owner that the connection failed.
                return -1;
            }
            catch (Exception exception)
            {
                // Connect might have failed. thread might have been closed.
                // let's reset connecting state no matter what.
                _Connecting = false;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(exception,
                        "Client Recv: failed to connect to ip=" + addr + " port=" + port.ToString());

                // Notify the owner that the connection failed.
                return -1;
            }
        }

        /// <summary>
        /// Use tis method to asynchronously connection with a delegate callback.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public int Connect_with_Callback(string addr, int port)
        {
            // not if already started
            if (Connecting || Connected)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                        "Can not create connection because an existing connection is connecting or connected.");
                return 0;
            }

            // We are connecting from now until Connect succeeds or fails
            _Connecting = true;

            // create a TcpClient with perfect IPv4, IPv6 and hostname resolving
            // support.
            //
            // * TcpClient(hostname, port): works but would connect (and block)
            //   already
            // * TcpClient(AddressFamily.InterNetworkV6): takes Ipv4 and IPv6
            //   addresses but only connects to IPv6 servers (e.g. Telepathy).
            //   does NOT connect to IPv4 servers (e.g. Mirror Booster), even
            //   with DualMode enabled.
            // * TcpClient(): creates IPv4 socket internally, which would force
            //   Connect() to only use IPv4 sockets.
            //
            // => the trick is to clear the internal IPv4 socket so that Connect
            //    resolves the hostname and creates either an IPv4 or an IPv6
            //    socket as needed (see TcpClient source)
            client = new TcpClient(); // creates IPv4 socket
            client.Client = null; // clear internal IPv4 socket until Connect()

            // client.Connect(ip, port) is blocking. let's call it in the thread
            // and return immediately.
            // -> this way the application doesn't hang for 30s if connect takes
            //    too long, which is especially good in games
            // -> this way we don't async client.BeginConnect, which seems to
            //    fail sometimes if we connect too many clients too fast
            receiveThread = new Thread(() => { ConnectionHandler_entry(addr, port); });
            receiveThread.IsBackground = true;
            receiveThread.Start();

            return 1;
        }

        // the thread function
        void ConnectionHandler_entry(string addr, int port)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // connect (blocking)
                client.Connect(addr, port);
                _Connecting = false;

                // set socket options after the socket was created in Connect()
                // (not after the constructor because we clear the socket there)
                client.NoDelay = NoDelay;
                client.SendTimeout = SendTimeout;

                // Notify the owner that a connection was made.
                if(_del_new_client_connection != null)
                {
                    _del_new_client_connection(this, this.client);
                }
            }
            catch (SocketException exception)
            {
                // this happens if (for example) the ip address is correct
                // but there is no server running on that ip/port

                // Connect might have failed. thread might have been closed.
                // let's reset connecting state no matter what.
                _Connecting = false;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(exception,
                        "Client Recv: failed to connect to ip=" + addr + " port=" + port.ToString());

                // Notify the owner that the connection failed.
                if(_del_Connection_Failed != null)
                {
                    _del_Connection_Failed();
                }
            }
            catch (Exception exception)
            {
                // Connect might have failed. thread might have been closed.
                // let's reset connecting state no matter what.
                _Connecting = false;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(exception,
                        "Client Recv: failed to connect to ip=" + addr + " port=" + port.ToString());

                // Notify the owner that the connection failed.
                if (_del_Connection_Failed != null)
                {
                    _del_Connection_Failed();
                }
            }
        }
    }
}
