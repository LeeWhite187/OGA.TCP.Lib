using System;

namespace OGA.TCP.Chunking.DTO
{
    /// <summary>
    /// Announces the abandonment of an in-progress chunked transfer.
    /// Sent as a json message by the sender when a transfer is cancelled or a mid-sequence send fails,
    ///     so the receiver can tear down the transfer's state immediately instead of waiting for the
    ///     idle-receiver prune.
    /// </summary>
    public class ChunkCancelDTO
    {
        /// <summary>
        /// The transfer's id, matching the ChunkStartDTO that opened it.
        /// </summary>
        public string TransferId { get; set; }

        /// <summary>
        /// Constructor preloads fields, so the DTO is never sent with nulls.
        /// </summary>
        public ChunkCancelDTO()
        {
            this.TransferId = "";
        }
    }
}
