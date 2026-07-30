using System;

namespace OGA.TCP.Channels
{
    /// <summary>
    /// Declares the kind of message traffic a channel carries.
    /// Each channel is declared as one kind at registration, and carries only that kind of message.
    /// The dispatcher enforces this: a message arriving on a channel of the wrong kind is rejected and logged, not delivered.
    /// Whether a given channel should be json or binary is the consuming application's choice.
    /// </summary>
    public enum eChannelKind
    {
        /// <summary>
        /// The channel carries json messages: serialized objects, delivered to handlers as (messagetype, json string).
        /// </summary>
        Json = 1,

        /// <summary>
        /// The channel carries binary messages: raw byte payloads, delivered to handlers as (messagetype, byte array).
        /// Binary traffic arrives with LibVersion 3 framing; a binary-kind channel on an earlier-version connection simply never receives traffic.
        /// </summary>
        Binary = 2
    }
}
