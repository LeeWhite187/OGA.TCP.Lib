using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OGA.TCP.Chunking.DTO;
using OGA.TCP.Messages;

namespace OGA.TCP.Chunking.Helpers
{
    /// <summary>
    /// Delegate signature for sending a chunking control message (start, end, cancel) as a json message.
    /// Matches the endpoints' serialized-object send methods.
    /// Returns 1 on success, 0 when the session cannot send, negatives on error.
    /// </summary>
    /// <param name="objecttype">Message type name of the control DTO.</param>
    /// <param name="jsonobject">Serialized control DTO.</param>
    /// <param name="channel">Channel of the original message; control rides the same channel.</param>
    /// <param name="scope">Scope of the original message.</param>
    /// <param name="corelationid">Correlation id of the original message, or empty.</param>
    /// <returns></returns>
    public delegate Task<int> DelSendChunkControl(string objecttype, string jsonobject, string channel, string scope, string corelationid);

    /// <summary>
    /// Delegate signature for sending one encoded frame body.
    /// Matches the endpoints' frame-send choke point.
    /// Returns 1 on success, 0 when the session cannot send, negatives on error.
    /// </summary>
    /// <param name="frametype">The frame's type byte, from the FrameTypes registry.</param>
    /// <param name="body">The frame's encoded body bytes.</param>
    /// <returns></returns>
    public delegate Task<int> DelSendFrame(byte frametype, byte[] body);

    /// <summary>
    /// Splits an oversized message's encoded body bytes into a chunked transfer:
    ///     a json ChunkStartDTO, N binary chunk-segment messages carrying raw byte slices, then a json ChunkEndDTO.
    /// The sender never interprets the bytes it slices — a chunked json message and a chunked binary message
    ///     are handled identically, and the start message's InnerFrameType tells the receiver how to re-inject
    ///     the reassembled bytes.
    /// Slicing is byte-true: each segment's complete binary body (routing header plus payload slice) is measured
    ///     against the frame cap, so a chunk frame can never itself violate the cap.
    /// Each frame send is awaited through the endpoint's write semaphore, so other channels' traffic interleaves
    ///     between segments (the share-the-pipe property). There are no acks or retransmissions: the transports
    ///     are reliable and ordered.
    /// One instance serves one transfer.
    /// </summary>
    public class LargeMsgSender
    {
        /// <summary>
        /// Class name used for logging.
        /// </summary>
        private string _classname;

        /// <summary>
        /// Logger instance. Optional.
        /// </summary>
        private NLog.ILogger Logger;

        /// <summary>
        /// The transfer's id, carried by every message of the transfer.
        /// </summary>
        public string TransferId { get; private set; }

        /// <summary>
        /// Frame type of the original, unsplit message body (from the FrameTypes registry).
        /// </summary>
        public byte InnerFrameType { get; private set; }

        /// <summary>
        /// The encoded body bytes being transferred.
        /// </summary>
        private byte[] _data;

        /// <summary>
        /// Channel of the original message; every message of the transfer rides it.
        /// </summary>
        public string Channel { get; private set; }

        /// <summary>
        /// Scope of the original message.
        /// </summary>
        public string Scope { get; private set; }

        /// <summary>
        /// Correlation id of the original message, or empty.
        /// </summary>
        public string CorelationId { get; private set; }

        /// <summary>
        /// The frame-body cap each segment's complete binary body must fit under. Assigned from the
        ///     sending endpoint's MaxFrameSize.
        /// </summary>
        public int MaxFrameSize { get; set; } = OGA.TCP.Constants.CONST_MAX_MessageSize;

        /// <summary>
        /// The most payload bytes one segment may carry. Zero means fit-to-frame: each segment carries as much
        ///     as fits under MaxFrameSize after the measured routing-header overhead.
        /// Assigned from the sending endpoint's MaxChunkPayloadSize; tuning it below fit-to-frame gives other
        ///     channels more frequent interleave slots during the transfer.
        /// </summary>
        public int MaxChunkPayloadSize { get; set; } = 0;

        /// <summary>
        /// Bytes of the transfer sent so far, for progress observation.
        /// </summary>
        public long SentBytes { get; private set; }

