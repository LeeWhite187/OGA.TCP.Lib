using OGA.TCP.Messages;
using OGA.TCP.SessionLayer;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static OGA.TCP.SessionLayer.Client_v1_Abstract;

namespace OGA.TCP.ClientAdapters
{
    /// <summary>
    /// Defines the contract for pluggable channel adapters that can be added to a generic tcp/ws client, for exchanging traffic.
    /// This pluggable architecture allows for a single client class type to serve new channels from a single client type.
    /// If your implementation requires a simple channel-assigned delegate, then simply use TCPClient_v1 client class.
    /// </summary>
    public class ChannelAdapter_DelegateType: ChannelAdapter_abstract, IChannelAdapter
    {
        private DelMessageReceived _callback;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="channelid"></param>
        /// <param name="callback"></param>
        /// <param name="expectedmessagetype"></param>
        /// <param name="logger"></param>
        public ChannelAdapter_DelegateType(string channelid, DelMessageReceived callback, string expectedmessagetype, NLog.ILogger logger = null) :
                                        base(channelid, expectedmessagetype, logger)
        {
            this._classname = nameof(ChannelAdapter_DelegateType);

            this._callback = callback;
        }

        /// <summary>
        /// Called by the client's internal receive logic, to process messages.
        /// Create an implementation in this method, that validate the received message envelope, deserialize its content, and dispatch or handle the inner message.
        /// NOTE: This is NOT the method you use to send messages to the remote endpoint.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="messagetype"></param>
        /// <param name="jsondata"></param>
        /// <returns></returns>
        override public int AcceptIncomingMessage(Client_v1_Abstract client, string messagetype, string jsondata)
        {
            if (this._callback != null)
            {
                var res = this._callback(client, messagetype, jsondata);
                return res;
            }

            return 1;
        }

        /// <summary>
        /// Calling this method will close and dereference any delegates and instances.
        /// </summary>
        override public void Close()
        {
            this._callback = null;
        }
    }
}
