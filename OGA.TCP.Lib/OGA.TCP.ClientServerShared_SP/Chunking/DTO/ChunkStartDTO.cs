using System;

namespace OGA.TCP.Chunking.DTO
{
    /// <summary>
    /// Announces the start of a chunked transfer: an oversized message whose encoded body bytes are being
    ///     split across frames.
    /// Sent as a json message on the original message's channel and scope, ahead of the binary chunk segments.
    /// The receiver uses this declaration to allocate and validate the transfer; sizes are byte-true.
    /// </summary>
    public class ChunkStartDTO
    {
        /// <summary>
        /// The transfer's id: the key every segment and the end/cancel messages carry.
        /// Deliberately distinct from any frame's MsgId, which changes per frame.
        /// </summary>
        public string TransferId { get; set; }

        /// <summary>
        /// The frame type (from the FrameTypes registry) of the original, unsplit message body.
        /// The receiver re-injects the reassembled bytes into normal processing as this type, so a chunked
        ///     json message and a chunked binary message each come out the other side as themselves.
        /// </summary>
        public byte InnerFrameType { get; set; }

        /// <summary>
        /// Total size of the original encoded body, in bytes.
        /// The receiver rejects transfers declaring more than its configured maximum transfer size, and
        ///     validates completeness against this value at end-of-transfer.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// The sender's segment payload sizing, in bytes (informational; the final segment carries the remainder).
        /// </summary>
        public int ChunkSize { get; set; }

        /// <summary>
        /// Number of segments the sender will emit.
        /// The receiver validates completeness against this count at end-of-transfer.
        /// </summary>
        public int ChunkCount { get; set; }

        /// <summary>
        /// Message type name of the original message, for diagnostics and logging.
        /// Routing of the reassembled message comes from its own reassembled body, not from this field.
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// UTC time the transfer was started.
        /// </summary>
        public DateTime SentTimeUTC { get; set; }

        /// <summary>
        /// Constructor preloads fields, so the DTO is never sent with nulls.
        /// </summary>
        public ChunkStartDTO()
        {
            this.TransferId = "";
            this.InnerFrameType = 0;
            this.TotalSize = 0;
            this.ChunkSize = 0;
            this.ChunkCount = 0;
            this.MessageType = "";
            this.SentTimeUTC = DateTime.UtcNow;
        }
    }
}
