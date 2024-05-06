using Newtonsoft.Json;
using OGA.Common.Process;
using OGA.TCP.Messages;
using OGA.TCP.Server.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static OGA.TCP.Server.TCPEndpoint;

namespace OGA.TCP.Server
{
    /// <summary>
    /// Represents a server-side tcp/ws socket endpoint.
    /// Provides framed message transfer with channel, scope, and custom properties.
    /// This abstract class gets derived for each transport type.
    /// </summary>
    public abstract class Endpoint_Abstract : IDisposable
    {
        #region Private Fields

        protected bool _alreadydisposed;

        protected string _classname;

        /// <summary>
        /// Indicates to the public send methods, when they are permitted to send outgoing messages on the websocket.
        /// This flag is normally set after the websocket is stood up and open for business.
        /// It is cleared as soon as something happens (shutdown, error, lost conn), indicating the websocket is no longer viable for sending messages.
        /// </summary>
        protected bool _allowsend;

        static protected int _instance_counter;

        protected int _Startup_Connect_Retry_Delay = 20000;

        /// <summary>
        /// Number of milliseconds between checkups of the connection loop's connected state.
        /// This value gets lowered if the keepalive interval goes below 5 seconds.
        /// </summary>
        protected int _Connected_InnerLoop_Delay = 5000;

        protected int _last_messageid = 0;

        protected CancellationTokenSource _cts;

        protected CancellationTokenSource _receive_cts;

        /// <summary>
        /// Max amount of time (in seconds) allowed between client messages, before considering the client dormant, and closeable.
        /// </summary>
        protected int _cfg_deadClientTimeout;

        // Create a semaphore to enforce thread safety when sending data to the client.
        protected readonly AsyncSemaphore _write_semaphore = new AsyncSemaphore(1);

        protected bool _alreadycalled_closedelegate = false;

        protected Dictionary<string, DelMessageReceived> _ChannelMessageHandlers;

        protected volatile int _receivedmessage_counter;

        #endregion


        #region Public Properties

        /// <summary>
        /// Maximum allowed message size for a single frame.
        /// Prevent allocation attacks. Each packet is prefixed with a length header, so an attacker could send a fake packet with length=2GB,
        /// causing the server to allocate 2GB and run out of memory quickly.
        /// -> simply increase max packet size if you want to send around bigger files!
        /// -> 1MB per message should be more than enough.
        /// </summary>
        public int MaxMessageSize { get; set; } = OGA.TCP.Constants.CONST_MAX_MessageSize;

        /// <summary>
        /// Set this to the lowercase name of the transport: tcp, ws, etc...
        /// </summary>
        abstract public string TransportShortName { get; }
        /// <summary>
        /// Set this to the name of the transport: TCPSocket, Websocket, etc...
        /// </summary>
        abstract public string TransportLongName { get; }

        /// <summary>
        /// Set this to the lowercase string-literal of the libver property that is passed during connection registration.
        /// For websocket clients, this is: "wslibver".
        /// For tcpsocket clients, this is: "tcplibver".
        /// </summary>
        abstract public string PropName_ClientLibVer { get; }

        /// <summary>
        /// Determines if a receiver loop is spawned.
        /// This should be set for websockets, clear for tcpsockets.
        /// </summary>
        abstract public bool Cfg_TransportRequiresReceiverLoop { get; }

        /// <summary>
        /// Instance Id of the tcp/websocket endpoint.
        /// </summary>
        public int InstanceId { get; protected set; }

        /// <summary>
        /// Holds high level information about the connected client, such as its deviceid, user auth status, etc.
        /// </summary>
        public ClientInfo ClientInfo { get; protected set; }

        /// <summary>
        /// This is a unique identifier of the connection.
        /// The TCP/WSHost controller generates it when a TCP/WSEndpoint is created for the connection.
        /// We added this id, so that we have a guaranteed unique identifier of every websocket connection, regardless of any client forgeries or changes to protocol-provided ids.
        /// </summary>
        public string WSId { get; set; }

        /// <summary>
        /// Set when the websocket allows sending messages.
        /// </summary>
        public bool AllowSend { get => this._allowsend; }

        /// <summary>
        /// Indicates when the endpoint has reached its keepalive timeout, and determined the client as failed.
        /// When set, the connection is shutting down, and can no longer be used.
        /// </summary>
        public bool Client_IsSilent { get; protected set; }

		public eEndpoint_ConnectionStatus State { get; protected set; }

        /// <summary>
        /// Can be checked for a positive connection.
        /// Your implementation should verify the endpoint is NOT disposed, the underlying transport instance is not null, and indicates connected.
        /// </summary>
        public abstract bool IsConnected { get; }
        //{
        //    get
        //    {
        //        try
        //        {
        //            if(disposedValue)
        //                return false;

        //            if (_webSocket == null)
        //                return false;

        //            return _webSocket.State == WebSocketState.Open;
        //        }
        //        catch(Exception e)
        //        {
        //            return false;
        //        }
        //    }
        //}

        /// <summary>
        /// Local timestamp of the last received message from the client.
        /// </summary>
        public DateTime LastReceivedTimeUTC { get; protected set; }

        /// <summary>
        /// Max amount of time (in seconds) allowed between client messages, before considering the client dormant, and closeable.
        /// </summary>
        public int Cfg_DeadClientTimeout
        {
            get => _cfg_deadClientTimeout;
            set
            {
                if (value < 5)
                    _cfg_deadClientTimeout = 5;
                else
                    _cfg_deadClientTimeout = value;
            }
        }
        /// <summary>
        /// Set this flag for normal operation.
        /// Clear it if we are breakpointing other things, and the stoppage may trigger connection closure.
        /// </summary>
        public bool Cfg_We_Require_Clients_to_Be_Chatty { get; set; }
        /// <summary>
        /// This property is set on connection registration by the client, if it wants all messages to be echoed back to him.
        /// Of course, there are some message types that don't get echo'd by this: ping, pong, registration, closure, etc...
        /// </summary>
        public bool Cfg_Connection_LoopBack_All { get; set; }
        /// <summary>
        /// Set this flag if the client wants to have a session without any keepalive messages.
        /// This is used for testing, so that, breakpointing will not cause a timeout to occur and cause a connection to be declared as dead.
        /// </summary>
        public bool Cfg_Disable_KeepAlive { get; set; }

        /// <summary>
        /// Total number of received messages since opening.
        /// </summary>
        public int ReceivedMessage_Counter { get => _receivedmessage_counter; }

        #endregion


        #region Public Delegates

        public delegate int DelMessageReceived(Endpoint_Abstract mep, string messagetype, string jsondata, string corelationid);
        protected DelMessageReceived _delOnMessageReceived;
        /// <summary>
        /// Public delegate hook for accepting message traffic.
        /// </summary>
        public DelMessageReceived OnMessageReceived
        {
            set
            {
                _delOnMessageReceived = value;
            }
        }

        public delegate int DelRawMessageReceived(Endpoint_Abstract mep, string rawstring);
        protected DelRawMessageReceived _delOnRawMessageReceived;
        /// <summary>
        /// Normally, this is not used as messages are exchanged as typed classes.
        /// However, attaching a handler to this will allow raw message strings to be processed.
        /// </summary>
        public DelRawMessageReceived OnRawMessageReceived
        {
            set
            {
                _delOnRawMessageReceived = value;
            }
        }

        public delegate void DelConnection(Endpoint_Abstract mep);
        protected DelConnection _delConnectionClosed;
        /// <summary>
        /// Public hook for a connection manager to be notified when an endpoint loses connection, has fatally errored, and is closing down.
        /// </summary>
        public DelConnection OnConnectionClosed
        {
            set
            {
                _delConnectionClosed = value;
            }
        }

        public delegate void DelConnRegisterReceived(Endpoint_Abstract mep, ClientInfo oldvals, ClientInfo newvals);
        protected DelConnRegisterReceived _delConnectionRegistration;
        /// <summary>
        /// Public hook for a connection manager to receive registration messages from the client.
        /// Registration messages include client updates, such as user context changes, the client's reported deviceId and connectionid.
        /// </summary>
        public DelConnRegisterReceived OnConnectionRegistration
        {
            set
            {
                _delConnectionRegistration = value;
            }
        }

		public delegate void dStatus_Change(Endpoint_Abstract mep, string statusupdate);
		protected dStatus_Change _del_Status_Change;
		/// <summary>
		/// Assign a handler to this delegate to receive status changes.
		/// </summary>
		public dStatus_Change OnStatus_Change
		{
			set
			{
				this._del_Status_Change = value;
			}
		}

        #endregion


        #region ctor / dtor

