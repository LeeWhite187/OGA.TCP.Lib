using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP
{
    /// <summary>
    /// Counter and timestamp set for a connection: message traffic, unknown-type occurrences, and
    ///     connection-loop activity.
    /// Owners (the receive loop and the endpoints) update the live instance; the public Metrics
    ///     properties hand out snapshot copies via CopyFrom, so consumers never mutate live counters.
    /// </summary>
    public class cEndpoint_Metrics
    {
        /// <summary>
        /// Number of connection-loop passes since the connection opened.
        /// </summary>
        public volatile int Loop_Counter;

        /// <summary>
        /// Time the current connection-loop pass was entered.
        /// </summary>
        public DateTime Loop_EntryTime;

        /// <summary>
        /// Duration (in milliseconds) of the last connection-loop pass.
        /// </summary>
        public volatile int Loop_Duration;

        /// <summary>
        /// Number of received messages whose type had no registered handler.
        /// </summary>
        volatile public int Unknown_MessageType_Count;
        /// <summary>
        /// Number of received replies whose type was not recognized.
        /// </summary>
        volatile public int Unknown_ReplyType_Count;

        /// <summary>
        /// Time the last unknown-type message was received.
        /// </summary>
        public DateTime Last_Unknown_MessageType_Time;
        /// <summary>
        /// Time the last unknown-type reply was received.
        /// </summary>
        public DateTime Last_Unknown_ReplyType_Time;

        /// <summary>
        /// Number of messages received on the connection, including wire keepalive frames.
        /// </summary>
        volatile public int Received_Message_Count;
        /// <summary>
        /// Number of messages sent on the connection.
        /// </summary>
        volatile public int Sent_Message_Count;

        /// <summary>
        /// Time of the last received message.
        /// The receive loop baselines this at construction and start (connection open counts as liveness),
        ///     so keepalive credit never sees a default time.
        /// </summary>
        public DateTime Last_Received_Message_Time;
        /// <summary>
        /// Time of the last sent message.
        /// </summary>
        public DateTime Last_Sent_Message_Time;

        /// <summary>
        /// Constructor zeroes all counters and sets sentinel timestamps.
        /// </summary>
        public cEndpoint_Metrics()
        {
            Initialize();
        }

        /// <summary>
        /// Zeroes all counters and sets sentinel timestamps.
        /// </summary>
        protected void Initialize()
        {
            Loop_Counter = 0;
            Loop_Duration = 0;
            Loop_EntryTime = DateTime.Parse("01/01/2001");

            Unknown_MessageType_Count = 0;
            Unknown_ReplyType_Count = 0;

            Last_Unknown_MessageType_Time = new DateTime(1900, 1, 1);
            Last_Unknown_ReplyType_Time = new DateTime(1900, 1, 1);

            Received_Message_Count = 0;
            Sent_Message_Count = 0;

            Last_Received_Message_Time = new DateTime(1900, 1, 1);
            Last_Sent_Message_Time = new DateTime(1900, 1, 1);
        }

        /// <summary>
        /// Public reset back to the initialized baseline.
        /// </summary>
        public void Reset_Metrics()
        {
            Initialize();
        }

        /// <summary>
        /// Copies all counters and timestamps from the given instance into this one.
        /// Used to build the snapshot copies that the endpoints' Metrics properties hand out.
        /// </summary>
        /// <param name="incoming">The instance to copy values from.</param>
        public void CopyFrom(cEndpoint_Metrics incoming)
        {
            Loop_Counter = incoming.Loop_Counter;
            Loop_Duration = incoming.Loop_Duration;
            Loop_EntryTime = incoming.Loop_EntryTime;


            this.Unknown_MessageType_Count = incoming.Unknown_MessageType_Count;
            this.Unknown_ReplyType_Count = incoming.Unknown_ReplyType_Count;

            this.Last_Unknown_MessageType_Time = incoming.Last_Unknown_MessageType_Time;
            this.Last_Unknown_ReplyType_Time = incoming.Last_Unknown_ReplyType_Time;

            this.Received_Message_Count = incoming.Received_Message_Count;
            this.Sent_Message_Count = incoming.Sent_Message_Count;

            this.Last_Received_Message_Time = incoming.Last_Received_Message_Time;
            this.Last_Sent_Message_Time = incoming.Last_Sent_Message_Time;
        }
    }
}
