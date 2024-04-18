using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Messages
{
    /// <summary>
    /// Used for data exchange between tcp/websocket clients and hosts.
    /// Provides a generic message wrapper with id, type, time.
    /// Also, has scope and properties that can be used in the future without affecting older version clients.
    /// </summary>
    public class MessageEnvelope
    {
        // Prevent allocation attacks. Each packet is prefixed with a length
        // header, so an attacker could send a fake packet with length=2GB,
        // causing the server to allocate 2GB and run out of memory quickly.
        // -> simply increase max packet size if you want to send around bigger
        //    files!
        // -> 16KB per message should be more than enough.
        static public int MaxMessageSize = 16 * 1024;

        public string MsgId { get; set; }

        public DateTime SentTimeUTC { get; set; }

        public string MessageType { get; set; }

        public string Data { get; set; }

        public string Scope { get; set; }

        public string Channel { get; set; }

        public string ReplyTo { get; set; }

        public string[] Props { get; set; }

        public MessageEnvelope()
        {
            MsgId = "";
            SentTimeUTC = DateTime.UtcNow;
            MessageType = "";
            Data = "";
            Scope = "";
            Channel = "";
            ReplyTo = "";
            Props = new string[0];
        }

        public string ToLogString()
        {
            StringBuilder b = new StringBuilder();

            b.AppendLine("MsgId = " + this.MsgId ?? "");
            b.AppendLine("SentTimeUTC = " + this.SentTimeUTC.ToString("O"));
            b.AppendLine("Message_Type = " + this.MsgId ?? "");

            b.AppendLine("Data = " + this.Data ?? "");
            b.AppendLine("Scope = " + this.Scope ?? "");
            b.AppendLine("Channel = " + this.Channel ?? "");
            b.AppendLine("ReplyTo = " + this.ReplyTo ?? "");

            if(Props == null)
            {
            }
            else if(Props.Length == 0)
            {
                b.AppendLine($"Empty Props");
            }
            else
            {
                int x = 0;
                for(x = 0; x < Props.Length; x++)
                {
                    b.AppendLine($"Prop {x.ToString()} = {(Props[x] ?? "")}");
                }
            }

            return b.ToString();
        }
    }
}
