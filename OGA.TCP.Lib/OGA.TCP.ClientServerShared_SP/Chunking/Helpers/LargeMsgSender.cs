using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OGA.TCP.Chunking.DTO;
using OGA.TCP.Messages;

namespace OGA.TCP.Chunking.Helpers
{
    /// <summary>
    /// Allows a sender to chunk up messages for sending to a remote endpoint.
    /// </summary>
    public class LargeMsgSender
    {
        #region Private Fields

        private string _data;

        private int _lastchunkid = 0;

        #endregion


        #region Public Properties

        public DateTime StartTimeUTC { get; private set; }

        public DateTime? EndTimeUTC { get; private set; }

        public TimeSpan? Duration { get; private set; }

        /// <summary>
        /// Transfer in bytes per second.
        /// </summary>
        public float? TransferSpeed { get; private set; }

        public int MaxChunkSize { get; set; }

        public int MessageSize { get; private set; }
        public int SentOffset { get; private set; }

        public int ChunkCount { get; private set; }

        public string MessageId { get; private set; }
        public string MessageType { get; private set; }
        public string Channel { get; private set; }
        public string Scope { get; private set; }
        public string CorelationId { get; private set; }

        #endregion


        #region ctor / dtor

        public LargeMsgSender()
        {
            SentOffset = 0;
        }

        #endregion


        #region Public Methods

        public int Load(string messageid, string messagetype, string jsonobject, string channel, string scope, string corelationid)
        {
            if (string.IsNullOrEmpty(jsonobject))
            {
                // Null message.
                return -1;
            }
            if (string.IsNullOrEmpty(messagetype))
            {
                // Null message type.
                return -1;
            }
            if (string.IsNullOrEmpty(messageid))
            {
                // Null messageId.
                return -1;
            }

            this._data = jsonobject;

            // Store properties for the composed message...
            this.MessageId = messageid;
            this.MessageType = messagetype;
            this.Channel = channel;
            this.Scope = scope;
            this.CorelationId = corelationid;


            // Get message length...
            MessageSize = _data.Length;

            // Determine how many chunks...
            Determine_Chunk_Count();

            return 1;
        }

        public async Task<int> SendChunksAsync(Func<string, string, string, string, string, Task<int>> CALLBACKsendmsg,
                                               CancellationToken token = default(CancellationToken))
        {
            try
            {
                if(CALLBACKsendmsg == null)
                {
                    // Nowhere to send.
                    return -1;
                }

                // Declare a start time...
                this.StartTimeUTC = DateTime.UtcNow;

                // Get the chunk start message...
                var resstart = await this.SendStartMessage(CALLBACKsendmsg);
                if(resstart != 1)
                {
                    // Failed to send start message.
                    return -1;
                }

                // Check if we are cancelled...
                if(token.IsCancellationRequested)
                {
                    // We are cancelled.
                    return 0;
                }

                // Get chunks until no more exist...
                while(true)
                {
                    // Check if we are cancelled...
                    if(token.IsCancellationRequested)
                    {
                        // We are cancelled.

                        // Attempt to send a cancellation, so the far end tears down its chunk composer...
                        _= Task.Run( async () => await this.SendCancel(CALLBACKsendmsg));

                        return 0;
                    }

                    // Get the next chunk...
                    var res = this.Get_Chunk(out var chunk);
                    if(res == 0)
                    {
                        // No more chunks.
                        break;
                    }

                    // Serialize the chunk...
                    var jsonmsg = Newtonsoft.Json.JsonConvert.SerializeObject(chunk);

                    // Check if we are cancelled...
                    if(token.IsCancellationRequested)
                    {
                        // We are cancelled.

                        // Attempt to send a cancellation, so the far end tears down its chunk composer...
                        _= Task.Run( async () => await this.SendCancel(CALLBACKsendmsg));

                        return 0;
                    }

                    // Send each chunk...
                    var ressend = await CALLBACKsendmsg(nameof(ChunkDTO), jsonmsg, this.Channel, this.Scope, this.CorelationId);
                    if(ressend != 1)
                    {
                        // Failed to send chunk.

                        // Attempt to send a cancellation, so the far end tears down its chunk composer...
                        _= Task.Run( async () => await this.SendCancel(CALLBACKsendmsg));

                        return -1;
                    }

                    // Update metrics...
                    this.Update_Metrics();
                }
                // All chunks are sent.

                // Send the chunk end message...
                var resend = await this.SendEndMessage(CALLBACKsendmsg);
                if(resend != 1)
                {
                    // Failed to send end message.
                    return -1;
                }

                // Update metrics...
                this.Update_Metrics(true);

                return 1;
            }
            catch(ObjectDisposedException ode)
            {
                return -1;
            }
            catch(Exception e)
            {
                return -1;
            }
        }

        #endregion


        #region Private Methods

