using OGA.TCP.Chunking.DTO;
using OGA.TCP.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OGA.TCP.Chunking.Helpers
{
    public class LargeMsgReceiver
    {
        #region Private Fields

        Dictionary<int, string> _chunks;

        #endregion


        #region Public Properties

        public DateTime MessageSentTimeUTC { get; private set; }
        public DateTime StartTimeUTC { get; private set; }

        public DateTime? LastReceivedTimeUTC { get; private set; }

        public DateTime? EndTimeUTC { get; private set; }

        public TimeSpan? Duration { get; private set; }

        /// <summary>
        /// Transfer in bytes per second.
        /// </summary>
        public float? TransferSpeed { get; private set; }

        public int MessageSize { get; private set; }
        public int ReceiveOffset { get; private set; }

        public int ChunkSize { get; private set; }
        public int ChunkCount { get; private set; }

        public string MessageId { get; private set; }
        public string MessageType { get; private set; }
        public string Channel { get; set; }
        public string Scope { get; set; }
        public string CorelationId { get; private set; }

        #endregion


        #region ctor / dtor

        public LargeMsgReceiver()
        {
            _chunks = new Dictionary<int, string>();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Sets up the message properties for the message that will be composed when all chunks are received.
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<int> AcceptChunkStart(ChunkStartDTO dto)
        {
            if(dto == null)
            {
                return -1;
            }

            // Validate the dto...
            if(string.IsNullOrEmpty(dto.MsgId))
            {
                // Empty message id.
                return -1;
            }
            if(string.IsNullOrEmpty(dto.MessageType))
            {
                // Empty MessageType.
                return -1;
            }

            // Set a start time for transfer...
            this.StartTimeUTC = DateTime.UtcNow;

            // Accept the message sent time, as we will use this as the composed message sent time...
            this.MessageSentTimeUTC = dto.SentTimeUTC;

            // Accept properties of the message to be composed...
            this.MessageId = dto.MsgId;
            this.MessageType = dto.MessageType;
            this.MessageSize = dto.MessageSize;
            this.ChunkSize = dto.ChunkSize;
            this.ChunkCount = dto.ChunkCount;
            //this.Channel = dto.Channel;
            //this.Scope = dto.Scope;
            this.CorelationId = CorelationId;

            this.Update_Metrics();

            return 1;
        }

        /// <summary>
        /// Accepts chunks, to compose the whole message.
        /// </summary>
        /// <returns></returns>
        public async Task<int> AcceptChunk(ChunkDTO dto)
        {
            if(dto == null)
            {
                return -1;
            }

            // Verify the chunk belongs to our message...
            if(dto.MsgId != this.MessageId)
            {
                // Wrong message id.
                return -1;
            }

            // We expect chunks to arrive in order.
            if(dto.Offset != this.ReceiveOffset)
            {
                return -1;
            }

            // Add the chunk...
            try
            {
                this._chunks.Add(dto.ChunkId, dto.Data);
            }
            catch(Exception ex)
            {
                // Already exists.
                this._chunks[dto.ChunkId] = dto.Data;
            }

            // Advance the receive offset...
            this.ReceiveOffset = this.ReceiveOffset + dto.ChunkSize;

            this.Update_Metrics();

            return 1;
        }

        /// <summary>
        /// When the chunk end message is received, this method will compose the actual message and return it.
        /// </summary>
        /// <returns></returns>
#if (NET452 || NET48)
        public async Task<(int res, MessageEnvelope me)> AcceptChunkEnd(ChunkEndDTO dto)
#else
        public async Task<(int res, MessageEnvelope? me)> AcceptChunkEnd(ChunkEndDTO dto)
#endif
        {
            if(dto == null)
            {
                return (-1, null);
            }

            // Verify the chunk belongs to our message...
            if(dto.MsgId != this.MessageId)
            {
                // Wrong message id.
                return (-1, null);
            }

            // We've received the whole message.
            // We can return it to the caller.
            var msg = new MessageEnvelope();
            msg.MsgId = this.MessageId;
            msg.SentTimeUTC = this.MessageSentTimeUTC;
            msg.MessageType = this.MessageType;
            msg.Channel = this.Channel;
            msg.Scope = this.Scope;
            msg.ReplyTo = "";
            msg.Props = new string[] { "corelationid=" + this.CorelationId ?? "" };

            StringBuilder b = new StringBuilder();
            foreach(var chunk in this._chunks)
                b.Append(chunk.Value);
            msg.Data = b.ToString();

            this.Update_Metrics();

            return (1, msg);
        }

        public async Task<int> AcceptChunkCancel(ChunkCancelDTO dto)
        {
            if(dto == null)
            {
                return -1;
            }

            // Verify the chunk belongs to our message...
            if(dto.MsgId != this.MessageId)
            {
                // Wrong message id.
                return -1;
            }

            this.Update_Metrics();

            return 1;
        }

        #endregion


        #region Private Methods

        private void Update_Metrics(bool isend = false)
        {
            this.LastReceivedTimeUTC = DateTime.UtcNow;

            // Calculate duration...
            var ctime = DateTime.UtcNow;
            this.Duration = ctime.Subtract(this.StartTimeUTC);

            // Calculate the transfer velocity...
            if(this.Duration.HasValue && this.Duration.Value.TotalSeconds > 0)
                this.TransferSpeed = (float)(this.ReceiveOffset / this.Duration.Value.TotalSeconds);
            else
                this.TransferSpeed = 0.0f;

            if(isend)
                this.EndTimeUTC = ctime;
        }

        #endregion
    }
}
