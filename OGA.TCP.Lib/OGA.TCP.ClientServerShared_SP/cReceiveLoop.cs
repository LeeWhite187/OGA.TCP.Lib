using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OGA.TCP.Shared.Encoding;

namespace OGA.TCP
{
    /// <summary>
    /// Delegate signature for receive-loop status change notification.
    /// </summary>
    /// <param name="mep">The receive loop reporting the change.</param>
    /// <param name="statusupdate">Human-readable description of the state transition.</param>
    public delegate void dStatus_Change(cReceiveLoop mep, string statusupdate);

    /// <summary>
    /// Delegate signature for a received frame.
    /// The loop delivers the frame type (from the FrameTypes registry) and the raw body bytes.
    /// The loop performs no body interpretation: json decoding and binary header parsing are the session layer's job.
    /// Runs on the loop's pump thread: keep handlers fast, and queue heavy work elsewhere.
    /// </summary>
    /// <param name="mep">The receive loop that received the frame.</param>
    /// <param name="frametype">The frame's type byte, from the FrameTypes registry.</param>
    /// <param name="body">The frame's body bytes. Never null; empty bodies are absorbed as keepalives and not delivered.</param>
    public delegate void dMessage_Received(cReceiveLoop mep, byte frametype, byte[] body);

    /// <summary>
    /// Delegate signature for connection-failure notification.
    /// Fired when the connection is lost, errored, or closed by the peer — never for a locally initiated CloseDown or Dispose.
    /// </summary>
    /// <param name="mep">The receive loop reporting the failure.</param>
    public delegate void dConnection_Went_Bad(cReceiveLoop mep);

    /// <summary>
    /// TCP frame-reading engine: supplies what raw TCP lacks and websocket framing provides natively — message
    ///     boundaries and a frame-type discriminator.
    /// Reads the five-byte preamble (4-byte little-endian body length + 1-byte frame type from the FrameTypes
    ///     registry), validates it, accumulates the body, and hands (frametype, bytes) to the assigned delegate.
    /// This class is a TCP-only construct: the websocket transport carries equivalent framing in its own protocol.
    /// It never interprets bodies, and knows nothing of envelopes, channels, or chunking.
    /// One instance serves one connection, for the life of that connection (single-use, like its owning endpoint).
    /// The engine runs a single async pump task; no threadpool threads are blocked while waiting for data.
    /// A whole-frame deadline (FrameReadTimeout) bounds how long a frame may take from its first byte to its last,
    ///     so a stalled or byte-dripping peer cannot hold the connection open indefinitely mid-frame.
    /// Idle time between frames is unlimited by design; connection-level liveness is the keepalive layer's job.
    /// </summary>
    public class cReceiveLoop : IDisposable
    {
        #region Private Fields

        /// <summary>
        /// Class name used for logging.
        /// </summary>
        private string _classname;

        /// <summary>
        /// Logger instance. Optional.
        /// </summary>
        private NLog.ILogger Logger;

        /// <summary>
        /// Number of instances created since process start. Interlocked, so concurrent accepts get distinct ids.
        /// </summary>
        static private int _instance_counter;

        /// <summary>
        /// The connection's TcpClient. Owned by the parent endpoint; this class never disposes it.
        /// </summary>
        private TcpClient _client;

        /// <summary>
        /// The connection's network stream. Owned by the parent endpoint via the TcpClient; never disposed here.
        /// </summary>
        private NetworkStream _stream;

        /// <summary>
        /// Cancellation source governing the pump task. Created by Begin_Comms; cancelled by CloseDown.
        /// </summary>
        private CancellationTokenSource _cts;

        /// <summary>
        /// The pump task, held for diagnostics. The pump owns its own exit; nothing joins on it.
        /// </summary>
        private Task _pumptask;

        /// <summary>
        /// Guards state transitions of the loop lifecycle (begin/close/dispose), which can race between
        ///     the owner's thread and the pump's failure paths.
        /// </summary>
        private readonly object _lifecyclelock = new object();

