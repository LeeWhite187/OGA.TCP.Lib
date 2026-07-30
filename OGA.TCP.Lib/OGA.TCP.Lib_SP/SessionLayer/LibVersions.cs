using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.SessionLayer
{
    /// <summary>
    /// Common list of all known TCP/WSLibVersion values.
    /// NOTE: The version constants are declared BEFORE the default that references them.
    /// Static field initializers run in declaration order, so a default declared first would capture null.
    /// </summary>
    static public class LibVersions
    {
        /// <summary>
        /// Baseline websocket behavior. Used in deployments up through 04/2023.
        /// See: https://oga.atlassian.net/wiki/spaces/~311198967/pages/118620161/WSHost+Versioning
        /// </summary>
        static public string CONST_LibVersion_1 = "1";

        /// <summary>
        /// Updated websocket behavior for clients, deployed after 04/2023.
        /// Adds client application properties to the connection registration.
        /// Can connect without a user's auth token.
        /// To use this behavior in a WSClient, you will need to override the SendConnectionRegistration method
        /// See: https://oga.atlassian.net/wiki/spaces/~311198967/pages/118620161/WSHost+Versioning
        /// </summary>
        static public string CONST_LibVersion_2 = "2";

        /// <summary>
        /// Typed-frame wire format, deployed with the v3 library update (07/2026).
        /// Replaces the v1/v2 raw-json framing with a five-byte preamble on every TCP frame:
        /// a four-byte little-endian body length followed by a one-byte frame type from the FrameTypes registry.
        /// This allows json and binary message bodies to share the same connection without base64 encoding.
        /// Also adds chunked (large-message) transfer of both json and binary payloads.
        /// v3 endpoints cannot exchange traffic with v1/v2 peers; both ends of a connection must be updated together.
        /// See the design spec (docs/OGA.TCP.Lib_SPEC.md), items KD-03, KD-04, KD-05, and KD-07, for the wire format details.
        /// </summary>
        static public string CONST_LibVersion_3 = "3";

        /// <summary>
        /// This is the default TCP/WSLibVersion behavior for the client.
        /// It defaults to the latest version, '3'.
        /// For compatibility testing of older client behavior (for app versions v1.6.3 and earlier), set the WSLibVersion property in the constructor of the derived class.
        /// </summary>
        static public string DEFAULT_CONST_WSLIBVERSION = CONST_LibVersion_3;
    }
}
