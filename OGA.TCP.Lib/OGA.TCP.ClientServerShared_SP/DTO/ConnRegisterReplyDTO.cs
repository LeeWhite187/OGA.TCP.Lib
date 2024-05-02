using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Messages
{
    /// <summary>
    /// Reply message for a connection registration.
    /// This is sent back to a client, to provide a registration status, and notification of when data exchange can begin.
    /// It also gives the client connection the ConnectionId that the client is known by, by the service and other clients.
    /// This updated connectionId (on registration) ensures that a client's ConnectionId is guaranteed unique by the connected service, and not susceptible to impersonation attacks.
    /// Upon receipt, by a client, it needs to update its own ConnectionId with the value in this message.
    /// </summary>
    public class ConnRegisterReplyDTO
    {
        /// <summary>
        /// Server-generated Connection Id, uniquely describes the websocket connection instance.
        /// This is what the service and other clients used to identify this connection.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// This is the original client-provided connectionId, sent by the registration message.
        /// We include this, to ensure that the client is updated to the correct server-provided ConnectionId.
        /// This can be a problem if a client has recycled its receive loop instance for a new connection, but the old receive loop was still handling a registration reply.
        /// If an old receive loop was processing a registration reply, it would erroneously accept an obsolete server-provided ConnectionId, and we would be out of sync.
        /// So, the server includes the old ConnectionId, here, allowing this message to be discarded if the client's current connectionid doesn't match.
        /// </summary>
        public string OldConnectionId { get; set; }

        /// <summary>
        /// UserId that is logged into the connected client. This is used so that the session router can know where to send messages.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Identifies the client device. This is used to distinguish connections for a user,
        ///     in the event that a particular message type or action needs to affect only one device of a user.
        /// This can be the case, when a user has logged in through multiple devices,
        ///     and there may be connection limits on the user's account, based on his privileges.
        /// In which case, this property allows us to determine which device needs to be logged out, when a user logs into a new device.
        /// </summary>
        public string DeviceId { get; set; }

        public string[] Props { get; set; }


        public ConnRegisterReplyDTO()
        {
            ConnectionId = "";
            OldConnectionId = "";
            UserId = Guid.Empty;
            DeviceId = "";
            Props = new string[0];
        }
    }
}