        /// <summary>
        /// Set once comms have begun. Begin_Comms is one-shot.
        /// </summary>
        private bool _comms_begun;

        /// <summary>
        /// Set once the loop has been told to end (CloseDown/Dispose) or has failed terminally.
        /// The pump observes it to exit quietly, and failure paths observe it to suppress
        ///     went-bad callbacks for locally initiated closures.
        /// </summary>
        private volatile bool _comms_ended;

        /// <summary>
        /// Live metrics store for the loop.
        /// </summary>
        private cEndpoint_Metrics _metrics;

        /// <summary>
        /// Consumer's frame handler. Set-only via OnMessage_Received; invoked per received frame.
        /// </summary>
        private dMessage_Received _delMessageReceived;

        /// <summary>
        /// Consumer's failure handler. Set-only via OnConnection_Went_Bad.
        /// </summary>
        private dConnection_Went_Bad _delConnectionWentBad;

        /// <summary>
        /// Consumer's status-change handler. Set-only via OnStatus_Change.
        /// </summary>
        private dStatus_Change _delStatusChange;

        #endregion


        #region Public Properties

        /// <summary>
        /// Instance Id of the loop, for log correlation.
        /// </summary>
        public int InstanceId { get; private set; }

        /// <summary>
        /// Current lifecycle state of the loop.
        /// Lost, Error, and Closed are terminal; see the enumeration docs for state meanings.
        /// </summary>
        public eLoop_ConnectionStatus State { get; private set; }

        /// <summary>
        /// The most bytes one frame body may carry.
        /// A received frame declaring a negative length, or a length above this cap, is treated as stream
        ///     corruption and is fatal to the connection.
        /// Coordinate with the owning endpoint's MaxFrameSize; the endpoint assigns this at connection setup.
        /// </summary>
        public int MaxFrameSize { get; set; } = OGA.TCP.Constants.CONST_MAX_MessageSize;

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
        /// Whole-frame deadline, in milliseconds (default 5000).
        /// Measured from a frame's first byte to its last: a peer that starts a frame must finish it within
        ///     this window, or the connection is declared Lost. This defeats byte-dripping (slowloris) peers.
        /// Idle time between frames is not limited by this value; between-frame liveness belongs to the keepalive layer.
        /// </summary>
        public int FrameReadTimeout { get; set; } = 5000;

        /// <summary>
        /// Returns a point-in-time copy of the loop's metrics.
        /// </summary>
        public cEndpoint_Metrics Metrics
        {
            get
            {
                var met = new cEndpoint_Metrics();
                met.CopyFrom(this._metrics);
                return met;
            }
        }

        /// <summary>
        /// Assign a handler to receive each frame's (frametype, body).
        /// Runs on the pump; keep it fast, and queue heavy work elsewhere.
        /// </summary>
        public dMessage_Received OnMessage_Received
        {
            set { this._delMessageReceived = value; }
        }

        /// <summary>
        /// Assign a handler to be notified when the connection is lost, errored, or closed by the peer.
        /// Not fired for locally initiated CloseDown or Dispose.
        /// </summary>
        public dConnection_Went_Bad OnConnection_Went_Bad
        {
            set { this._delConnectionWentBad = value; }
        }

        /// <summary>
        /// Assign a handler to receive lifecycle state-change notifications.
        /// </summary>
        public dStatus_Change OnStatus_Change
        {
            set { this._delStatusChange = value; }
        }

        #endregion


        #region ctor / dtor

        /// <summary>
        /// Constructor requires a connected TcpClient. Throws for a null or unconnected client,
        ///     matching the contract that a loop only ever fronts a live connection.
        /// </summary>
        /// <param name="client">The connected TcpClient whose stream the loop reads.</param>
        /// <param name="logger">Optional logger instance.</param>
        public cReceiveLoop(TcpClient client, NLog.ILogger logger = null)
        {
            this._classname = nameof(cReceiveLoop);
            this.InstanceId = Interlocked.Increment(ref _instance_counter);
            this.Logger = logger;

            if (client == null)
                throw new ArgumentNullException(nameof(client), "Receive loop requires a TcpClient instance.");
            if (!client.Connected)
                throw new ArgumentException("Receive loop requires a connected TcpClient instance.", nameof(client));

            this._client = client;
            this._metrics = new cEndpoint_Metrics();
            // Baseline the received timestamp at construction, so last-received consumers
            //  (keepalive credit, staleness checks) never see a default time.
            this._metrics.Last_Received_Message_Time = DateTime.UtcNow;

            this.State = eLoop_ConnectionStatus.Initialized;
        }

