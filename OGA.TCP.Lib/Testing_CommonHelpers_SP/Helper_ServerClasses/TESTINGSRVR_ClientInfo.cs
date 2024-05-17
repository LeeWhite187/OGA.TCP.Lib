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
        /// Client-provided process PID of the running client application.
        /// </summary>
        public int Pid { get; set; }

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
        /// Available to discriminate different instances of a client process.
        /// When a client populates this with a Guid that changes each time the client starts,
        ///     this value will easily distinguish multiple copies of the same client instance.
        /// This value is passed as ancillary data by the client logic.
        /// </summary>
        public string RuntimeId { get; set; }

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
            this.RuntimeId = "";
            this.AppVersion = "";
            this.Language = "";
            this.LibVersion = "";
        }

        public void CopyFrom(TESTINGSRVR_ClientInfo dto)
        {
            this.UserId = dto.UserId;
            this.ConnectionId = dto.ConnectionId;
            this.DeviceId = dto.DeviceId;

            this.Pid = dto.Pid;

            this.AuthLevel = dto.AuthLevel;
            this.ClientIP = dto.ClientIP;
            this.ConnectionTimeUTC = dto.ConnectionTimeUTC;

            this.AppId = dto.AppId;
            this.RuntimeId = dto.RuntimeId;
            this.AppVersion = dto.AppVersion;
            this.Language = dto.Language;
            this.LibVersion = dto.LibVersion;
        }

        /// <summary>
        /// Creates a loggable delta of two given client info instances.
        /// </summary>
        /// <param name="ci1"></param>
        /// <param name="ci2"></param>
        /// <returns></returns>
        static public string LogDelta(TESTINGSRVR_ClientInfo ci1, TESTINGSRVR_ClientInfo ci2)
        {
            StringBuilder b = new StringBuilder();

            b.AppendLine("UserId : '" + ci1.UserId.ToString() +
                         "' => '" + ci2.UserId.ToString() + "';");
            b.AppendLine("ConnectionId = '" + (ci1.ConnectionId ?? "") +
                         "' => '" + (ci2.ConnectionId ?? "") + "';");
            b.AppendLine("DeviceId = '" + (ci1.DeviceId ?? "") +
                         "' => '" + (ci2.DeviceId ?? "") + "';");
            b.AppendLine("Pid = '" + (ci1.Pid.ToString() ?? "") +
                         "' => '" + (ci2.Pid.ToString() ?? "") + "';");

            b.AppendLine("AuthLevel = '" + ci1.AuthLevel.ToString() +
                         "' => '" + ci2.AuthLevel.ToString() + "';");
            b.AppendLine("ClientIP = '" + (ci1.ClientIP ?? "") +
                         "' => '" + (ci2.ClientIP ?? "") + "';");
            b.AppendLine("ConnectionTimeUTC = '" + ci1.ConnectionTimeUTC.ToString("O") +
                         "' => '" + ci2.ConnectionTimeUTC.ToString("O") + "';");

            b.AppendLine("AppId = '" + (ci1.AppId ?? "") +
                         "' => '" + (ci2.AppId ?? "") + "';");
            b.AppendLine("RuntimeId = '" + (ci1.RuntimeId ?? "") +
                         "' => '" + (ci2.RuntimeId ?? "") + "';");
            b.AppendLine("AppVersion = '" + (ci1.AppVersion ?? "") +
                         "' => '" + (ci2.AppVersion ?? "") + "';");
            b.AppendLine("Language = '" + (ci1.Language ?? "") +
                         "' => '" + (ci2.Language ?? "") + "';");
            b.AppendLine("LibVersion = '" + (ci1.LibVersion ?? "") +
                          "' => '" + (ci2.LibVersion ?? "") + "';");

            b.AppendLine("UnRegisteredAge = '" + ci1.UnRegisteredAge.ToString() +
                         "' => '" + ci2.UnRegisteredAge.ToString() + "';");
            b.AppendLine("IsRegistered = '" + ci1.IsRegistered.ToString() +
                         "' => '" + ci2.IsRegistered.ToString() + "';");

            return b.ToString();
        }
    }
}

