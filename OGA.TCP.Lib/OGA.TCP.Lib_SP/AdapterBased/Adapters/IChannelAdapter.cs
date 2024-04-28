using OGA.TCP.Messages;
using OGA.TCP.SessionLayer;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OGA.TCP.ClientAdapters
{
    /// <summary>
    /// Defines the contract for pluggable channel adapters that can be added to a generic tcp/ws client, for exchanging traffic.
    /// This pluggable architecture allows for a single client class type to serve new channels from a single client type.
    /// If your implementation requires a simple channel-assigned delegate, then simply use TCPClient_v1 client class.
    /// </summary>
    public interface IChannelAdapter
    {
        /// <summary>
        /// Tracks the number of received messages through the channel.
        /// </summary>
        int ReceiveCount { get; }
        /// <summary>
        /// Tracks the number of sent messages through the channel.
        /// </summary>
        int SentCount { get; }
        /// <summary>
        /// Tracks the number of malformed messages received by the channel.
        /// </summary>
        int BadCount { get; }

        /// <summary>
        /// Defines the channel this adapter sends and receives messages on.
        /// Is set at construction.
        /// </summary>
        string ChannelId { get; }

        /// <summary>
        /// Defines the message type name to expect.
        /// Has no bearing on actual message deserialization.
        /// </summary>
        string ExpectedMessageType { get; }
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
        /// Used internally by a socket client, during adapter registration.
        /// </summary>
        /// <param name="clientref"></param>
        void RegisterAdapter(Client_v1_Abstract clientref);

        /// <summary>
        /// Called by the client's internal receive logic, to process messages.
        /// Create an implementation in this method, that validate the received message envelope, deserialize its content, and dispatch or handle the inner message.
        /// NOTE: This is NOT the method you use to send messages to the remote endpoint.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="messagetype"></param>
        /// <param name="jsondata"></param>
        /// <returns></returns>
        int AcceptIncomingMessage(Client_v1_Abstract client, string messagetype, string jsondata);

        /// <summary>
        /// Calling this method will close and dereference any delegates and instances.
        /// </summary>
        void Close();
    }
}
