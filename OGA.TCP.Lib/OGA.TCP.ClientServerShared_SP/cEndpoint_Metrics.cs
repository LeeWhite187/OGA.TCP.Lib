using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP
{
    public class cEndpoint_Metrics
    {
        public volatile int Loop_Counter;

        public DateTime Loop_EntryTime;

        public volatile int Loop_Duration;

        volatile public int Unknown_MessageType_Count;
        volatile public int Unknown_ReplyType_Count;

        public DateTime Last_Unknown_MessageType_Time;
        public DateTime Last_Unknown_ReplyType_Time;

        volatile public int Received_Message_Count;
        volatile public int Sent_Message_Count;

        public DateTime Last_Received_Message_Time;
        public DateTime Last_Sent_Message_Time;

        public cEndpoint_Metrics()
        {
            Initialize();
        }

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

        public void Reset_Metrics()
        {
            Initialize();
        }

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
