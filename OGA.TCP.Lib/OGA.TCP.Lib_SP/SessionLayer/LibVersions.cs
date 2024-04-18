using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.SessionLayer
{
    /// <summary>
    /// Common list of all known TCP/WSLibVersion values.
    /// </summary>
    static public class LibVersions
    {
        /// <summary>
        /// This is the default TCP/WSLibVersion behavior for the client.
        /// It defaults to the latest version, '2'.
        /// For compatibility testing of older client behavior (for app versions v1.6.3 and earlier), set the WSLibVersion property in the constructor of the derived class.
        /// </summary>
        static public string DEFAULT_CONST_WSLIBVERSION = CONST_WSLibVersion_2;

        /// <summary>
        /// Baseline websocket behavior. Used in deployments up through 04/2023.
        /// See: https://oga.atlassian.net/wiki/spaces/~311198967/pages/118620161/WSHost+Versioning
        /// </summary>
        static public string CONST_WSLibVersion_1 = "1";

        /// <summary>
        /// Updated websocket behavior for clients, deployed after 04/2023.
        /// Adds client application properties to the connection registration.
        /// Can connect without a user's auth token.
        /// To use this behavior in a WSClient, you will need to override the SendConnectionRegistration method 
        /// See: https://oga.atlassian.net/wiki/spaces/~311198967/pages/118620161/WSHost+Versioning
        /// </summary>
        static public string CONST_WSLibVersion_2 = "2";
    }
}
