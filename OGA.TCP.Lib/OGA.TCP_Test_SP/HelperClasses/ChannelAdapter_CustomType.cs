using OGA.TCP.Channels;
using OGA.TCP.Messages;
using OGA.TCP.SessionLayer;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static OGA.TCP.SessionLayer.Client_v1_Abstract;

namespace OGA.TCP.Channels
{
    /// <summary>
    /// FOR TESTING. NOT FOR PRODUCTION USAGE.
    /// This is a simple channel adapter implementation used to exercise channel adapter usage.
    /// It is based on the delegate type channel adapter class, but authored directly against the abstract base,
    ///     so tests exercise the consumer-authored-adapter path.
    /// </summary>
    public class ChannelAdapter_CustomType : ChannelAdapter_abstract, IChannelAdapter
    {
        /// <summary>
        /// Consumer callback the adapter forwards received json messages to.
        /// Uses the client-side delegate signature, captured at construction.
        /// </summary>
        private DelMessageReceived _callback;

        /// <summary>
        /// Constructor accepts the channel name and the client-typed callback to forward messages to.
        /// Creates a json-kind adapter.
        /// </summary>
        /// <param name="channelid">Channel name this adapter serves.</param>
        /// <param name="callback">Consumer callback receiving each json message on the channel.</param>
        /// <param name="logger">Optional logger instance.</param>
        public ChannelAdapter_CustomType(string channelid, DelMessageReceived callback, NLog.ILogger logger = null) :
                                        base(channelid, eChannelKind.Json, logger)
        {
            this._classname = nameof(ChannelAdapter_CustomType);

            this._callback = callback;
        }

        /// <summary>
        /// Called by the dispatcher to deliver a json message to the adapter.
        /// Forwards to the consumer callback, downcasting the host to the client type the callback expects.
        /// </summary>
        /// <param name="host">The endpoint that received the message.</param>
        /// <param name="messagetype">Lowercased message type name from the envelope.</param>
        /// <param name="jsondata">The message payload json.</param>
        /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
        /// <returns></returns>
        override public int AcceptIncomingMessage(IMessagingHost host, string messagetype, string jsondata, string corelationid)
        {
            if (this._callback != null)
            {
                var res = this._callback(host as Client_v1_Abstract, messagetype, jsondata);
                return res;
            }

            return 1;
        }

        /// <summary>
        /// Send method on the channel adapter.
        /// This is localised on the channel adapter, so the adapter instance can be retained by an other service, without having to have a direct reference to the socket client.
        /// Accepts a message of any class. The correlation id is optional.
        /// </summary>
        /// <param name="msg">Object instance to serialize and send.</param>
        /// <param name="corelationid">Optional correlation id, carried for tracing.</param>
        /// <returns></returns>
        public async Task<int> SendMessage(object msg, string corelationid = "")
        {
            return await this.Send_toPeer(msg, "", corelationid);
        }

        /// <summary>
        /// Calling this method will close and dereference any delegates and instances.
        /// </summary>
        override public void Close()
        {
            this._callback = null;

            base.Close();
        }
    }
}