        /// <summary>
        /// This is the normal constructor for creating a TCPEndpoint instance.
        /// It requires the web listener already having created the websocket instance from the initial http request.
        /// </summary>
        /// <param name="webSocket"></param>
        public Endpoint_Abstract()
        {
            _instance_counter++;
            this.InstanceId = _instance_counter;

            _ChannelMessageHandlers = new Dictionary<string, DelMessageReceived>();

            ClientInfo = new ClientInfo();

            // Create a local unique identifier of the tcp/websocket instance.
            // We will use this value for all tracking, in case a protocol change creates collisions of the connectionId property.
            WSId = NUlid.Ulid.NewUlid().ToString();

            _alreadydisposed = false;

            _cfg_deadClientTimeout = 600;

            LastReceivedTimeUTC = DateTime.UnixEpoch;

            // Clear the allow sending flag, to prevent any outgoing messages...
            this._allowsend = false;
        }

        protected void Dispose(bool disposing)
        {
            if (!_alreadydisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)

                    Stop_Async().GetAwaiter();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _alreadydisposed = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~WSService()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// This method is called by the http listener or socket listener, to startup the server-side of the tcp/websocket.
        /// This call blocks for the life of the tcp/websocket connection.
        /// For websocket implementations, call this method synchronously (with await) on the web request thread.
        /// This ensures the web request middleware will not close the websocket.
        /// For tcpsocket implementations, call this method asynchronously from the tcp listener (with Task.Run or by creating a new thread) from the tcp listener's thread.
        /// This ensures the tcp listener is able to continue processing, after starting this instance up.
        /// </summary>
        /// <returns></returns>
        public async Task<int> Start_Async()
        {
            // The constructor accepted the context and tcp/websocket instance.
            // We need to begin our receive loop, and make ourselves available for sending messages to the client.

            // Start the receive loop, so we can accept incoming messages from the client...

            try
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                    $"Attempting to start {(this.TransportLongName?.ToLower() ?? "socket")} endpoint instance...");

                // Check if connected...
                if (_alreadydisposed)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                        $"{(this.TransportLongName ?? "Socket")} is already disposed.");

                    return -1;
                }

                // Clear the allow sending flag, to prevent any outgoing messages...
                this._allowsend = false;

