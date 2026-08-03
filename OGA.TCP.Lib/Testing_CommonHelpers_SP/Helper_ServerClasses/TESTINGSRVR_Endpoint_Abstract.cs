using Newtonsoft.Json;
using OGA.Common.Process;
using OGA.TCP.Chunking;
using OGA.TCP.Chunking.DTO;
using OGA.TCP.Chunking.Helpers;
using OGA.TCP.Messages;
using OGA.TCP.Server.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP.Server
{
    /// <summary>
    /// NOT FOR PRODUCTION USE.
    /// THIS IS A COPY OF ConnectionEntry_v1, INTENDED TO REPLICATE SERVER-SIDE FUNCTIONALITY FOR CLIENT SIDE LIBRARY TESTS.
    /// </summary>
    public abstract class TESTINGSRVR_Endpoint_Abstract : IDisposable
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

        /// <summary>
        /// List of large message receivers, keyed by messageid.
        /// </summary>
        protected Dictionary<string, LargeMsgReceiver> _largemsgreceivers;

        protected int _cfg_ReceiverTimeout;

        #endregion


        #region Public Properties

        /// <summary>
        /// Maximum allowed message size for a single frame.
        /// Prevent allocation attacks. Each packet is prefixed with a length header, so an attacker could send a fake packet with length=2GB,
        /// causing the server to allocate 2GB and run out of memory quickly.
        /// -> simply increase max packet size if you want to send around bigger files!
        /// -> 1MB per message should be more than enough.
        /// </summary>
        public int MaxFrameSize { get; set; } = OGA.TCP.Constants.CONST_MAX_MessageSize;

        /// <summary>
        /// The most payload bytes one chunk-data message may carry, when a large message is split for transfer.
        /// Zero (the default) means fit-to-frame: each chunk carries as much as fits under MaxFrameSize after
        ///     the measured chunk-message overhead.
        /// </summary>
        public int MaxChunkPayloadSize { get; set; } = 0;

        /// <summary>
        /// The most total bytes a chunked (multi-frame) message may reassemble to; the true message-size ceiling,
        ///     and the receiver's memory defense.
        /// </summary>
        public long MaxTransferSize { get; set; } = OGA.TCP.Constants.CONST_MAX_TransferSize;

        /// <summary>
        /// Superseded name for MaxFrameSize, retained so existing call sites compile; forwards to MaxFrameSize.
        /// </summary>
        [Obsolete("Use MaxFrameSize. This alias forwards to it.")]
        public int MaxMessageSize
        {
            get => this.MaxFrameSize;
            set => this.MaxFrameSize = value;
        }

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
        /// Amount of time, in seconds, that a large message receiver can sit idle before
        ///     it gets recycled, for not finishing.
        /// </summary>
        public int Cfg_ReceiverTimeout
        {
            get => _cfg_ReceiverTimeout;
            set
            {
                if (value < 5) _cfg_ReceiverTimeout = 5;
                else _cfg_ReceiverTimeout = value;
            }
        }

        /// <summary>
        /// Number of milliseconds between checkups of the connection loop's connected state.
        /// This value gets lowered if the keepalive interval goes below 5 seconds.
        /// </summary>
        public int Cfg_Connected_InnerLoop_Delay { get; set; } = 5000;

        /// <summary>
        /// Instance Id of the tcp/websocket endpoint.
        /// </summary>
        public int InstanceId { get; protected set; }

        /// <summary>
        /// Holds high level information about the connected client, such as its deviceid, user auth status, etc.
        /// </summary>
        public TESTINGSRVR_ClientInfo ClientInfo { get; protected set; }

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

        /// <summary>
        /// Set if the connection supports chunking of large messages at the channel layer.
        /// </summary>
        public bool Cfg_EnableChannelLayerChunking { get; set; } = true;

        #endregion


        #region Public Delegates

        public delegate int DelMessageReceived(TESTINGSRVR_Endpoint_Abstract mep, string messagetype, string jsondata, string corelationid);
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

        public delegate int DelRawMessageReceived(TESTINGSRVR_Endpoint_Abstract mep, byte frametype, byte[] raw);
        protected DelRawMessageReceived _delOnRawMessageReceived;
        /// <summary>
        /// Normally, this is not used as messages are exchanged as typed classes.
        /// However, attaching a handler to this will allow raw frames to be processed.
        /// The tap is a dumb-pipe mode: the assigned handler owns ALL processing, and normal
        ///     interpretation (internal messages, chunking, dispatch) is bypassed.
        /// </summary>
        public DelRawMessageReceived OnRawMessageReceived
        {
            set
            {
                _delOnRawMessageReceived = value;
            }
        }

        public delegate int DelBinaryMessageReceived(TESTINGSRVR_Endpoint_Abstract mep, string messagetype, byte[] payload, string channel, string scope, string corelationid);
        protected DelBinaryMessageReceived _delOnBinaryMessageReceived;
        /// <summary>
        /// Public delegate hook for accepting binary message traffic.
        /// This testing fork has no channel-adapter machinery, so all received binary messages land here.
        /// </summary>
        public DelBinaryMessageReceived OnBinaryMessageReceived
        {
            set
            {
                _delOnBinaryMessageReceived = value;
            }
        }

        public delegate void DelConnection(TESTINGSRVR_Endpoint_Abstract mep);
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

        public delegate void DelConnRegisterReceived(TESTINGSRVR_Endpoint_Abstract mep, TESTINGSRVR_ClientInfo oldvals, TESTINGSRVR_ClientInfo newvals);
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

        public delegate void dStatus_Change(TESTINGSRVR_Endpoint_Abstract mep, string statusupdate);
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
        public TESTINGSRVR_Endpoint_Abstract()
        {
            _instance_counter++;
            this.InstanceId = _instance_counter;

            _ChannelMessageHandlers = new Dictionary<string, DelMessageReceived>();

            ClientInfo = new TESTINGSRVR_ClientInfo();

            // Create a local unique identifier of the tcp/websocket instance.
            // We will use this value for all tracking, in case a protocol change creates collisions of the connectionId property.
            WSId = Guid.NewGuid().ToString();

            _alreadydisposed = false;

            _cfg_deadClientTimeout = 30;

#if (NET452 || NET48)
            // DateTime.UnixEpoch is not available in NET Framework versions of the DateTime.
            // So, we will simply default the received time to yesterday...
            LastReceivedTimeUTC = DateTime.UtcNow.Subtract(new TimeSpan(24, 0, 0));
#else
            LastReceivedTimeUTC = DateTime.UnixEpoch;
#endif

            this._largemsgreceivers = new Dictionary<string, LargeMsgReceiver>();

            this._cfg_ReceiverTimeout = 120;

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
            catch (Exception e)
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
        public void Populate_ConnectionEntry(TESTINGSRVR_ConnectionEntry_v1 ce)
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
#if (NET452 || NET48)
            if (!this._ChannelMessageHandlers.ContainsKey(channel))
                this._ChannelMessageHandlers.Add(channel, handler);
            else
#else
            if (!this._ChannelMessageHandlers.TryAdd(channel, handler))
#endif
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

        /// <summary>
        /// Creates a loggable string block of the current configuration for the client.
        /// </summary>
        /// <returns></returns>
        public virtual string ToLogString_Config()
        {
            StringBuilder b = new StringBuilder();

            b.AppendLine($"TransportLongName = " + TransportLongName.ToString() + ";");
            b.AppendLine($"Cfg_Connected_InnerLoop_Delay = " + Cfg_Connected_InnerLoop_Delay.ToString() + ";");
            b.AppendLine($"Cfg_Connection_LoopBack_All = " + Cfg_Connection_LoopBack_All.ToString() + ";");
            b.AppendLine($"Cfg_Disable_KeepAlive = " + Cfg_Disable_KeepAlive.ToString() + ";");
            b.AppendLine($"Cfg_DeadClientTimeout = " + Cfg_DeadClientTimeout.ToString() + ";");
            b.AppendLine($"Cfg_EnableChannelLayerChunking = " + Cfg_EnableChannelLayerChunking.ToString() + ";");
            b.AppendLine($"Cfg_ReceiverTimeout = " + Cfg_ReceiverTimeout.ToString() + ";");
            b.AppendLine($"Cfg_We_Require_Clients_to_Be_Chatty = " + Cfg_We_Require_Clients_to_Be_Chatty.ToString() + ";");
            b.AppendLine($"MaxMessageSize = " + MaxMessageSize.ToString() + ";");
            b.AppendLine($"***End of Configuration***");

            return b.ToString();
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

                            // Prune any old/stale large message receivers...
                            this.PruneStaleLargeMessageReceivers();

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
                            await Task.Delay(Cfg_Connected_InnerLoop_Delay, _cts.Token);
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
                        try { this._receive_cts?.Cancel(); } catch (Exception) { }
                        await Task.Delay(200);
                        try { this._receive_cts?.Dispose(); } catch (Exception) { }
                        await Task.Delay(200);
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
                    } catch (Exception ex) { }

                    // Call the receiver loop if the transport needs one...
                    if (this.Cfg_TransportRequiresReceiverLoop)
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

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
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

            // Clear out any large message receivers...
            lock (this._receiverslock) { this._largemsgreceivers.Clear(); }
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
            if (!this._allowsend)
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

            // Size handling happens downstream, byte-true, after the envelope is encoded:
            //  the serialized-object send diverts oversized messages to the chunking layer.

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
        protected async Task<int> SendRegistrationReply(TESTINGSRVR_ClientInfo ci, string oldconnid)
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
            if (string.IsNullOrEmpty(oldconnid))
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
            //using var action_start = OGA.Telemetry.Lib.TelemetryBase.ProcessActivitySource?
            //                            .StartActivity(this.TransportLongName.ToLower() ?? "socket" + "-outgoing-jsonobject-send");
            //action_start?.SetTag("corelationid", corelationid);

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_SerializedObject_toClient_Async)} - " +
                "Sending serialized message to client...");

            // Ensure any json string exists at this point...
            if (jsonobject == null)
                jsonobject = "";

            // Create and stuff an envelope...
            MessageEnvelope me = new MessageEnvelope();
            me.MsgId = GetNextMessageId();
            me.SentTimeUTC = DateTime.UtcNow;
            // Ensure that the data section is never null...
            me.Data = jsonobject ?? "";
            me.Channel = channel;
            me.Scope = scope;
            me.MessageType = objecttype;
            me.Props = new string[] { "corelationid=" + corelationid ?? "" };

            // Encode the envelope, so the size decision is byte-true...
            var jsonmsg = JsonConvert.SerializeObject(me);
            byte[] body = Encoding.UTF8.GetBytes(jsonmsg);

            // An encoded body that fits in one frame ships directly...
            if (body.Length <= this.MaxFrameSize)
                return await this.Send_Frame_toClient_Async(FrameTypes.Json, body);

            // Oversized: the chunking layer splits the encoded bytes, when enabled...
            if (!this.Cfg_EnableChannelLayerChunking)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_SerializedObject_toClient_Async)} - " +
                    $"Message is too large ({body.Length.ToString()} bytes) to send in one frame, and chunking is disabled.");

                return -10;
            }

            return await this.Send_ChunkedBody_toClient_Async(FrameTypes.Json, body, channel, scope, corelationid);
        }

        /// <summary>
        /// Transfers an oversized encoded body via the chunking layer: a start declaration, binary segments of
        ///     raw byte slices, and an end declaration — interleaving with other channels' traffic between frames.
        /// Used by both the json and binary send paths; the frame type tells the receiver how to re-inject
        ///     the reassembled bytes.
        /// Returns 1 on success, 0 when cancelled, negatives on failure (a best-effort cancel is sent so the
        ///     receiver tears down its transfer state).
        /// </summary>
        /// <param name="frametype">Frame type of the original message body.</param>
        /// <param name="body">The encoded body bytes to transfer.</param>
        /// <param name="channel">Channel of the original message.</param>
        /// <param name="scope">Scope of the original message.</param>
        /// <param name="corelationid">Correlation id of the original message, or empty.</param>
        /// <returns></returns>
        protected async Task<int> Send_ChunkedBody_toClient_Async(byte frametype, byte[] body, string channel, string scope, string corelationid)
        {
            // Setup our large message sender...
            var lms = new LargeMsgSender(OGA.SharedKernel.Logging_Base.Logger_Ref);
            lms.MaxFrameSize = this.MaxFrameSize;
            lms.MaxChunkPayloadSize = this.MaxChunkPayloadSize;

            // Load the encoded bytes to be chunked out, keyed by a fresh transfer id...
            var resload = lms.Load(GetNextMessageId(), frametype, body, channel, scope, corelationid);
            if (resload != 1)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_ChunkedBody_toClient_Async)} - " +
                    $"Failed to load outgoing message for chunking.");

                return -1;
            }

            // Send the transfer: control messages ride the json path, segments ride binary frames.
            // Each frame send awaits the write semaphore, so other traffic interleaves between segments.
            var ressend = await lms.SendChunksAsync(
                this.Send_SerializedObject_toClient_Async,
                this.Send_Frame_toClient_Async,
                this._cts != null ? this._cts.Token : CancellationToken.None);

            if (ressend == 0)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_ChunkedBody_toClient_Async)} - " +
                    $"Send was cancelled while conveying chunked message to the remote endpoint.");

                return 0;
            }
            else if (ressend < 0)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_ChunkedBody_toClient_Async)} - " +
                    $"Send failed while conveying chunked message to the remote endpoint.");

                return -1;
            }

            return 1;
        }

        /// <summary>
        /// Sends a raw binary payload to the connected client, routed by the same channel, scope, and
        ///     correlation metadata json messages carry.
        /// The payload rides the wire uncoded (no base64 or escaping cost); oversized payloads are transparently
        ///     chunked when chunking is enabled.
        /// Returns 1 on success, 0 if the session is not ready to send, negatives on error.
        /// </summary>
        /// <param name="payload">The raw payload bytes to send.</param>
        /// <param name="channel">Optional channel name.</param>
        /// <param name="scope">Optional scope value.</param>
        /// <param name="corelationid">Optional correlation id, carried for tracing.</param>
        /// <param name="messagetype">Optional message type name, for consumer routing.</param>
        /// <returns></returns>
        public async Task<int> SendBinary_toClient(byte[] payload, string channel = "", string scope = "", string corelationid = "", string messagetype = "")
        {
            if (payload == null)
                return -1;

            // Do we allow sending...
            if (!this._allowsend)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(SendBinary_toClient)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is not currently open for sending messages.");

                return 0;
            }

            // Compose the binary message body: routing header, then the raw payload...
            var header = new BinaryMessageHeader();
            header.MsgId = GetNextMessageId();
            header.SentTimeUTC = DateTime.UtcNow;
            header.MessageType = messagetype ?? "";
            header.Channel = channel ?? "";
            header.Scope = scope ?? "";
            header.Props = new string[] { "corelationid=" + (corelationid ?? "") };

            var body = header.ComposeBody(payload);

            // A body that fits in one frame ships directly...
            if (body.Length <= this.MaxFrameSize)
                return await this.Send_Frame_toClient_Async(FrameTypes.Binary, body);

            // Oversized: the chunking layer splits the encoded bytes, when enabled...
            if (!this.Cfg_EnableChannelLayerChunking)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(SendBinary_toClient)} - " +
                    $"Binary message is too large ({body.Length.ToString()} bytes) to send in one frame, and chunking is disabled.");

                return -10;
            }

            return await this.Send_ChunkedBody_toClient_Async(FrameTypes.Binary, body, channel ?? "", scope ?? "", corelationid ?? "");
        }

        /// <summary>
        /// Accepts a prepared message envelope, and sends it to the client as a json frame.
        /// Retained for the loopback echo paths, which resend a received envelope as-is.
        /// </summary>
        /// <param name="me"></param>
        /// <returns></returns>
        protected async Task<int> Send_MessageEnvelope_toClient_Async(MessageEnvelope me)
        {
            // Serialize the envelope and convert it to bytes...
            var jsonmsg = JsonConvert.SerializeObject(me);
            byte[] d = Encoding.UTF8.GetBytes(jsonmsg);

            return await this.Send_Frame_toClient_Async(FrameTypes.Json, d);
        }

        /// <summary>
        /// The single frame-send choke point: every outgoing frame of either type funnels through here.
        /// Verifies the endpoint can send, serializes the write through the send semaphore (so frames never
        ///     interleave mid-frame on the wire), and maps transport failures to result codes.
        /// Returns 1 on success, 0 when not connected or the transport refuses, negatives on error.
        /// </summary>
        /// <param name="frametype">The frame's type byte, from the FrameTypes registry.</param>
        /// <param name="body">The frame's encoded body bytes.</param>
        /// <returns></returns>
        protected async Task<int> Send_Frame_toClient_Async(byte frametype, byte[] body)
        {
            try
            {
                // Check if connected...
                if (_alreadydisposed)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                        $"{(this.TransportLongName ?? "Socket")} is already disposed.");

                    return -1;
                }
                if (!IsConnected)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                        $"{(this.TransportLongName ?? "Socket")} is not connected.");

                    return 0;
                }

                // Enforce the frame cap byte-true, at the last common point before the wire...
                if ((body?.Length ?? 0) > this.MaxFrameSize)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                        $"Frame body ({body.Length.ToString()} bytes) exceeds MaxFrameSize ({this.MaxFrameSize.ToString()}). " +
                        "Oversized messages must go through the chunking layer.");

                    return -10;
                }

                //************************************************************************************************************
                // Start Send Thread Lock
                //************************************************************************************************************
                // Enter a thread lock, to prevent garbling of sent data...
                using (await _write_semaphore.WaitAsync())
                {
                    // Send the message...
                    var res = await this.RawTransportSend(frametype, body);
                    if (res >= 1)
                    {
                        // Send was successful.
                        // We will return after the using releases our send mutex.
                    }
                    else
                    {
                        // Call failed.
                        // We will return, here, and the using will release our send mutex.
                        return res;
                    }
                }
                //************************************************************************************************************
                // End Send Thread Lock
                //************************************************************************************************************

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                    "Message sent to remote endpoint.");

                return 1;
            }
            catch (WebSocketException wse)
            {
                // Websocket exception occurred.
                // Meaning, the websocket has been closed by the other end.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} was closed by the other end, and cannot send messages.");

                return -1;
            }
            catch (System.Net.Sockets.SocketException)
            {
                // TCPsocket exception occurred.
                // Meaning, the tcpsocket has been closed by the other end.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} was closed by the other end, and cannot send messages.");

                return -1;
            }
            catch (ObjectDisposedException ode)
            {
                // Socket is disposed.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is disposed, and cannot accept messages.");

                return -1;
            }
            catch (InvalidOperationException ioe)
            {
                // Socket is not open.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is not open, and cannot accept messages.");

                return 0;
            }
            catch (Exception e)
            {
                // Unknown exception type...

                var f = e?.GetType()?.FullName ?? "";

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Frame_toClient_Async)} - " +
                    $"Unknown exception type occurred ({f}) while attempting to send message over {(this.TransportLongName?.ToLower() ?? "socket")}.");

                return -2;
            }
        }

        /// <summary>
        /// Override this method with the transport-specific means to send one frame.
        /// The TCP transport prepends the five-byte preamble (4-byte little-endian body length plus the
        ///     frame-type byte from the FrameTypes registry).
        /// No need for any try-catch, as the call to this method is safely wrapped.
        /// </summary>
        /// <param name="frametype">The frame's type byte, from the FrameTypes registry.</param>
        /// <param name="body">The frame's body bytes. May be empty, never null.</param>
        /// <returns></returns>
        abstract protected Task<int> RawTransportSend(byte frametype, byte[] body);

        /// <summary>
        /// Call this to create the identifier for each message to be sent.
        /// </summary>
        /// <returns></returns>
        protected string GetNextMessageId()
        {
            // Create a new messageid...
            // This is done in a thread-safe manner, as the send is multi-threaded.
            // And, we do it, here, since both single message and chunked messages use the same message identifier.
            var newval = Interlocked.Increment(ref _last_messageid);

            return newval.ToString();
        }

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

        //                    // Changed memorystream creation to a using block, to control disposal...
        //                    using (var ms = new MemoryStream())
        //                      {
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
                                //} // end using (var ms = new MemoryStream())
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
        //                //await Task.Delay(Cfg_Startup_Connect_Retry_Delay, _receive_cts.Token);
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
        /// First-handler of any received frame: the merge point of the receive paths.
        /// Order of processing: the raw tap (exclusive, when assigned), then interpretation by frame type —
        ///     json bodies flow through envelope processing (internal messages, chunking, dispatch),
        ///     binary bodies through routing-header parsing and binary dispatch.
        /// Returns the following:
        ///  1 = Message was handled.
        ///  0 = Message could not be deserialized or handled. Ignoring and continuing on.
        /// -1 = Registration failed. The receive loop cannot continue, and the connection must close down.
        /// </summary>
        /// <param name="frametype">The frame's type byte, from the FrameTypes registry.</param>
        /// <param name="body">The frame's body bytes.</param>
        /// <returns></returns>
        protected int Process_ReceivedMessage_from_Client(byte frametype, byte[] body)
        {
            try
            {
                // Check if a raw message handler is set...
                // The tap is a dumb-pipe mode: the assigned handler owns ALL processing, and everything
                //  below (internal messages, chunking, dispatch) is bypassed.
                var tap = this._delOnRawMessageReceived;
                if (tap != null)
                {
                    // Call the raw message handler...
                    tap(this, frametype, body);

                    return 1;
                }

                if (frametype == FrameTypes.Json)
                {
                    // Json body: decode once, here, and hand to envelope processing...
                    var rawmsg = Encoding.UTF8.GetString(body);
                    return this.Process_ReceivedJsonMessage_from_Client(rawmsg);
                }

                if (frametype == FrameTypes.Binary)
                {
                    return this.Process_ReceivedBinaryMessage_from_Client(body);
                }

                // The receive loop validates frame types before delivery, so this is unreachable in practice...
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage_from_Client)} - " +
                    $"Received frame of unhandled type ({frametype.ToString()}).");

                return 0;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage_from_Client)} - " +
                    "Exception occurred while processing received frame.");

                return 0;
            }
        }

        /// <summary>
        /// Parses a binary message body (routing header plus raw payload) and dispatches it.
        /// Chunk-data messages are diverted to the chunking layer by their message type; everything else is
        ///     handed to the consumer's binary delegate (this testing fork has no channel-adapter machinery).
        /// Returns 1 handled, 0 recoverable problem (message disregarded).
        /// </summary>
        /// <param name="body">The binary frame's body bytes.</param>
        /// <returns></returns>
        protected int Process_ReceivedBinaryMessage_from_Client(byte[] body)
        {
            BinaryMessageHeader header;
            byte[] payload;
            var parseres = BinaryMessageHeader.TryParseBody(body, out header, out payload);
            if (parseres != 1)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedBinaryMessage_from_Client)} - " +
                    $"Received binary message body was malformed (parse result {parseres.ToString()}). Message disregarded.");

                return 0;
            }

            var mt = (header.MessageType ?? "").ToLower();

            // Recover any correlation id from the header props, so dispatch can carry it to handlers...
            string cid = "";
            try
            {
                if (header.Props != null)
                {
                    foreach (var p in header.Props)
                    {
                        if (p != null && p.StartsWith("corelationid="))
                        {
                            cid = p.Substring(13);
                            break;
                        }
                    }
                }
            }
            catch (Exception) { }

            // Divert chunk-data messages to the chunking layer...
            if (mt == ChunkingConstants.CONST_ChunkSegment_MessageType)
            {
                return this.Process_ChunkSegment(header, payload);
            }

            // Hand the message to the consumer's binary delegate...
            var d = this._delOnBinaryMessageReceived;
            if (d != null)
            {
                var res = d(this, mt, payload, header.Channel ?? "", header.Scope ?? "", cid);
                return res >= 1 ? 1 : 0;
            }

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedBinaryMessage_from_Client)} - " +
                $"Binary message received without an assigned handler. Message type is: {mt}.");

            return 0;
        }

        /// <summary>
        /// First-handler of a received json message body.
        /// Will hydrate it to the standard message envelope, and dispense it as internal or consumer message.
        /// Returns the following:
        ///  1 = Message was handled.
        ///  0 = Message could not be deserialized or handled. Ignoring and continuing on.
        /// -1 = Registration failed. The receive loop cannot continue, and the connection must close down.
        /// </summary>
        /// <param name="rawmsg"></param>
        /// <returns></returns>
        protected int Process_ReceivedJsonMessage_from_Client(string rawmsg)
        {
            string cid = "";

            // Each message arrives as a json string of a message envelope.
            // We need to deserialize that, recover the message type, and deserialize that.

            //using var action_start = OGA.Telemetry.Lib.TelemetryBase.ProcessActivitySource?
            //                            .StartActivity(this.TransportShortName.ToLower() ?? "socket" + "-message-received");

            try
            {
                // Recover the envelope...
                var me = JsonConvert.DeserializeObject<MessageEnvelope>(rawmsg);
                if (me == null)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage_from_Client)} - " +
                        "Received message was not a message envelope type, and could not be deserialized.");

                    return 0;
                }

                //action_start?.SetTag("messageid", me.MsgId ?? "");
                // Extract any corelationid from message props, or create a new one for this message...
                try
                {
                    if (me.Props != null && me.Props.Length != 0)
                    {
                        var cidkv = me.Props.FirstOrDefault(n => n.StartsWith("corelationid"));
                        cid = cidkv?.Substring(13) ?? Guid.NewGuid().ToString();
                        //action_start?.SetTag("corelationid", cid);
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

                // Here, we intercept the chunking layer's control messages (start, end, cancel).
                // Chunk data does not appear on this path: segments ride binary frames, intercepted by their
                //  routing-header message type in the binary processing path.
                if(mt == nameof(ChunkStartDTO).ToLower() ||
                    mt == nameof(ChunkEndDTO).ToLower() ||
                    mt == nameof(ChunkCancelDTO).ToLower()
                    )
                {
                    // Received message is a chunking control message.
                    // Chunk control is synchronous work (no awaits), so its failures are observable here.
                    return this.ProcessChunkingControl(mt, me.Data);
                }
                // Not a chunking message type.
                // We will dispatch it as normal.

                // We will let subscribers of our received delegate do deserialization...
                _= Task.Run(() => DispatchReceivedMessage(mt, me.Data, me.Channel, me.Scope, cid));

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

        /// <summary>
        /// Guards the large-message receiver registry: registrations arrive on the receive path while the
        ///     connection loop prunes, so every access is serialized here.
        /// </summary>
        private readonly object _receiverslock = new object();

        /// <summary>
        /// Handles the chunking layer's json control messages: start (create a receiver), end (validate
        ///     completeness and re-inject the reassembled message), and cancel (tear down the transfer).
        /// Synchronous by design: chunk control performs no awaits, so failures are observable by the caller,
        ///     and segment ordering cannot slip past control ordering.
        /// Returns 1 handled, 0 for malformed or unknown-transfer messages (disregarded).
        /// </summary>
        /// <param name="messagetype">Lowercased control message type.</param>
        /// <param name="data">The control DTO json.</param>
        /// <returns></returns>
        private int ProcessChunkingControl(string messagetype, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                    "Received chunking control message with no payload.");

                return 0;
            }

            if (messagetype == nameof(ChunkStartDTO).ToLower())
            {
                // The far end is announcing a chunked transfer...
                ChunkStartDTO dto;
                try
                {
                    dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ChunkStartDTO>(data);
                }
                catch (Exception)
                {
                    dto = null;
                }
                if (dto == null || string.IsNullOrEmpty(dto.TransferId))
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                        "Failed to deserialize chunk start message.");

                    return 0;
                }

                // Stand up a receiver for the transfer, enforcing the local transfer-size cap...
                var lmr = new LargeMsgReceiver();
                var res = lmr.AcceptChunkStart(dto, this.MaxTransferSize);
                if (res != 1)
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                        $"Rejected chunk start for transfer ({dto.TransferId}): result {res.ToString()} " +
                        $"(declared {dto.TotalSize.ToString()} bytes; local MaxTransferSize {this.MaxTransferSize.ToString()}).");

                    return 0;
                }

                lock (this._receiverslock)
                {
                    // A duplicate transfer id is a protocol error: refuse the new transfer, keep the old...
                    if (this._largemsgreceivers.ContainsKey(dto.TransferId))
                    {
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                            $"Received duplicate chunk start for transfer ({dto.TransferId}). New transfer refused.");

                        return 0;
                    }

                    this._largemsgreceivers.Add(dto.TransferId, lmr);
                }

                return 1;
            }
            else if (messagetype == nameof(ChunkEndDTO).ToLower())
            {
                // The far end has finished a transfer: validate completeness and release the message...
                ChunkEndDTO dto;
                try
                {
                    dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ChunkEndDTO>(data);
                }
                catch (Exception)
                {
                    dto = null;
                }
                if (dto == null || string.IsNullOrEmpty(dto.TransferId))
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                        "Failed to deserialize chunk end message.");

                    return 0;
                }

                LargeMsgReceiver lmr;
                lock (this._receiverslock)
                {
                    if (!this._largemsgreceivers.TryGetValue(dto.TransferId, out lmr))
                    {
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                            $"Received chunk end for unknown transfer ({dto.TransferId}).");

                        return 0;
                    }

                    // The transfer is complete or dead either way: its state leaves the registry now...
                    this._largemsgreceivers.Remove(dto.TransferId);
                }

                byte frametype;
                byte[] body;
                var res = lmr.AcceptChunkEnd(out frametype, out body);
                if (res != 1)
                {
                    // Incomplete transfers are discarded, never dispatched...
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                        $"Discarded incomplete chunked transfer ({dto.TransferId}): result {res.ToString()}.");

                    return 0;
                }

                // Re-inject the reassembled message at the top of normal processing, marked as reassembled
                //  so nested chunk messages are rejected...
                return this.Process_ReassembledMessage(frametype, body);
            }
            else if (messagetype == nameof(ChunkCancelDTO).ToLower())
            {
                // The far end abandoned a transfer: tear down its state immediately...
                ChunkCancelDTO dto;
                try
                {
                    dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ChunkCancelDTO>(data);
                }
                catch (Exception)
                {
                    dto = null;
                }
                if (dto == null || string.IsNullOrEmpty(dto.TransferId))
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                        "Failed to deserialize chunk cancel message.");

                    return 0;
                }

                lock (this._receiverslock)
                {
                    this._largemsgreceivers.Remove(dto.TransferId);
                }

                return 1;
            }

            // Unknown chunking message type...
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingControl)} - " +
                $"Received unknown chunking message type: ({(messagetype ?? "")}).");

            return 0;
        }

        /// <summary>
        /// Accepts a binary chunk-segment message: locates the transfer's receiver by the header's transfer id,
        ///     and accumulates the payload slice at the declared offset.
        /// A rejected segment (out-of-order, over-declared) abandons the transfer.
        /// Returns 1 handled, 0 disregarded.
        /// </summary>
        /// <param name="header">The segment's routing header, carrying transfer id and offset props.</param>
        /// <param name="payload">The segment's payload slice.</param>
        /// <returns></returns>
        private int Process_ChunkSegment(BinaryMessageHeader header, byte[] payload)
        {
            // Recover the transfer id and offset from the header props...
            string transferid = "";
            long offset = -1;
            try
            {
                if (header.Props != null)
                {
                    foreach (var p in header.Props)
                    {
                        if (p == null)
                            continue;
                        if (p.StartsWith(ChunkingConstants.CONST_Prop_TransferId))
                            transferid = p.Substring(ChunkingConstants.CONST_Prop_TransferId.Length);
                        else if (p.StartsWith(ChunkingConstants.CONST_Prop_Offset))
                            long.TryParse(p.Substring(ChunkingConstants.CONST_Prop_Offset.Length), out offset);
                    }
                }
            }
            catch (Exception) { }

            if (string.IsNullOrEmpty(transferid) || offset < 0)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ChunkSegment)} - " +
                    "Received chunk segment with missing transfer id or offset.");

                return 0;
            }

            LargeMsgReceiver lmr;
            lock (this._receiverslock)
            {
                if (!this._largemsgreceivers.TryGetValue(transferid, out lmr))
                {
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ChunkSegment)} - " +
                        $"Received chunk segment for unknown transfer ({transferid}).");

                    return 0;
                }
            }

            var res = lmr.AcceptSegment(offset, payload);
            if (res != 1)
            {
                // A rejected segment abandons the whole transfer: remove its state...
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ChunkSegment)} - " +
                    $"Rejected chunk segment at offset ({offset.ToString()}) for transfer ({transferid}): result {res.ToString()}. Transfer abandoned.");

                lock (this._receiverslock)
                {
                    this._largemsgreceivers.Remove(transferid);
                }

                return 0;
            }

            return 1;
        }

        /// <summary>
        /// Re-injects a reassembled message into normal processing as its original frame type.
        /// A reassembled body that itself parses to chunking messages is a protocol error (nesting is not
        ///     legal), and is rejected here rather than recursing.
        /// Returns 1 handled, 0 disregarded.
        /// </summary>
        /// <param name="frametype">The original message's frame type.</param>
        /// <param name="body">The reassembled body bytes.</param>
        /// <returns></returns>
        private int Process_ReassembledMessage(byte frametype, byte[] body)
        {
            try
            {
                if (frametype == FrameTypes.Json)
                {
                    var rawmsg = Encoding.UTF8.GetString(body);

                    // Guard against nested chunking...
                    var me = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageEnvelope>(rawmsg);
                    if (me == null)
                    {
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReassembledMessage)} - " +
                            "Reassembled message was not a message envelope.");

                        return 0;
                    }

                    var mt = (me.MessageType ?? "").ToLower();
                    if (mt == nameof(ChunkStartDTO).ToLower() || mt == nameof(ChunkEndDTO).ToLower() ||
                        mt == nameof(ChunkCancelDTO).ToLower())
                    {
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReassembledMessage)} - " +
                            "Reassembled message contained nested chunking messages. Protocol error; message discarded.");

                        return 0;
                    }

                    return this.Process_ReceivedJsonMessage_from_Client(rawmsg);
                }

                if (frametype == FrameTypes.Binary)
                {
                    // Guard against nested chunking: a reassembled binary body must not be a chunk segment...
                    BinaryMessageHeader header;
                    byte[] payload;
                    if (BinaryMessageHeader.TryParseBody(body, out header, out payload) == 1)
                    {
                        var mt = (header.MessageType ?? "").ToLower();
                        if (mt == ChunkingConstants.CONST_ChunkSegment_MessageType)
                        {
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReassembledMessage)} - " +
                                "Reassembled message contained a nested chunk segment. Protocol error; message discarded.");

                            return 0;
                        }
                    }

                    return this.Process_ReceivedBinaryMessage_from_Client(body);
                }

                return 0;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReassembledMessage)} - " +
                    "Exception occurred while processing reassembled message.");

                return 0;
            }
        }

        /// <summary>
        /// Removes any large message receivers that haven't been updated in a while.
        /// Runs on the connection loop's tick; accesses the registry under its lock, so a segment arriving
        ///     mid-prune cannot corrupt the collection.
        /// </summary>
        private void PruneStaleLargeMessageReceivers()
        {
            // Get the current time...
            var ctime = DateTime.UtcNow;

            lock (this._receiverslock)
            {
                if (this._largemsgreceivers.Count == 0)
                    return;

                List<string> entriestodelete = new List<string>();

                // Loop through each receiver...
                foreach (var r in this._largemsgreceivers)
                {
                    if (r.Value == null)
                    {
                        entriestodelete.Add(r.Key);
                        continue;
                    }

                    // Calculate when the receiver expires...
                    var etime = r.Value.LastReceivedTimeUTC.AddSeconds(this._cfg_ReceiverTimeout);

                    // See if it expired...
                    if (etime.CompareTo(ctime) < 0)
                    {
                        // The receiver has expired.
                        entriestodelete.Add(r.Key);
                    }
                }

                // Delete entries...
                foreach (var d in entriestodelete)
                    this._largemsgreceivers.Remove(d);
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
                var oldvals = new TESTINGSRVR_ClientInfo();
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

                // These two properties are included in the v1 client type.
                // These locals start null, meaning "not present in this registration".
                // Recognized props overwrite them; omitted props are back-filled from the recorded
                //  ClientInfo values below, so a re-registration that omits a prop preserves what the
                //  connection already declared instead of wiping or downgrading it (OI-29).
                string pid = null;
                string runtimeid = null;

                // These four properties were added for TCP/WSLib V2, so the cloud has more context of how to treat outgoing messages to a client.
                // They are submitted, by clients, in the Props array of a connection registration.
                string appid = null;
                string appver = null;
                string language = null;
                string wslibver = null;

                // Process any properties of the connection request...
                // Property elements are of the form: "key":"value".
                // Fragments are parsed by PropString (values keep their colons; escaping is honored),
                //  and keys are matched by exact, case-insensitive equality — never by substring — so
                //  keys cannot hijack one another and branch order does not matter (OI-29).
                try
                {
                    if(dto.Props != null)
                    {
                        foreach(var v in dto.Props)
                        {
                            // Skip empty properties...
                            if (string.IsNullOrEmpty(v))
                                continue;

                            string key;
                            string val;
                            if (!PropString.TryParse(v, out key, out val))
                                continue;

                            if (string.Equals(key, "loopback", StringComparison.OrdinalIgnoreCase))
                            {
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
                            else if (string.Equals(key, "keepalive", StringComparison.OrdinalIgnoreCase))
                            {
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
                            else if (string.Equals(key, "appid", StringComparison.OrdinalIgnoreCase))
                            {
                                appid = val;
                            }
                            else if (string.Equals(key, "appver", StringComparison.OrdinalIgnoreCase))
                            {
                                appver = val;
                            }
                            else if (string.Equals(key, "language", StringComparison.OrdinalIgnoreCase))
                            {
                                language = val;
                            }
                            else if (string.Equals(key, "runtimeid", StringComparison.OrdinalIgnoreCase))
                            {
                                runtimeid = val;
                            }
                            else if (string.Equals(key, "pid", StringComparison.OrdinalIgnoreCase))
                            {
                                pid = val;
                            }
                            else if (string.Equals(key, this.PropName_ClientLibVer, StringComparison.OrdinalIgnoreCase))
                            {
                                wslibver = val;
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    // Unable to parse the connection registration property block.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Failed to parse the connection registration property block.");
                }

                // Back-fill props not present in this registration from the recorded values, so a
                //  re-registration that omits a prop preserves what the connection already declared.
                // First-registration defaults apply when nothing is recorded yet: language defaults to
                //  english, and a version-less registration is a V1 client...
                if (appid == null)
                    appid = this.ClientInfo.AppId ?? "";
                if (appver == null)
                    appver = this.ClientInfo.AppVersion ?? "";
                if (runtimeid == null)
                    runtimeid = this.ClientInfo.RuntimeId ?? "";
                if (language == null)
                    language = string.IsNullOrEmpty(this.ClientInfo.Language) ? "en-us" : this.ClientInfo.Language;
                if (wslibver == null)
                    wslibver = string.IsNullOrEmpty(this.ClientInfo.LibVersion) ? "1" : this.ClientInfo.LibVersion;


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
                if(libver < 1 || libver > 3)
                {
                    // TCP/WSLib Version is not supported by this WSEndpoint.
                    // So, we must abort the connection.
                
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        $"Client sent a registration DTO, with a TCP/WSLibVersion ({libver.ToString()}) that is not supported by this Endpoint. We must close the client connection.");

                    return -10;
                }

                // Do WSLibVersion>=2 property checks...
                if(libver > 1)
                {
                    // WSLib Version is at least a V2.
                    // V2 added mandatory client application properties, and each later version carries that
                    //  registration contract forward (owner decision, OI-50).

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
                }

                // We don't allow the AppId to change, once given.
                // This applies to any registration that supplies one, regardless of version.
                if(!string.IsNullOrEmpty(appid) && !string.IsNullOrEmpty(this.ClientInfo.AppId))
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

                // Several client properties should have been received, via Props array.
                // We will retrieve them, here...
                // Locals for omitted props were back-filled from the recorded values above, so these
                //  assignments cannot wipe or downgrade previously-recorded identity (OI-29)...
                this.ClientInfo.AppId = appid;
                this.ClientInfo.AppVersion = appver;
                this.ClientInfo.Language = language;
                this.ClientInfo.LibVersion = wslibver;
                this.ClientInfo.RuntimeId = runtimeid;

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
                var newvals = new TESTINGSRVR_ClientInfo();
                newvals.CopyFrom(this.ClientInfo);

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Endpoint ClientInfo update:\r\n" +
                    TESTINGSRVR_ClientInfo.LogDelta(oldvals, this.ClientInfo));

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
            //using var action_start = OGA.Telemetry.Lib.TelemetryBase.ProcessActivitySource?
            //                            .StartActivity(this.TransportShortName.ToLower() ?? "socket" + "-message-dispatch");
            //action_start?.SetTag("corelationid", corelationid);

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
        protected void DispatchConnectionRegistered(TESTINGSRVR_ClientInfo oldvals, TESTINGSRVR_ClientInfo newvals)
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

