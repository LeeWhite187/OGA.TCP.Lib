using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Chunking.DTO
{
    public class ChunkStartDTO
    {
        public string MsgId { get; set; }

        public DateTime SentTimeUTC { get; set; }

        public string MessageType { get; set; }

        public int MessageSize { get; set; }
        public int ChunkSize { get; set; }
        public int ChunkCount { get; set; }

        public ChunkStartDTO()
        {
            MsgId = "";
            SentTimeUTC = DateTime.UtcNow;
            MessageType = "";
        }

        public string ToLogString()
        {
            StringBuilder b = new StringBuilder();

            b.AppendLine("MsgId = " + this.MsgId ?? "");
            b.AppendLine("SentTimeUTC = " + this.SentTimeUTC.ToString("O"));
            b.AppendLine("Message_Type = " + this.MsgId ?? "");

            b.AppendLine("MessageSize = " + this.MessageSize.ToString());
            b.AppendLine("ChunkSize = " + this.ChunkSize.ToString());
            b.AppendLine("ChunkCount = " + this.ChunkCount.ToString());

            return b.ToString();
        }
    }
}
