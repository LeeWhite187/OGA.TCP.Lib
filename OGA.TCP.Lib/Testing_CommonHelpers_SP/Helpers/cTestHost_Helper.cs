using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Testing_CommonHelpers_SP.Helpers
{
    /// <summary>
    /// FOR TESTING. NOT FOR PRODUCTION USAGE.
    /// Resolves the principal IPv4 address of the testing host, so tests that exercise a real network
    ///     interface can bind and connect without a hardcoded address.
    /// The resolution deliberately avoids the naive "first address of the host" lookup, which on developer
    ///     machines can randomly return a VPN client's tunnel address (whose interface may refuse binds or
    ///     drop local connections), historically forcing tests to hardcode a machine-specific IP.
    /// Strategy, in order:
    ///     1. Scan network interfaces for one that is up, is a real NIC (not loopback, tunnel, or ppp),
    ///        and has an IPv4 default gateway. The gateway requirement is what excludes VPN adapters:
    ///        a routable physical interface has one; tunnel adapters typically do not.
    ///     2. Ask the OS routing table directly: a UDP socket "connected" to a public address (no packet
    ///        is actually sent) exposes the local address the OS would route from.
    ///     3. Fall back to loopback, which always supports the bind-and-connect shape the tests use.
    /// The result is cached for the life of the test process, so all test classes agree on one address.
    /// </summary>
    static public class cTestHost_Helper
    {
        /// <summary>
        /// Cached resolution result, so every test class in a run uses the same address.
        /// </summary>
        static private string _resolved;

        /// <summary>
        /// Lock guarding first-use resolution, since test classes may initialize concurrently.
        /// </summary>
        static private readonly object _lock = new object();

        /// <summary>
        /// Returns the principal IPv4 address of the testing host, as a string.
        /// Falls back to 127.0.0.1 when no routable interface can be identified,
        ///     so tests remain runnable on isolated or oddly-configured hosts.
        /// </summary>
        /// <returns></returns>
        static public string GetPrimaryIPv4()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_resolved))
                    return _resolved;

                // Strategy 1: a real, up NIC with an IPv4 default gateway...
                var viaNic = Resolve_via_NicScan();
                if (!string.IsNullOrEmpty(viaNic))
                {
                    _resolved = viaNic;
                    return _resolved;
                }

                // Strategy 2: ask the routing table which local address reaches out...
                var viaRoute = Resolve_via_RouteLookup();
                if (!string.IsNullOrEmpty(viaRoute))
                {
                    _resolved = viaRoute;
                    return _resolved;
                }

                // Strategy 3: loopback always works for the bind-and-connect shape the tests use...
                _resolved = "127.0.0.1";
                return _resolved;
            }
        }

        /// <summary>
        /// Scans network interfaces for an operational, physical NIC carrying an IPv4 unicast address
        ///     and an IPv4 default gateway. The gateway requirement excludes VPN tunnel adapters.
        /// Returns null when no interface qualifies.
        /// </summary>
        /// <returns></returns>
        static private string Resolve_via_NicScan()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // Only interfaces that are actually up...
                    if (nic.OperationalStatus != OperationalStatus.Up)
                        continue;

                    // Exclude non-physical interface types (loopback, tunnels, dial-up style adapters)...
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Ppp)
                        continue;

                    var props = nic.GetIPProperties();

                    // Require an IPv4 default gateway: the marker of the host's routable, primary path.
                    // VPN adapters generally lack one, which is the point of this filter.
                    bool hasv4gateway = false;
                    foreach (var gw in props.GatewayAddresses)
                    {
                        if (gw?.Address != null &&
                            gw.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.Any.Equals(gw.Address))
                        {
                            hasv4gateway = true;
                            break;
                        }
                    }
                    if (!hasv4gateway)
                        continue;

                    // Take the interface's IPv4 unicast address...
                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua?.Address != null &&
                            ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(ua.Address))
                        {
                            return ua.Address.ToString();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Resolution is best-effort; the caller falls through to the next strategy.
            }

            return null;
        }

        /// <summary>
        /// Asks the OS routing table for the local address used to reach a public endpoint.
        /// Connecting a UDP socket transmits nothing; it only binds the socket to the outbound route's
        ///     local address, which we then read back.
        /// Returns null when the lookup fails (e.g., no route, no network).
        /// </summary>
        /// <returns></returns>
        static private string Resolve_via_RouteLookup()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    // The address only needs to be routable; nothing is sent to it.
                    socket.Connect("8.8.8.8", 53);

                    var local = socket.LocalEndPoint as IPEndPoint;
                    if (local != null && !IPAddress.IsLoopback(local.Address))
                        return local.Address.ToString();
                }
            }
            catch (Exception)
            {
                // Resolution is best-effort; the caller falls through to the next strategy.
            }

            return null;
        }
    }
}
