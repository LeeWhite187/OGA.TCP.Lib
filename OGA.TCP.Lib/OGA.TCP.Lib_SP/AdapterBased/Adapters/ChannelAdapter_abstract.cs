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
        /// Number of instances that have been created since the process started.
        /// </summary>
        static protected int _instance_counter;

        /// <summary>
        /// Instance Id of the endpoint.
        /// </summary>
        public int InstanceId { get; protected set; }

        /// <summary>
        /// Tracks the number of received messages through the channel.
        /// </summary>
        public int ReceiveCount { get; protected set; }
        /// <summary>
        /// Tracks the number of sent messages through the channel.
        /// </summary>
        public int SentCount { get; protected set; }
        /// <summary>
        /// Tracks the number of malformed messages received by the channel.
        /// </summary>
        public int BadCount { get; protected set; }

        /// <summary>
        /// Defines the channel this adapter sends and receives messages on.
        /// Is set at construction.
        /// </summary>
        public string ChannelId { get; protected set; }

        /// <summary>
        /// Defines the message type name to expect.
        /// Has no bearing on actual message deserialization.
        /// </summary>
        public string ExpectedMessageType { get; protected set; }

        /// <summary>
        /// This property tells the channel adapter to log incoming messages.
        /// Or not.
        /// Set to off by default.
        /// </summary>
        public bool LogReceivedMessages { get; set; }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="channelid"></param>
        /// <param name="expectedmessagetype"></param>
        /// <param name="logger"></param>
        public ChannelAdapter_abstract(string channelid, string expectedmessagetype, NLog.ILogger logger = null)
        {
            this._classname = nameof(ChannelAdapter_abstract);
            _instance_counter++;

            this.ChannelId = channelid;
            this.ExpectedMessageType = expectedmessagetype;
            this.Logger = logger;

            this.ResetCounters();
        }

        /// <summary>
        /// Call this method to reset adapter metrics.
        /// </summary>
        public void ResetCounters()
        {
            this.ReceiveCount = 0;
            this.SentCount = 0;
            this.BadCount = 0;
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
        abstract public int AcceptIncomingMessage(Client_v1_Abstract client, string messagetype, string jsondata);

        /// <summary>
        /// Calling this method will close and dereference any delegates and instances.
        /// Override this method with logic to close and null out any references the adapter holds.
        /// </summary>
        abstract public void Close();
    }
}