        private async Task<int> SendStartMessage(Func<string, string, string, string, string, Task<int>> cALLBACKsendmsg)
        {
            try
            {
                // Attempt to send the chunk start message...
                var msg = CreateChunkStartMsg();
                msg.MsgId = this.MessageId;
                var jsonmsg = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                await cALLBACKsendmsg(nameof(ChunkStartDTO), jsonmsg, this.Channel, this.Scope, this.CorelationId);

                return 1;
            }
            catch (Exception)
            {
                return -2;
            }
        }

        private async Task SendCancel(Func<string, string, string, string, string, Task<int>> cALLBACKsendmsg)
        {
            try
            {
                // Attempt to send a cancel, in case the connection is still open...
                var msg = new ChunkCancelDTO();
                msg.MsgId = this.MessageId;
                var jsonmsg = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                await cALLBACKsendmsg(nameof(ChunkCancelDTO), jsonmsg, this.Channel, this.Scope, this.CorelationId);
            }
            catch (Exception)
            {
                return;
            }
        }

        private async Task<int> SendEndMessage(Func<string, string, string, string, string, Task<int>> cALLBACKsendmsg)
        {
            try
            {
                // Attempt to send the chunk end message...
                var msg = CreateChunkEndMsg();
                msg.MsgId = this.MessageId;
                var jsonmsg = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                await cALLBACKsendmsg(nameof(ChunkEndDTO), jsonmsg, this.Channel, this.Scope, this.CorelationId);

                return 1;
            }
            catch (Exception)
            {
                return -2;
            }
        }

        private int CreateChunkId()
        {
            this._lastchunkid++;

            return this._lastchunkid;
        }

        private ChunkStartDTO CreateChunkStartMsg()
        {
            var cim = new ChunkStartDTO();
            cim.MsgId = this.MessageId;
            cim.SentTimeUTC = this.StartTimeUTC;
            cim.MessageType = this.MessageType ?? "";
            cim.MessageSize = this.MessageSize;
            cim.ChunkSize = this.MaxChunkSize;
            cim.ChunkCount = this.ChunkCount;
            return cim;
        }

        private ChunkEndDTO CreateChunkEndMsg()
        {
            var cim = new ChunkEndDTO();
            cim.MsgId = this.MessageId;
            return cim;
        }

        /// <summary>
        /// Returns 1 if a chunk is available, 0 if not.
        /// </summary>
        /// <param name="me"></param>
        /// <returns></returns>
        private int Get_Chunk(out ChunkDTO me)
        {
            me = null;

            if(this.SentOffset >= this.MessageSize)
            {
                // Nothing left to send.
                return 0;
            }

            // Create a chunk message for the next chunk...
            me = new ChunkDTO();
            me.MsgId = MessageId;
            me.ChunkId = this.CreateChunkId();
            me.Offset = this.SentOffset;

            // Determine how much to put in the chunk...
            var remaining = this.MessageSize - this.SentOffset;

            if (remaining >= this.MaxChunkSize)
            {
                // There is more data than will fit into a chunk.
                // We will create a full chunk.
                me.ChunkSize = this.MaxChunkSize;
            }
            else
            {
                // There is not enough remaining data to fill a chunk.
                // We will load whatever is left.
                me.ChunkSize = remaining;
            }

            // Load the next chunk...
            me.Data = this._data.Substring(this.SentOffset, me.ChunkSize);

            // Advance the sent offset...
            this.SentOffset = this.SentOffset + me.ChunkSize;

            return 1;
        }

        private void Determine_Chunk_Count()
        {
            int chunkcount = 0;

            // Start with the raw message...
            int bytesleft = MessageSize;

            // Create a list of chunks...
            while (bytesleft > 0)
            {
                // See if the remaining message is smaller than a chunk....
                if (bytesleft <= Constants.CONST_MAX_MessageSize)
                {
                    // The rest of the message will fit into a chunk.

                    bytesleft = bytesleft - bytesleft;
                    chunkcount++;
                }
                else
                {
                    // The rest of the message will not fit into a single chunk.
                    // We will fill a chunk and have leftover.

                    bytesleft = bytesleft - Constants.CONST_MAX_MessageSize;
                    chunkcount++;
                }
            }

            ChunkCount = chunkcount;
        }

        private void Update_Metrics(bool isend = false)
        {
            // Calculate duration...
            var ctime = DateTime.UtcNow;
            this.Duration = ctime.Subtract(this.StartTimeUTC);

            // Calculate the transfer velocity...
            if(this.Duration.HasValue && this.Duration.Value.TotalSeconds > 0)
                this.TransferSpeed = (float)(this.SentOffset / this.Duration.Value.TotalSeconds);
            else
                this.TransferSpeed = 0.0f;

            if(isend)
                this.EndTimeUTC = ctime;
        }

        #endregion
    }
}
