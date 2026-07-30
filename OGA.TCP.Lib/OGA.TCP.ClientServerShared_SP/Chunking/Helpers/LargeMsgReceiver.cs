using System;
using OGA.TCP.Chunking.DTO;

namespace OGA.TCP.Chunking.Helpers
{
    /// <summary>
    /// Reassembles a chunked transfer: accumulates the binary chunk segments' byte slices against the
    ///     ChunkStartDTO declaration, and releases the reassembled body at end-of-transfer.
    /// Byte-true and strictly in-order: a segment must land exactly at the accumulation offset (the transports
    ///     are ordered, so a mismatch is corruption, not reordering), accumulation may never exceed the
    ///     declaration, and end-of-transfer validates both total bytes and segment count before the message
    ///     is released — a truncated transfer is discarded, never dispatched.
    /// One instance serves one transfer; the owning endpoint keys instances by TransferId and prunes idle ones.
    /// </summary>
    public class LargeMsgReceiver
    {
        /// <summary>
        /// The transfer's id, from the ChunkStartDTO that opened it.
        /// </summary>
        public string TransferId { get; private set; }

        /// <summary>
        /// Frame type the reassembled body is re-injected as (from the FrameTypes registry).
        /// </summary>
        public byte InnerFrameType { get; private set; }

        /// <summary>
        /// Declared total size of the transfer, in bytes.
        /// </summary>
        public long TotalSize { get; private set; }

        /// <summary>
        /// Declared segment count of the transfer.
        /// </summary>
        public int ChunkCount { get; private set; }

        /// <summary>
        /// Bytes accumulated so far. Doubles as the required offset of the next segment.
        /// </summary>
        public long ReceivedBytes { get; private set; }

        /// <summary>
        /// Segments accepted so far.
        /// </summary>
        public int ReceivedSegments { get; private set; }

        /// <summary>
        /// UTC time of the last accepted message of the transfer. The owning endpoint prunes receivers whose
        ///     last activity is older than its receiver timeout.
        /// </summary>
        public DateTime LastReceivedTimeUTC { get; private set; }

        /// <summary>
        /// The reassembly buffer, allocated from the start declaration (which the owner has already
        ///     validated against its MaxTransferSize).
        /// </summary>
        private byte[] _buffer;

        /// <summary>
        /// Accepts the transfer's start declaration, validating it against the receiver-side transfer cap.
        /// Returns 1 on success; -1 for a malformed declaration; -2 when the declared size exceeds the cap.
        /// </summary>
        /// <param name="dto">The transfer's start declaration.</param>
        /// <param name="maxtransfersize">The receiving endpoint's transfer-size cap, enforced here.</param>
        /// <returns></returns>
        public int AcceptChunkStart(ChunkStartDTO dto, long maxtransfersize)
        {
            if (dto == null || string.IsNullOrEmpty(dto.TransferId))
                return -1;
            if (dto.TotalSize <= 0 || dto.ChunkCount <= 0)
                return -1;
            if (dto.InnerFrameType != FrameTypes.Json && dto.InnerFrameType != FrameTypes.Binary)
                return -1;

            // The declared size is bounded by local policy: this is the receiver's memory defense...
            if (dto.TotalSize > maxtransfersize)
                return -2;

            this.TransferId = dto.TransferId;
            this.InnerFrameType = dto.InnerFrameType;
            this.TotalSize = dto.TotalSize;
            this.ChunkCount = dto.ChunkCount;
            this.ReceivedBytes = 0;
            this.ReceivedSegments = 0;
            this._buffer = new byte[dto.TotalSize];

            this.LastReceivedTimeUTC = DateTime.UtcNow;

            return 1;
        }

        /// <summary>
        /// Accepts one segment's payload slice at the declared offset.
        /// Returns 1 on success; -1 when no start was accepted; -2 for an out-of-order offset (corruption guard);
        ///     -3 when accumulation would exceed the declaration (an under-declaring sender cannot bypass the cap).
        /// A failure abandons the transfer: the owner removes this receiver.
        /// </summary>
        /// <param name="offset">The segment's declared byte offset within the transfer.</param>
        /// <param name="payload">The segment's payload slice.</param>
        /// <returns></returns>
        public int AcceptSegment(long offset, byte[] payload)
        {
            if (this._buffer == null)
                return -1;
            if (payload == null || payload.Length == 0)
                return -1;

            // Strict in-order: the transports are ordered, so a mismatched offset means corruption, not reordering...
            if (offset != this.ReceivedBytes)
                return -2;

            // Accumulation may never exceed the declaration...
            if (this.ReceivedBytes + payload.Length > this.TotalSize)
                return -3;

            Array.Copy(payload, 0, this._buffer, this.ReceivedBytes, payload.Length);
            this.ReceivedBytes += payload.Length;
            this.ReceivedSegments++;

            this.LastReceivedTimeUTC = DateTime.UtcNow;

            return 1;
        }

        /// <summary>
        /// Accepts the transfer's end declaration, validating completeness before releasing the message.
        /// Returns 1 with the reassembled body on success; -1 when no start was accepted; -2 when the
        ///     accumulated bytes or segment count do not match the start declaration (the transfer is
        ///     incomplete, and must be discarded rather than dispatched).
        /// </summary>
        /// <param name="frametype">The frame type to re-inject the reassembled body as.</param>
        /// <param name="body">The reassembled body bytes, on success.</param>
        /// <returns></returns>
        public int AcceptChunkEnd(out byte frametype, out byte[] body)
        {
            frametype = 0;
            body = null;

            if (this._buffer == null)
                return -1;

            // Completeness: every declared byte and every declared segment must have arrived...
            if (this.ReceivedBytes != this.TotalSize || this.ReceivedSegments != this.ChunkCount)
                return -2;

            frametype = this.InnerFrameType;
            body = this._buffer;
            this._buffer = null;

            this.LastReceivedTimeUTC = DateTime.UtcNow;

            return 1;
        }
    }
}
