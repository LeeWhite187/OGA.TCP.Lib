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
        /// <summary>
        /// Message id of the envelope. Assigned per sent frame; diagnostic and correlational - nothing
        ///     orders or dedupes by it.
        /// </summary>
        public string MsgId { get; set; }

        /// <summary>
        /// UTC time the message was sent, stamped by the sender.
        /// </summary>
        public DateTime SentTimeUTC { get; set; }

        /// <summary>
        /// Message type name, used for routing and deserialization on the receiving end.
        /// Compared case-insensitively; internal types (ping, pong, registration, chunk control) are
        ///     intercepted by the session layer before consumer dispatch.
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// The message payload, as serialized json. Empty for payload-less internal messages (ping/pong).
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Routing scope value. Empty = unset. The value 'loopback=rawmsg' asks the server to echo the
        ///     message back (a diagnostics feature).
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// Channel name the message is routed to. Empty = no channel; the message lands on the receiving
        ///     side's no-channel handler.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Reserved reply-routing field. Carried on the wire for future use; not consumed by this library.
        /// </summary>
        public string ReplyTo { get; set; }

        /// <summary>
        /// Property strings carried with the message ('key=value' convention, e.g. the correlation id).
        /// Additive extension point: new props do not affect older receivers.
        /// </summary>
        public string[] Props { get; set; }

        /// <summary>
        /// Constructor preloads every field, so an envelope is never sent with nulls.
        /// </summary>
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

        /// <summary>
        /// Creates a loggable, line-per-field dump of the envelope, for diagnostics.
        /// </summary>
        /// <returns></returns>
        public string ToLogString()
        {
            StringBuilder b = new StringBuilder();

            b.AppendLine("MsgId = " + (this.MsgId ?? ""));
            b.AppendLine("SentTimeUTC = " + this.SentTimeUTC.ToString("O"));
            b.AppendLine("Message_Type = " + (this.MessageType ?? ""));

            b.AppendLine("Data = " + (this.Data ?? ""));
            b.AppendLine("Scope = " + (this.Scope ?? ""));
            b.AppendLine("Channel = " + (this.Channel ?? ""));
            b.AppendLine("ReplyTo = " + (this.ReplyTo ?? ""));

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
