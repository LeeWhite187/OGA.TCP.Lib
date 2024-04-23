using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP
{
    static public class Constants
    {
        /// <summary>
        /// Maximum allowed message size for a single frame.
        /// Prevent allocation attacks. Each packet is prefixed with a length header, so an attacker could send a fake packet with length=2GB,
        /// causing the server to allocate 2GB and run out of memory quickly.
        /// -> simply increase max packet size if you want to send around bigger files!
        /// -> 1MB per message should be more than enough.
        /// </summary>
        static public int CONST_MAX_MessageSize = 1024 * 1024;
    }
}
