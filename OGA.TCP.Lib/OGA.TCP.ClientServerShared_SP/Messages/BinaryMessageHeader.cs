using System;
using System.Text;
using OGA.TCP.Shared.Encoding;

namespace OGA.TCP.Messages
{
    /// <summary>
    /// Routing header of a binary message: the MessageEnvelope's routing fields, minus the Data field,
    ///     because a binary message's payload rides as raw bytes outside the json.
    /// A binary frame's body is laid out as: a 4-byte little-endian header length, this header as UTF-8 json,
    ///     then the raw payload bytes. The payload carries no base64 or escaping cost.
    /// Over the websocket transport, the identical body layout rides inside a native binary websocket message.
    /// </summary>
    public class BinaryMessageHeader
    {
        /// <summary>
        /// Message id of the frame. Diagnostic and correlational; nothing orders or dedupes by it.
        /// </summary>
        public string MsgId { get; set; }

        /// <summary>
        /// UTC time the message was sent.
        /// </summary>
        public DateTime SentTimeUTC { get; set; }

        /// <summary>
        /// Message type name, for consumer routing. Compared case-insensitively on receive, like the envelope's.
        /// Empty is legal: an opaque payload whose meaning the channel implies.
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// Routing scope value. Empty = unset.
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// Channel name the message is routed to. Empty = no channel (delivered to the no-channel binary fallback).
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Property strings carried with the message (e.g. the correlation id, chunk-transfer metadata).
        /// Same "key=value" string convention as the envelope's Props.
        /// </summary>
        public string[] Props { get; set; }

        /// <summary>
        /// Constructor preloads fields, so a header is never sent with nulls.
        /// </summary>
        public BinaryMessageHeader()
        {
            this.MsgId = "";
            this.SentTimeUTC = DateTime.UtcNow;
            this.MessageType = "";
            this.Scope = "";
            this.Channel = "";
            this.Props = new string[0];
        }

        /// <summary>
        /// Composes a binary frame body from this header and a payload: [Int32 headerLen][UTF-8 json header][payload].
        /// </summary>
        /// <param name="payload">The raw payload bytes. May be empty, never null.</param>
        /// <returns></returns>
        public byte[] ComposeBody(byte[] payload)
        {
            var headerjson = Newtonsoft.Json.JsonConvert.SerializeObject(this);
            var headerbytes = Encoding.UTF8.GetBytes(headerjson);

            int payloadlength = payload?.Length ?? 0;
            var body = new byte[4 + headerbytes.Length + payloadlength];

            cCustom_Serializer.Serialize_Integer32(headerbytes.Length, ref body, 0);
            Array.Copy(headerbytes, 0, body, 4, headerbytes.Length);

            if (payloadlength > 0)
                Array.Copy(payload, 0, body, 4 + headerbytes.Length, payloadlength);

            return body;
        }

        /// <summary>
        /// Parses a binary frame body into its header and payload.
        /// Returns 1 on success; negatives when the body is malformed (too short, bad header length,
        ///     or an undeserializable header) - the caller treats those as a malformed message, not a
        ///     connection fault.
        /// </summary>
        /// <param name="body">The received binary frame body.</param>
        /// <param name="header">The parsed routing header, on success.</param>
        /// <param name="payload">The raw payload bytes, on success. Empty when the message carried none.</param>
        /// <returns></returns>
        static public int TryParseBody(byte[] body, out BinaryMessageHeader header, out byte[] payload)
        {
            header = null;
            payload = null;

            if (body == null || body.Length < 4)
                return -1;

            int headerlength;
            var buf = body;
            cCustom_Serializer.Deserialize_Integer32(ref buf, 0, out headerlength);

            // The header must fit inside the body, and must be non-empty...
            if (headerlength <= 0 || headerlength > (body.Length - 4))
                return -2;

            string headerjson;
            try
            {
                headerjson = Encoding.UTF8.GetString(body, 4, headerlength);
                header = Newtonsoft.Json.JsonConvert.DeserializeObject<BinaryMessageHeader>(headerjson);
            }
            catch (Exception)
            {
                header = null;
                return -3;
            }

            if (header == null)
                return -3;

            int payloadlength = body.Length - 4 - headerlength;
            payload = new byte[payloadlength];
            if (payloadlength > 0)
                Array.Copy(body, 4 + headerlength, payload, 0, payloadlength);

            return 1;
        }
    }
}