                // Clear the keepalive alarm...
                this.Client_IsSilent = false;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                    $"Attempting to connect {(this.TransportLongName?.ToLower() ?? "socket")} client.");

                // Reset the closure delegate marker...
                _alreadycalled_closedelegate = false;

                _cts = new CancellationTokenSource();

                //// Trigger a thread to run the maintenance loop...
                //_ = Task.Run(async () => await ConnectionLoop());

                // Call the connection loop...
                // When it returns, we are closed down, and will unwind the web request (for websockets) or listener callback (for tcp sockets) that created this instance.
                int res = await ConnectionLoop();

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} connection loop returned for connection ({this.WSId}).");

                // Do common connection closure things...
                DoCommonClosureThings();

                return 1;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                    $"Exception occurred while attempting to connect {(this.TransportLongName?.ToLower() ?? "socket")} client.");

                return -2;
            }
        }

        /// <summary>
        /// Normally this method doesn't need to be called, directly, as it is called during diposal.
        /// </summary>
        /// <returns></returns>
        public async Task Stop_Async()
        {
            // Wrap all this in a try-catch, to ensure we don't throw an exception in the raw task...
            try
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Stop_Async)} - " +
                    $"Attempting to stop {(this.TransportLongName?.ToLower() ?? "socket")} client at connection ({this.WSId})...");

                // Clear the allow sending flag, to prevent any outgoing messages...
                this._allowsend = false;

                // Disconnect any message handlers...
                this._ChannelMessageHandlers.Clear();
                this._delOnMessageReceived = null;
                this._delOnRawMessageReceived = null;

                // Wrap this in a try-catch to ensure overridden method doesn't unwind us with an exception...
                try
                {
                    await this.CloseandDisposeTransport();
                }
                catch (Exception e) { }

                // Wait a tick before cancelling the receive loop...
                // This lets the receive handler accept any closure frames, to close gracefully.
                await Task.Delay(100);

                if (_receive_cts != null)
                {
                    try
                    {
                        _receive_cts?.Cancel();
                    }
                    catch (Exception) { }

                    await Task.Delay(100);

                    try
                    {
                        _receive_cts?.Dispose();
                    }
                    catch (Exception) { }

                    _receive_cts = null;
                }

                if (_cts != null)
                {
                    try
                    {
                        _cts?.Cancel();
                    }
                    catch (Exception) { }

                    await Task.Delay(100);

                    try
                    {
                        _cts?.Dispose();
                    }
                    catch (Exception) { }

                    _cts = null;
                }

                // Wrap in a try-catch to ensure the method override doesn't throw an exception and unwind us...
                try
                {
                    this.DereferenceTransport();
                }
                catch (Exception) { }

                // Clear any delegates...
                this._delConnectionClosed = null;
                this._delConnectionRegistration = null;
                this._del_Status_Change = null;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Stop_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} connection ({this.WSId}) is closed.");

                return;
            }
            catch(Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Stop_Async)} - " +
                    $"Exception caught while attemptint to stop the endpoint.");
            }
        }

        /// <summary>
        /// Used by the Connection Manager when submitting client connection data for routing.
        /// </summary>
        /// <param name="ce"></param>
        public void Populate_ConnectionEntry(ConnectionEntry_v1 ce)
        {
            // Copy the client info, so we can submit it for this connection...
            ce.UserId = this.ClientInfo.UserId;
            ce.DeviceId = this.ClientInfo.DeviceId;
            ce.Pid = this.ClientInfo.Pid;
            ce.ConnectionTimeUTC = this.ClientInfo.ConnectionTimeUTC;
            ce.AppId = this.ClientInfo.AppId ?? "";
            ce.RuntimeId = this.ClientInfo.RuntimeId ?? "";
            ce.AppVersion = this.ClientInfo.AppVersion ?? "";
            ce.Language = this.ClientInfo.Language ?? "";
            ce.LibVersion = this.ClientInfo.LibVersion ?? "";
            // These last three values are not known by the TCP/WSEndpoint. They will be populated by the owning Connection Manager.
            ce.Hostname = "";
            ce.Host_Port = 0;
            ce.Region = "";

            // NOTE: We populate a connection entry with a server-generated identifier, instead of the one provided by the client or the protocol layer.
            // We have migrated away from using protocol-provided connectionIds and client-provided ones, to using our own Id, created at connection.
            // This allows us to not be concerned with client-forged connectionIds taking advantage of lingering routing data from recently bounced connections.
            // It allows us to be free of any concerns that a protocol-provided connection id is unique.
            // And, it allows us to harmonize client connection identifiers across any transport type, besides websocket.

            // Ensure we use the WSId as our connectionid...
            ce.ConnectionId = this.WSId;
            //ce.ConnectionId = this.ClientInfo.ConnectionId;
        }

        public int Add_ChannelHandler(string channel, DelMessageReceived handler)
        {
            if (!this._ChannelMessageHandlers.TryAdd(channel, handler))
            {
                // The channel is already assigned.

                return -1;
            }

            return 1;
        }
        public int Remove_ChannelHandler(string channel)
        {
            this._ChannelMessageHandlers.Remove(channel);

            return 1;
        }

        #endregion


        #region Connection Management

        /// <summary>
        /// For a websocket implementation, this method runs on the thread that called the Start_Async method.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> ConnectionLoop()
        {
            // Check if connected...
            if (_alreadydisposed)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                    "Websocket is already disposed.");

                return -1;
            }

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                "Starting connection loop...");

            try
            {
                // Verify that we are NOT disposed and the connection is Open...
                if (this.IsConnected)
                {
                    // We should be connected.

                    // Mark the connection time...
                    this.ClientInfo.ConnectionTimeUTC = System.DateTime.UtcNow;

                    // Reset the received message counter...
                    // We do this, here, so the counter can include transport-layer messages in its received count.
                    this._receivedmessage_counter = 0;

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                        "Doing any post connection work...");

                    // If passed, do preamble things...
                    // For a WSEndpoint (server-side), this includes starting the receive loop.
                    int rrr = await Do_Post_Connection_Work_Async();
                    if (rrr == 1)
                    {
                        // Post connection work is done.
                        // For a WSEndpoint, this means the receive loop has been started.

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                            $"Connected to client, ConnectionId = {this.WSId}.");

                        // Update our last message time, to account for the newly opened connection...
                        LastReceivedTimeUTC = DateTime.UtcNow;

                        // We are connected.

                        // Set the allow sending flag, to allow public outgoing messages...
                        this._allowsend = true;

                        // Start the inner status loop...
                        while (!_cts.IsCancellationRequested && this.IsConnected)
                        {
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                $"Connection active with client, ConnectionId = {this.WSId}.");

                            // While connected, we normally require some minimal level of traffic across the websocket, to ensure it is viable.
                            // This does a couple things:
                            //  It prevents any intermediate firewalls from closing the connection for lack of traffic.
                            //  It lets us know that the remote end is actively spinning, and hasn't died.
                            // It is normally a client's responsibility to initiate the minimal level of traffic we need to see on a connection.
                            // To do this, a client's connection loop will periodically check how long it's been since a message was sent.
                            // And if it's been too long, the client will send us a ping.
                            // For client-side testing, we can disable this requirement, client-side.
                            // A client can tell us to disable keepalive monitoring.
                            // And, we can change our requirement for clients to be chatty, so we can debug, server-side.
                            // Both of these flags, together, determine if we will watch for the idle timeout to expire.

                            // See if we need keepalives from the client...
                            // This may be false if we're testing other things, and the breakpoints will cause enough delay to seem like silence.
                            if (Cfg_We_Require_Clients_to_Be_Chatty && !Cfg_Disable_KeepAlive)
                            {
                                // We need to check if we've received a message lately.

                                // Check if we've received any traffic from the client in the timeout period...
                                // NOTE: This actually publishes the keepalive state for a connection manager to observe.
                                this.Client_IsSilent = HasClientGoneSilent();
                                if (this.Client_IsSilent)
                                {
                                    // The client has not sent us anything, not even a keep alive message in a while.
                                    // We will regard him as dormant, and close his connection.

                                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                        $"Client connection has gone silent, ConnectionId = {this.WSId}.");

                                    // Leave the while loop, so we can close down the connection...
                                    break;
                                }
                            }
                            else
                            {
                                // We are not monitoring keepalives.
                                // So, we will ensure the keepalive flag is cleared.
                                this.Client_IsSilent = false;
                            }
                            // If here, the client is still active.
                            // We will loop back to the top for another iteration.

                            // Slow down the connection loop, so we're not hogging CPU, but can still monitor watchdogs and such...
                            await Task.Delay(_Connected_InnerLoop_Delay, _cts.Token);
                        }
                        // Bottom of the connection monitoring loop.
                        // If we fell here, we lost connection, got cancelled, or the client went silent.
                        // For a client: we will loop back to the top of the outer loop to leave.
                        // For a server: we shutdown the connection, because it's the client's responsibility to reconnect.

                        // See what happened...
                        if (this.Client_IsSilent)
                        {
                            // The client went silent.

                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                $"Connected client has gone silent for ConnectionId = {this.WSId}. Leaving...");
                        }
                        if (_cts.IsCancellationRequested)
                        {
                            // We were cancelled.

                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                $"Connection cancelled for ConnectionId = {this.WSId}. Leaving...");
                        }
                        else if (!this.IsConnected)
                        {
                            // We lost connection.

                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                $"Connection lost with client, ConnectionId = {this.WSId}.");
                        }

                        // Do common connection closure things...
                        DoCommonClosureThings();

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                            $"Connection loop closed down for ConnectionId = {this.WSId}. Leaving...");

                        return 1;
                    }
                    else
                    {
                        // We failed to get through post-connection work (starting the receive loop).
                        // So, we must recycle this connection, and leave.

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                            "Failed to complete post connection work.");

                        // Do common connection closure things...
                        DoCommonClosureThings();

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                            $"Connection loop closed down for ConnectionId = {this.WSId}. Leaving...");

                        return 1;
                    }
                }
                else
                {
                    // We were told to startup the connection loop.
                    // But, it is not open, or is disposed.
                    // We must render this connection instance unusable, and dispose the underlying websocket.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                        $"Connection with ConnectionId = {this.WSId}, is no longer open. We will close it down...");

                    // Do common connection closure things...
                    DoCommonClosureThings();

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                        $"Connection loop closed down for ConnectionId = {this.WSId}. Leaving...");

                    return 1;
                }
            }
            catch when (_cts.IsCancellationRequested)
            {
                // We were cancelled.
                // We can leave.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                    $"Websocket for ConnectionId = {this.WSId}, is cancelled, and being shut down.");

                // Do common connection closure things...
                DoCommonClosureThings();

                return 1;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                    $"Exception occurred while connected to client, ConnectionId = {this.WSId}.");

                // Do common connection closure things...
                DoCommonClosureThings();

                return -1;
            }
        }

        protected bool HasClientGoneSilent()
        {
            // Check if the last received message it recent or not...

            DateTime ctime = DateTime.UtcNow;

            if (ctime.CompareTo(LastReceivedTimeUTC.AddSeconds(_cfg_deadClientTimeout)) > 0)
            {
                // The connection has been silent for too long.
                // We must close it.

                return true;
            }
            // If here, we've received messages recently, and consider the client active.

            return false;
        }

        /// <summary>
        /// Performs any activities required for a new connection, such as id, registration, and worker setup.
        /// Runs on the thread given to the ConnectionLoop method.
        /// Which, for a websocket implementation, this would be the thread that started the instance.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> Do_Post_Connection_Work_Async()
        {
            // We need to register with the service, so it readily knows our connection Id and other parameters....

            // Additionally, do things in this method, to catch the app up to current state, since it was last online. Things like:
            //  Pulling chat message updates
            //  Checking for queued friend requests

            try
            {
                // Check if connected...
                if (_alreadydisposed)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                        $"{(this.TransportLongName ?? "socket")} is already disposed.");

                    return -1;
                }
                if (!IsConnected)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                        $"{(this.TransportLongName ?? "socket")} is not connected.");

                    return 0;
                }

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                    $"Setting up receive loop for new connection...");

                // Setup receive loop...
                // This was added to a try-catch because an edge case occurred in which the receive_cts was seen as not null by the if, then was null within the if block.
                // This race condition occurred while stopping the debugger, and probably triggered a client restart, during a kill.
                // For this, we added a catch that will abort setup and leave.
                try
                {
                    if (_receive_cts != null)
                    {
                        _receive_cts.Cancel();
                        await Task.Delay(100);
                        _receive_cts.Dispose();
                        _receive_cts = null;
                    }

                    _receive_cts = new CancellationTokenSource();

                    // Do any transport-specific post connection setup work...
                    // This hook allows the TCP endpoint implementation a spot in the setup flow, to get its network stream instance.
                    // Call this in a try-catch to ensure its override doesn't throw and unwind us...
                    try
                    {
                        var res = await this.Do_TransportSpecific_PostConnectionWork_Async();
                        if (res != 1)
                        {
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                                $"Failed to do transport specific post connnection work. " +
                                    $"ConnectionID = {this.WSId}.");

                            return -3;
                        }
                    } catch ( Exception ex ) { }

                    // Call the receiver loop if the transport needs one...
                    if(this.Cfg_TransportRequiresReceiverLoop)
                    {
                        /// Returns  1 if cancelled.
                        /// Returns  0 if failed to parse the received message.
                        /// Returns -1 if unable to accept received messages.
                        /// Returns -2 if received a close message.
                        _ = Task.Run(async () =>
                        {
                            // Wrap this in a try-catch to ensure the implementation doesn't throw, and create an app exception.
                            try
                            {
                                await ReceiveLoop_from_Client();
                            } 
                            catch (Exception) { }
                        });
                    }
                }
                catch (Exception tre)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(tre,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                        $"Exception occurred while attempting to setup receive loop cancelation token and receive loop. " +
                            $"ConnectionID = {this.WSId}.");

                    return -3;
                }

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                    $"Receive loop setup for new connection.");

                //// Get our userid...
                //var uid = this._usersvc.CurrentUserId;
                //if(uid != null)
                //{
                //success = false;

                return 1;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                    $"Exception occurred while starting receive loop. ");

                return -2;
            }
        }

        /// <summary>
        /// This virtual method provides a way for the derived type to perform any transport-specific setup after an initial connection is made.
        /// This method was created for the TCP socket implementation, because it has a two-part client (client and network stream),
        ///     and the network stream instance must be retrieved from the client instance, to expose the read and write methods.
        /// </summary>
        virtual protected async Task<int> Do_TransportSpecific_PostConnectionWork_Async()
        {
            return 1;
        }

        /// <summary>
        /// Common method that groups the normally executed closure actions, so they are always the same and don't need duplication.
        /// </summary>
        protected void DoCommonClosureThings()
        {
            // Reset the allow sending flag, to prevent outgoing messages...
            this._allowsend = false;

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(DoCommonClosureThings)} - " +
                $"Closing connection ({this.WSId})...");

            // Signal that the connection was lost...
            DispatchConnectionClosed();

            // Wrap this in a try-catch to ensure overridden method doesn't unwind us with an exception...
            try
            {
                this.CloseandDisposeTransport().GetAwaiter();
            }
            catch (Exception e) { }
            
            // Wrap this in a try-catch to ensure overridden method doesn't unwind us with an exception...
            try
            {
                this.DereferenceTransport();
            }
            catch (Exception e) { }

            
        }

        /// <summary>
        /// In the implementation of this method, perform a close on the transport, and dispose of it.
        /// Don't dereference the transport instance, yet.
        /// </summary>
        abstract protected Task CloseandDisposeTransport();
        //        {
        //            if (_webSocket != null)
        //            {
        //                // Close the connection...
        //                try
        //                {
        //#pragma warning disable CS8602 // Dereference of a possibly null reference.
        //                    // We will call the close output async, so we are not waiting for a reply...
        //                    // https://learn.microsoft.com/en-us/dotnet/api/system.web.websockets.aspnetwebsocket.closeasync?view=netframework-4.8#remarks
        //                    await _webSocket?.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        //                    // We've waffled back and forth on which to call, here: CloseAsync or CloseOutputAsync.
        //                    // What we've determined is that: CloseAsync would be the more correct. BUT. Big BUT.
        //                    // Using CloseAsync REQUIRES that BOTH client and server use it, or the one side that did, will hang indefinitely.
        //                    // And since hanging indefinitely is a VERY bad failure mode for production code, this is not a tolerable side-effect.
        //                    // So, we use the CloseOutputAsync and suffer the transient WebSocketException it may cause the other end, which should close down anyway.
        //#pragma warning restore CS8602 // Dereference of a possibly null reference.
        //                }
        //                catch (Exception) { }

        //                try
        //                {
        //                    _webSocket?.Dispose();
        //                }
        //                catch (Exception) { }
        //            }
        //        }

        /// <summary>
        /// In the implementation of this method, perform a dereference of the transport instance.
        /// Don't dispose it or anything else, here.
        /// </summary>
        abstract protected void DereferenceTransport();
        //{
        //    this._websocket = null;
        //}

        #endregion


        #region Send Methods

        /// <summary>
        /// Public call to send a message to the connected client.
        /// Returns  1 = Message was sent.
        /// Returns  0 = No connection. Cannot send.
        /// Returns -1 = Disposed instance. Cannot send.
        /// Returns -2 = Unknown exception. Cannot send.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="channel"></param>
        /// <param name="scope"></param>
        /// <param name="corelationid"></param>
        /// <returns></returns>
        public async Task<int> SendMessage_toClient(object msg, string channel = "", string scope = "", string corelationid = "")
        {
            // Do we allow sending...
            if(!this._allowsend)
            {
                // No outgoing messages are allowed.
                return 0;
            }

            /// Returns  1 = Message was sent.
            /// Returns  0 = No connection. Cannot send.
            /// Returns -1 = Disposed instance. Cannot send.
            /// Returns -2 = Unknown exception. Cannot send.
            return await Send_Object_toClient(msg, channel, scope, corelationid);
        }

        /// <summary>
        /// Sends an object instance to the connected client.
        /// Will serialize it, and pass it along for packaging.
        /// Returns  1 = Message was sent.
        /// Returns  0 = No connection. Cannot send.
        /// Returns -1 = Disposed instance. Cannot send.
        /// Returns -2 = Unknown exception. Cannot send.
        /// </summary>
        /// <param name="objectinstance"></param>
        /// <param name="channel"></param>
        /// <param name="scope"></param>
        /// <param name="corelationid"></param>
        /// <returns></returns>
        protected async Task<int> Send_Object_toClient(object objectinstance, string channel = "", string scope = "", string corelationid = "")
        {
            string messagetype = OGA.SharedKernel.Serialization.Serialization_Helper.GetType_forSerialization(objectinstance);
            string jsonmsg = JsonConvert.SerializeObject(objectinstance);

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Object_toClient)} - " +
                "Attempting to send object to client...");

            /// Returns  1 = Message was sent.
            /// Returns  0 = No connection. Cannot send.
            /// Returns -1 = Disposed instance. Cannot send.
            /// Returns -2 = Unknown exception. Cannot send.
            return await Send_SerializedObject_toClient_Async(messagetype, jsonmsg, channel, scope, corelationid);
        }

        /// <summary>
        /// Used by this layer, to exchange keep-alive messages with the caller.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> SendPing_toClient_Async()
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(SendPing_toClient_Async)} - " +
                "Attempting to send ping message to client...");

            return await Send_SerializedObject_toClient_Async("ping", "");
        }
        /// <summary>
        /// Used by this layer, to exchange keep-alive messages with the caller.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> SendPong_toClient_Async()
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(SendPong_toClient_Async)} - " +
                "Attempting to send pong message to client...");

            return await Send_SerializedObject_toClient_Async("pong", "");
        }

        /// <summary>
        /// Called by the endpoint, after a connection registration message has been received.
        /// This method sends back a reply that gives the client their server-created ConnectionId, and notifies the client to allow comms.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> SendRegistrationReply(ClientInfo ci, string oldconnid)
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(SendRegistrationReply)} - " +
                "Attempting to send registration reply to client...");

            // If this is the first time the client has sent a connection registration message to us,
            //  our "oldconnid" is actually blank.
            // This is because we didn't yet have that value, until we received the first one.
            // So. If the "oldconnid" is blank, we will instead use the connectionIdin the given ClientInfo struct.

            // Use the old connectionid, unless this is the first registration pass.
            string connectionidtoincludeasoldvalue = "";
            if(string.IsNullOrEmpty(oldconnid))
            {
                // First registration cycle.
                // We will reply with the live connectionid (which is what the client thinks they are)...
                connectionidtoincludeasoldvalue = ci.ConnectionId;
            }
            else
            {
                // Use the actual last registered connectionid...
                connectionidtoincludeasoldvalue = oldconnid;
            }

            // Formulate a registration reply message...
            var msg = new ConnRegisterReplyDTO();
            msg.Props = new string[0];
            // Give the client their deviceid and userid....
            msg.DeviceId = ci.DeviceId;
            msg.UserId = ci.UserId;
            // We know our server-created ConnectionId at endpoint construction.
            // It's actually the WSId property.
            // We include it, here.
            msg.ConnectionId = this.WSId;
            msg.OldConnectionId = connectionidtoincludeasoldvalue;

            // Send the reply to the client...
            return await Send_Object_toClient(msg);
        }

        /// <summary>
        /// Accepts any json-serialized object type, wraps it in a message envelope, and sends it to the client.
        /// Returns  1 = Message was sent.
        /// Returns  0 = No connection. Cannot send.
        /// Returns -1 = Disposed instance. Cannot send.
        /// Returns -2 = Unknown exception. Cannot send.
        /// </summary>
        /// <param name="objecttype"></param>
        /// <param name="jsonobject"></param>
        /// <param name="channel"></param>
        /// <param name="scope"></param>
        /// <param name="corelationid"></param>
        /// <returns></returns>
        public async Task<int> Send_SerializedObject_toClient_Async(string objecttype, string jsonobject, string channel = "", string scope = "", string corelationid = "")
        {
            using var action_start = OGA.Telemetry.Lib.TelemetryBase.ProcessActivitySource?
                                        .StartActivity(this.TransportLongName.ToLower() ?? "socket" + "-outgoing-jsonobject-send");
            action_start?.SetTag("corelationid", corelationid);

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_SerializedObject_toClient_Async)} - " +
                "Sending serialized message to client...");

            // Ensure any json string exists at this point...
            if(jsonobject == null)
                jsonobject = "";

            // Ensure the serialized buffer is not too large for the receiver...
            // We derate the max size enough to fit the message envelope and header (length value).
            if(jsonobject.Length > (this.MaxMessageSize - 1024))
            {
                // Message is too large to fit in a single message frame.
                // We will tell the caller, so they can send the message, piece-wise.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_SerializedObject_toClient_Async)} - " +
                    $"Message is too large ({(jsonobject?.Length.ToString() ?? "unknown size")}) to send to the remote endpoint.");

                return -10;
            }
            // The raw message will fit into a single message frame.

            // Create and stuff an envelope...
            MessageEnvelope me = new MessageEnvelope();
            me.MsgId = _last_messageid++.ToString();
            me.SentTimeUTC = DateTime.UtcNow;
            // Ensure that the data section is never null...
            me.Data = jsonobject ?? "";
            me.Channel = channel;
            me.Scope = scope;
            me.MessageType = objecttype;
            me.Props = new string[] { "corelationid=" + corelationid ?? "" };

            /// Returns  1 = Message was sent.
            /// Returns  0 = No connection. Cannot send.
            /// Returns -1 = Disposed instance. Cannot send.
            /// Returns -2 = Unknown exception. Cannot send.
            return await Send_MessageEnvelope_toClient_Async(me);
        }

        /// <summary>
        /// Accepts a prepared message envelope, and sends it to the tcp/websocket client.
        /// Returns  1 = Message was sent.
        /// Returns  0 = No connection. Cannot send.
        /// Returns -1 = Disposed instance. Cannot send.
        /// Returns -2 = Unknown exception. Cannot send.
        /// </summary>
        /// <param name="me"></param>
        /// <returns></returns>
        protected async Task<int> Send_MessageEnvelope_toClient_Async(MessageEnvelope me)
        {
            try
            {
                // Check if connected...
                if (_alreadydisposed)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toClient_Async)} - " +
                        $"{(this.TransportLongName ?? "Socket")} is already disposed.");

                    return -1;
                }
                if (!IsConnected)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toClient_Async)} - " +
                        $"{(this.TransportLongName ?? "Socket")} is not connected.");

                    return 0;
                }

                // Serialize the envelope and convert it to bytes...
                var jsonmsg = JsonConvert.SerializeObject(me);
                // Convert the string to bytes for transport...
                byte[] d = Encoding.UTF8.GetBytes(jsonmsg);

                //************************************************************************************************************
                // Start Send Thread Lock
                //************************************************************************************************************
                // Enter a thread lock, to prevent garbling of sent data...
                using (await _write_semaphore.WaitAsync())
                {
                    // Send the message...
                    var res = await this.RawTransportSend(d);
                    if (res >= 1)
                        return 1;
                    else
                        return res;
                }
                //************************************************************************************************************
                // End Send Thread Lock
                //************************************************************************************************************

                return 1;
            }
            catch(WebSocketException wse)
            {
                // Websocket exception occurred.
                // Meaning, the websocket has been closed by the other end.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toClient_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} was closed by the other end, and cannot send messages.");

                return -1;
            }
            catch(System.Net.Sockets.SocketException)
            {
                // TCPsocket exception occurred.
                // Meaning, the tcpsocket has been closed by the other end.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toClient_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} was closed by the other end, and cannot send messages.");

                return -1;
            }
            catch (ObjectDisposedException ode)
            {
                // Socket is disposed.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toClient_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is disposed, and cannot accept messages.");

                return -1;
            }
            catch (InvalidOperationException ioe)
            {
                // Socket is not open.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toClient_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is not open, and cannot accept messages.");

                return 0;
            }
            catch (Exception e)
            {
                // Unknown exception type...

                var f = e?.GetType()?.FullName ?? "";

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toClient_Async)} - " +
                    $"Unknown exception type occurred ({f}) while attempting to send message over {(this.TransportLongName.ToLower() ?? "socket")}.");

                return -2;
            }
        }

        /// <summary>
        /// Override this method with the transport-specific means to send the given array.
        /// No need for any try-catch, as the call to this method is safely wrapped.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        abstract protected Task<int> RawTransportSend(byte[] data);
        //{
        //    await _webSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
        //    return 1;
        //}

        #endregion


        #region Receiving Methods

        /// <summary>
        /// NOTE: This method executes on its own thread, started by the post connection method.
        /// Runs the receive loop logic to accept all received messages and dispatch them.
        /// This is called each time a new connection is made.
        /// Returns  1 if cancelled.
        /// Returns  0 if failed to parse the received message.
        /// Returns -1 if unable to accept received messages.
        /// Returns -2 if received a close message.
        /// </summary>
        /// <returns></returns>
        abstract protected Task<int> ReceiveLoop_from_Client();
        //{
        //    OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
        //        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //        "Receive loop method has been called.");

        //    // Check if connected...
        //    if (disposedValue)
        //    {
        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
        //            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //            "Websocket is already disposed.");

        //        return -1;
        //    }

        //    // Do any setup....

        //    OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
        //        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //        "Websocket receive loop is starting...");

        //    var buffer = new ArraySegment<byte>(new byte[2048]);
        //    WebSocketReceiveResult result;
        //    MemoryStream ms = null;

        //    // Enter the loop...
        //    try
        //    {
        //        // Run the outer loop...
        //        while (this.IsConnected && _receive_cts != null && !_receive_cts.IsCancellationRequested)
        //        {
        //            // Loop inside a try, to ensure we don't leave unless we want to...
        //            try
        //            {
        //                // Check if we are connected...
        //                if (_webSocket.State != WebSocketState.Open)
        //                {
        //                    // We are not open.
        //                    // We cannot accept messages.
        //                    // Leave the receive loop if we are not connected...

        //                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
        //                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                        "Receive loop detected a closed websocket. Leaving the receive loop...");

        //                    break;
        //                }
        //                else
        //                {
        //                    // We can receive.

        //                    ms = new MemoryStream();

        //                    // Loop until we receive the entire message...
        //                    do
        //                    {
        //                        // Collect the available piece...
        //                        result = await _webSocket.ReceiveAsync(buffer, _receive_cts.Token);

        //                        // Check if we were given a close message...
        //                        if(result.MessageType == WebSocketMessageType.Close)
        //                        {
        //                            // We were given a close message.

        //                            // Clear the send flag, to prevent outgoing messages...
        //                            this._allowsend = false;

        //                            try
        //                            {
        //                                // Reply back with a close message...
        //                                // See this for which close method to call:
        //                                // https://learn.microsoft.com/en-us/dotnet/api/system.web.websockets.aspnetwebsocket.closeasync?view=netframework-4.8#remarks
        //                                await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", _receive_cts.Token);
        //                            }
        //                            catch(WebSocketException wse)
        //                            {
        //                                int x = 0;
        //                            }
        //                            catch(Exception e)
        //                            {
        //                                int x = 0;
        //                            }

        //                            return -2;
        //                        }
        //                        // Check if we received binary data...
        //                        if(result.MessageType == WebSocketMessageType.Binary)
        //                        {
        //                            // We were given a binary message.
        //                            // We cannot currently process binary data.
        //                            // So, we will consider this a protocol error.

        //                            // Clear the send flag, to prevent outgoing messages...
        //                            this._allowsend = false;

        //                            try
        //                            {
        //                                // Reply back with a close message...
        //                                // See this for which close method to call:
        //                                // https://learn.microsoft.com/en-us/dotnet/api/system.web.websockets.aspnetwebsocket.closeasync?view=netframework-4.8#remarks
        //                                await _webSocket.CloseOutputAsync(WebSocketCloseStatus.ProtocolError, "", _receive_cts.Token);
        //                            }
        //                            catch(WebSocketException wse)
        //                            {
        //                                int x = 0;
        //                            }
        //                            catch(Exception e)
        //                            {
        //                                int x = 0;
        //                            }

        //                            return -2;
        //                        }

        //                        // If here, we will accept the received block of data...
        //                        ms.Write(buffer.Array, buffer.Offset, result.Count);
        //                    }
        //                    while (!result.EndOfMessage);

        //                    // See if the message is a close request...
        //                    // If so, leave...
        //                    if (result.MessageType == WebSocketMessageType.Close)
        //                    {
        //                        // Clear the send flag, to prevent outgoing messages...
        //                        this._allowsend = false;

        //                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
        //                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                            "We received a close request from the client. Leaving the receive loop...");

        //                        break;
        //                    }

        //                    // If not a close, process the message...
        //                    ms.Seek(0, SeekOrigin.Begin);
        //                    using (var reader = new StreamReader(ms, Encoding.UTF8))
        //                    {
        //                        // Read in the raw message...
        //                        string rawmsg = await reader.ReadToEndAsync();

        //                        // Update our received timestamp...
        //                        LastReceivedTimeUTC = DateTime.UtcNow;

        //                        // Increment the received message counter...
        //                        Interlocked.Increment(ref this._receivedmessage_counter);

        //                        // Send it off for processing....
        //                        ///  1 = Message was handled.
        //                        ///  0 = Message could not be deserialized or handled. Ignoring and continuing on.
        //                        /// -1 = Registration failed. The receive loop cannot continue, and the connection must close down.
        //                        int res = Process_ReceivedMessage_from_Client(rawmsg);
        //                        if (res == 0)
        //                        {
        //                            // Message process and dispatch had a problem, but we can keep going.

        //                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
        //                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                                "Failed to process and dispatch received message.");
        //                        }
        //                        else if (res == -1)
        //                        {
        //                            // Message processing failed.
        //                            // We will consider this fatal to the current connection.

        //                            // We failed to complete post-connection work.
        //                            // We must recycle this connection, and try again.

        //                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
        //                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                                "Messaging processing failed in a fatal way, and we need to recycle this connection.");

        //                            // Do all the common connection closure things...
        //                            this.DoCommonClosureThings();

        //                            return 0;
        //                        }
        //                        // If here, we processed and dispatched the message.
        //                        // We can continue on to the next.
        //                    }
        //                    // Finished processing the current message.
        //                    // We will return back to the top of the while to check status and wait for another message.
        //                }
        //            }
        //            catch when (_receive_cts == null)
        //            {
        //                // We were cancelled.
        //                // We can leave.

        //                // Clear the send flag, to prevent outgoing messages...
        //                this._allowsend = false;

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
        //                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                    "Receive loop was cancelled.");

        //                return 1;
        //            }
        //            catch when (_receive_cts.IsCancellationRequested)
        //            {
        //                // We were cancelled.
        //                // We can leave.

        //                // Clear the send flag, to prevent outgoing messages...
        //                this._allowsend = false;

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
        //                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                    "Receive loop was cancelled.");

        //                return 1;
        //            }
        //            catch (WebSocketException wse)
        //            {
        //                // Clear the send flag, to prevent outgoing messages...
        //                this._allowsend = false;

        //                // Get the exception type...
        //                var gg = wse.InnerException?.GetType().Name ?? "";

        //                if (gg == nameof(ConnectionResetException))
        //                {
        //                    // The connection was closed.

        //                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
        //                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                        "The connection was closed.");

        //                    return 1;
        //                }

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
        //                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                    "Web Socket Exception occurred during receive loop. Likely from a connection closure.");

        //                return 1;
        //            }
        //            catch (Exception e)
        //            {
        //                // Clear the send flag, to prevent outgoing messages...
        //                this._allowsend = false;

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
        //                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //                    "*************Generic exception occurred while during receive loop. " +
        //                    "This one is marked, so we can see if it ever occurs, as it may indicate a flaw. " +
        //                    $"And, we're not quite sure if the block this exception is in should force connection closure, or allow a retry.");

        //                return 1;
        //                //// Pausing for a bit, before attempting to connect again...
        //                //await Task.Delay(_Startup_Connect_Retry_Delay, _receive_cts.Token);
        //            }
        //        }
        //        // Bottom of the outer loop.
        //        // We have left the loop.

        //        return 1;
        //    }
        //    catch (Exception ef)
        //    {
        //        // Clear the send flag, to prevent outgoing messages...
        //        this._allowsend = false;

        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ef,
        //            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //            "Exception occurred while looping, most likely from the cancellation token being disposed or null.");

        //        return -1;
        //    }
        //    finally
        //    {
        //        ms?.Dispose();

        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
        //            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_WSClient)} - " +
        //            "Receive loop method is returning.");
        //    }
        //}

        /// <summary>
        /// Processes all received messages.
        /// Returns the following:
        ///  1 = Message was handled.
        ///  0 = Message could not be deserialized or handled. Ignoring and continuing on.
        /// -1 = Registration failed. The receive loop cannot continue, and the connection must close down.
        /// </summary>
        /// <param name="rawmsg"></param>
        /// <returns></returns>
        protected int Process_ReceivedMessage_from_Client(string rawmsg)
        {
            string cid = "";

            // Each message arrives as a json string of a message envelope.
            // We need to deserialize that, recover the message type, and deserialize that.

            using var action_start = OGA.Telemetry.Lib.TelemetryBase.ProcessActivitySource?
                                        .StartActivity(this.TransportShortName.ToLower() ?? "socket" + "-message-received");

            try
            {
                // Check if a raw message handler is set...
                if(this._delOnRawMessageReceived != null)
                {
                    // Call the raw message handler...
                    this._delOnRawMessageReceived(this, rawmsg);

                    return 1;
                }

                // Recover the envelope...
                var me = JsonConvert.DeserializeObject<MessageEnvelope>(rawmsg);
                if (me == null)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage_from_Client)} - " +
                        "Received message was not a message envelope type, and could not be deserialized.");

                    return 0;
                }

                action_start?.SetTag("messageid", me.MsgId ?? "");
                // Extract any corelationid from message props, or create a new one for this message...
                try
                {
                    if (me.Props != null && me.Props.Length != 0)
                    {
                        var cidkv = me.Props.FirstOrDefault(n => n.StartsWith("corelationid"));
                        cid = cidkv?.Substring(13) ?? Guid.NewGuid().ToString();
                        action_start?.SetTag("corelationid", cid);
                    }
                } catch (Exception) { }

                // Get the message type...
                var mt = me.MessageType.ToLower();

                // Do a quick check if the given message is a local loopback...
                if(me.Scope.ToLower() == "loopback=rawmsg")
                {
                    // The client wants the given message echoed back to them.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage_from_Client)} - " +
                        "Received message set for loopback-rawmsg. Will echo it back.");

                    // But, we will clear the echo scope, so the client doesn't reply back with it...
                    me.Scope = "";

                    // Echo the message back to the client...
                    Task.Run(() => Send_MessageEnvelope_toClient_Async(me));

                    return 1;
                }

                // See if the message is something we handle, and don't pass along...
                /// Returns   1 if the message was handled.
                /// Returns   0 if the message is not internal.
                /// Returns -10 if the message is internal but could not be handled, and the connection needs to close down.
                int res = Process_InternalMessage(mt, me.Data);
                if (res == 1)
                {
                    // The message was an internal message for us only.
                    // And, it has been handled.

                    // We will return success...
                    return 1;
                }
                else if (res == -10)
                {
                    // Failed registration.
                    // Cannot continue.

                    return -1;
                }
                else if (res < 0)
                {
                    // Something was wrong with the received message.
                    // We must disregard it, and try again.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage_from_Client)} - " +
                        "Received internal message could not be processed.");

                    return 0;
                }
                // If here, the message is not an internal message.
                // We will pass it along for processing.

                // At this point, we MUST have handled any messages that this layer deals with.
                // So, any messages reaching this point are destined for other layers.
                // And so: If the client asked for raw message loopback on registration, any messages reaching this point will be echoed back, without further handling.

                // See if we are to echo raw messages back to the client...
                if(Cfg_Connection_LoopBack_All)
                {
                    // The client registered to have all messages echo'd back.

                    // Echo the message back to the client as is...
                    // No need to clear the scope, with this, as the echo request was set during connection registration.
                    Task.Run(() => Send_MessageEnvelope_toClient_Async(me));

                    return 1;
                }

                // All non-internal messages require a payload.
                // Check that a payload exists...
                if (string.IsNullOrWhiteSpace(me.Data))
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage_from_Client)} - " +
                        "Received message has no payload.");

                    return 0;
                }

                // We will let subscribers of our received delegate do deserialization...
                Task.Run(() => DispatchReceivedMessage(mt, me.Data, me.Channel, me.Scope, cid));

                return 1;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage_from_Client)} - " +
                    "Exception occurred while processing received message.");

                return 0;
            }
        }

        #endregion


        #region Handle Internal Messages

        /// <summary>
        /// Filters out any internal messages.
        /// </summary>
        /// <param name="messagetype"></param>
        /// <param name="messagedata"></param>
        /// <returns></returns>
        protected int Process_InternalMessage(string messagetype, string messagedata)
        {
            // We have a few message types that we watch out for.
            // Check if the given message is one...
            if (messagetype == "ping")
            {
                // The other end sent us a ping message.
                // This is an attempt to keep the connection alive.
                // We must reply back with a pong.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Received ping request from {(this.TransportLongName ?? "socket")} client.");

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Sending ping reply to {(this.TransportLongName ?? "socket")} client...");

                // Send a pong reply...
                Task.Run(async () =>
                {
                    // Wrap the send method in a try-catch to ensure it never throws and unwinds to the Task Scheduler base.
                    try
                    {
                        await this.SendPong_toClient_Async();
                    }
                    catch(Exception e) { }
                });

                return 1;
            }
            else if (messagetype == "pong")
            {
                // The other end sent us a pong message.
                // This is an attempt to keep the connection alive.
                // We must reply back with a pong.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Received ping reply from {(this.TransportLongName ?? "socket")} client.");

                return 1;
            }
            else if (messagetype == nameof(ConnRegisterDTO).ToLower())
            {
                // The client sent us a registration message, to tell us the connectionid, user, and deviceid of the connection.
                // Since a user can log in while a websocket connection is active, this block of code needs to allow multiple registrations
                //  over the same connection.
                // Meaning: Each subsequent registration updates the metadata for the connection (userid, connId, deviceId, etc...).

                // Attempt to deserialize the message...
                ConnRegisterDTO dto = null;
                try
                {
                    if(string.IsNullOrEmpty(messagedata))
                    {
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                            "Failed to deserialize the registration DTO. Cannot accept the client connection.");

                        return -10;
                    }

                    dto = JsonConvert.DeserializeObject<ConnRegisterDTO>(messagedata);
                    if (dto == null)
                    {
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                            "Failed to deserialize the registration DTO. Cannot accept the client connection.");

                        return -10;
                    }
                }
                catch (Exception e)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Failed to deserialize the registration DTO. Cannot accept the client connection.");

                    return -10;
                }
                // If here, we have the client's registration DTO.

                // We require the client to tell us what the connectionId is and the DeviceId.
                // The userid is optional, and only applies if a user context exists on the client.

                // Check for connectionId and DeviceId...
                if(string.IsNullOrEmpty(dto.ConnectionId))
                {
                    // The client failed to give us the connectionid.
                    // We will regard this as a malformed registration message.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Client send a registration DTO, without a connectionId. Cannot accept the client connection.");

                    return -10;
                }
                if(string.IsNullOrEmpty(dto.DeviceId))
                {
                    // The client failed to give us the DeviceId.
                    // We will regard this as a malformed registration message.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Client send a registration DTO, without a DeviceId. Cannot accept the client connection.");

                    return -10;
                }

                // As well, we do NOT allow the client to change its DeviceId, nor do we allow the client to change the connectionId.
                // If either of these are different, we must close the websocket with an error.

                // Check if the client is sending us a different ConnectionId...
                if(!string.IsNullOrEmpty(this.ClientInfo.ConnectionId))
                {
                    if(this.ClientInfo.ConnectionId != dto.ConnectionId)
                    {
                        // Our ConnectionId has been set before, and the client is telling us a new value.
                        // We will regard this as an error, and close the connection.

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                            "Client sent a registration DTO, with a different ConnectionId than earlier. We must close the client connection.");

                        return -10;
                    }
                }
                // Check if the client is sending us a different DeviceId...
                if(!string.IsNullOrEmpty(this.ClientInfo.DeviceId))
                {
                    if(this.ClientInfo.DeviceId != dto.DeviceId)
                    {
                        // Our DeviceId has been set before, and the client is telling us a new value.
                        // We will regard this as an error, and close the connection.

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                            "Client sent a registration DTO, with a different DeviceId than earlier. We must close the client connection.");

                        return -10;
                    }
                }
                // At this point, we have validated the registrtion data, and have the parameters we require.
                // We can accept the connection registration.

                // A connection registration message can be received multiple times during a connection.
                // This allows for the user context to be changed during a long-standing tcp/websocket connection.
                // It also allows for a tcp/websocket connection to be opened without a user logged in at the client.

                // Save off the old values, so the registration handler can apply any context it needs...
                var oldvals = new ClientInfo();
                oldvals.CopyFrom(this.ClientInfo);

                // Accept new values that have dedicated properties...
                // We will pull in others, after we've digested the Props array.
                this.ClientInfo.UserId = dto.UserId;
                this.ClientInfo.DeviceId = dto.DeviceId;

                // Log any change to the connectionid...
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Endpoint ClientInfo.Connectionid updated from '{(this.ClientInfo.ConnectionId ?? "")}' to '{(dto.ConnectionId ?? "")}'.");

                this.ClientInfo.ConnectionId = dto.ConnectionId;

                // These four properties were added for TCP/WSLib V2, so the cloud has more context of how to treat outgoing messages to a client.
                // They are submitted, by clients, in the Props array of a connection registration.
                string appid = "";
                string appver = "";
                // Default the language to english, if not given...
                string language = "en-us";
                // Default the ws lib version to 1, if not given...
                string wslibver = "1";
                // Default the process pid to unset, if not given...
                string pid = "";
                // Default the RuntimeId to unset, if not given...
                string runtimeid = "";

                // Process any properties of the connection request...
                try
                {
                    if(dto.Props == null)
                    {
                        // No properties.
                    }
                    else if(dto.Props.Length == 0)
                    {
                        // No properties.
                    }
                    else
                    {
                        // At least one property is defined.
                        // Property elements are of the form:
                        //  "key": "value"

                        foreach(var v in dto.Props)
                        {
                            // Skip empty properties...
                            if (string.IsNullOrEmpty(v))
                                continue;

                            var parts = v.Split(':', StringSplitOptions.RemoveEmptyEntries);
                            if(parts.Length > 1)
                            {
                                // Have a key-value pair.

                                if (parts[0].ToLower().Contains("loopback"))
                                {
                                    // Read the value...
                                    var val = parts[1].Replace("\"", "");

                                    if(val.ToLower() == "rawmsg")
                                    {
                                        // The client connection wants all messages to be echoed back to him.
                                        // We will set a property of the socket, so we know to do this.

                                        // "loopback": "rawmsg"

                                        // Create a log message if we are turning this on...
                                        if(this.Cfg_Connection_LoopBack_All == false)
                                        {
                                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                                                "Client has asked for all messages to be echoed back to it.");

                                            this.Cfg_Connection_LoopBack_All = true;
                                        }
                                    }
                                    else if(val.ToLower() == "off")
                                    {
                                        // The client connection wants to turn off WSHost-level message echo.
                                        // We will clear the property of the socket, so we know to stop doing this.

                                        // "loopback": "off"

                                        // Create a log message if we are switching this off...
                                        if(this.Cfg_Connection_LoopBack_All == true)
                                        {
                                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                                                "Client has asked to turn off WSHost-level loopback of all messages.");

                                            this.Cfg_Connection_LoopBack_All = false;
                                        }
                                    }
                                }
                                else if (parts[0].ToLower().Contains("keepalive"))
                                {
                                    // Read the value...
                                    var val = parts[1].Replace("\"", "");

                                    if(val.ToLower() == "off")
                                    {
                                        // The client connection wants to disable keepalives.
                                        // We will set a property of the socket, so we know to not require them.

                                        // Create a log message if we are disabling keepalives...
                                        if(this.Cfg_Disable_KeepAlive == false)
                                        {
                                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                                                "Client has asked for no keepalives this session.");

                                            this.Cfg_Disable_KeepAlive = true;
                                        }
                                    }
                                    else if(val.ToLower() == "on")
                                    {
                                        // The client connection wants to enable keepalives.
                                        // We will set a property of the socket, so we know to require them.

                                        // Create a log message if we are enabling keepalives...
                                        if(this.Cfg_Disable_KeepAlive == true)
                                        {
                                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                                                "Client has asked to require keepalives for this session.");

                                            this.Cfg_Disable_KeepAlive = false;
                                        }
                                    }
                                }
                                else if (parts[0].ToLower().Contains("appid"))
                                {
                                    // Read the value...
                                    appid = parts[1].Replace("\"", "");
                                }
                                else if (parts[0].ToLower().Contains("runtimeid"))
                                {
                                    // Read the value...
                                    runtimeid = parts[1].Replace("\"", "");
                                }
                                else if (parts[0].ToLower().Contains("pid"))
                                {
                                    // Read the value...
                                    pid = parts[1].Replace("\"", "");
                                }
                                else if (parts[0].ToLower().Contains("appver"))
                                {
                                    // Read the value...
                                    appver = parts[1].Replace("\"", "");
                                }
                                else if (parts[0].ToLower().Contains("language"))
                                {
                                    // Read the value...
                                    language = parts[1].Replace("\"", "");
                                }
                                else if (parts[0].ToLower().Contains(this.PropName_ClientLibVer))
                                {
                                    // Read the value...
                                    wslibver = parts[1].Replace("\"", "");
                                }
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    // Unable to parse the connection registration property block.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Client asked for all messages to be echoed to him.");
                }


                // Check that the client has a valid TCP/WSLibVersion...
                // NOTE: we pre-populate the value with "1", for V1 clients (ones that don't send a version).
                int libver = 0;
                try
                {
                    libver = Convert.ToInt32(wslibver);
                }
                catch(Exception e)
                {
                    // Client sent a non-numberic TCP/WSLib Version.
                    // So, we must treat it as a malformed connection registration.
                
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Client sent a registration DTO, with a TCP/WSLibVersion that is not an integer. We must close the client connection.");

                    return -10;
                }
                // If here, we have a numeric TCP/WSLib value.
                if(libver < 0)
                {
                    // Invalid TCP/WSLib Version.
                    // So, we must treat it as a malformed connection registration.
                
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Client sent a registration DTO, with a TCP/WSLibVersion that is not an integer. We must close the client connection.");

                    return -10;
                }

                // Check that the TCP/WSLibVersion is in an allowed range for this WSendpoint
                if(libver < 1 || libver > 2)
                {
                    // TCP/WSLib Version is not supported by this WSEndpoint.
                    // So, we must abort the connection.
                
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        $"Client sent a registration DTO, with a TCP/WSLibVersion ({libver.ToString()}) that is not supported by this Endpoint. We must close the client connection.");

                    return -10;
                }

                // Do WSLibVersion=2 property checks...
                if(libver > 1)
                {
                    // WSLib Version is at least a V2.
                    // We must enforce AppId and AppVersion property presence.
                    // And, we don't allow the AppId to change.

                    // Enforce mandatory AppId and AppVersion properties...
                    {
                        // Check that an AppId was given...
                        if(string.IsNullOrEmpty(appid))
                        {
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                                "Client sent a registration DTO, missing the AppId. We must close the client connection.");

                            return -10;
                        }

                        // Check that an AppVersion was given...
                        if(string.IsNullOrEmpty(appver))
                        {
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                                "Client sent a registration DTO, missing the AppVersion. We must close the client connection.");

                            return -10;
                        }
                    }
                    // If here, all manadatory TCP/WSLibVersion>=2 properties are present.

                    // Check if the client ever sends us an AppId that is different than the first one...
                    if(!string.IsNullOrEmpty(this.ClientInfo.AppId))
                    {
                        if(this.ClientInfo.AppId != appid)
                        {
                            // Our AppId has been set before, and the client is telling us a new value.
                            // We will regard this as an error, and close the connection.

                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                                "Client sent a registration DTO, with a different AppId than earlier. We must close the client connection.");

                            return -10;
                        }
                    }
                }

                // Several client properties should have been received, via Props array.
                // We will retrieve them, here...
                this.ClientInfo.AppId = appid ?? "";
                this.ClientInfo.AppVersion = appver ?? "";
                this.ClientInfo.Language = language ?? "";
                this.ClientInfo.LibVersion = wslibver;
                this.ClientInfo.RuntimeId = runtimeid ?? "";

                // Accept the process pid if given...
                if(!string.IsNullOrEmpty(pid))
                {
                    try
                    {
                        this.ClientInfo.Pid = Convert.ToInt32(pid);
                    }
                    catch(Exception e) { }
                }

                // Since we now register some client properties as generic strings, we will pass an instance of client info, instead of a conn registration DTO.
                var newvals = new ClientInfo();
                newvals.CopyFrom(this.ClientInfo);

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Endpoint ClientInfo update:\r\n" +
                    ClientInfo.LogDelta(oldvals, this.ClientInfo));

                // We want to send the client their registration reply before we register their connection for other clients to find.
                // But, both actions may stall our receive loop.
                // So, we will execute them on an alternate thread.
                Task.Run( async () =>
                {
                    // Send a registration reply back to the client...
                    var res1 = await SendRegistrationReply(this.ClientInfo, oldvals.ConnectionId);
                    if(res1 != 1)
                    {
                        // The reply send call failed.
                        // We don't really care if the registration reply message fails or not.
                        // But, we will, for completeness, here, only dispatch the connection registered event if the reply was sent.

                        // Skip calling the dispatch method...
                        return;
                    }

                    // We have sent the client a registration reply message.
                    // One purpose of this message is to notify the client of the ConnectionId we call it, server-side.
                    // The client will accept this server-side ConnectionId as its ConnectionId.
                    // We need to do the same, here...
                    // We store the server-side Connection in 'WSId'.
                    // We will copy that into the ConnectionId of our ClientInfo...
                    this.ClientInfo.ConnectionId = this.WSId;

                    // Send out an event that the client has registered his connection, so he can accept traffic on the websocket.
                    DispatchConnectionRegistered(oldvals, newvals);
                });

                return 1;
            }
            // If here, the message type is not a known internal message.
            // We will return that it was unhandled.

            // Return that the message was not handled, internally.
            return 0;
        }

        #endregion


        #region External Dispatch Methods

        /// <summary>
        /// Sends received messages to any connected delegates.
        /// </summary>
        /// <param name="messagetype"></param>
        /// <param name="jsondata"></param>
        /// <param name="channel"></param>
        /// <param name="scope"></param>
        /// <param name="corelationid"></param>
        protected int DispatchReceivedMessage(string messagetype, string jsondata, string channel = "", string scope = "", string corelationid = "")
        {
            using var action_start = OGA.Telemetry.Lib.TelemetryBase.ProcessActivitySource?
                                        .StartActivity(this.TransportShortName.ToLower() ?? "socket" + "-message-dispatch");
            action_start?.SetTag("corelationid", corelationid);

            try
            {
                if(string.IsNullOrEmpty(channel))
                {
                    // No channel is set.

                    // Send the message to the generic handler...
                    if (_delOnMessageReceived != null)
                    {
                        var res = _delOnMessageReceived(this, messagetype, jsondata, corelationid);

                        return res;
                    }

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchReceivedMessage)} - " +
                        $"Default message handler is not defined. Message type is: {messagetype}.");

                    return 0;
                }
                else
                {
                    // A channel is defined for the message.
                    // We will attempt to route it.

                    DelMessageReceived handler = null;

                    // Get the handler from our delegate list...
                    if(!this._ChannelMessageHandlers.TryGetValue(channel, out handler))
                    {
                        // We don't have a subscribed handler matching the channel name.

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchReceivedMessage)} - " +
                            $"Received message from channel ({channel}), but no handler is defined. Message type is: {messagetype}.");

                        return -1;
                    }
                    // If here, we have a handler for the message.

                    // Dispatch the message to the handler...
                    var res = handler(this, messagetype, jsondata, corelationid);

                    return res;
                }

                return 1;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchReceivedMessage)} - " +
                    $"Exception occurred while dispatching received message to delegate for channel ({channel}). Exception Message = {e.Message}");

                return -10;
            }
        }

        /// <summary>
        /// Sends connection closed event to any connected delegate.
        /// </summary>
        protected void DispatchConnectionClosed()
        {
            try
            {
                // Ensure we haven't already called the closure delegate...
                if (_alreadycalled_closedelegate)
                    return;
                // If here, we have not yet called the closure delegate.
                // We will call it now.

                if (_delConnectionClosed != null)
                {
                    _delConnectionClosed(this);

                    _alreadycalled_closedelegate = true;
                }
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchConnectionClosed)} - " +
                    "Exception occurred while dispatching connection closed event to a delegate.");
            }
        }

        /// <summary>
        /// Sends new connection event to any connected delegate.
        /// </summary>
        protected void DispatchConnectionRegistered(ClientInfo oldvals, ClientInfo newvals)
        {
            try
            {
                if (_delConnectionRegistration != null)
                {
                    _delConnectionRegistration(this, oldvals, newvals);
                }
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchConnectionRegistered)} - " +
                    "Exception occurred while dispatching connection registered event to a delegate.");
            }
        }

        #endregion


        #region Status Change Methods

        protected void UpdateState(eEndpoint_ConnectionStatus newstate)
        {
            UpdateState(newstate, true);
        }
        protected void UpdateState(eEndpoint_ConnectionStatus newstate, bool publish_change)
        {
            string state_change_string = "";

            if (this.State == newstate)
            {
                // No change.
                return;
            }

            // Also, we will not allow changes from aborted or completed, except to retired.
            // We will consider these as terminal states.
            if (this.State == eEndpoint_ConnectionStatus.Closed)
            {
                // No state change.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
                    "State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");

                return;
            }
            if (this.State == eEndpoint_ConnectionStatus.Lost)
            {
                // No state change.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
                    "State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");
                return;
            }
            if (this.State == eEndpoint_ConnectionStatus.Error)
            {
                // No state change.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
                    "State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");
                return;
            }
            // The state is changing.

            // Create the state change string that we will pass along.
            state_change_string = "Status changed from " + this.State.ToString() + " to " + newstate.ToString() + ".";

            // Capture the new state.
            this.State = newstate;

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
                state_change_string);

            if (publish_change == false)
            {
                // We are not to publish status changes.
                // This is usually because we are being closed down by an external owner who already knows what's up with us.
                return;
            }

            // Call the status change handler if registered.
			if (this._del_Status_Change != null)
			{
				// Call the status change handler.
				try
				{
					this._del_Status_Change(this, state_change_string);
				}
				catch (Exception) { }
			}
        }
        protected void PromoteStatus_from_NewlyOpen_to_Open()
        {
            if (this.State == eEndpoint_ConnectionStatus.Newly_Opened)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(PromoteStatus_from_NewlyOpen_to_Open)} - " +
                    "Promoting status from Newly Open to Open.");

                this.UpdateState(eEndpoint_ConnectionStatus.Open);
            }
        }

        #endregion
    }
}