        /// <summary>
        /// One timestamp for every segment header of the transfer, captured at Load.
        /// Segment headers are serialized twice per offset (once to measure, once to send), and json
        ///     DateTime serialization is variable-length (trailing zeros of the fraction are trimmed) —
        ///     a per-serialization timestamp would make the measured size differ from the sent size.
        /// </summary>
        private DateTime _headertimestamp;

        /// <summary>
        /// Constructor accepts an optional logger.
        /// </summary>
        /// <param name="logger">Optional logger instance.</param>
        public LargeMsgSender(NLog.ILogger logger = null)
        {
            this._classname = nameof(LargeMsgSender);
            this.Logger = logger;
        }

        /// <summary>
        /// Loads the transfer: the id, the original body's frame type, the encoded bytes, and the routing values.
        /// Returns 1 on success, -1 for missing id or data.
        /// </summary>
        /// <param name="transferid">The transfer's id; unique per in-flight transfer on the connection.</param>
        /// <param name="innerframetype">Frame type of the original message body.</param>
        /// <param name="data">The encoded body bytes to transfer.</param>
        /// <param name="channel">Channel of the original message.</param>
        /// <param name="scope">Scope of the original message.</param>
        /// <param name="corelationid">Correlation id of the original message, or empty.</param>
        /// <returns></returns>
        public int Load(string transferid, byte innerframetype, byte[] data, string channel, string scope, string corelationid)
        {
            if (string.IsNullOrEmpty(transferid) || data == null || data.Length == 0)
                return -1;

            this.TransferId = transferid;
            this.InnerFrameType = innerframetype;
            this._data = data;
            this.Channel = channel ?? "";
            this.Scope = scope ?? "";
            this.CorelationId = corelationid ?? "";
            this.SentBytes = 0;
            this._headertimestamp = DateTime.UtcNow;

            return 1;
        }

        /// <summary>
        /// Performs the transfer: start message, segments, end message — awaiting each send.
        /// On cancellation or a mid-sequence send failure, a best-effort cancel message is sent so the
        ///     receiver can tear down its state, and the method returns 0 (cancelled) or a negative (failure).
        /// Returns 1 when the full transfer was sent.
        /// </summary>
        /// <param name="sendcontrol">Callback that sends a json control message.</param>
        /// <param name="sendframe">Callback that sends one encoded frame body.</param>
        /// <param name="token">Cancellation token; observed between sends.</param>
        /// <returns></returns>
        public async Task<int> SendChunksAsync(DelSendChunkControl sendcontrol, DelSendFrame sendframe, CancellationToken token)
        {
            if (this._data == null)
                return -1;

            // Pre-compute the segmentation, so the start message declares an accurate count...
            int chunkcount = 0;
            long offset = 0;
            int firstslice = 0;
            while (offset < this._data.Length)
            {
                int slice = this.Measure_SegmentPayloadSize(offset);
                if (slice <= 0)
                {
                    // The frame cap is too small to fit a segment header plus any payload.
                    this.Logger?.Error(
                        $"{_classname}::{nameof(SendChunksAsync)} - " +
                        $"MaxFrameSize ({this.MaxFrameSize.ToString()}) is too small to carry chunk segments. Transfer abandoned.");

                    return -2;
                }

                if (chunkcount == 0)
                    firstslice = slice;

                chunkcount++;
                offset += slice;
            }

            // ---- Start message ----
            var start = new ChunkStartDTO();
            start.TransferId = this.TransferId;
            start.InnerFrameType = this.InnerFrameType;
            start.TotalSize = this._data.Length;
            start.ChunkSize = firstslice;
            start.ChunkCount = chunkcount;
            start.SentTimeUTC = DateTime.UtcNow;

            var res = await sendcontrol(nameof(ChunkStartDTO),
                                        Newtonsoft.Json.JsonConvert.SerializeObject(start),
                                        this.Channel, this.Scope, this.CorelationId);
            if (res != 1)
            {
                this.Logger?.Error(
                    $"{_classname}::{nameof(SendChunksAsync)} - " +
                    $"Failed to send chunk start message for transfer ({this.TransferId}). Transfer abandoned.");

                return res < 0 ? res : -3;
            }

            // ---- Segments ----
            offset = 0;
            while (offset < this._data.Length)
            {
                if (token.IsCancellationRequested)
                {
                    await this.Send_Cancel(sendcontrol);
                    return 0;
                }

                int slice = this.Measure_SegmentPayloadSize(offset);

                // Compose the segment's binary body: routing header (transfer id + offset in props) plus the raw slice...
                var header = this.Compose_SegmentHeader(offset);
                var payload = new byte[slice];
                Array.Copy(this._data, offset, payload, 0, slice);
                var body = header.ComposeBody(payload);

                res = await sendframe(FrameTypes.Binary, body);
                if (res != 1)
                {
                    this.Logger?.Error(
                        $"{_classname}::{nameof(SendChunksAsync)} - " +
                        $"Failed to send chunk segment at offset ({offset.ToString()}) for transfer ({this.TransferId}). Transfer abandoned.");

                    await this.Send_Cancel(sendcontrol);
                    return res < 0 ? res : -4;
                }

                offset += slice;
                this.SentBytes = offset;
            }

            // ---- End message ----
            var end = new ChunkEndDTO();
            end.TransferId = this.TransferId;

            res = await sendcontrol(nameof(ChunkEndDTO),
                                    Newtonsoft.Json.JsonConvert.SerializeObject(end),
                                    this.Channel, this.Scope, this.CorelationId);
            if (res != 1)
            {
                this.Logger?.Error(
                    $"{_classname}::{nameof(SendChunksAsync)} - " +
                    $"Failed to send chunk end message for transfer ({this.TransferId}). Transfer abandoned.");

                await this.Send_Cancel(sendcontrol);
                return res < 0 ? res : -5;
            }

            return 1;
        }

