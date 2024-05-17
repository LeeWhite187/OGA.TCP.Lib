using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Chunking.DTO
{
    /// <summary>
    /// Currently unused chunk message type.
    /// </summary>
    public class ChunkCancelDTO
    {
        public string MsgId { get; set; }


        public ChunkCancelDTO()
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
