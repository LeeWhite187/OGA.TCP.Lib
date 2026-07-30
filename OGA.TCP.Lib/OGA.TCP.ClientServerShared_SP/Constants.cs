using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP
{
    /// <summary>
    /// Compiled defaults for the library's size limits.
    /// These are the seed values for the per-instance limit properties on clients and endpoints;
    ///     runtime tuning belongs on those instance properties, not here.
    /// </summary>
    static public class Constants
    {
        /// <summary>
        /// Default cap on a single frame's body size (1 MiB).
        /// Prevents allocation attacks: each frame is prefixed with a length header, so an attacker could
        ///     declare a fake 2GB frame and exhaust server memory; the cap bounds what a receiver will accept.
        /// Once chunking is in play, this is the interleave quantum of the pipe — the longest one frame can
        ///     hold the connection before another channel's frame gets a turn — not the message-size ceiling.
        /// Seeds the per-instance MaxFrameSize property.
        /// </summary>
        public const int CONST_MAX_MessageSize = 1024 * 1024;

        /// <summary>
        /// Default cap on a chunked message's total reassembled size (64 MiB).
        /// This is the true message-size ceiling once chunking is available, and the receiver's memory defense:
        ///     a transfer declaring more than this is rejected at its start, and accumulation beyond the
        ///     declaration is a protocol error.
        /// Seeds the per-instance MaxTransferSize property.
        /// </summary>
        public const long CONST_MAX_TransferSize = 64L * 1024L * 1024L;
    }
}
