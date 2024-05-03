using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Server.Model
{
    /// <summary>
    /// Holds a record of the host and connection.
    /// Tracks the associated connectionid, and the host and port of how to get to it.
    /// It does hold the same properties, for now.
    /// But, it is for exclusive use in the cache table,
    ///     and cannot be updated through simple library updates, without additionalevaluation.
    /// </summary>
    public class ConnectionEntry_v1
    {
        /// <summary>
        /// Client-generated Connection Id, uniquely describes the socket connection instance.
        /// Each time a client connects, a different connection Id is generated.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// UserId that is logged into the connected client.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Identifies the client device.
        /// This is used to distinguish connections for a user, in the event that a particular message type or action needs to affect only one device of a user.
        /// This can be the case, when a user has logged in through multiple devices,
        /// and there may be connection limits on the user's account, based on his privileges.
        /// In which case, this property allows us to determine which device needs to be logged out, when a user logs into a new device.
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// Client-provided process PID of the running client application.
        /// </summary>
        public int Pid { get; set; }

        /// <summary>
        /// UTC time the tcp/websocket connection was opened.
        /// </summary>
        public DateTime ConnectionTimeUTC { get; set; }

        /// <summary>
        /// Id of the Host instance that holds the connection.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Port of the Host instance holding the connection.
        /// </summary>
        public int Host_Port { get; set; }

        /// <summary>
        /// Holds the version3 string of the connected client version.
        /// Of the form: x.y.z
        /// NOTE: We don't store the build number, as the version3 should be enough to discriminate DTO version usage.
        /// </summary>
        public string AppVersion { get; set; }

        /// <summary>
        /// Used to identify the application of the connected client.
        /// This isn't required yet, since we only have a mobile.
        /// But, it will later be used to distinguish web or desktop types.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// Region identifier of the connected client.
        /// Doesn't relate to end user language or culture data.
        /// Is to identify the region where the client is located or connected.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Defines the selected language of the connected client.
        /// Should follow the culture string, like these: https://oga.atlassian.net/wiki/spaces/~311198967/pages/118554625/Language+and+Culture+Support
        ///     Will be of the form: language-region
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// TCP/WSLib version of the connected client.
        /// This value indicates transport-level functionality, behaviors, and available classes the client can use.
        /// </summary>
        public string LibVersion { get; set; }

        public ConnectionEntry_v1()
        {
            ConnectionId = "";
            UserId = Guid.Empty;
            DeviceId = "";
            Pid = 0;
            ConnectionTimeUTC = DateTime.UtcNow;
            Hostname = "";
            Host_Port = 0;
            AppId = "";
            AppVersion = "";
            Region = "";
            Language = "en-us";
            LibVersion = "";
        }

        public void CopyFrom(ConnectionEntry_v1 entry)
        {
            ConnectionId = entry.ConnectionId;
            UserId = entry.UserId;
            DeviceId = entry.DeviceId;
            Pid = entry.Pid;
            ConnectionTimeUTC = entry.ConnectionTimeUTC;
            Hostname = entry.Hostname;
            Host_Port = entry.Host_Port;
            AppId = entry.AppId;
            AppVersion = entry.AppVersion;
            Region = entry.Region;
            Language = entry.Language;
            LibVersion = entry.LibVersion;
        }

        public string ToLogString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("ConnectionId = " + ConnectionId);
            stringBuilder.AppendLine("UserId = " + UserId.ToString());
            stringBuilder.AppendLine("DeviceId = " + DeviceId);
            stringBuilder.AppendLine("Pid = " + Pid.ToString());
            stringBuilder.AppendLine("ConnectionTimeUTC = " + ConnectionTimeUTC.ToString("O"));
            stringBuilder.AppendLine("Hostname = " + Hostname);
            stringBuilder.AppendLine("Host_Port = " + Host_Port.ToString());
            stringBuilder.AppendLine("AppId = " + AppId);
            stringBuilder.AppendLine("AppVersion = " + AppVersion);
            stringBuilder.AppendLine("Region = " + Region);
            stringBuilder.AppendLine("Language = " + Language);
            stringBuilder.AppendLine("LibVersion = " + LibVersion);
            return stringBuilder.ToString();
        }
    }
}
