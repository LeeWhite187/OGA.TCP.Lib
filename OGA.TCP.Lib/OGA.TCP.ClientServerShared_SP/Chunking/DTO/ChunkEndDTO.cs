using System;

namespace OGA.TCP.Chunking.DTO
{
    /// <summary>
    /// Announces the completion of a chunked transfer.
    /// Sent as a json message after the final binary chunk segment.
    /// On receipt, the receiver validates completeness (accumulated bytes and segment count against the
    ///     start declaration) and re-injects the reassembled message into normal processing; an incomplete
    ///     transfer is discarded rather than dispatched.
    /// </summary>
    public class ChunkEndDTO
    {
        /// <summary>
        /// The transfer's id, matching the ChunkStartDTO that opened it.
        /// </summary>
        public string TransferId { get; set; }

        /// <summary>
        /// Constructor preloads fields, so the DTO is never sent with nulls.
        /// </summary>
        public ChunkEndDTO()
        {
            this.TransferId = "";
        }
    }
}
