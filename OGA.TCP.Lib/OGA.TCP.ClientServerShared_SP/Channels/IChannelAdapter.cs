using System;

namespace OGA.TCP.Channels
{
    /// <summary>
    /// Contract for pluggable channel adapters that can be registered on any messaging endpoint (client or server, tcp or websocket),
    ///     for exchanging traffic over a named channel.
    /// This pluggable architecture allows a single endpoint class to serve new channels without derivation.
    /// If your implementation only needs a simple channel-assigned delegate, use the endpoint's Add_ChannelHandler method instead,
    ///     which wraps your delegate in an adapter for you.
    /// Each adapter declares its channel kind (json or binary) at construction; the dispatcher delivers only matching traffic to it.
    /// </summary>
    public interface IChannelAdapter
    {
        /// <summary>
        /// Defines the channel this adapter sends and receives messages on.
        /// Is set at construction, and is the routing key for dispatch.
        /// </summary>
        string ChannelId { get; }

        /// <summary>
        /// Declares the kind of traffic (json or binary) this adapter's channel carries.
        /// Is set at construction. The dispatcher rejects messages of the other kind arriving on this channel.
        /// </summary>
        eChannelKind Kind { get; }

        /// <summary>
        /// Tracks the number of received messages delivered through the channel.
        /// </summary>
        int ReceiveCount { get; }
        /// <summary>
        /// Tracks the number of sent messages through the channel.
        /// </summary>
        int SentCount { get; }
        /// <summary>
        /// Tracks the number of received messages that failed handling (a negative handler result or a delivery fault).
        /// </summary>
        int BadCount { get; }

        /// <summary>
        /// This property tells the channel adapter to log incoming messages.
        /// Or not.
        /// Set to off by default.
        /// </summary>
        bool LogReceivedMessages { get; set; }

        /// <summary>
        /// Call this method to reset adapter metrics.
        /// </summary>
        void ResetCounters();

        /// <summary>
        /// Used internally by the hosting endpoint, during adapter registration.
        /// Gives the adapter a back-reference it can use for its own send methods.
        /// </summary>
        /// <param name="host">The endpoint the adapter is being registered on.</param>
        void RegisterAdapter(IMessagingHost host);

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
        int AcceptIncomingMessage(IMessagingHost host, string messagetype, string jsondata, string corelationid);

        /// <summary>
        /// Called by the dispatcher to deliver a binary message payload to the adapter.
        /// Only called for binary-kind adapters, and only once binary traffic exists on the connection (LibVersion 3).
        /// Runs on the endpoint's receive path: keep it fast, and queue heavy work elsewhere.
        /// Return 1 if handled, 0 if not handled, negatives for errors.
        /// </summary>
        /// <param name="host">The endpoint that received the message.</param>
        /// <param name="messagetype">Lowercased message type name from the routing header.</param>
        /// <param name="payload">The raw payload bytes.</param>
        /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
        /// <returns></returns>
        int AcceptIncomingMessage(IMessagingHost host, string messagetype, byte[] payload, string corelationid);

        /// <summary>
        /// Called by the dispatcher after each delivery, with the delivery's result code, so the adapter's
        ///     receive and bad counters stay accurate regardless of how the adapter is implemented.
        /// </summary>
        /// <param name="resultcode">The result returned by the delivery (or the fault code, if delivery threw).</param>
        void RecordReceiveResult(int resultcode);

        /// <summary>
        /// Calling this method will close and dereference any delegates and instances.
        /// Called when the adapter is removed, and when the hosting endpoint shuts down.
        /// Must not throw.
        /// </summary>
        void Close();
    }
}
