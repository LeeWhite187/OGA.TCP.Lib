using System;

namespace OGA.TCP.Chunking
{
    /// <summary>
    /// Wire-string constants of the chunking protocol.
    /// The chunking layer splits an oversized message's encoded body bytes across frames: a json ChunkStartDTO,
    ///     N binary chunk-segment messages, then a json ChunkEndDTO. Control messages are json (internal
    ///     machinery rides json); segment payloads are raw bytes (no encoding cost).
    /// </summary>
    static public class ChunkingConstants
    {
        /// <summary>
        /// Message type (lowercase) carried in a chunk-segment binary message's routing header.
        /// Segments are diverted to the chunking layer by this type, before channel dispatch.
        /// </summary>
        public const string CONST_ChunkSegment_MessageType = "chunksegment";

        /// <summary>
        /// Routing-header prop key carrying the segment's transfer id ("transferid=&lt;id&gt;").
        /// The transfer id is the chunking layer's own key, deliberately distinct from any frame's MsgId.
        /// </summary>
        public const string CONST_Prop_TransferId = "transferid=";

        /// <summary>
        /// Routing-header prop key carrying the segment's byte offset within the transfer ("offset=&lt;n&gt;").
        /// Segments arrive strictly in order; the offset is a corruption guard, not a reordering mechanism.
        /// </summary>
        public const string CONST_Prop_Offset = "offset=";
    }
}
