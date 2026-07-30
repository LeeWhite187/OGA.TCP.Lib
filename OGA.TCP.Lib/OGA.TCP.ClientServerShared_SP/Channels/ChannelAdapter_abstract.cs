using System;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP.Channels
{
    /// <summary>
    /// Base implementation for channel adapters, carrying the boilerplate: identity, kind, counters, and the host back-reference.
    /// Derive from this and implement the json AcceptIncomingMessage overload (and override the binary overload, for binary-kind channels).
    /// Adapters are host-agnostic: the same adapter type serves client-side and server-side endpoints, over tcp or websocket.
    /// </summary>
    abstract public class ChannelAdapter_abstract : IChannelAdapter
    {
        /// <summary>
        /// Logger instance.
        /// </summary>
        protected NLog.ILogger Logger;

        /// <summary>
        /// Class name used for logging.
        /// </summary>
        protected string _classname;

        /// <summary>
        /// The endpoint this adapter is registered on.
        /// Set during registration, and used by adapters that provide their own send methods.
        /// </summary>
        protected IMessagingHost _hostref;

        /// <summary>
        /// Number of instances that have been created since the process started.
        /// </summary>
        static protected int _instance_counter;

        /// <summary>
        /// Instance Id of the adapter, for log correlation.
        /// </summary>
        public int InstanceId { get; protected set; }

        /// <summary>
        /// Backing store for the received-message counter. Updated via Interlocked, since delivery and readers can be on different threads.
        /// </summary>
        private int _receivecount;
        /// <summary>
        /// Backing store for the sent-message counter. Updated via Interlocked.
        /// </summary>
        private int _sentcount;
        /// <summary>
        /// Backing store for the failed-message counter. Updated via Interlocked.
        /// </summary>
        private int _badcount;

        /// <summary>
        /// Tracks the number of received messages delivered through the channel.
        /// </summary>
        public int ReceiveCount { get => _receivecount; }
        /// <summary>
        /// Tracks the number of sent messages through the channel.
        /// </summary>
        public int SentCount { get => _sentcount; }
        /// <summary>
        /// Tracks the number of received messages that failed handling (a negative handler result or a delivery fault).
        /// </summary>
        public int BadCount { get => _badcount; }

        /// <summary>
        /// Defines the channel this adapter sends and receives messages on.
        /// Is set at construction.
        /// </summary>
        public string ChannelId { get; protected set; }

        /// <summary>
        /// Declares the kind of traffic (json or binary) this adapter's channel carries.
        /// Is set at construction. The dispatcher rejects messages of the other kind arriving on this channel.
        /// </summary>
        public eChannelKind Kind { get; protected set; }

        /// <summary>
        /// This property tells the channel adapter to log incoming messages.
        /// Or not.
        /// Set to off by default.
        /// </summary>
        public bool LogReceivedMessages { get; set; }

        /// <summary>
        /// Constructor requires the channel name and the kind of traffic the channel carries.
        /// </summary>
        /// <param name="channelid">Channel name this adapter serves; the routing key for dispatch.</param>
        /// <param name="kind">Kind of traffic (json or binary) the channel carries.</param>
        /// <param name="logger">Optional logger instance.</param>
        public ChannelAdapter_abstract(string channelid, eChannelKind kind, NLog.ILogger logger = null)
        {
            this._classname = nameof(ChannelAdapter_abstract);
            _instance_counter++;
            this.InstanceId = _instance_counter;

            this.ChannelId = channelid;
            this.Kind = kind;
            this.Logger = logger;

            this.ResetCounters();
        }

        /// <summary>
        /// Call this method to reset adapter metrics.
        /// </summary>
        public void ResetCounters()
        {
            Interlocked.Exchange(ref _receivecount, 0);
            Interlocked.Exchange(ref _sentcount, 0);
            Interlocked.Exchange(ref _badcount, 0);
        }

        /// <summary>
        /// Used internally by the hosting endpoint, during adapter registration.
        /// Gives the adapter a back-reference it can use for its own send methods.
        /// </summary>
        /// <param name="host">The endpoint the adapter is being registered on.</param>
        public void RegisterAdapter(IMessagingHost host)
        {
            this._hostref = host;
        }

        /// <summary>
        /// Called by the dispatcher to deliver a json message to the adapter.
        /// Implement logic that validates the received message, deserializes its content, and dispatches or handles it.
        /// Runs on the endpoint's receive path: keep it fast, and queue heavy work elsewhere.
        /// Return 1 if handled, 0 if not handled, negatives for errors.
        /// NOTE: This is NOT the method you use to send messages to the remote endpoint.
        /// </summary>
        /// <param name="host">The endpoint that received the message.</param>
        /// <param name="messagetype">Lowercased message type name from the envelope.</param>
        /// <param name="jsondata">The message payload json.</param>
        /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
        /// <returns></returns>
        abstract public int AcceptIncomingMessage(IMessagingHost host, string messagetype, string jsondata, string corelationid);

        /// <summary>
        /// Called by the dispatcher to deliver a binary message payload to the adapter.
        /// The base implementation refuses delivery, which is correct for json-kind adapters;
        ///     binary-kind adapters override this with their payload handling.
        /// </summary>
        /// <param name="host">The endpoint that received the message.</param>
        /// <param name="messagetype">Lowercased message type name from the routing header.</param>
        /// <param name="payload">The raw payload bytes.</param>
        /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
        /// <returns></returns>
        virtual public int AcceptIncomingMessage(IMessagingHost host, string messagetype, byte[] payload, string corelationid)
        {
            this.Logger?.Error(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(AcceptIncomingMessage)} - " +
                $"Adapter for channel ({(this.ChannelId ?? "")}) received a binary payload it does not implement handling for.");

            return -1;
        }

        /// <summary>
        /// Called by the dispatcher after each delivery, with the delivery's result code, so the adapter's
        ///     receive and bad counters stay accurate regardless of how the adapter is implemented.
        /// </summary>
        /// <param name="resultcode">The result returned by the delivery (or the fault code, if delivery threw).</param>
        public void RecordReceiveResult(int resultcode)
        {
            Interlocked.Increment(ref _receivecount);

            if (resultcode < 0)
                Interlocked.Increment(ref _badcount);
        }

        /// <summary>
        /// Sends a serializable object to the connected peer over this adapter's channel, and counts the send.
        /// Available to derived adapters that expose their own typed send methods.
        /// Returns 1 on success, 0 if no host is attached or the session is not ready, negatives on error.
        /// </summary>
        /// <param name="msg">Object instance to serialize and send.</param>
        /// <param name="scope">Optional scope value.</param>
        /// <param name="corelationid">Optional correlation id, carried for tracing.</param>
        /// <returns></returns>
        protected async Task<int> Send_toPeer(object msg, string scope = "", string corelationid = "")
        {
            // Do we have an endpoint to send through...
            var host = this._hostref;
            if (host == null)
            {
                // No host attached.
                return 0;
            }

            try
            {
                var res = await host.SendMessage_toPeer(msg, this.ChannelId, scope, corelationid);

                if (res == 1)
                    Interlocked.Increment(ref _sentcount);

                return res;
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Send_toPeer)} - " +
                    $"Exception occurred while sending over channel ({(this.ChannelId ?? "")}).");

                return -2;
            }
        }

        /// <summary>
        /// Calling this method will close and dereference any delegates and instances.
        /// Override this method with logic to close and null out any references the adapter holds,
        ///     and call the base method to release the host reference. Must not throw.
        /// </summary>
        virtual public void Close()
        {
            this._hostref = null;
        }
    }
}
