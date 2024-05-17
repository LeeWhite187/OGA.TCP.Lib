using Newtonsoft.Json;
using OGA.TCP.Chunking.DTO;
using OGA.TCP.Chunking.Helpers;
using OGA.TCP.ClientAdapters;
using OGA.TCP.Messages;
using OGA.TCP.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP.SessionLayer
{
    /// <summary>
    /// Represents a client-side tcp/ws socket endpoint.
    /// Provides framed message transfer with channel, scope, and custom properties.
    /// This abstract class gets derived for each transport type.
    /// Your implementation will need to provide a delegate-based or adapter-based method of dispatching channel-assigned messages.
    /// </summary>
    abstract public class Client_v1_Abstract : IDisposable
    {
        #region Private Fields

        protected NLog.ILogger Logger;

        protected string _classname;

        static protected int _instance_counter;

        /// <summary>
        /// Duration, in seconds, after the most recent message that a keep alive is performed.
        /// This controls the period between ping-pong checks.
        /// </summary>
        protected int _cfg_keepAliveInterval;

        protected int _keepAliveStatus;

        static protected int _last_messageid = 0;

        protected CancellationTokenSource _cts;

        protected bool disposedValue;

        protected CancellationTokenSource _receive_cts;

        protected int connlost_truecounter;

        /// <summary>
        /// Create a semaphore to enforce thread safety when sending data to the client.
        /// </summary>
        protected SemaphoreSlim _write_semaphore = new SemaphoreSlim(1, 1);

        protected bool _allowsend;

        /// <summary>
        /// Simple lock to ensure the connection closure delegate armed flag is set and cleared, without a race.
        /// </summary>
        protected object lclock = new object();
        /// <summary>
        /// Tells the Connection Lost delegate logic that it can fire.
        /// This flag uses 4-part handshaking between the Connection Loop and the Connection Lost delegate.
        /// It is set on connection open, and cleared when the connection lost delegate fires.
        /// </summary>
        protected bool _connectionclosuredelegate_armed;

        /// <summary>
        /// Holds the transport-agnostic connection data.
        /// For TCP sockets, this would be host:port.
        /// For websockets, this would be the connection url.
        /// </summary>
        protected string _connection_string;

        /// <summary>
        /// Indicates when the connection has received a registration reply.
        /// The connection loop logic uses this flag to know when it can continue processing the new connection.
        /// This flag is cleared each time a new connection is attempted.
        /// It is set by the receive loop when it successfully handles a registration reply message.
        /// </summary>
        protected bool _registrationreplyreceived;

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
        /// Duration, in seconds, after the most recent message that a keep alive is performed.
        /// This controls the period between ping-pong checks.
        /// </summary>
        public int Cfg_KeepAliveInterval
        {
            get => _cfg_keepAliveInterval;
            set
            {
                // Make sure the given value never goes below our lower limit...
                if (value < 3)
                    value = 3;

                // Since the keepalive interval determines how often ping messages are sent, the connection innner-loop must be responsive enough to evaluate the keepalive.
                // So, we will depress the inner loop delay if the keepalive is below the normal inner loop limit.
                if (value < 5)
                {
                    // The given keepalive interval is to be set less than the current inner loop delay.
                    // So, we will depress the inner loop delay as well...
                    this.Cfg_Connected_InnerLoop_Delay = (int)(value * 1000);
                }
                else
                {
                    // The given keepalive interval is above the standard inner loop delay.
                    // So, we will ensure the inner loop delay is baselined to its standard level...
                    this.Cfg_Connected_InnerLoop_Delay = 5000;
                }

                // Accept the keep alive interval...
                _cfg_keepAliveInterval = value;
            }
        }

        /// <summary>
        /// Max receiver idle time in seconds before a keepalive ping is sent over the connection.
        /// If a ping-pong is inflight, this duration becomes the connection timeout.
        /// Specifically, the connection will recycle if the last received message is older than this duration, while a ping-pong is inflight.
        /// If no ping is inflight, the normal Cfg_KeepAliveInterval duration applies.
        /// </summary>
        public int Cfg_KeepAlive_ReplyMaxDuration = 20;


        /// <summary>
        /// Set this flag if the client wants to have a session without any keepalive messages.
        /// This is used for testing, so that, breakpointing will not cause a timeout to occur and cause a connection to be declared as dead.
        /// </summary>
        public bool Cfg_Disable_KeepAlive { get; set; } = false;

        /// <summary>
        /// Amount of time, in milliseconds, that a new connection is willing to wait for a
        ///     connection registration reply to be received from the server.
        /// The connection loop logic will wait this duration to ensure a reply is received, or the connection must restart.
        /// </summary>
        public int Cfg_RegistrationReplyTimeout { get; set; }

        /// <summary>
        /// Amount of time, in seconds, that a large message receiver can sit idle before
        ///     it gets recycled, for not finishing.
        /// </summary>
        public int Cfg_ReceiverTimeout
        {
            get => _cfg_ReceiverTimeout;
            set
            {
                if(value < 5) _cfg_ReceiverTimeout = 5;
                else _cfg_ReceiverTimeout = value;
            }
        }

        /// <summary>
        /// Defines if the connection loop requires a registration reply message be received, before allowing comms.
        /// Is set by default.
        /// </summary>
        public bool Cfg_ConnectionWaitsforRegistrationReply { get; set; }

        /// <summary>
        /// Max amount of time the connection loop will delay between connection attempt retries.
        /// </summary>
        public int Cfg_Startup_Connect_Retry_Delay { get; set; } = 5000;

        /// <summary>
        /// Number of milliseconds between checkups of the connection loop's connected state.
        /// This value gets lowered if the keepalive interval goes below 5 seconds.
        /// </summary>
        public int Cfg_Connected_InnerLoop_Delay  { get; set; } = 5000;

        /// <summary>
        /// Amount of time, in milliseconds, to wait before retrying the connection, after it was lost during post-conn work or keepalive.
        /// </summary>
        public int Cfg_PostConnect_FailDelay { get; set; }  = 5000;

        /// <summary>
        /// Amount of delay before retrying the connection, after a network loss has been observed.
        /// </summary>
        public int Cfg_NetworkLoss_WaitDelay { get; set; } = 5000;

        /// <summary>
        /// Instance Id of the endpoint.
        /// </summary>
        public int InstanceId { get; protected set; }

        /// <summary>
        /// Holds the unique connectionId of the websocket client.
        /// </summary>
        public string ConnectionId { get; protected set; }

        /// <summary>
        /// Id of the active user on the client.
        /// If no user logged in, set to Guid.Empty.
        /// </summary>
        public Guid UserId { get; set; }
        /// <summary>
        /// Set to the string indentifier of the client device.
        /// Usually, the RuntimeId.
        /// </summary>
        public string DeviceId { get; set; }
        /// <summary>
        /// Set to the process pid of the running client application.
        /// </summary>
        public int Pid { get; set; }

        /// <summary>
        /// Available to discriminate different instances of a client process.
        /// When a client populates this with a Guid that changes each time the client starts,
        ///     this value will easily distinguish multiple copies of the same client instance.
        /// This value is passed as ancillary data by the client logic.
        /// </summary>
        public string RuntimeId { get; set; }

        /// <summary>
        /// This defines the current TCP/WSLib version behavior of the client.
        /// It should be set in the constructor of deriving classes of this abstract.
        /// </summary>
        public string LibVersion { get; protected set; }

        /// <summary>
        /// Tracks the time of the last message received.
        /// Could be an actual message, or a keepalive ping.
        /// </summary>
        public DateTime LastReceivedTime { get; protected set; }

        /// <summary>
        /// Set when the websocket allows sending messages.
        /// </summary>
        public bool AllowSend { get => this._allowsend; }

        /// <summary>
        /// Set when the raw socket indicates open.
        /// Otherwise, false.
        /// </summary>
        public virtual bool IsConnected { get; }

        /// <summary>
        /// Ignores object state and disposed boolean. Just reports the raw socket being open or not.
        /// </summary>
        abstract public bool TransportIsOpen { get; }

        /// <summary>
        /// Set this flag if the client instance wants all messages to be echoed back, without further processing.
        /// </summary>
        public bool Register_with_Loopback_AllMessages { get; set; }

        /// <summary>
        /// Total number of connection attempts of the instance, regardless of success or failure.
        /// </summary>
        public int ConnAttempt_TotalCounter { get => _connattempt_totalcounter; }
        protected volatile int _connattempt_totalcounter;

        /// <summary>
        /// Total number of received messages since opening.
        /// </summary>
        public int ReceivedMessage_Counter { get => _receivedmessage_counter; }
        protected volatile int _receivedmessage_counter;

        /// <summary>
        /// Tracks current client state.
        /// </summary>
        public eEndpoint_ConnectionStatus State { get; protected set; }

        /// <summary>
        /// Set if the connection supports chunking of large messages at the channel layer.
        /// </summary>
        public bool Cfg_EnableChannelLayerChunking { get; set; } = true;

        #endregion


        #region Public Delegates

        /// <summary>
        /// The channel adapters listing.
        /// Received messages are forwarded to an instance in this collection based on the message channel.
        /// </summary>
        protected Dictionary<string, IChannelAdapter> _ChannelMessageHandlers;

        public delegate int DelMessageReceived(Client_v1_Abstract mep, string messagetype, string msg);
        protected DelMessageReceived _delOnMessageReceived;
        /// <summary>
        /// Add a callback, here, to capture raw messages, without channel handling.
        /// If your implementation use channel-based messaging, don't hook this up.
        /// NOTE: Any callback assigned, here, runs on the Receive Loop's thread.
        /// NOTE: Don't block this thread for longer than required to validate the received message as viable (returning an error if not). Then, spawn a thread to dispatch it.
        /// </summary>
        public DelMessageReceived OnMessageReceived
        {
            set
            {
                this._delOnMessageReceived = value;
            }
        }

        public delegate int DelRawMessageReceived(Client_v1_Abstract mep, string rawstring);
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

        public delegate void DelConnectionLost(Client_v1_Abstract mep);
        protected DelConnectionLost _delConnectionLost;
        /// <summary>
        /// Add a callback, here, to watch for lost connection events.
        /// NOTE: Do not block this call, as it runs on the Connection loop thread.
        /// </summary>
        public DelConnectionLost OnConnectionLost
        {
            set
            {
                this._delConnectionLost = value;
            }
        }

        public delegate void dStatus_Change(Client_v1_Abstract mep, string statusupdate);
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
        /// Constructor requires a logger instance.
        /// </summary>
        public Client_v1_Abstract(NLog.ILogger logger = null)
        {
            _instance_counter++;
            this.InstanceId = _instance_counter;

            _classname = nameof(Client_v1_Abstract);

            // Preset the WSLib Version to the first version...
            LibVersion = LibVersions.CONST_LibVersion_1;

            this.Logger = logger;

            disposedValue = false;

            this._cfg_keepAliveInterval = 15;

            this.Cfg_RegistrationReplyTimeout = 5000;

            this.Cfg_ConnectionWaitsforRegistrationReply = true;

#if (NET452 || NET48)
            this.LastReceivedTime = DateTime.MinValue;
#else
            this.LastReceivedTime = DateTime.UnixEpoch;
#endif

            _ChannelMessageHandlers = new Dictionary<string, IChannelAdapter>();

            this._largemsgreceivers = new Dictionary<string, LargeMsgReceiver>();

            this._cfg_ReceiverTimeout = 120;

            // Clear the allow sending flag, to prevent any outgoing messages...
            this._allowsend = false;
        }

        /// <summary>
        /// The 'working' disposed method (the one that does the actual work).
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)

                    Stop_Async().GetAwaiter();

                    this.Logger = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~WSClient()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        /// <summary>
        /// Public dispose method
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Call this method, once the client is configured, to begin connection.
        /// </summary>
        /// <returns></returns>
        public async Task<int> Start_Async()
        {
            try
            {
                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                    $"Attempting to start {(this.TransportShortName?.ToLower() ?? "socket")} client...");

                // Check if connected...
                if (this.disposedValue)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                        $"{(this.TransportShortName ?? "socket")} is already disposed.");

                    return -1;
                }

                // Clear the allow sending flag, to prevent any outgoing messages...
                this._allowsend = false;

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                    $"Attempting to connect {(this.TransportShortName?.ToLower() ?? "socket")} client.");

                _cts = new CancellationTokenSource();

                // Trigger a thread to run the maintenance loop...
                _ = Task.Run(async () =>
                {
                    // Wrap the loop method in a try-catch to ensure it never throws and unwinds to the Task Scheduler base.
                    try
                    {
                        await ConnectionLoop();
                    }
                    catch(Exception e) { }
                });

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                    $"{(this.TransportShortName ?? "socket")} client connection loop started.");

                return 1;
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Start_Async)} - " +
                    $"Exception occurred while attempting to connect {(this.TransportShortName?.ToLower() ?? "socket")} client.");

                return -2;
            }
        }

        /// <summary>
        /// Call this method to close the connection, and shut down the client.
        /// </summary>
        /// <returns></returns>
        public async Task Stop_Async()
        {
            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Stop_Async)} - " +
                $"Attempting to stop {(this.TransportShortName?.ToLower() ?? "socket")} client...");

            // Clear the allow sending flag, to prevent any outgoing messages...
            this._allowsend = false;

            // Clear out any large message receivers...
            this._largemsgreceivers.Clear();

            // Disconnect any message handlers...
            this.Close_ChannelAdapters();
            this._delOnMessageReceived = null;
            this._delOnRawMessageReceived = null;

            // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
            try
            {
                await this.CloseandDisposeTransport();
            }
            catch (Exception) { }

            // Wait a tick before cancelling the receive loop...
            // This lets the receive handler accept any closure frames, to close gracefully.
            await Task.Delay(100);

            if (this._receive_cts != null)
            {
                try
                {
                    this._receive_cts?.Cancel();
                }
                catch (Exception) { }

                await Task.Delay(100);

                try
                {
                    this._receive_cts?.Dispose();
                }
                catch (Exception) { }

                this._receive_cts = null;
            }

            if (this._cts != null)
            {
                try
                {
                    this._cts?.Cancel();
                }
                catch (Exception) { }

                await Task.Delay(100);

                try
                {
                    this._cts?.Dispose();
                }
                catch (Exception) { }

                this._cts = null;
            }

            try
            {
                this.DereferenceTransport();
            }
            catch (Exception) { }

            // Clear any delegates...
            this._delConnectionLost = null;
            this._del_Status_Change = null;

            this.Logger?.Info(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Stop_Async)} - " +
                $"{(this.TransportShortName ?? "socket")} connection is closed.");

            return;
        }

        /// <summary>
        /// Call this method to add a channel adapter for handling received messages.
        /// </summary>
        /// <param name="adapter"></param>
        /// <returns></returns>
        public int Add_ChannelAdapter(IChannelAdapter adapter)
        {
            if(adapter == null)
            {
                return -1;
            }

            // Validate the adapter...
            if(string.IsNullOrEmpty(adapter.ChannelId))
            {
                // Invalid channel name.
                return -2;
            }

            // Make sure the channel is empty...
            if(this._ChannelMessageHandlers.ContainsKey(adapter.ChannelId))
            {
                // The channel is already assigned.
                return -1;
            }

            // Add it to the adapters listing...
            this._ChannelMessageHandlers.Add(adapter.ChannelId, adapter);

            // Give the channel adapter a reference to this client.
            // Doing so, allows a channel adapter to provide its own send methods supporting their own types.
            adapter.RegisterAdapter(this);

            return 1;
        }

        /// <summary>
        /// Call this method to add a message handler for a string-named channel.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public int Add_ChannelHandler(string channel, DelMessageReceived handler)
        {
            // Create a delegate adapter...
            var da = new ChannelAdapter_DelegateType(channel, handler, "", this.Logger);

            // Add it to the adapters listing...
            var res = this.Add_ChannelAdapter(da);
            return res;
        }
        /// <summary>
        /// Call this method if there is a need to remove or replace a channel's message handler.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public int Remove_ChannelHandler(string channel)
        {
            // Attempt to close the adapter...
            IChannelAdapter ca = null;
            try
            {
                ca = this._ChannelMessageHandlers[channel];
            }
            catch(Exception e)
            {
                // Not found.
                return -1;
            }

            if (ca == null)
                return 1;

            // Close the adapter...
            ca.Close();

            // Remove the adapter from our listing...
            this._ChannelMessageHandlers.Remove(channel);

            return 1;
        }

        /// <summary>
        /// Cleanup method that closes down all channel adapters.
        /// </summary>
        protected void Close_ChannelAdapters()
        {
            while(this._ChannelMessageHandlers.Count != 0)
            {
                try
                {
                    // Close the adapter...
                    var ch = this._ChannelMessageHandlers.ElementAt(0);
                    ch.Value.Close();

                    // Remove it from the listing...
                    this._ChannelMessageHandlers.Remove(ch.Key);
                }
                catch(Exception) { }
            }
        }

        /// <summary>
        /// Creates a loggable string block of the current configuration for the client.
        /// </summary>
        /// <returns></returns>
        public virtual string ToLogString_Config()
        {
            StringBuilder b= new StringBuilder();

            b.AppendLine($"***Client Configuration***");
            b.AppendLine($"LibVersion = " + LibVersion.ToString() + ";");
            b.AppendLine($"TransportLongName = " + TransportLongName.ToString() + ";");
            b.AppendLine($"Cfg_ConnectionWaitsforRegistrationReply = " + Cfg_ConnectionWaitsforRegistrationReply.ToString() + ";");
            b.AppendLine($"Cfg_Connected_InnerLoop_Delay = " + Cfg_Connected_InnerLoop_Delay.ToString() + ";");
            b.AppendLine($"Cfg_Disable_KeepAlive = " + Cfg_Disable_KeepAlive.ToString() + ";");
            b.AppendLine($"Cfg_EnableChannelLayerChunking = " + Cfg_EnableChannelLayerChunking.ToString() + ";");
            b.AppendLine($"Cfg_KeepAliveInterval = " + Cfg_KeepAliveInterval.ToString() + ";");
            b.AppendLine($"Cfg_KeepAlive_ReplyMaxDuration = " + Cfg_KeepAlive_ReplyMaxDuration.ToString() + ";");
            b.AppendLine($"Cfg_NetworkLoss_WaitDelay = " + Cfg_NetworkLoss_WaitDelay.ToString() + ";");
            b.AppendLine($"Cfg_PostConnect_FailDelay = " + Cfg_PostConnect_FailDelay.ToString() + ";");
            b.AppendLine($"Cfg_RegistrationReplyTimeout = " + Cfg_RegistrationReplyTimeout.ToString() + ";");
            b.AppendLine($"Cfg_ReceiverTimeout = " + Cfg_ReceiverTimeout.ToString() + ";");
            b.AppendLine($"Cfg_Startup_Connect_Retry_Delay = " + Cfg_Startup_Connect_Retry_Delay.ToString() + ";");
            b.AppendLine($"MaxMessageSize = " + MaxMessageSize.ToString() + ";");

            return b.ToString();
        }

        #endregion


        #region Connection Management

        /// <summary>
        /// This hold the main connection loop.
        /// It also runs on its own thread, spawned in Start_Async.
        /// It returns when the connection closes.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> ConnectionLoop()
        {
            bool success = false;

            // Check if connected...
            if (this.disposedValue)
            {
                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is already disposed.");

                return -1;
            }

            this.Logger?.Info(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                "Starting connection loop...");

            // Do any setup....


            // Create an exponential backoff instance that will slow down network status checks as failures grow...
            ExpBackoff_wJitter network_visibility_delay_eb = new ExpBackoff_wJitter(0, 200, this.Cfg_NetworkLoss_WaitDelay);
            network_visibility_delay_eb.EnableJitter = true;
            // Reset it to minimum...
            network_visibility_delay_eb.Reset();

            // Create an exponential backoff instance that will let us quickly reconnect, but slow down as failure count grows...
            ExpBackoff_wJitter startup_delay_eb = new ExpBackoff_wJitter(0, 200, this.Cfg_Startup_Connect_Retry_Delay);
            startup_delay_eb.EnableJitter = true;
            // Reset it to minimum...
            startup_delay_eb.Reset();

            // Use an exponential backoff instance for post-connect failure delay...
            // But, we only want to use the jitter function of it... not the growing delay.
            ExpBackoff_wJitter postconnect_fail_delay_eb = new ExpBackoff_wJitter(0, this.Cfg_PostConnect_FailDelay, this.Cfg_PostConnect_FailDelay);
            postconnect_fail_delay_eb.EnableJitter = true;
            postconnect_fail_delay_eb.JitterHeight = 0.7f;
            // Reset it to minimum...
            postconnect_fail_delay_eb.Reset();


            // Enter the loop...
            try
            {
                // Run the outer loop...
                while (!this.disposedValue && _cts != null && !_cts.IsCancellationRequested)
                {
                    // Loop inside a try, to ensure we don't leave unless we want to...
                    try
                    {
                        // Clear the connection lost delegate, so it does not fire...
                        lock (lclock)
                        {
                            this._connectionclosuredelegate_armed = false;
                        }

                        // We will not attempt to open a websocket connection until we know there is internet visibility.
                        // This method call, should be overridden with an implementation-specific check of network visibility.
                        // Wrap it in a try-catch to ensure the override doesn't throw and unwind us...
                        bool inetavail = false;
                        try
                        {
                            inetavail = this.IsInternetAvailable();
                        } catch(Exception e) { }
                        if (!inetavail)
                        {
                            // We will pause for a little bit, while waiting for internet visibility.

                            // Wait before checking the network again...
                            //await Task.Delay(this._networkLoss_WaitDelay, _cts.Token);
                            network_visibility_delay_eb.Delay(_cts.Token);

                            // Now, go back to the top and get a status update...
                            continue;
                        }
                        // If here, the platform says we have internet visibility.
                        // So, we can continue setting up a websocket connection.

                        // Now, that we have network visibility, reset the network visibility delay check to minimum...
                        network_visibility_delay_eb.Reset();

                        // Get things we need for connection...
                        // Auth tokens, etc...
                        if (await this.Do_Setup_Before_Connection() != 1)
                        {
                            // Failed to complete setup before connection.

                            // Reset the allow sending flag, to prevent outgoing messages...
                            this._allowsend = false;

                            this.Logger?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                $"Could not finish setup before {(this.TransportLongName?.ToLower() ?? "socket")} connection.");

                            // We will do an exponential backoff retry...
                            //await Task.Delay(this.Cfg_Startup_Connect_Retry_Delay, _cts.Token);
                            startup_delay_eb.Delay(_cts.Token);

                            continue;
                        }
                        // If here, we have setup done.

                        // Attempt connection...
                        try
                        {
                            // Attempt a new connection, if not connected...
                            if (!this.TransportIsOpen)
                            {
                                success = false;

                                // Create a client instance...
                                if (await this.Do_Setup_Before_Connection() != 1)
                                {
                                    // Setup failed.
                                    // Probably because we're using dynamic connection url, and the host service couldn't give us one.
                                    // We will spin-wait, here, and try again in a bit...

                                    // Reset the allow sending flag, to prevent outgoing messages...
                                    this._allowsend = false;

                                    this.Logger?.Debug(
                                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                            $"Failed to complete connection setup.");

                                    // Pausing for a bit, before attempting to connect again...
                                    // We will do an exponential backoff retry...
                                    //await Task.Delay(this.Cfg_Startup_Connect_Retry_Delay, _cts.Token);
                                    startup_delay_eb.Delay(_cts.Token);

                                    continue;
                                }

                                // Increment our connection attempt counter...
                                Interlocked.Increment(ref this._connattempt_totalcounter);

                                this.Logger?.Debug(
                                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                    $"Attempting to create a {(this.TransportLongName?.ToLower() ?? "socket")} connection (attempt # {(this._connattempt_totalcounter.ToString())})...");


                                // Attempt the transport-specific connection...
                                // Do it in a try-catch to ensure the override method doesn't throw and unwind us...
                                try
                                {
                                    success = await this.TransportSpecific_Connect();
                                }
                                catch(Exception ex)
                                {
                                    // Assume failure...
                                    success = false;
                                }

                                // See if the async connect succeeded...
                                if (!success)
                                {
                                    // Connection success was not achieved.
                                    // We are in a connect loop, so we will simply do another pass and try again.

                                    // Reset the allow sending flag, to prevent outgoing messages...
                                    this._allowsend = false;

                                    this.Logger?.Debug(
                                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                            $"Failed to connect with server, ({(this._connection_string ?? "<connectionstring not defined>")}).");

                                    // Signal that the connection was lost...
                                    DispatchConnectionLost();

                                    // Pausing for a bit, before attempting to connect again...
                                    // We will do an exponential backoff retry...
                                    //await Task.Delay(this.Cfg_Startup_Connect_Retry_Delay, _cts.Token);
                                    startup_delay_eb.Delay(_cts.Token);

                                    continue;
                                }
                                // If here, we had success in connecting, and will fall into the connected loop work, below...

                                this.Logger?.Debug(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                        $"Connection attempt was successful with server, ({(this._connection_string ?? "<connectionstring not defined>")}).");

                                // Reset the startup delay backoff, since we've advanced past its relevance...
                                startup_delay_eb.Reset();
                            }
                            // NOTE: THIS IF SHOULD NOT BE AN ELSE IF.
                            // IT WORKS BECAUSE IT IS EVALUATED SEPARATELY.
                            if (this.TransportIsOpen)
                            {
                                // We should be connected.
                                // But, we can't yet do anything, without having registered.

                                this.Logger?.Debug(
                                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                    "Connected to server.");

                                // We are connected.
                                // We must do any post-connection work, such as registering our connection Id and such...

                                // Reset the received message counter...
                                // We do this, here, so the counter can include transport-layer messages in its received count.
                                this._receivedmessage_counter = 0;

                                this.Logger?.Debug(
                                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                    "Doing any post connection work...");

                                // If passed, do preamble things...
                                int rrr = await Do_Post_Connection_Work_Async();
                                if (rrr != 1)
                                {
                                    // We failed to complete post-connection work.
                                    // We must recycle this connection, and try again.

                                    // Reset the allow sending flag, to prevent outgoing messages...
                                    this._allowsend = false;

                                    this.Logger?.Debug(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                        "Failed to complete post connection work.");

                                    this.Logger?.Warn(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                        "Recycling connection, so we can try again...");

                                    // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
                                    try
                                    {
                                        await this.CloseandDisposeTransport();
                                    }
                                    catch (Exception) { }
                                    try
                                    {
                                        this.DereferenceTransport();
                                    }
                                    catch (Exception) { }

                                    // Wait a little bit, before attempting to connect again...
                                    //await Task.Delay(this._PostConnect_FailDelay, _cts.Token);
                                    postconnect_fail_delay_eb.Delay(_cts.Token);

                                    int x = 0;
                                }
                                else
                                {
                                    // Post connection work was successful.
                                    // We have an open web socket ready for traffic.

                                    // Set the allow sending flag, to allow outgoing messages...
                                    this._allowsend = true;

                                    // Publish a connected event...
                                    // This call will arm the connection lost delegate, for good symmetry.
                                    // Wrap this override in a try-catch to ensure it doesn't throw and unwind us...
                                    try
                                    {
                                        DispatchConnected();
                                    }
                                    catch(Exception) { }

                                    // Start the inner status loop...
                                    while (!_cts.IsCancellationRequested && this.IsConnected)
                                    {
                                        // Do a sanity check of network status to ensure that we have the minimum network capabilities for maintaining our websocket connection.
                                        // We do this, because a websocket connection will not always recognize a connection loss if the platform loses network access.
                                        // Wrap it in a try-catch to ensure the override doesn't throw and unwind us...
                                        bool inetavail2 = false;
                                        try
                                        {
                                            inetavail2 = this.IsInternetAvailable();
                                        } catch(Exception e) { }
                                        if (!inetavail2)
                                        {
                                            // The device platform is reporting that internet access has been lost.
                                            // So, we need to pull down our connection loop, because the platform says it has no internet visibility.

                                            Logger.Debug($"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                                "Internet visibility was lost.");

                                            // Reset the allow sending flag, to prevent outgoing messages...
                                            this._allowsend = false;

                                            this.Logger?.Warn(
                                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                                $"Recycling {(this.TransportLongName?.ToLower() ?? "socket")} connection, and waiting for internet visibility...");

                                            // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
                                            try
                                            {
                                                await this.CloseandDisposeTransport();
                                            }
                                            catch (Exception) { }
                                            try
                                            {
                                                this.DereferenceTransport();
                                            }
                                            catch (Exception) { }

                                            // Signal that the connection was lost...
                                            DispatchConnectionLost();

                                            // Wait a little bit, before attempting to connect again...
                                            //await Task.Delay(this._PostConnect_FailDelay, _cts.Token);
                                            postconnect_fail_delay_eb.Delay(_cts.Token);

                                            // Leave the inner status while loop...
                                            break;
                                        }

                                        this.Logger?.Debug(
                                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                            $"Inner Connection Loop with server, ({(this._connection_string ?? "<connectionstring not defined>")}). " +
                                            $"ConnectionID = {(this.ConnectionId ?? "")}.");

                                        // Prune any old/stale large message receivers...
                                        this.PruneStaleLargeMessageReceivers();

                                        // See if we need to send a keep alive...
                                        if (Cfg_Disable_KeepAlive)
                                        {
                                            // Keepalives are disabled.
                                            // We will not send pings, and the server will not require them.
                                        }
                                        else
                                        {
                                            // Keepalive is needed for this connection.

                                            if (await Send_KeepAlive_IfNeeded_Async() == -1)
                                            {
                                                // Failed to send ping request.
                                                // We must close the connection.

                                                // Reset the allow sending flag, to prevent outgoing messages...
                                                this._allowsend = false;

                                                this.Logger?.Warn(
                                                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                                    "Recycling connection, so we can try again...");

                                                // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
                                                try
                                                {
                                                    await this.CloseandDisposeTransport();
                                                }
                                                catch (Exception) { }
                                                try
                                                {
                                                    this.DereferenceTransport();
                                                }
                                                catch (Exception) { }

                                                // Signal that the connection was lost...
                                                DispatchConnectionLost();

                                                // Wait a little bit, before attempting to connect again...
                                                //await Task.Delay(this._PostConnect_FailDelay, _cts.Token);
                                                postconnect_fail_delay_eb.Delay(_cts.Token);

                                                // Leave the inner status while loop...
                                                break;
                                            }
                                        }

                                        //// Add any periodic things to do, here...
                                        //_ = Task.Run(() => Send_Test_Data("sfsdfsdsdf"));

                                        await Task.Delay(Cfg_Connected_InnerLoop_Delay, _cts.Token);
                                    }
                                    // Bottom of Inner Status While Loop.
                                    // If here, we either lost connection, or are cancelled.
                                    // In either case, we will loop back to the top of the outer loop to leave.

                                    // See what happened...
                                    if (_cts.IsCancellationRequested)
                                    {
                                        // We were cancelled.

                                        // Reset the allow sending flag, to prevent outgoing messages...
                                        this._allowsend = false;

                                        // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
                                        try
                                        {
                                            await this.CloseandDisposeTransport();
                                        }
                                        catch (Exception) { }
                                        try
                                        {
                                            this.DereferenceTransport();
                                        }
                                        catch (Exception) { }

                                        // Signal that the connection was lost...
                                        DispatchConnectionLost();

                                        this.Logger?.Debug(
                                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                            $"Connection cancelled with server, ({(this._connection_string ?? "<connectionstring not defined>")}). ConnectionID = {this.ConnectionId}.");

                                        return 1;
                                    }
                                    else if (!this.IsConnected)
                                    {
                                        // We lost connection.

                                        // Reset the allow sending flag, to prevent outgoing messages...
                                        this._allowsend = false;

                                        this.Logger?.Debug(
                                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                            $"Connection lost with server, ({(this._connection_string ?? "<connectionstring not defined>")}). ConnectionID = {this.ConnectionId}.");

                                        // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
                                        try
                                        {
                                            await this.CloseandDisposeTransport();
                                        }
                                        catch (Exception) { }
                                        try
                                        {
                                            this.DereferenceTransport();
                                        }
                                        catch (Exception) { }

                                        // Signal that the connection was lost...
                                        DispatchConnectionLost();

                                        this.Logger?.Debug(
                                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                            $"Looping around to try reconnection...");
                                    }
                                }
                            }
                        }
                        catch when (_cts == null)
                        {
                            // We were cancelled.
                            // We can leave.

                            // Reset the allow sending flag, to prevent outgoing messages...
                            this._allowsend = false;

                            // Signal that the connection was lost...
                            // We need to call this, here, in case this exception was caught after the connection went active.
                            // The dispatch call will determine whether or not, to actually send the connection lost event, so we don't need to worry about it.
                            DispatchConnectionLost();

                            this.Logger?.Warn(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                $"Client ConnectionID = {(this.ConnectionId ?? "")}, is cancelled, and being shut down.");
                        }
                        catch when (_cts.IsCancellationRequested)
                        {
                            // We were cancelled.
                            // We can leave.

                            // Reset the allow sending flag, to prevent outgoing messages...
                            this._allowsend = false;

                            // Signal that the connection was lost...
                            // We need to call this, here, in case this exception was caught after the connection went active.
                            // The dispatch call will determine whether or not, to actually send the connection lost event, so we don't need to worry about it.
                            DispatchConnectionLost();

                            this.Logger?.Warn(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                $"Client ConnectionID = {(this.ConnectionId ?? "")}, is cancelled, and being shut down.");
                        }
                        catch (Exception e)
                        {
                            // Reset the allow sending flag, to prevent outgoing messages...
                            this._allowsend = false;

                            // Signal that the connection was lost...
                            // We need to call this, here, in case this exception was caught after the connection went active.
                            // The dispatch call will determine whether or not, to actually send the connection lost event, so we don't need to worry about it.
                            DispatchConnectionLost();

                            this.Logger?.Error(e,
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                $"Exception occurred while connecting to server, ConnectionID = {(this.ConnectionId ?? "")}.");

                            // Pausing for a bit, before attempting to connect again...
                            //await Task.Delay(this.Cfg_Startup_Connect_Retry_Delay, _cts.Token);
                            startup_delay_eb.Delay(_cts.Token);
                        }
                    }
                    catch (Exception e)
                    {
                        // Reset the allow sending flag, to prevent outgoing messages...
                        this._allowsend = false;

                        this.Logger?.Error(e,
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                            "Exception occurred during an iteration of the connection loop.");
                    }
                }
                // Bottom of the outer loop.
                // We have left the loop.

                // Reset the allow sending flag, to prevent outgoing messages...
                this._allowsend = false;

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                    "Connection loop closed down. Leaving...");

                return 1;
            }
            catch (Exception e)
            {
                // Reset the allow sending flag, to prevent outgoing messages...
                this._allowsend = false;

                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                    "Exception occurred outside of the while loop, most likely because the cancellation token was disposed.");

                return -1;
            }
        }

        /// <summary>
        /// Override this method with the transport-specific connect logic.
        /// Return true if successful. False, if not.
        /// </summary>
        /// <returns></returns>
        abstract protected Task<bool> TransportSpecific_Connect();

        /// <summary>
        /// Does any instance setup before attempting connection.
        /// Things like url composition, id generation, delegate setup, and such.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> Do_Setup_Before_Connection()
        {
            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Setup_Before_Connection)} - " +
                $"Performing setup before connection...");

            // Determine the connection info...
            // For websockets, this call is a hook point where the client can ask for dynamic connection url from a host service.
            // For clients that connect to static hosts, this call doesn't do anything.

            // Wrap in a try-catch to ensure the override doesn't throw and unwind us...
            try
            {
                if (await Get_ConnectionInfo() != 1)
                {
                    // We failed to get connection info.
                    // Cannot connect without it.

                    this.Logger?.Warn(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Setup_Before_Connection)} - " +
                        $"Connection setup did not resolve connection info.");

                    return 0;
                }
            }
            catch(Exception e)
            {
                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Setup_Before_Connection)} - " +
                    $"Exception caught from the Get_ConnectionInfo method call.");

                return 0;
            }


            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Setup_Before_Connection)} - " +
                $"Connection info was determined. Setting up new websocket instance...");

            // Reset the keepalive state...
            this._keepAliveStatus = 0;

            // Assign a new connectionId for each connection/attempt...
            this.CreateNewConnectionID();

            // Clear out any large message receivers...
            this._largemsgreceivers.Clear();

            // This is a call point, for the transport specific implementation, to create its socket, websocket, etc...
            // Wrap it in a try-catch to ensure it doesn't throw and unwind us...
            try
            {
                if (await this.TransportSpecific_CreateNewConnection() != 1)
                {
                    // We failed to setup the actual transport connection.
                    // Cannot continue without it.

                    this.Logger?.Warn(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Setup_Before_Connection)} - " +
                        $"Connection setup failed to instantiate transport.");

                    return 0;
                }
            }
            catch(Exception e)
            {
                this.Logger?.Warn(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Setup_Before_Connection)} - " +
                    $"Exception occurred while creating new connection.");

                return 0;
            }

            // Reset the connection lost diagnostic counter...
            this.connlost_truecounter = 0;

            // Reset our registration reply received flag...
            this._registrationreplyreceived = false;

            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Setup_Before_Connection)} - " +
                $"New websocket instance has been setup.");

            return 1;
        }

        /// <summary>
        /// Performs any activities required for a new connection, such as id, registration, and worker setup.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> Do_Post_Connection_Work_Async()
        {
            // We need to register with the service, so it readily knows our connection Id and other parameters....

            // Additionally, do things in this method, to catch the app up to current state, since it was last online. Things like:
            //  Pulling chat message updates
            //  Checking for queued friend requests

            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                $"Attempting to perform post connection work (connection registration)...");

            try
            {
                // Check if connected...
                if (this.disposedValue)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                        "Websocket is already disposed.");

                    return -1;
                }
                if (!this.IsConnected)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                        "Websocket is not connected.");

                    return 0;
                }

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                    $"Setting up receive loop for new connection...");

                // Setup receive loop...
                // This was added to a try-catch because an edge case occurred in which the receive_cts was seen as not null by the if, then was null within the if block.
                // This race condition occurred while stopping the debugger, and probably triggered a client restart, during a kill.
                // For this, we added a catch that will abort setup and leave.
                try
                {
                    if (this._receive_cts != null)
                    {
					    try { this._receive_cts?.Cancel(); } catch (Exception) { }
                        await Task.Delay(100);
					    try { this._receive_cts?.Dispose(); } catch (Exception) { }
                        this._receive_cts = null;
                    }

                    this._receive_cts = new CancellationTokenSource();

                    // Do any transport-specific post connection setup work...
                    // This hook allows the TCP endpoint implementation a spot in the setup flow, to get its network stream instance.
                    // Do it in a try-catch to ensure the override method doesn't throw and unwind us...
                    try
                    {
                        var respcw = await this.Do_TransportSpecific_PostConnectionWork_Async();
                        if (respcw != 1)
                        {
                            this.Logger?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                                $"Failed to do transport specific post connnection work. " +
                                    $"ConnectionID = {(this.ConnectionId ?? "")}.");

                            return -3;
                        }
                    }
                    catch(Exception ex)
                    {
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                            $"Exception caught while performing transport specific post connnection work. " +
                                $"ConnectionID = {(this.ConnectionId ?? "")}.");

                        return -4;
                    }

                    // Call the receiver loop if the transport needs one...
                    if (this.Cfg_TransportRequiresReceiverLoop)
                    {
                        /// Returns  1 if cancelled.
                        /// Returns  0 if failed to parse the received message.
                        /// Returns -1 if unable to accept received messages.
                        /// Returns -2 if received a close message.
                        _ = Task.Run(async () =>
                        {
                            // Wrap the receive loop override in a try-catch to ensure it doesn't throw and unwind to the Task base.
                            try
                            {
                                await ReceiveLoop();
                            }
                            catch(Exception e) { }
                        });
                    }
                }
                catch (Exception tre)
                {
                    this.Logger?.Error(tre,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                        $"Exception occurred while attempting to setup receive loop cancelation token and receive loop. " +
                            $"ConnectionID = {(this.ConnectionId ?? "")}.");

                    return -5;
                }

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                    $"Receive loop setup for new connection.");

                //// Get our userid...
                //var uid = this._usersvc.CurrentUserId;
                //if(uid != null)
                //{
                //success = false;

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                    $"Sending registration data to service...");

                // Send the client registration message...
                // No need to wrap this override call in a try-catch, as there's a catch below that can handle it.
                int res = await Send_RegistrationMessage();
                if (res != 1)
                {
                    // Failed to send the registration message.

                    // Check if the pipe is closed...
                    if (!this.TransportIsOpen)
                    {
                        // We lost connection.

                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                            $"Connection was lost while sending registration data to service.");

                        return -6;
                    }

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                        $"Error returned from Send Registration call.");

                    // Cannot continue without proper registration.

                    return -7;
                }
                // If here, we sent our registration data to the service.
                // So, it the connectionId, userid, and device Id of our client.

                // See if we are to wait for registration reply messages...
                if(this.Cfg_ConnectionWaitsforRegistrationReply)
                {
                    // We are to wait for a registration reply message to be received and handled.

                    // The above registration call sent a registration message to the server, with our client-created ConnectionId.
                    // Since the server assigns us a real ConnectionId on registration, we don't know it upfront.
                    // So, the server sends it to us in a registration reply.
                    // We will wait for that registration reply to be handled, here.
                    // If our receive loop gets a valid registration reply message, we consider registration complete.
                    // Otherwise, we hit a timeout and consider the connection failed, and attempt a new one.
                    // NOTE: The condition we gave the wait, is actually a simple check of the received boolean flag, with a check every 50 msec.
                    // This call will return 1 if the reply is received.
                    // Otherwise, it will return 0 (timeout) or negatives for cancelled.
                    int rescrwait = await WaitforCondition(() => this._registrationreplyreceived, this.Cfg_RegistrationReplyTimeout, 50, this._cts.Token);
                    if(rescrwait != 1)
                    {
                        // We timed out, waiting for the registration reply.
                        // Or, the timeout was cancelled because our connection has been recycled or closed.
                        // Regardless of the reason, we assume the valid reply never came.

                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                            $"Timed out while waiting for receive loop to handle the registration reply message for our connection. Returning error." +
                                $"ConnectionID = {(this.ConnectionId ?? "")}.");

                        return -8;
                    }
                    // The receive loop handled a valid registration reply for our connection.
                    // We can continue as normal.
                }

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                    $"Registered with service, ({(this._connection_string ?? "<connectionstring not defined>")}). " +
                        $"ConnectionID = {(this.ConnectionId ?? "")}.");

                return 1;
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_Post_Connection_Work_Async)} - " +
                    $"Exception occurred while attempting to register with service, ({(this._connection_string ?? "<connectionstring not defined>")}). " +
                        $"ConnectionID = {(this.ConnectionId ?? "")}.");
                return -9;
            }
        }

        /// <summary>
        /// Will wait for the given function to become true, the timeout to occur, or the cancellation token to signal.
        /// Includes an optional scan parameter to define how often the given function is evaluated during the wait period.
        /// Returns 1 if the condition evaluated as true.
        /// Returns 0 if the timeout occurred.
        /// Returns -1 if the token was cancelled.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="timeout"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        static public async Task<int> WaitforCondition(Func<bool> condition, int timeout, int scaninterval = 50, CancellationToken token = default(CancellationToken))
        {
            DateTime starttime = DateTime.UtcNow;

            TimeSpan duration = new TimeSpan();

            // Ensure the scan interval is no longer than the timeout...
            if(scaninterval > timeout)
            {
                // Reduce the scan interval to the timeout...
                scaninterval = timeout;
            }

            // We employ a spin-wait, here to evaluate the condition, or leave if cancelled...
            var reswait1 = System.Threading.SpinWait.SpinUntil(()=> condition() || token.IsCancellationRequested, timeout);

            if(reswait1 && !token.IsCancellationRequested)
            {
                // The condition went true.
                return 1;
            }
            else if(token.IsCancellationRequested)
            {
                // We got cancelled.
                return -1;
            }
            else
            {
                // The timeout occurred.

                duration = DateTime.UtcNow.Subtract(starttime);

                return 0;
            }
            //try
            //{

                //// 

                //// Loop until we timeout, cancelled, or condition goes true...
                //while(!token.IsCancellationRequested)
                //{
                //    // Do a check...
                //    // Wrap it in a try-catch, to ensure it can't throw and unwind our own logic.
                //    try
                //    {
                //        if(condition())
                //        {
                //            // The condition became true.
                //            // We can leave.
                //            return 1;
                //        }
                //    }
                //    catch(Exception e) { }
                //    // Not yet true.

                //    // We will wait a scan interval before checking it again...
                //    await Task.Delay(scaninterval, token);
                //}

                //return 0;
            //}
            //catch (OperationCanceledException) when (token.IsCancellationRequested)
            //{
            //    return 0;
            //}
            //catch (Exception)
            //{
            //    return -1;
            //}
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
        /// Call hook in the Do Setup Before Connecting method, for the transport-specific implementation to create the websocket, tcpclient, etc...
        /// </summary>
        /// <returns></returns>
        abstract protected Task<int> TransportSpecific_CreateNewConnection();
        //{
        //    // Declare the ws instance...
        //    ClientWebSocket newcws = null;
        //    newcws = new System.Net.WebSockets.ClientWebSocket();

        //    // Setup options...
        //    newcws.Options.RemoteCertificateValidationCallback += this.CALLBACK_Check_Server_Certificate;
        //    newcws.Options.KeepAliveInterval = new TimeSpan(0, 0, 300);


        //    // Figure out the auth token to use...
        //    // We call a method, in case there's an implementation that has to do some query or other, to find out the appropriate auth token to use.
        //    var atk = this.Determine_AuthToken();

        //    // Add authorization token if we have one...
        //    if (!string.IsNullOrEmpty(atk))
        //        newcws.Options.SetRequestHeader(CONST_HTTP_TokenMarker, atk);

        //    // We are to swap in our new client, here.
        //    // Copy off the old one, so we can dispose it...
        //    var oldcws = this.cws;

        //    // Do a quick copy over, to minimize any transients of other threads seeing the client as inconsistent...
        //    this.cws = newcws;

        //    // Dispose of the old one...
        //    try { oldcws?.Dispose(); } catch (Exception e) { }
        //}

        /// <summary>
        /// Override this method with any platform-specific logic to check if network visibility exists, for a websocket connection attempt to make sense to do.
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsInternetAvailable()
        {
            // Return true, by default.
            return true;
        }

        /// <summary>
        /// Call this method periodically, to send keep alive messages as needed.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> Send_KeepAlive_IfNeeded_Async()
        {
            DateTime ctime = DateTime.UtcNow;

            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_KeepAlive_IfNeeded_Async)} - " +
                $"Doing periodic check for keepalive, over ConnectionId = {(this.ConnectionId ?? "")}...");

            // Check if we have a ping in flight, and waiting on a reply...
            if (this._keepAliveStatus == 1)
            {
                // A ping has been sent.
                // We are waiting on a reply for it.

                // Check if it's been too long...
                if (ctime.CompareTo(LastReceivedTime.AddSeconds(this.Cfg_KeepAlive_ReplyMaxDuration)) < 0)
                {
                    // We have not received an expected pong reply from the server.
                    // We will assume it is dead.

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_KeepAlive_IfNeeded_Async)} - " +
                        $"Failed to receive ping reply from web service, over ConnectionId = {(this.ConnectionId ?? "")}.");

                    return -1;
                }

                this.Logger?.Trace(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_KeepAlive_IfNeeded_Async)} - " +
                    $"Still waiting for a keepalive response (Pong), over ConnectionId = {(this.ConnectionId ?? "")}.");

                return 1;
            }

            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_KeepAlive_IfNeeded_Async)} - " +
                $"Checking if we need to send a Ping to WSEndpoint, over ConnectionId = {(this.ConnectionId ?? "")}...");

            // Check if the last received message is recent or not...

            if (ctime.CompareTo(LastReceivedTime.AddSeconds(this._cfg_keepAliveInterval)) < 0)
            {
                // The last received message was recent, so we don't need to send anything.

                this.Logger?.Trace(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_KeepAlive_IfNeeded_Async)} - " +
                    $"No need to send a Ping to WSEndpoint, over ConnectionId = {(this.ConnectionId ?? "")}.");

                return 1;
            }
            // The connection has been silent for too long.
            // We must send a ping pong.

            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_KeepAlive_IfNeeded_Async)} - " +
                $"Connection has been silent. Attempting to send Ping to WSEndpoint, over ConnectionId = {(this.ConnectionId ?? "")}...");

            if (await this.SendPing_toEndpoint_Async() != 1)
            {
                // Failed to send ping message.

                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_KeepAlive_IfNeeded_Async)} - " +
                    $"Failed to send ping message to web service, over ConnectionId = {(this.ConnectionId ?? "")}.");

                return 1;
            }
            // If here, the ping message was sent.

            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_KeepAlive_IfNeeded_Async)} - " +
                $"Ping was sent to WSEndpoint, over ConnectionId = {(this.ConnectionId ?? "")}.");

            // Set the ping status, so we can wait for a reply, instead of resending a ping...
            this._keepAliveStatus = 1;

            return 1;
        }

        /// <summary>
        /// Create a new connection Id for the client.
        /// Gets called when a new connection is initiated.
        /// NOTE: This is the client-created connectionId.
        /// Meaning, it will be replaced by the server-created ConnectionId on successful registration.
        /// </summary>
        protected void CreateNewConnectionID()
        {
            var g = System.Guid.NewGuid();

            this.ConnectionId = g.ToString();
        }

        /// <summary>
        /// This is a hook, in the Setup Before Connection logic flow, to provide a call point for determining any dynamic connection info, such as host, port, or url.
        /// This is especially used by websocket clients, whose connection url is determined by server load balancing and region.
        /// For a simple TCP socket client connecting to a static, target server, this method will simply return success (1).
        /// NOTE: This method is called each time the client attempts to connect.
        /// </summary>
        /// <returns></returns>
        protected virtual async Task<int> Get_ConnectionInfo()
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
            // For a tcp socket, this may be a call to 
            return 1;
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
        /// Public method for sending an arbitrary message across the websocket.
        /// Accepts a message of any class. Needs a target channel name. The scope is optional.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="channel"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        public async Task<int> SendMessage_to_Endpoint(object msg, string channel = "", string scope = "", string corelationid = "")
        {
            // Do we allow sending...
            if (!this._allowsend)
            {
                // No outgoing messages are allowed.
                return 0;
            }

            return await Send_Object_to_Endpoint(msg, channel, scope, corelationid);
        }

        /// <summary>
        /// This method sends the client's connection registration data, once connected to the WSHost.
        /// This method is also be called from outside the client, to send updated registrations, for events like user log out, language change, or changes to transport properties like keepalive and echo.
        /// NOTE:   This method is virtual, so it can be overridden for new registration behavior.
        ///         Specifically, this call, performs a connection registration as a WSLibVersion = 1 client, and will error out if executed on a non version=1 instance.
        /// </summary>
        /// <returns></returns>
        public virtual async Task<int> Send_RegistrationMessage()
        {
            try
            {
                // Confirm we are set as a TCP/WSLibVersion=1 client...
                if (this.LibVersion != LibVersions.CONST_LibVersion_1)
                {
                    // We are not defined as a version 1 client.
                    // Which means the deriving class did not include an override of this method as a non version 1 client.
                    // So, we must error the client connection.


                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_RegistrationMessage)} - " +
                        $"Cannot send {(this.PropName_ClientLibVer ?? "")}=1 registration data, for a non version 1 client. " +
                        $"This method must be overridden for proper registration behavior.");

                    return -3;
                }

                // MAKE NO CHECKS THAT WE ARE ALLOWED TO SEND IN THIS METHOD, AS THIS METHOD MUST REGISTER THE CONNECTION BEFORE MESSAGES ARE ALLOWED TO SEND.
                //// Do we allow sending...
                //if(!this._allowsend)
                //{
                //    // No outgoing messages are allowed.
                //    return 0;
                //}

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_RegistrationMessage)} - " +
                    "Attempting to send registration message to server...");

                // Compose a registration message...
                var rmsg = new ConnRegisterDTO();
                rmsg.ConnectionId = this.ConnectionId;
                rmsg.UserId = this.UserId;
                rmsg.DeviceId = this.DeviceId;

                List<string> props = new List<string>();

                // Set the loopback echo flag is needed...
                if (this.Register_with_Loopback_AllMessages)
                {
                    // Set a property for loopback of raw messages...
                    props.Add("\"loopback\":\"rawmsg\"");
                }
                else
                {
                    // Loopback is not needed.
                    // Have the WShost remove the loopback flag if it's set...
                    props.Add("\"loopback\":\"off\"");
                }

                // Set the disable keepalive if needed...
                if (this.Cfg_Disable_KeepAlive)
                {
                    // Set a property to turn off keepalives...
                    props.Add("\"keepalive\":\"off\"");
                }
                else
                {
                    // Set a property to turn on keepalives...
                    props.Add("\"keepalive\":\"on\"");
                }

                // Set a process pid if defined...
                if (this.Pid > 0)
                {
                    // Set a property for the process pid...
                    props.Add("\"pid\":\"" + this.Pid.ToString() + "\"");
                }

                // Set a RuntimeId if defined...
                if (!string.IsNullOrEmpty(this.RuntimeId))
                {
                    // Set a property for the process RuntimeId...
                    props.Add("\"runtimeid\":\"" + this.RuntimeId + "\"");
                }

                rmsg.Props = props.ToArray();

                var val = await this.Send_Object_to_Endpoint(rmsg);
                if (val != 1)
                {
                    // Error occurred while attempting to send the registration message to the server.

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_RegistrationMessage)} - " +
                        "Error occurred while attempting to send registration message to server.");

                    return -1;
                }

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_RegistrationMessage)} - " +
                    "Registration message was sent to server.");

                return val;
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_RegistrationMessage)} - " +
                    "Exception occurred while attempting to send registration message to server.");

                return -2;
            }
        }

        /// <summary>
        /// Internal method for sending arbitrary messages across the websocket.
        /// Give it the object instance to send and the channel it will be sent over.
        /// The scope is optional.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="channel"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        protected async Task<int> Send_Object_to_Endpoint(object payload, string channel = "", string scope = "", string corelationid = "")
        {
            string messagetype = payload.GetType().Name;
            string jsonmsg = JsonConvert.SerializeObject(payload);

            // See if chunking is enabled for this connection...
            if(!this.Cfg_EnableChannelLayerChunking)
            {
                // Chunking is off.
                // We will attempt to send all messages in a single frame.
                // This will be done, below.
            }
            else
            {
                // Chunking is enabled.
                // We will check if the message needs to be split up for sending.

                // Ensure the serialized buffer is not too large for the receiver...
                // We derate the max size enough to fit the message envelope and header (length value).
                if (jsonmsg.Length > (this.MaxMessageSize - 1024))
                {
                    // Message is too large to fit in a single message frame.
                    // Message needs to be split up.

                    // Given message frame is too large to send in one frame.
                    // We will chunk it up, sending pieces, and the other end will reassemble them for processing.

                    // The given message has an assigned channel.
                    // We will attempt to honor that channel assignment, and send chunks over it.

                    // Setup our large message sender...
                    var lms = new LargeMsgSender();
                    lms.MaxChunkSize = (this.MaxMessageSize - 1024);
                    // Load the raw message to be chunked out...
                    // Give the chunker the messageid of the composed message...
                    var resload = lms.Load(GetNextMessageId(), messagetype, jsonmsg, channel, scope, corelationid);
                    if (resload != 1)
                    {
                        // Failed to load the message for chunking.

                        this.Logger?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Object_to_Endpoint)} - " +
                            $"Failed to load outgoing message for chunking.");

                        return -1;
                    }

                    // Tell it to do a blocking send, to our send call...
                    // This will block the thread, until all chunks are sent, the connection drops, or we are cancelled.
                    // Give it a send delegate, a delegate for creating messageIds, and our cancellation token.
                    var ressend = await lms.SendChunksAsync(this.Send_SerializedObject_toEndpoint_Async, this._cts.Token);
                    if (ressend == 0)
                    {
                        // The send was cancelled.

                        this.Logger?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Object_to_Endpoint)} - " +
                            $"Send was cancelled while conveying chunked message to the remote endpoint.");

                        return 0;
                    }
                    else if (ressend < 0)
                    {
                        // The send failed.

                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Object_to_Endpoint)} - " +
                            $"Send failed while conveying chunked message to the remote endpoint.");

                        return -1;
                    }
                    // If here, we were able to send all chunks of the message.

                    return 1;
                }
                else
                {
                    // Message can be sent in a single frame.
                    // We will pass it along like normal.
                    // This will be done, below.
                }
            }

            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_Object_to_Endpoint)} - " +
                $"Attempting to send object to {(this.TransportShortName?.ToUpper() ?? "")}Endpoint...");

            return await Send_SerializedObject_toEndpoint_Async(messagetype, jsonmsg, channel, scope, corelationid);
        }

        /// <summary>
        /// Used by this layer, to exchange keep-alive messages with the caller.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> SendPing_toEndpoint_Async()
        {
            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(SendPing_toEndpoint_Async)} - " +
                $"Attempting to send ping message to {(this.TransportShortName?.ToUpper() ?? "")}Endpoint...");

            return await Send_SerializedObject_toEndpoint_Async("ping", "");
        }
        /// <summary>
        /// Used by this layer, to exchange keep-alive messages with the caller.
        /// </summary>
        /// <returns></returns>
        protected async Task<int> SendPong_toEndpoint_Async()
        {
            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(SendPong_toEndpoint_Async)} - " +
                $"Attempting to send pong message to {(this.TransportShortName?.ToUpper() ?? "")}Endpoint...");

            return await Send_SerializedObject_toEndpoint_Async("pong", "");
        }

        /// <summary>
        /// Accepts any json-serialized object type, wraps it in a message envelope, and sends it to the websocket service.
        /// </summary>
        /// <param name="objecttype"></param>
        /// <param name="jsonobject"></param>
        /// <param name="channel"></param>
        /// <param name="scope"></param>
        /// <param name="corelationid"></param>
        /// <returns></returns>
        protected async Task<int> Send_SerializedObject_toEndpoint_Async(string objecttype, string jsonobject, string channel = "", string scope = "", string corelationid = "")
        {
            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_SerializedObject_toEndpoint_Async)} - " +
                $"Sending serialized message to {(this.TransportLongName?.ToLower() ?? "")} service.");

            // Ensure any json string exists at this point...
            if (jsonobject == null)
                jsonobject = "";

            if(!this.Cfg_EnableChannelLayerChunking)
            {
                // No large message support.

                // Ensure the serialized buffer is not too large for the receiver...
                // We derate the max size enough to fit the message envelope and header (length value).
                if(jsonobject.Length > (this.MaxMessageSize - 1024))
                {
                    // Message is too large to fit in a single message frame.
                    // We will tell the caller, so they can send the message, piece-wise.

                    this.Logger?.Debug(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_SerializedObject_toEndpoint_Async)} - " +
                        $"Message is too large ({(jsonobject?.Length.ToString() ?? "unknown size")}) to send to the remote endpoint.");

                    return -10;
                }
            }
            // The raw message will fit into a single message frame.

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

            return await Send_MessageEnvelope_toEndpoint_Async(me);
        }

        /// <summary>
        /// Accepts a prepared message envelope, and sends it to the tcp/websocket service.
        /// </summary>
        /// <param name="me"></param>
        /// <returns></returns>
        protected async Task<int> Send_MessageEnvelope_toEndpoint_Async(MessageEnvelope me)
        {
            try
            {
                // Check if connected...
                if (this.disposedValue)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                        $"{(this.TransportLongName ?? "Socket")} is already disposed.");

                    return -1;
                }
                if (!this.IsConnected)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                        $"{(this.TransportLongName ?? "Socket")} is not connected.");

                    return 0;
                }

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                    "Attempting to send message to remote endpoint...");

                // Serialize the envelope and convert it to bytes...
                var jsonmsg = JsonConvert.SerializeObject(me);
                // Convert the string to bytes for transport...
                byte[] d = Encoding.UTF8.GetBytes(jsonmsg);


                //************************************************************************************************************
                // Start Send Thread Lock
                //************************************************************************************************************
                // Enter a thread lock, to prevent garbling of sent data...
                await _write_semaphore.WaitAsync();
                try
                {
                    // Send the message...
                    var res = await this.RawTransportSend(d);
                    if (res >= 1)
                    {
                        // Send was successful.
                        // We will return after the finally.
                    }
                    else
                    {
                        // Call failed.
                        // We will return, here, and the finally will release our send mutex.
                        return res;
                    }
                }
                finally
                {
                    _write_semaphore.Release();
                }
                //************************************************************************************************************
                // End Send Thread Lock
                //************************************************************************************************************

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                    "Message sent to remote endpoint.");

                return 1;
            }
            catch (WebSocketException wse)
            {
                // Websocket exception occurred.
                // Meaning, the websocket has been closed by the other end.

                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} was closed by the other end, and cannot send messages.");

                return -1;
            }
            catch (System.Net.Sockets.SocketException)
            {
                // TCPsocket exception occurred.
                // Meaning, the tcpsocket has been closed by the other end.

                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} was closed by the other end, and cannot send messages.");

                return -1;
            }
            catch (ObjectDisposedException ode)
            {
                // Socket is disposed.

                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is disposed, and cannot send messages.");

                return -1;
            }
            catch (InvalidOperationException ioe)
            {
                // Socket is not open.

                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is not open, and cannot send messages.");

                return 0;
            }
            catch (Exception e)
            {
                // Unknown exception type...

                var f = e.GetType();

                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_MessageEnvelope_toEndpoint_Async)} - " +
                    $"Unknown exception type occurred ({f}) while attempting to send message over {(this.TransportLongName?.ToLower() ?? "socket")}.");

                return -1;
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
        abstract protected Task<int> ReceiveLoop();
        //{
        //    OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
        //        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
        //        "Receive loop method has been called.");

        //    // Check if connected...
        //    if (disposedValue)
        //    {
        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
        //            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
        //            "Websocket is already disposed.");

        //        return -1;
        //    }

        //    // Do any setup....

        //    OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
        //        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
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
        //                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
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
        //                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
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
        //                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
        //                                "Failed to process and dispatch received message.");
        //                        }
        //                        else if (res == -1)
        //                        {
        //                            // Message processing failed.
        //                            // We will consider this fatal to the current connection.

        //                            // We failed to complete post-connection work.
        //                            // We must recycle this connection, and try again.

        //                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
        //                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
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
        //                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
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
        //                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
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
        //                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
        //                        "The connection was closed.");

        //                    return 1;
        //                }

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
        //                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
        //                    "Web Socket Exception occurred during receive loop. Likely from a connection closure.");

        //                return 1;
        //            }
        //            catch (Exception e)
        //            {
        //                // Clear the send flag, to prevent outgoing messages...
        //                this._allowsend = false;

        //                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
        //                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
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
        //            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
        //            "Exception occurred while looping, most likely from the cancellation token being disposed or null.");

        //        return -1;
        //    }
        //    finally
        //    {
        //        ms?.Dispose();

        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
        //            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
        //            "Receive loop method is returning.");
        //    }
        //}

        /// <summary>
        /// This method is called anytime the websocket receives a message.
        /// This method is NOT meant to handle the received message.
        /// It is intended to provide a hook for app-based, realtime update of received message diagnostic counts.
        /// NOTE: This method is called on the Receive Loop's thread, so DO NOT block this thread.
        /// Best practice is to spawn a thread in this method, that sends out the received event.
        /// </summary>
        protected virtual void FireMessageReceivedEvent()
        {
            return;
        }

        /// <summary>
        /// First-handler of any received message.
        /// Will hydrate it to the standard message envelope, and dispense it as internal or consumer message.
        /// </summary>
        /// <param name="rawmsg"></param>
        /// <returns></returns>
        protected int Process_ReceivedMessage(string rawmsg)
        {
            // Each message arrives as a json string of a message envelope.
            // We need to deserialize that, recover the message type, and deserialize that.

            try
            {
                // Check if a raw message handler is set...
                if (this._delOnRawMessageReceived != null)
                {
                    // Call the raw message handler...
                    this._delOnRawMessageReceived(this, rawmsg);

                    return 1;
                }

                // Recover the envelope...
                var me = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageEnvelope>(rawmsg);
                if (me == null)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage)} - " +
                        "Received message was not a message envelope type, and could not be deserialized.");

                    return 0;
                }

                // The message envelope has a message id, timestamp, data type and payload as json.

                // Get the message type...
                var mt = me.MessageType.ToLower();

                // See if the message is something we handle, and don't pass along...
                int res = this.Process_InternalMessage(mt, me.Data);
                if (res < 0)
                {
                    // Something was wrong with the received message.
                    // We must disregard it, and try again.

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage)} - " +
                        "Received internal message could not be processed.");

                    return 0;
                }
                else if (res == 1)
                {
                    // The message was an internal message for us only.
                    // And, it has been handled.
                    // We will return success.

                    return 1;
                }
                // If here, the message is not an internal message.
                // We will pass it along for processing.

                // All non-internal messages require a payload.
                // Check that a payload exists...
                if (string.IsNullOrWhiteSpace(me.Data))
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage)} - " +
                        "Received message has no payload.");

                    return 0;
                }

                // Here, we intercept incoming messages, and handling any chunking of large messages.
                // We will look for any of the chunk message types...
                // ChunkAckDTO, ChunkRequestDTO, ChunkDTO, ChunkStartDTO.
                // If we encounter one, we forward it to the chunk handler...
                if(mt == nameof(ChunkStartDTO).ToLower() ||
                    mt == nameof(ChunkDTO).ToLower() ||
                    //mt == nameof(ChunkAckDTO).ToLower() \\
                    //mt == nameof(ChunkCancelDTO).ToLower() ||
                    //mt == nameof(ChunkRequestDTO).ToLower() ||
                    mt == nameof(ChunkEndDTO).ToLower()
                    )
                {
                    // Received message is a chunking message.
                    // We will forward it to our chunking handler...
                    ProcessChunkingMessage(me.MsgId, mt, me.Data, me.Channel, me.Scope);

                    return 1;
                }
                // Not a chunking message type.
                // We will dispatch it as normal.

                // We will let subscribers of our received delegate do deserialization...
                DispatchReceivedMessage(mt, me.Data, me.Channel, me.Scope);

                //// Deserialize the message to the correct type...
                //if (mt == nameof(ChatMessageDTO).ToLower())
                //    DispatchReceivedMessage(nameof(ChatMessageDTO), JsonConvert.DeserializeObject<ChatMessageDTO>(me.Data));
                //else if (mt == nameof(String).ToLower())
                //    DispatchReceivedMessage(nameof(String), JsonConvert.DeserializeObject<ChatMessageDTO>(me.Data));
                //else
                //{
                //    // Not a supported message type.
                //    return 0;
                //}

                return 1;
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_ReceivedMessage)} - " +
                    "Exception occurred while processing received message.");

                return 0;
            }
        }

        /// <summary>
        /// Process large messages that are conveyed via chunks on channels.
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="messagetype"></param>
        /// <param name="data"></param>
        /// <param name="channel"></param>
        /// <param name="scope"></param>
        private async void ProcessChunkingMessage(string msgId, string messagetype, string data, string channel, string scope)
        {
            if(string.IsNullOrEmpty(msgId))
            {
                // Invalid messageid.
                return;
            }
            if(string.IsNullOrEmpty(messagetype))
            {
                // Invalid message type.
                return;
            }
            if(string.IsNullOrEmpty(data))
            {
                // Invalid message data.
                return;
            }

            // Determine what action to take...
            if(messagetype == nameof(ChunkStartDTO).ToLower())
            {
                // The far end is attempting to send us a large message, one chunk at a time.
                // And, they've sent us the metadata for the message.

                ChunkStartDTO dto;
                try
                {
                    dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ChunkStartDTO>(data);
                    if(dto == null)
                    {
                        // Failed to deserialize chunk start message.
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                            $"Failed to deserialize chunk start message.");

                        return;
                    }
                }
                catch(Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                        $"Exception occurred while attempting to deserialize chunk start message.");

                    return;
                }

                // We will create a new large message receiver, to handle the in-progress large message...
                var lmr = new LargeMsgReceiver();
                lmr.Scope = scope;
                lmr.Channel = channel;
                var res = await lmr.AcceptChunkStart(dto);
                if(res != 1)
                {
                    // Failed to accept chunk start message.
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                        $"Failed to accept chunk start for large message.");

                    return;
                }
                // If here, we accepted the chunk start message.
                // We can add the receiver to our running list.
                this._largemsgreceivers.Add(dto.MsgId, lmr);

                return;
            }
            else if(messagetype == nameof(ChunkDTO).ToLower())
            {
                // The far end has sent us a chunk that we need to include with the large message we're building.

                ChunkDTO dto;
                try
                {
                    dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ChunkDTO>(data);
                    if(dto == null)
                    {
                        // Failed to deserialize chunk message.
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                            $"Failed to deserialize chunk message.");

                        return;
                    }
                }
                catch(Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                        $"Exception occurred while attempting to deserialize chunk message.");

                    return;
                }

                // Look for the receiver...
                if(this._largemsgreceivers.ContainsKey(dto.MsgId))
                {
                    // Have a receiver for the in-progress message.
                    var rcv = this._largemsgreceivers[dto.MsgId];

                    // Accept the received chunk...
                    var res = await rcv.AcceptChunk(dto);
                    if(res != 1)
                    {
                        // Failed to accept message chunk.
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                            $"Failed to accept message chunk of large message.");

                        return;
                    }
                }
                else
                {
                    // We don't have a receiver for the message.
                    // Since, we never received a start message, we cannot handle it.

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                        $"Received message chunk without a chunk start message. Cannot process message, msgId: ({(dto.MsgId ?? "")}).");

                    return;
                }
            }
            else if(messagetype == nameof(ChunkEndDTO).ToLower())
            {
                // The far end has sent us an end message, so we know that we can compose and handle the large message.

                ChunkEndDTO dto;
                try
                {
                    dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ChunkEndDTO>(data);
                    if(dto == null)
                    {
                        // Failed to deserialize chunk start message.
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                            $"Failed to deserialize chunk end message.");

                        return;
                    }
                }
                catch(Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                        $"Exception occurred while attempting to deserialize chunk end message.");

                    return;
                }

                // Look for the receiver...
                if(this._largemsgreceivers.ContainsKey(dto.MsgId))
                {
                    // Have a receiver for the in-progress message.
                    var rcv = this._largemsgreceivers[dto.MsgId];

                    // Accept the chunk end message...
                    var res = await rcv.AcceptChunkEnd(dto);
                    if(res.res != 1 && res.me != null)
                    {
                        // Failed to accept chunk end message.
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                            $"Failed to accept chunk end message.");

                        return;
                    }
                    // If here, we have composed the large message, and can dispatch it as we would normal size messages.

                    // We will let subscribers of our received delegate do deserialization...
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    DispatchReceivedMessage(res.me.MessageType, res.me.Data, res.me.Channel, res.me.Scope);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                else
                {
                    // We don't have a receiver for the message.
                    // Since, we never received a start message, we cannot handle it.

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                        $"Received chunk end message without a chunk start message. Cannot process message, msgId: ({(dto.MsgId ?? "")}).");

                    return;
                }
            }
            else if(messagetype == nameof(ChunkCancelDTO).ToLower())
            {
                // The far end has sent us a cancel message.
                // We need to teardown any receiver for the message.

                ChunkCancelDTO dto;
                try
                {
                    dto = Newtonsoft.Json.JsonConvert.DeserializeObject<ChunkCancelDTO>(data);
                    if(dto == null)
                    {
                        // Failed to deserialize chunk cancel message.
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                            $"Failed to deserialize chunk cancel message.");

                        return;
                    }
                }
                catch(Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                        $"Exception occurred while attempting to deserialize chunk cancel message.");

                    return;
                }

                // Remove the receiver...
                this._largemsgreceivers.Remove(dto.MsgId);
            }
            else
            {
                // Unknown chunking message type.
                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ProcessChunkingMessage)} - " +
                    $"Received unknown chunking message type: ({(messagetype ?? "")}).");

                return;
            }
        }

        /// <summary>
        /// Removes any large message receivers that haven't been updated in a while.
        /// </summary>
        private void PruneStaleLargeMessageReceivers()
        {
            if(this._largemsgreceivers == null)
                this._largemsgreceivers = new Dictionary<string, LargeMsgReceiver>();

            if (this._largemsgreceivers.Count == 0)
                return;

            // Get the current time...
            var ctime = DateTime.UtcNow;

            List<string> entriestodelete = new List<string>();

            // Loop through each receiver...
            foreach(var r in this._largemsgreceivers)
            {
                if (r.Value == null)
                {
                    entriestodelete.Add(r.Key);
                    continue;
                }

                // Calculate when the entry expires...
                if(!r.Value.LastReceivedTimeUTC.HasValue)
                    continue;

                // Calculate when the receiver expires...
                var etime = r.Value.LastReceivedTimeUTC.Value.AddSeconds(this._cfg_ReceiverTimeout);

                // See if it expired...
                if(etime.CompareTo(ctime) < 0)
                {
                    // The receiver has expired.
                    entriestodelete.Add(r.Key);
                }
            }

            // Delete entries...
            foreach(var d in entriestodelete)
                this._largemsgreceivers.Remove(d);
        }

        #endregion


        #region Handle Internal Messages

        /// <summary>
        /// Filters out any internal messages (ping, pong, registration replies).
        /// Returns 1 if the message was handled.
        /// Returns 0 if the message is not internal.
        /// Returns negatives for errors.
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

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    "Received ping request from web service.");

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    "Sending ping reply to web service...");

                // Send a pong reply...
                Task.Run(async () =>
                {
                    // Wrap the send method in a try-catch to ensure it never throws and unwinds to the Task Scheduler base.
                    try
                    {
                        await this.SendPong_toEndpoint_Async();
                    }
                    catch(Exception e) { }
                });

                // Notify the caller that the received message is an internal one, and we've handled it...
                return 1;
            }
            else if (messagetype == "pong")
            {
                // The other end sent us a pong message.
                // This is an attempt to keep the connection alive.

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    "Received ping reply from web service.");

                // We can reset the ping status...
                this._keepAliveStatus = 0;

                // Notify the caller that the received message is an internal one, and we've handled it...
                return 1;
            }
            else if (messagetype == nameof(ConnRegisterReplyDTO).ToLower())
            {
                // The received message is a Registration Reply message.
                // This is a reply to a previously sent registration message.
                // And, it will contain our server-created ConnectionId.
                // We will need this before we are "open for business".

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Registration reply received. Attempting to handle...");

                // NOTE: Our client will send registration messages each time the connection context changes.
                // This will occur at events, such as user login and logout, and when connection properties change, such as loopback, echo, etc.
                // So, we should expect to receive these replies more than once.
                // And, be able to handle each one.

                // Attempt to deserialize the message...
                ConnRegisterReplyDTO dto = null;
                try
                {
                    if(string.IsNullOrEmpty(messagedata))
                    {
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                            "Failed to deserialize the ConnRegisterReplyDTO. Cannot accept the reply message.");

                        // We must report this as a fatal connection error...
                        return -10;
                    }

                    dto = JsonConvert.DeserializeObject<ConnRegisterReplyDTO>(messagedata);
                    if (dto == null)
                    {
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                            "Failed to deserialize the ConnRegisterReplyDTO. Cannot accept the reply message.");

                        // We must report this as a fatal connection error...
                        return -10;
                    }
                }
                catch (Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Failed to deserialize the ConnRegisterReplyDTO. Cannot accept the reply message.");

                    // We must report this as a fatal connection error...
                    return -10;
                }
                // If here, we have the ConnRegisterReplyDTO.
                // Each time we receive this reply message, we need to accept the server-provided ConnectionId as our connectionid.

                // We need to check the ConnectionId for a few things:
                // We need to confirm the server-created ConnectionId is valid.
                // We need to confirm that the reply message contains our current ConnectionId.
                // This second check verifies the reply message belongs to the current connection attempt of our client.
                // I'll explain:
                // Since our client uses a separate thread for receiving messages, the receive loop may not be fully closed when a connection is recycled and retried.
                // Specifically, it is possible for our receive loop to be handling a registration reply from a previous connection attempt, while we are trying to establish a new one.
                // If this were the case, the reply message would have a ConnectionId of our previous connection.
                // And as such, we will erroneously accept the server-provided ConnectionId of the previous connection.
                // To prevent this from occurring, the server sends us the ConnectionId we are to accept, and the old ConnectionId that we started with.
                // And, we simply verify the old ConnectionId matches what we currently have, disregarding the reply message if the two don't match.

                // Check for connectionId and DeviceId...
                if(string.IsNullOrEmpty(dto.ConnectionId))
                {
                    // The server gave us a malformed connectionid.
                    // We will regard this as a malformed registration message.

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Server sent a ConnRegisterReplyDTO, with a malformed ConnectionId. We cannot accept the server-provided connectionId, and must close.");

                    // We must report this as a fatal connection error...
                    return -10;
                }

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Registration reply received with valied ConnectionId.\r\n" +
                    $"Client Current ConnectionId = '{(this.ConnectionId ?? "")}'\r\n" +
                    $"DTO Old ConnectionId = '{(dto.OldConnectionId ?? "")}'\r\n" +
                    $"DTO New ConnectionId = '{(dto.ConnectionId ?? "")}'");

                // Verify the old ConnectionId in the reply message matches our current ConnectionId...
                if(this.ConnectionId != dto.OldConnectionId)
                {
                    // The registration reply message, from the server, does not belong to our current connection attempt.
                    // It has a previous, or unknown, OldConnectionId.
                    // So, we must assume that may be running in an old instance of the receive loop, and are processing a reply from that previous connection.
                    // So, we will simply disregard the reply message, and refuse handling.

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                        "Server sent us a registration reply with a mismatched OldConnectionId. We cannot accept the server-provided connectionId, from this message.");

                    // It is uncertain if we need to return an error for this problem, as this receive loop has likely just not yet checked on its cancellation token, and began teardown.
                    // The catch is that: If we do return an error from this method, the entire connection will be determined as bad and get recycled.
                    // The problem with doing so (returning an error) is that the connection likely already got recycled and is trying to reconnect.
                    // So, we will disregard the errant message and notify the caller that we handled it.

                    // As well. Our connection loop includes logic that will wait for a registration reply to come back.
                    // And if the wait timeout occurs, the connection loop will recycle, on its own, and reattempt.
                    // So, we can simply let that timeout cover the scenario of having received an obsolete registration message.
                    // So, it seems logical that we can safely return a handled response to the caller.

                    // Notify the caller that we handled the registration message...
                    // See above for rationale.
                    return 1;
                }
                // If here, the registration reply includes a ConnectionId we can accept.

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    $"Registration reply received.\r\n" +
                    $"Updating Connectionid from '{(this.ConnectionId ?? "")}' to '{(dto.ConnectionId ?? "")}'...");

                // Update our connectionId to what the server gave us...
                // No other properties need to be used from the reply message.
                this.ConnectionId = dto.ConnectionId;

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_InternalMessage)} - " +
                    "Registration reply received and handled. Setting reply received flag...");

                // Now that we have accepted the server-provided ConnectionId, we can claim we are "open for business", and can allow messages to be sent.
                // Our connection becomes open for business by setting the "_allowsend" flag.
                // Since our receive loop is considered a slave loop to the connection loop logic, we will allow that loop to be authoritative over the "_allowsend" flag.
                // We will notify the connection loop that we've received a valid connection reply message, here...
                this._registrationreplyreceived = true;

                // Notify the caller that the received message is an internal one, and we've handled it...
                return 1;
            }
            // If here, the message type is not a known internal message.
            // We will return that it was unhandled.

            // Return that the message was not handled, internally...
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
        protected int DispatchReceivedMessage(string messagetype, string jsondata, string channel = "", string scope = "")
        {
            try
            {
                if (string.IsNullOrEmpty(channel))
                {
                    // No channel is set.

                    // Send the message to the generic handler...
                    if (_delOnMessageReceived != null)
                    {
                        var res = _delOnMessageReceived(this, messagetype, jsondata);

                        return res;
                    }

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchReceivedMessage)} - " +
                        $"Default message handler is not defined. Message type is: {messagetype}.");

                    return 0;
                }
                else
                {
                    // A channel is defined for the message.
                    // We will attempt to route it.

                    IChannelAdapter ca = null;

                    // Get the handler from our adapter list...
                    if (!this._ChannelMessageHandlers.TryGetValue(channel, out ca))
                    {
                        // We don't have a subscribed handler matching the channel name.

                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchReceivedMessage)} - " +
                            $"Received message from channel ({channel}), but no handler is defined. Message type is: {messagetype}.");

                        return -1;
                    }
                    // If here, we have a handler for the message.

                    // Dispatch the message to the handler...
                    try
                    {
                        var res = ca.AcceptIncomingMessage(this, messagetype, jsondata);
                        return res;
                    }
                    catch(Exception e)
                    {
                        this.Logger?.Error(e,
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchReceivedMessage)} - " +
                            $"Exception occurred during message dispatch. Exception Message = {e.Message}");

                        return -10;
                    }
                }

                return 1;
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchReceivedMessage)} - " +
                    $"Exception occurred while dispatching received message to a delegate. Exception Message = {e.Message}");

                return -10;
            }
        }

        /// <summary>
        /// Sends connection lost event to any connected delegate.
        /// </summary>
        protected void DispatchConnectionLost()
        {
            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchConnectionLost)} - " +
                "Connection lost or closed. Determining if needing to call conn loss delegate...");

            this.connlost_truecounter++;

            // We will only call the connection lost delegate ONCE and once only, on connection loss or closure.
            // We do this, so consumers don't need to worry about idempotency issues.
            // To ensure this, we employ 4-part handshaking with the connection loop.
            // The connection loop will set a boolean when it achieves connection.
            // That true state allows us to call our delegate once, clearing the flag before we call it.
            // If we are here, and the flag is clear, we assume we've called the delegate, and simply return.
            lock(this.lclock)
            {
                if(!this._connectionclosuredelegate_armed)
                {
                    // We've already called the connection lost method.

                    this.Logger?.Trace(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchConnectionLost)} - " +
                        "Conn loss delegate already called. No need to call it again, until a new connection is made.");

                    return;
                }

                // Reset the flag, so we can call the delegate...
                this._connectionclosuredelegate_armed = false;
            }

            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchConnectionLost)} - " +
                "Conn loss delegate has not been called for closure. Attempting to call conn loss delegate...");

            try
            {
                if (this._delConnectionLost != null)
                {
                    this._delConnectionLost(this);
                }
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchConnectionLost)} - " +
                    "Exception occurred while dispatching connectionlost even to a delegate.");
            }
        }

        /// <summary>
        /// Override this method to publish connection made event.
        /// Make sure that any override calls the base method, first.
        /// </summary>
        protected virtual void DispatchConnected()
        {
            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(DispatchConnected)} - " +
                "Connection is successful. Firing connected event...");

            // We will arm the connection lost delegate, so it can trigger on closure.
            lock(this.lclock)
            {
                // Arm the connection closure delegate...
                this._connectionclosuredelegate_armed = true;
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
            // The state is changing.

            // Create the state change string that we will pass along.
            state_change_string = "Status changed from " + this.State.ToString() + " to " + newstate.ToString() + ".";

            // Capture the new state.
            this.State = newstate;

            this.Logger?.Debug(
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
                this.Logger?.Info(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(PromoteStatus_from_NewlyOpen_to_Open)} - " +
                    "Promoting status from Newly Open to Open.");

                this.UpdateState(eEndpoint_ConnectionStatus.Open);
            }
        }

        #endregion
    }
}