        /// <summary>
        /// Measures the payload size of the segment starting at the given offset: the remaining bytes, capped by
        ///     the configured chunk payload size and by what fits under MaxFrameSize after the measured
        ///     routing-header overhead of THIS segment (header size varies with the offset's digit count).
        /// Returns 0 or negative when nothing can fit — a configuration error.
        /// </summary>
        /// <param name="offset">Byte offset of the segment within the transfer.</param>
        /// <returns></returns>
        private int Measure_SegmentPayloadSize(long offset)
        {
            // Measure the segment's real header cost at this offset...
            var header = this.Compose_SegmentHeader(offset);
            var headerbytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(header));

            // The binary body is [4-byte header length][header][payload]; it must fit under the frame cap...
            int room = this.MaxFrameSize - 4 - headerbytes.Length;
            if (room <= 0)
                return room;

            long remaining = this._data.Length - offset;

            int slice = (int)Math.Min(remaining, room);
            if (this.MaxChunkPayloadSize > 0 && slice > this.MaxChunkPayloadSize)
                slice = this.MaxChunkPayloadSize;

            return slice;
        }

        /// <summary>
        /// Composes the routing header of the segment at the given offset: the chunk-segment message type, the
        ///     original message's channel and scope, and props carrying the correlation id, transfer id, and offset.
        /// </summary>
        /// <param name="offset">Byte offset of the segment within the transfer.</param>
        /// <returns></returns>
        private BinaryMessageHeader Compose_SegmentHeader(long offset)
        {
            var header = new BinaryMessageHeader();
            // The transfer-constant timestamp keeps the serialized header length identical between the
            //  measuring pass and the sending pass (see _headertimestamp)...
            header.SentTimeUTC = this._headertimestamp;
            header.MessageType = ChunkingConstants.CONST_ChunkSegment_MessageType;
            header.Channel = this.Channel;
            header.Scope = this.Scope;
            header.Props = new string[]
            {
                "corelationid=" + this.CorelationId,
                ChunkingConstants.CONST_Prop_TransferId + this.TransferId,
                ChunkingConstants.CONST_Prop_Offset + offset.ToString()
            };

            return header;
        }

        /// <summary>
        /// Best-effort cancel announcement to the receiver, so its transfer state tears down immediately.
        /// Failures are swallowed: the receiver's idle prune is the backstop.
        /// </summary>
        /// <param name="sendcontrol">Callback that sends a json control message.</param>
        /// <returns></returns>
        private async Task Send_Cancel(DelSendChunkControl sendcontrol)
        {
            try
            {
                var cancel = new ChunkCancelDTO();
                cancel.TransferId = this.TransferId;

                await sendcontrol(nameof(ChunkCancelDTO),
                                  Newtonsoft.Json.JsonConvert.SerializeObject(cancel),
                                  this.Channel, this.Scope, this.CorelationId);
            }
            catch (Exception) { }
        }
    }
}
