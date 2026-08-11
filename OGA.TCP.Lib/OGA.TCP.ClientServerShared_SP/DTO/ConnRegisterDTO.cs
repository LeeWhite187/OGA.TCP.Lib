using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Messages
{
    /// <summary>
    /// An instance of this type is received from a websocket client, during client registration.
    /// This class intends to convey details about the recently connected client,
    ///     so the User Mapping Service can successfully track what users and devices are connected to what websocket hosts and connections.
    /// </summary>
    public class ConnRegisterDTO
    {
        /// <summary>
        /// Client-generated Connection Id, uniquely describes the websocket connection instance.
        ///     Each time a client connects, a different connection Id is generated.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// UserId that is logged into the connected client. This is used so that the session
        ///     router can know where to send chat messages.
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

        /// <summary>
        /// Property strings carried with the registration ('"key":"value"' fragments; composed and parsed
        ///     by PropString). Keys in use: loopback, keepalive, pid, runtimeid, and for v2+: appid,
        ///     appver, language, and the transport libver prop. Additive extension point (§9.1.3).
        /// </summary>
        public string[] Props { get; set; }

        /// <summary>
        /// Constructor preloads every field, so a registration is never sent with nulls.
        /// </summary>
        public ConnRegisterDTO()
        {
            ConnectionId = "";
            UserId = Guid.Empty;
            DeviceId = "";
            Props = new string[0];
        }
    }
}
