using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Chunking.DTO
{
    public class ChunkEndDTO
    {
        public string MsgId { get; set; }

        public ChunkEndDTO()
        {
            MsgId = "";
        }

        public string ToLogString()
        {
            StringBuilder b = new StringBuilder();

            b.AppendLine("MsgId = " + this.MsgId ?? "");

            return b.ToString();
        }
    }
}
