using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Server.Model
{
    /// <summary>
    /// NOT FOR PRODUCTION USE.
    /// THIS IS A COPY OF ClientInfo, INTENDED TO REPLICATE SERVER-SIDE FUNCTIONALITY FOR CLIENT SIDE LIBRARY TESTS.
    /// Small property of an Endpoint instance that holds its client info.
    /// </summary>
    public class TESTINGSRVR_ClientInfo
    {
        /// <summary>
        /// Local timestamp when the client socket was opened.
        /// </summary>
        public DateTime ConnectionTimeUTC { get; set; }

        /// <summary>
        /// Indicates the current authentication level of the socket: non auth, client auth, user auth, latent-user.
        /// </summary>
        public eAuthLevel AuthLevel { get; set; }

        /// <summary>
        /// This is set to the public address of the connecting client.
        /// It is assigned by the TCP/WSHost Controller on initial connection.
        /// </summary>
        public string ClientIP { get; set; }
        /// <summary>
        /// This is a unique identifier of the connection.
        /// It is provided by the client during registration.
        /// It remains constant through the life of the connection.
        /// Until the client sends a registration message, this property remains empty.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// Guid of the user on the client device.
        /// If empty, a user is not authenticated, or was logged out.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Client-provided identifier of the remote device.
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// Amount of time the tcp/websocket has existed while waiting for its client to register it
        ///     with a connection id, userid, device id, etc...
        /// This is used to determine when to purge unregistered connections that never got registered.
        /// </summary>
        public TimeSpan UnRegisteredAge
        {
            get
            {
                if (IsRegistered)
                    return TimeSpan.Zero;

                return DateTime.UtcNow.Subtract(ConnectionTimeUTC);
            }
        }

        /// <summary>
        /// Since an endpoint instance requires a connectionId from a connection registration message,
        /// If we have one, the client has registered itself.
        /// </summary>
        public bool IsRegistered
        {
            get
            {
                if (string.IsNullOrEmpty(ConnectionId))
                    return false;
                else
                    return true;
            }
        }


        /// <summary>
        /// Used to identify the application of the connected client.
        /// This isn't required yet, since we only have a mobile.
        /// But, it will later be used to distinguish web or desktop types.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// Holds the version3 string of the connected client version.
        /// Of the form: x.y.z
        /// NOTE: We don't store the build number, as the version3 should be enough to discriminate DTO version usage.
        /// </summary>
        public string AppVersion { get; set; }

        /// <summary>
        /// Defines the selected language of the connected client.
        /// Should follow the culture string, like these: https://oga.atlassian.net/wiki/spaces/~311198967/pages/118554625/Language+and+Culture+Support
        /// Will be of the form: language-region
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// TCP/WSLib version of the connected client.
        /// This value indicates transport-level functionality, bejaviors, and available classes the client can use.
        /// </summary>
        public string LibVersion { get; set; }


        public TESTINGSRVR_ClientInfo()
        {
            AuthLevel = eAuthLevel.NoAuth;
            this.UserId = Guid.Empty;
            this.DeviceId = "";
            this.ConnectionId = "";
            this.ClientIP = "";

            this.AppId = "";
            this.AppVersion = "";
            this.Language = "";
            this.LibVersion = "";
        }

        public void CopyFrom(TESTINGSRVR_ClientInfo dto)
        {
            this.UserId = dto.UserId;
            this.ConnectionId = dto.ConnectionId;
            this.DeviceId = dto.DeviceId;

            this.AuthLevel = dto.AuthLevel;
            this.ClientIP = dto.ClientIP;
            this.ConnectionTimeUTC = dto.ConnectionTimeUTC;

            this.AppId = dto.AppId;
            this.AppVersion = dto.AppVersion;
            this.Language = dto.Language;
            this.LibVersion = dto.LibVersion;
        }
    }
}

