using System;
using System.Threading.Tasks;

namespace OGA.TCP.Channels
{
    /// <summary>
    /// Delegate signature for json channel handlers registered directly on the dispatcher's adapter substrate.
    /// Endpoint classes offer their own side-typed registration facades that wrap into this form.
    /// Return 1 if handled, 0 if not handled, negatives for errors.
    /// </summary>
    /// <param name="host">The endpoint that received the message.</param>
    /// <param name="messagetype">Lowercased message type name from the envelope.</param>
    /// <param name="jsondata">The message payload json.</param>
    /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
    /// <returns></returns>
    public delegate int DelChannelJsonReceived(IMessagingHost host, string messagetype, string jsondata, string corelationid);

    /// <summary>
    /// Delegate signature for binary channel handlers registered directly on the dispatcher's adapter substrate.
    /// Return 1 if handled, 0 if not handled, negatives for errors.
    /// </summary>
    /// <param name="host">The endpoint that received the message.</param>
    /// <param name="messagetype">Lowercased message type name from the routing header.</param>
    /// <param name="payload">The raw payload bytes.</param>
    /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
    /// <returns></returns>
    public delegate int DelChannelBinaryReceived(IMessagingHost host, string messagetype, byte[] payload, string corelationid);

    /// <summary>
    /// Channel adapter that forwards received messages to a consumer-supplied delegate.
    /// This is the adapter type behind the endpoints' Add_ChannelHandler convenience methods, so consumers who
    ///     think in delegates never need to author an adapter class.
    /// The constructor overload chosen (json or binary delegate) declares the channel's kind.
    /// </summary>
    public class ChannelAdapter_DelegateType : ChannelAdapter_abstract, IChannelAdapter
    {
        /// <summary>
        /// Consumer callback for json-kind channels. Null when the adapter is binary-kind or closed.
        /// </summary>
        private DelChannelJsonReceived _jsoncallback;

        /// <summary>
        /// Consumer callback for binary-kind channels. Null when the adapter is json-kind or closed.
        /// </summary>
        private DelChannelBinaryReceived _binarycallback;

        /// <summary>
        /// Creates a json-kind delegate adapter for the given channel.
        /// </summary>
        /// <param name="channelid">Channel name this adapter serves.</param>
        /// <param name="callback">Consumer callback receiving each json message on the channel.</param>
        /// <param name="logger">Optional logger instance.</param>
        public ChannelAdapter_DelegateType(string channelid, DelChannelJsonReceived callback, NLog.ILogger logger = null) :
                                        base(channelid, eChannelKind.Json, logger)
        {
            this._classname = nameof(ChannelAdapter_DelegateType);

            this._jsoncallback = callback;
        }

        /// <summary>
        /// Creates a binary-kind delegate adapter for the given channel.
        /// Binary traffic exists from LibVersion 3 onward; on earlier-version connections the adapter simply never fires.
        /// </summary>
        /// <param name="channelid">Channel name this adapter serves.</param>
        /// <param name="callback">Consumer callback receiving each binary payload on the channel.</param>
        /// <param name="logger">Optional logger instance.</param>
        public ChannelAdapter_DelegateType(string channelid, DelChannelBinaryReceived callback, NLog.ILogger logger = null) :
                                        base(channelid, eChannelKind.Binary, logger)
        {
            this._classname = nameof(ChannelAdapter_DelegateType);

            this._binarycallback = callback;
        }

        /// <summary>
        /// Called by the dispatcher to deliver a json message to the adapter.
        /// Forwards to the consumer's json callback.
        /// </summary>
        /// <param name="host">The endpoint that received the message.</param>
        /// <param name="messagetype">Lowercased message type name from the envelope.</param>
        /// <param name="jsondata">The message payload json.</param>
        /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
        /// <returns></returns>
        override public int AcceptIncomingMessage(IMessagingHost host, string messagetype, string jsondata, string corelationid)
        {
            var cb = this._jsoncallback;
            if (cb != null)
            {
                var res = cb(host, messagetype, jsondata, corelationid);
                return res;
            }

            return 1;
        }

        /// <summary>
        /// Called by the dispatcher to deliver a binary message payload to the adapter.
        /// Forwards to the consumer's binary callback.
        /// </summary>
        /// <param name="host">The endpoint that received the message.</param>
        /// <param name="messagetype">Lowercased message type name from the routing header.</param>
        /// <param name="payload">The raw payload bytes.</param>
        /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
        /// <returns></returns>
        override public int AcceptIncomingMessage(IMessagingHost host, string messagetype, byte[] payload, string corelationid)
        {
            var cb = this._binarycallback;
            if (cb != null)
            {
                var res = cb(host, messagetype, payload, corelationid);
                return res;
            }

            return 1;
        }

        /// <summary>
        /// Send method on the channel adapter.
        /// This is localised on the channel adapter, so the adapter instance can be retained by another service,
        ///     without having to have a direct reference to the socket client or endpoint.
        /// Accepts a message of any class; sends it over this adapter's channel. The correlation id is optional.
        /// Only valid on json-kind adapters; returns -2 for a binary-kind adapter.
        /// </summary>
        /// <param name="msg">Object instance to serialize and send.</param>
        /// <param name="corelationid">Optional correlation id, carried for tracing.</param>
        /// <returns></returns>
        public async Task<int> SendMessage(object msg, string corelationid = "")
        {
            // Sending a serialized object is a json-channel behavior...
            if (this.Kind != eChannelKind.Json)
            {
                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(SendMessage)} - " +
                    $"Attempted json object send on binary-kind channel ({(this.ChannelId ?? "")}).");

                return -2;
            }

            return await this.Send_toPeer(msg, "", corelationid);
        }

        /// <summary>
        /// Calling this method will close and dereference any delegates and instances.
        /// </summary>
        override public void Close()
        {
            this._jsoncallback = null;
            this._binarycallback = null;

            base.Close();
        }
    }
}
