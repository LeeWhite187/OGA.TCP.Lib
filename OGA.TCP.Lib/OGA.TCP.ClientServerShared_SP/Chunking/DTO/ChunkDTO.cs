using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Chunking.DTO
{
    public class ChunkDTO
    {
        public string MsgId { get; set; }

        public int ChunkId { get; set; }

        public int Offset { get; set; }

        public int ChunkSize { get; set; }

        public string Data { get; set; }
    }
}
