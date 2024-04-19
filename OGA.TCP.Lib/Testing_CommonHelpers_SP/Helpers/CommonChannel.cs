using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OGA.TCP.Messages;

namespace Testing.WSEndpoint_Tests.HelperClasses
{
    /// <summary>
    /// Copied from OGA_WSHost_Base.Dev/WSEndpoint_Tests.
    /// Converted to use concurrent queue, instead of Channels. This allows for usage by earlier NET framework versions.
    /// </summary>
    static public class CommonChannel
    {
        static public ConcurrentQueue<string> Callback_Queue;

        static public ConcurrentQueue<MessageEnvelope> CallbackChannel_MessageEnvelope;
    }
}