        /// <summary>
        /// Disposes the loop: closes it down and dereferences delegates.
        /// The TcpClient and stream are owned by the parent endpoint and are not disposed here.
        /// </summary>
        public void Dispose()
        {
            this.CloseDown();

            this._delMessageReceived = null;
            this._delConnectionWentBad = null;
            this._delStatusChange = null;

            this._client = null;
            this._stream = null;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Starts the read pump. One-shot: a loop instance reads one connection, once.
        /// Returns 1 on success; -1 when the loop is not in a startable state (already begun, closed, or disposed)
        ///     or the transport is not usable.
        /// </summary>
        /// <returns></returns>
        public int Begin_Comms()
        {
            lock (this._lifecyclelock)
            {
                if (this._comms_begun || this._comms_ended)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
                        "Receive loop was already started or is closed. Loops are single-use.");

                    return -1;
                }

                if (this.State != eLoop_ConnectionStatus.Initialized)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
                        $"Receive loop cannot start from state {this.State.ToString()}.");

                    return -1;
                }

                // Acquire the stream...
                try
                {
                    var cl = this._client;
                    if (cl == null || !cl.Connected)
                    {
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
                            "TcpClient is not usable. Cannot start receive loop.");

                        this.UpdateState(eLoop_ConnectionStatus.Error);
                        return -1;
                    }

