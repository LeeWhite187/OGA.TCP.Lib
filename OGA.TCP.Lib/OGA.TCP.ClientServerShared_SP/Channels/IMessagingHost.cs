using System;
using System.Threading.Tasks;

namespace OGA.TCP.Channels
{
    /// <summary>
    /// Minimal contract of a messaging endpoint that can host channel adapters.
    /// Implemented by the client-side session class and the server-side endpoint class, so one adapter
    ///     family (and one dispatcher) serves both sides of a conversation, and both transports.
    /// This is deliberately the smallest surface an adapter needs: a way to send, and identity for logging.
    /// It carries no transport specifics, so adapter implementations remain transport- and side-agnostic.
    /// </summary>
    public interface IMessagingHost
    {
        /// <summary>
        /// Diagnostic instance id of the hosting endpoint, for log correlation.
        /// </summary>
        int InstanceId { get; }

        /// <summary>
        /// Lowercase short name of the hosting endpoint's transport (e.g. "tcp", "ws"), for log context.
        /// </summary>
        string TransportShortName { get; }

        /// <summary>
        /// Sends a serializable object to the connected peer as a json message.
        /// On the client side this sends to the server; on the server side this sends to the connected client.
        /// Returns 1 on success, 0 if the session is not ready to send, negatives on error.
        /// </summary>
        /// <param name="payload">Object instance to serialize and send.</param>
        /// <param name="channel">Optional channel name the message is routed to on the far side.</param>
        /// <param name="scope">Optional scope value.</param>
        /// <param name="corelationid">Optional correlation id, carried in the message properties for tracing.</param>
        /// <returns></returns>
        Task<int> SendMessage_toPeer(object payload, string channel = "", string scope = "", string corelationid = "");
    }
}
