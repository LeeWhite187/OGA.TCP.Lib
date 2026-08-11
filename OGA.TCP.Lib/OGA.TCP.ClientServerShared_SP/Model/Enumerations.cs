using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Server.Model
{
    /// <summary>
    /// Authentication level a connection registered with, recorded in the server-side ClientInfo.
    /// </summary>
    public enum eAuthLevel
    {
        /// <summary>
        /// The connection registered without credentials.
        /// </summary>
        NoAuth,
        /// <summary>
        /// The connection registered with client-application credentials, but no user context.
        /// </summary>
        ClientAuth,
        /// <summary>
        /// The connection registered with an authenticated user context.
        /// </summary>
        UserAuth
    }
}