                    this._stream = cl.GetStream();
                }
                catch (Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
                        "Failed to acquire the network stream. Cannot start receive loop.");

                    this.UpdateState(eLoop_ConnectionStatus.Error);
                    return -1;
                }

                this._comms_begun = true;

                // Connection open counts as liveness: baseline the received timestamp at startup, so
                //  last-received consumers (keepalive credit, staleness checks) never see a default time.
                this._metrics.Last_Received_Message_Time = DateTime.UtcNow;

                this.UpdateState(eLoop_ConnectionStatus.Newly_Opened);

                this._cts = new CancellationTokenSource();

                // Spawn the single pump task. It owns all reading for the connection's life.
                this._pumptask = Task.Run(() => this.ReadPump());
            }

            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
                "Receive loop started.");

            return 1;
        }

        /// <summary>
        /// Locally initiated shutdown of the loop.
        /// Idempotent. Does not fire the went-bad delegate: a local closedown is not a connection failure.
        /// The TcpClient and stream are released but not disposed; the parent endpoint owns them.
        /// </summary>
        public void CloseDown()
        {
            lock (this._lifecyclelock)
            {
                if (this._comms_ended)
                    return;

                this._comms_ended = true;

                // Transition to Closed through the legal path.
                // Both transitions are published: consumers (and the historical contract) observe the
                //  Shutting_Down step as well as the final Closed.
                if (this.State == eLoop_ConnectionStatus.Newly_Opened || this.State == eLoop_ConnectionStatus.Open)
                {
                    this.UpdateState(eLoop_ConnectionStatus.Shutting_Down);
                    this.UpdateState(eLoop_ConnectionStatus.Closed);
                }
                else if (this.State == eLoop_ConnectionStatus.Initialized)
                {
                    // Never started: stays Initialized, matching the historical contract that an
                    //  unstarted loop remains in its initial state through close and dispose.
                }
                // Terminal states (Lost, Error, Closed) stay as they are.

                // Cancel the pump. A pump blocked in a read is released when the owner closes the
                //  transport (which faults the read), or by the frame deadline.
                try { this._cts?.Cancel(); } catch (Exception) { }
            }

            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(CloseDown)} - " +
                "Receive loop closed down.");
        }

        #endregion


        #region Read Pump

        /// <summary>
        /// The single async read pump: preamble, validate, body, deliver — for the life of the connection.
        /// All failure classification happens here; the pump exits exactly once, through Fail_and_Close
        ///     or through a quiet locally-initiated exit.
        /// </summary>
        /// <returns></returns>
        private async Task ReadPump()
        {
            byte[] preamble = new byte[5];

            try
            {
                while (!this._comms_ended && !this._cts.IsCancellationRequested)
                {
                    // ---- Preamble phase ----
                    // Idle time before a frame's first byte is unlimited; once the first byte arrives,
                    //  the whole-frame deadline starts.
                    DateTime deadline = DateTime.MaxValue;

                    int gotten = 0;
                    while (gotten < 5)
                    {
                        int n = await this.Read_wDeadline(preamble, gotten, 5 - gotten, deadline);
                        if (n < 0)
                            return;     // Failure already classified and reported.
                        if (n == 0)
                        {
                            this.Fail_and_Close(eLoop_ConnectionStatus.Closed, "Connection was closed by the remote endpoint.");
                            return;
                        }

                        if (gotten == 0)
                        {
                            // First byte of the frame: promote status, and arm the whole-frame deadline.
                            this.PromoteStatus_from_NewlyOpen_to_Open();
                            deadline = DateTime.UtcNow.AddMilliseconds(this.FrameReadTimeout);
                        }

                        gotten += n;
                    }

                    // ---- Preamble validation ----
                    int bodylength;
                    cCustom_Serializer.Deserialize_Integer32(ref preamble, 0, out bodylength);
                    byte frametype = preamble[4];

                    if (bodylength < 0 || bodylength > this.MaxFrameSize)
                    {
                        this.Fail_and_Close(eLoop_ConnectionStatus.Error,
                            $"Frame body length ({bodylength.ToString()}) failed the sanity check (cap {this.MaxFrameSize.ToString()}). Treating as stream corruption.");
                        return;
                    }

                    if (frametype == FrameTypes.Reserved_LegacyJsonFraming)
                    {
                        // A legacy pre-preamble peer's first body byte (the json open brace) lands here.
                        // Diagnose it specifically, so a mixed-version deployment is identifiable from the log alone.
                        this.Fail_and_Close(eLoop_ConnectionStatus.Error,
                            "Legacy JSON framing detected: the peer is speaking the pre-LibVersion-3 wire format. " +
                            "Both ends of a connection must run the same framing generation.");
                        return;
                    }

                    if (frametype != FrameTypes.Json && frametype != FrameTypes.Binary)
                    {
                        this.Fail_and_Close(eLoop_ConnectionStatus.Error,
                            $"Unknown frame type ({frametype.ToString()}). Treating as stream corruption.");
                        return;
                    }

                    // ---- Zero-length frame: implicit keepalive ----
                    if (bodylength == 0)
                    {
                        this._metrics.Received_Message_Count++;
                        this._metrics.Last_Received_Message_Time = DateTime.UtcNow;
                        continue;
                    }

                    // ---- Body phase ----
                    byte[] body = new byte[bodylength];
                    gotten = 0;
                    while (gotten < bodylength)
                    {
                        int n = await this.Read_wDeadline(body, gotten, bodylength - gotten, deadline);
                        if (n < 0)
                            return;
                        if (n == 0)
                        {
                            this.Fail_and_Close(eLoop_ConnectionStatus.Closed, "Connection was closed by the remote endpoint mid-frame.");
                            return;
                        }

                        gotten += n;
                    }

                    // ---- Deliver ----
                    this._metrics.Received_Message_Count++;
                    this._metrics.Last_Received_Message_Time = DateTime.UtcNow;

                    try
                    {
                        this._delMessageReceived?.Invoke(this, frametype, body);
                    }
                    catch (Exception e)
                    {
                        // A throwing consumer handler must not kill the connection.
                        this.Logger?.Error(e,
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReadPump)} - " +
                            "Exception occurred in the frame-received handler.");
                    }
                }

                // Locally initiated exit: CloseDown already handled state; nothing to report.
            }
            catch (Exception e)
            {
                if (this._comms_ended || (this._cts?.IsCancellationRequested ?? true))
                {
                    // The owner closed us (or the transport) on purpose; the faulted read is expected. Exit quietly.
                    return;
                }

                this.Fail_and_Close(eLoop_ConnectionStatus.Error,
                    $"Exception occurred while reading from the connection: {e.Message}");
            }
        }

        /// <summary>
        /// Reads into the buffer, bounded by the whole-frame deadline.
        /// Returns the bytes read (0 = remote closed), or -1 after classifying and reporting a failure.
        /// NOTE: On .NET Framework targets, a pending NetworkStream read cannot be cancelled by token; the
        ///     timeout is enforced by racing the read against a delay, and the abandoned read is faulted when
        ///     the connection is subsequently torn down. Its exception is observed and discarded.
        /// </summary>
        /// <param name="buffer">Destination buffer.</param>
        /// <param name="offset">Destination offset.</param>
        /// <param name="count">Maximum bytes to read.</param>
        /// <param name="deadline">Whole-frame deadline (UTC); DateTime.MaxValue = no deadline armed yet.</param>
        /// <returns></returns>
        private async Task<int> Read_wDeadline(byte[] buffer, int offset, int count, DateTime deadline)
        {
            var stream = this._stream;
            if (stream == null)
            {
                this.Fail_and_Close(eLoop_ConnectionStatus.Error, "Network stream is no longer available.");
                return -1;
            }

            Task<int> readtask;
            try
            {
                // NOTE: The read is deliberately started WITHOUT the cancellation token.
                // Cancelling a pending socket read aborts the connection itself, and a locally closed
                //  loop must leave the socket usable — the parent endpoint owns the transport.
                // Local closedown instead cancels the delay this read is raced against, and the
                //  abandoned read is observed (see Observe_AbandonedRead).
                readtask = stream.ReadAsync(buffer, offset, count, CancellationToken.None);
            }
            catch (Exception e)
            {
                if (this._comms_ended)
                    return -1;

                this.Fail_and_Close(eLoop_ConnectionStatus.Error, $"Failed to start a read on the connection: {e.Message}");
                return -1;
            }

            // No deadline armed (waiting for a frame's first byte): idle time is unlimited, so race the
            //  read against local closedown only.
            if (deadline == DateTime.MaxValue)
            {
                try
                {
                    var done = await Task.WhenAny(readtask, Task.Delay(Timeout.Infinite, this._cts.Token));
                    if (!ReferenceEquals(done, readtask))
                    {
                        // Locally initiated exit: leave the read pending; the owner tears down the transport.
                        this.Observe_AbandonedRead(readtask);
                        return -1;
                    }

                    var n = await readtask;

                    // A read that lands after local closedown is not delivered...
                    if (this._comms_ended)
                        return -1;

                    return n;
                }
                catch (Exception e)
                {
                    if (this._comms_ended || this._cts.IsCancellationRequested)
                    {
                        this.Observe_AbandonedRead(readtask);
                        return -1;
                    }

                    this.Fail_and_Close(eLoop_ConnectionStatus.Error, $"Read failed on the connection: {e.Message}");
                    return -1;
                }
            }

            // Deadline armed: race the read against the remaining frame time.
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                this.Observe_AbandonedRead(readtask);
                this.Fail_and_Close(eLoop_ConnectionStatus.Lost,
                    $"Frame read deadline ({this.FrameReadTimeout.ToString()} ms) expired mid-frame. Peer is stalled or dripping bytes.");
                return -1;
            }

            Task finished;
            try
            {
                finished = await Task.WhenAny(readtask, Task.Delay(remaining, this._cts.Token));
            }
            catch (Exception)
            {
                // The delay faulted on cancellation: locally initiated exit.
                this.Observe_AbandonedRead(readtask);
                return -1;
            }

            if (!ReferenceEquals(finished, readtask))
            {
                if (this._comms_ended || this._cts.IsCancellationRequested)
                {
                    this.Observe_AbandonedRead(readtask);
                    return -1;
                }

                // The frame deadline expired before the read completed.
                this.Observe_AbandonedRead(readtask);
                this.Fail_and_Close(eLoop_ConnectionStatus.Lost,
                    $"Frame read deadline ({this.FrameReadTimeout.ToString()} ms) expired mid-frame. Peer is stalled or dripping bytes.");
                return -1;
            }

            try
            {
                var n = await readtask;

                // A read that lands after local closedown is not delivered...
                if (this._comms_ended)
                    return -1;

                return n;
            }
            catch (Exception e)
            {
                if (this._comms_ended || this._cts.IsCancellationRequested)
                    return -1;

                this.Fail_and_Close(eLoop_ConnectionStatus.Error, $"Read failed on the connection: {e.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Attaches a fault observer to a read task we are walking away from, so its eventual exception
        ///     (from the connection being torn down underneath it) never surfaces as unobserved.
        /// </summary>
        /// <param name="readtask">The abandoned read task.</param>
        private void Observe_AbandonedRead(Task<int> readtask)
        {
            _ = readtask.ContinueWith(t => { var _ex = t.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        #endregion


        #region Status Handling

        /// <summary>
        /// Classifies a connection failure: logs it, transitions to the given terminal state through the legal
        ///     path, marks comms ended, and fires the went-bad delegate exactly once.
        /// Suppressed entirely when the closure was locally initiated (CloseDown/Dispose already ran).
        /// </summary>
        /// <param name="finalstate">Terminal state to land in (Closed, Lost, or Error).</param>
        /// <param name="reason">Log message describing the failure.</param>
        private void Fail_and_Close(eLoop_ConnectionStatus finalstate, string reason)
        {
            lock (this._lifecyclelock)
            {
                if (this._comms_ended)
                    return;

                this._comms_ended = true;

                if (finalstate == eLoop_ConnectionStatus.Closed)
                {
                    this.Logger?.Info(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Fail_and_Close)} - " + reason);

                    // Closed is reached through Shutting_Down, per the transition rules.
                    // Both transitions are published, matching the historical contract...
                    this.UpdateState(eLoop_ConnectionStatus.Shutting_Down);
                    this.UpdateState(eLoop_ConnectionStatus.Closed);
                }
                else
                {
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Fail_and_Close)} - " + reason);

                    this.UpdateState(finalstate);
                }

                try { this._cts?.Cancel(); } catch (Exception) { }
            }

            // Fire the failure delegate outside the lock, so a consumer handler cannot deadlock the lifecycle...
            try
            {
                this._delConnectionWentBad?.Invoke(this);
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Fail_and_Close)} - " +
                    "Exception occurred in the connection-went-bad handler.");
            }
        }

        /// <summary>
        /// Transitions loop state, publishing the change to the status delegate.
        /// </summary>
        /// <param name="newstate">The state to transition to.</param>
        private void UpdateState(eLoop_ConnectionStatus newstate)
        {
            this.UpdateState(newstate, true);
        }

        /// <summary>
        /// Transitions loop state, optionally publishing the change to the status delegate.
        /// </summary>
        /// <param name="newstate">The state to transition to.</param>
        /// <param name="publish_change">Set false for intermediate transitions the owner does not need to hear about.</param>
        private void UpdateState(eLoop_ConnectionStatus newstate, bool publish_change)
        {
            if (this.State == newstate)
                return;

            var change = "Status changed from " + this.State.ToString() + " to " + newstate.ToString() + ".";
            this.State = newstate;

            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " + change);

            if (!publish_change)
                return;

            try
            {
                this._delStatusChange?.Invoke(this, change);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Promotes the loop from Newly_Opened to Open on the first received byte. A no-op in any other state.
        /// </summary>
        private void PromoteStatus_from_NewlyOpen_to_Open()
        {
            if (this.State == eLoop_ConnectionStatus.Newly_Opened)
                this.UpdateState(eLoop_ConnectionStatus.Open);
        }

        #endregion
    }
}
