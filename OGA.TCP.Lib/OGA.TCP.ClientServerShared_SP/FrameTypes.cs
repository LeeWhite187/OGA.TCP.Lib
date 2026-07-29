using System;

namespace OGA.TCP
{
    /// <summary>
    /// Registry of TCP frame-type values, carried in the fifth byte of the frame preamble.
    /// The TCP frame preamble is: a 4-byte little-endian body length, then a 1-byte frame type from this registry, then the body.
    /// The length counts body bytes only, excluding the 5-byte preamble.
    /// Each frame on a connection declares its own type, so consecutive frames may carry differing types.
    /// This registry is append-only: values are only ever added, never repurposed or reused, since the wire meaning of a value is permanent.
    /// Receipt of a value not defined here is fatal to the connection, as it can only indicate stream corruption or a mismatched peer.
    /// NOTE: This registry is a TCP-transport construct. The websocket transport does not use it, as websocket framing natively carries
    ///     an equivalent Text/Binary discriminator in its own protocol.
    /// </summary>
    static public class FrameTypes
    {
        /// <summary>
        /// Reserved as invalid, and never assigned to a frame kind.
        /// A zeroed buffer, torn write, or uninitialized preamble must never read as a valid frame,
        ///     so zero is deliberately unusable. Receipt indicates stream corruption.
        /// </summary>
        public const byte Invalid = 0;

        /// <summary>
        /// Body is a UTF-8 JSON MessageEnvelope, identical to the pre-preamble wire format.
        /// All session-machinery traffic (registration, keepalive ping-pong, chunking control messages)
        ///     rides this frame type, as do ordinary json application messages.
        /// </summary>
        public const byte Json = 1;

        /// <summary>
        /// Body is a binary message: a 4-byte little-endian header length, a UTF-8 JSON routing header
        ///     (channel, scope, message type, message id, correlation props), then raw payload bytes.
        /// The payload rides outside the JSON header, so it carries no base64 or escaping cost.
        /// Chunk-data messages of the chunking layer also ride this frame type, identified by their routing header.
        /// </summary>
        public const byte Binary = 2;

        /// <summary>
        /// Permanently reserved as invalid, and never to be assigned to a frame kind.
        /// This value is the open-brace character ('{', 0x7B, decimal 123).
        /// A peer speaking the legacy (pre-preamble) framing sends frames of: a 4-byte length, then a UTF-8 JSON envelope body.
        /// Its first body byte is always the envelope's open brace, which lands exactly in the preamble position where
        ///     this library reads the frame type. Reserving the brace value guarantees that a legacy peer is detected
        ///     deterministically on its first frame, and reported specifically as a legacy-framing peer,
        ///     rather than surfacing as a generic corruption error.
        /// Receipt is fatal to the connection, like any invalid frame type, but must be logged with the
        ///     legacy-framing diagnosis so a mixed-version deployment is identifiable from the log alone.
        /// </summary>
        public const byte Reserved_LegacyJsonFraming = 123;
    }
}
