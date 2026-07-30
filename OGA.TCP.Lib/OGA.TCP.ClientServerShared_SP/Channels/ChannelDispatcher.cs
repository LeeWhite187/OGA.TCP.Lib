using System;
using System.Collections.Generic;

namespace OGA.TCP.Channels
{
    /// <summary>
    /// Owns channel-based message dispatch for a messaging endpoint: the adapter registry, routing,
    ///     channel-kind enforcement, delivery of both payload shapes, and the no-channel fallback hooks.
    /// One instance is composed by each endpoint class (client-side session and server-side endpoint), so routing
    ///     behavior is a single implementation consumed by both sides rather than two hand-mirrored ones.
    /// The registry is thread-safe: registration and removal may occur at runtime while traffic is being dispatched.
    /// Delivery runs on the caller's thread; callers that want task-pool delivery wrap their call sites.
    /// Dispatch return codes: 1 or the handler's positive result = handled; 0 = no handler available for a
    ///     channel-less message; -1 = unknown channel (message dropped); -2 = channel-kind mismatch (message dropped);
    ///     -10 = the handler threw.
    /// </summary>
    public class ChannelDispatcher
    {
        /// <summary>
        /// Class name used for logging.
        /// </summary>
        protected string _classname;

        /// <summary>
        /// Logger instance. May be null; dispatch logging is best-effort.
        /// </summary>
        protected NLog.ILogger Logger;

        /// <summary>
        /// Guards the adapter registry. Held only for registry operations, never during delivery,
        ///     so a slow handler cannot block registration or removal.
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// The adapter registry, keyed by channel name. Exact, case-sensitive matching.
        /// </summary>
        private readonly Dictionary<string, IChannelAdapter> _adapters;

        /// <summary>
        /// Instance id of the endpoint that owns this dispatcher, for log correlation.
        /// </summary>
        private readonly int _ownerinstanceid;

        /// <summary>
        /// Handler for json messages that carry no channel. Wired by the owning endpoint to its
        ///     consumer-facing message-received delegate. When null, channel-less json messages are logged and dropped.
        /// </summary>
        public Func<IMessagingHost, string, string, string, int> NoChannelJsonHandler { get; set; }

        /// <summary>
        /// Handler for binary messages that carry no channel. Wired by the owning endpoint to its
        ///     consumer-facing binary-received delegate. When null, channel-less binary messages are logged and dropped.
        /// </summary>
        public Func<IMessagingHost, string, byte[], string, int> NoChannelBinaryHandler { get; set; }

        /// <summary>
        /// Number of currently registered channel adapters.
        /// </summary>
        public int Count
        {
            get
            {
                lock (this._lock)
                {
                    return this._adapters.Count;
                }
            }
        }

        /// <summary>
        /// Constructor accepts the owning endpoint's identity for logging.
        /// </summary>
        /// <param name="ownerinstanceid">Instance id of the owning endpoint, for log correlation.</param>
        /// <param name="logger">Optional logger instance.</param>
        public ChannelDispatcher(int ownerinstanceid, NLog.ILogger logger = null)
        {
            this._classname = nameof(ChannelDispatcher);
            this._ownerinstanceid = ownerinstanceid;
            this.Logger = logger;

            this._adapters = new Dictionary<string, IChannelAdapter>();
        }

        /// <summary>
        /// Registers a channel adapter.
        /// Returns 1 on success, -1 for a null adapter or an already-registered channel, -2 for an empty channel name.
        /// Safe to call at runtime while traffic is flowing.
        /// </summary>
        /// <param name="adapter">The adapter to register.</param>
        /// <returns></returns>
        public int Add(IChannelAdapter adapter)
        {
            if (adapter == null)
                return -1;

            // Validate the adapter...
            if (string.IsNullOrEmpty(adapter.ChannelId))
            {
                // Invalid channel name.
                return -2;
            }

            lock (this._lock)
            {
                // Make sure the channel is empty...
                if (this._adapters.ContainsKey(adapter.ChannelId))
                {
                    // The channel is already assigned.
                    return -1;
                }

                // Add it to the adapters listing...
                this._adapters.Add(adapter.ChannelId, adapter);
            }

            return 1;
        }

        /// <summary>
        /// Checks whether a channel has a registered adapter.
        /// </summary>
        /// <param name="channel">Channel name to look up.</param>
        /// <returns></returns>
        public bool Contains(string channel)
        {
            if (string.IsNullOrEmpty(channel))
                return false;

            lock (this._lock)
            {
                return this._adapters.ContainsKey(channel);
            }
        }

        /// <summary>
        /// Attempts to retrieve the registered adapter for a channel.
        /// Exposed so owning endpoints can offer adapter queries; routing consumers use the Dispatch methods instead.
        /// </summary>
        /// <param name="channel">Channel name to look up.</param>
        /// <param name="adapter">The registered adapter, when found.</param>
        /// <returns></returns>
        public bool TryGet(string channel, out IChannelAdapter adapter)
        {
            adapter = null;

            if (string.IsNullOrEmpty(channel))
                return false;

            lock (this._lock)
            {
                return this._adapters.TryGetValue(channel, out adapter);
            }
        }

        /// <summary>
        /// Removes a channel's adapter, then closes it.
        /// Removal happens before close, so an adapter whose Close throws can never wedge the registry.
        /// Returns 1 on success, -1 when the channel has no registered adapter.
        /// </summary>
        /// <param name="channel">Channel name whose adapter is removed.</param>
        /// <returns></returns>
        public int Remove(string channel)
        {
            if (string.IsNullOrEmpty(channel))
                return -1;

            IChannelAdapter ca = null;

            lock (this._lock)
            {
                if (!this._adapters.TryGetValue(channel, out ca))
                {
                    // Not found.
                    return -1;
                }

                // Remove the adapter from our listing...
                this._adapters.Remove(channel);
            }

            // Close the adapter, outside the lock...
            try
            {
                ca?.Close();
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(Remove)} - " +
                    $"Exception occurred while closing adapter for channel ({channel}).");
            }

            return 1;
        }

        /// <summary>
        /// Cleanup method that removes and closes all channel adapters.
        /// The registry is emptied first, then each adapter is closed; a throwing Close cannot stall the cleanup
        ///     or leave the registry populated.
        /// </summary>
        public void CloseAll()
        {
            List<IChannelAdapter> closing = null;

            lock (this._lock)
            {
                closing = new List<IChannelAdapter>(this._adapters.Values);
                this._adapters.Clear();
            }

            foreach (var ca in closing)
            {
                try
                {
                    ca?.Close();
                }
                catch (Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(CloseAll)} - " +
                        $"Exception occurred while closing adapter for channel ({(ca?.ChannelId ?? "")}).");
                }
            }
        }

        /// <summary>
        /// Routes a received json message: to the channel's adapter when a channel is set, otherwise to the
        ///     no-channel json handler.
        /// Enforces channel kind: a json message arriving on a binary-kind channel is rejected with -2.
        /// See the class summary for the full return-code table.
        /// </summary>
        /// <param name="host">The endpoint that received the message.</param>
        /// <param name="channel">Channel name from the message, or empty.</param>
        /// <param name="messagetype">Lowercased message type name.</param>
        /// <param name="jsondata">The message payload json.</param>
        /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
        /// <returns></returns>
        public int DispatchJson(IMessagingHost host, string channel, string messagetype, string jsondata, string corelationid)
        {
            if (string.IsNullOrEmpty(channel))
            {
                // No channel is set.

                // Send the message to the no-channel handler...
                var h = this.NoChannelJsonHandler;
                if (h == null)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchJson)} - " +
                        $"Default message handler is not defined. Message type is: {messagetype}.");

                    return 0;
                }

                try
                {
                    return h(host, messagetype, jsondata, corelationid);
                }
                catch (Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchJson)} - " +
                        $"Exception occurred in the default message handler. Message type is: {messagetype}.");

                    return -10;
                }
            }
            // A channel is defined for the message.
            // We will attempt to route it.

            IChannelAdapter ca = null;
            lock (this._lock)
            {
                this._adapters.TryGetValue(channel, out ca);
            }

            if (ca == null)
            {
                // We don't have a subscribed handler matching the channel name.

                this.Logger?.Error(
                    $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchJson)} - " +
                    $"Received message from channel ({channel}), but no handler is defined. Message type is: {messagetype}.");

                return -1;
            }

            // Enforce the channel's declared kind...
            if (ca.Kind != eChannelKind.Json)
            {
                this.Logger?.Error(
                    $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchJson)} - " +
                    $"Json message arrived on binary-kind channel ({channel}). Message dropped. Message type is: {messagetype}.");

                ca.RecordReceiveResult(-2);
                return -2;
            }

            // Dispatch the message to the adapter...
            int res;
            try
            {
                res = ca.AcceptIncomingMessage(host, messagetype, jsondata, corelationid);
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchJson)} - " +
                    $"Exception occurred during message dispatch to channel ({channel}). Exception Message = {e.Message}");

                res = -10;
            }

            ca.RecordReceiveResult(res);
            return res;
        }

        /// <summary>
        /// Routes a received binary message payload: to the channel's adapter when a channel is set, otherwise
        ///     to the no-channel binary handler.
        /// Enforces channel kind: a binary payload arriving on a json-kind channel is rejected with -2.
        /// Binary traffic exists from LibVersion 3 onward; this path is dormant on earlier-version connections.
        /// See the class summary for the full return-code table.
        /// </summary>
        /// <param name="host">The endpoint that received the message.</param>
        /// <param name="channel">Channel name from the routing header, or empty.</param>
        /// <param name="messagetype">Lowercased message type name.</param>
        /// <param name="payload">The raw payload bytes.</param>
        /// <param name="corelationid">Correlation id carried with the message, or empty.</param>
        /// <returns></returns>
        public int DispatchBinary(IMessagingHost host, string channel, string messagetype, byte[] payload, string corelationid)
        {
            if (string.IsNullOrEmpty(channel))
            {
                // No channel is set.

                // Send the payload to the no-channel binary handler...
                var h = this.NoChannelBinaryHandler;
                if (h == null)
                {
                    this.Logger?.Error(
                        $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchBinary)} - " +
                        $"Default binary handler is not defined. Message type is: {messagetype}.");

                    return 0;
                }

                try
                {
                    return h(host, messagetype, payload, corelationid);
                }
                catch (Exception e)
                {
                    this.Logger?.Error(e,
                        $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchBinary)} - " +
                        $"Exception occurred in the default binary handler. Message type is: {messagetype}.");

                    return -10;
                }
            }
            // A channel is defined for the message.
            // We will attempt to route it.

            IChannelAdapter ca = null;
            lock (this._lock)
            {
                this._adapters.TryGetValue(channel, out ca);
            }

            if (ca == null)
            {
                // We don't have a subscribed handler matching the channel name.

                this.Logger?.Error(
                    $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchBinary)} - " +
                    $"Received binary message from channel ({channel}), but no handler is defined. Message type is: {messagetype}.");

                return -1;
            }

            // Enforce the channel's declared kind...
            if (ca.Kind != eChannelKind.Binary)
            {
                this.Logger?.Error(
                    $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchBinary)} - " +
                    $"Binary message arrived on json-kind channel ({channel}). Message dropped. Message type is: {messagetype}.");

                ca.RecordReceiveResult(-2);
                return -2;
            }

            // Dispatch the payload to the adapter...
            int res;
            try
            {
                res = ca.AcceptIncomingMessage(host, messagetype, payload, corelationid);
            }
            catch (Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this._ownerinstanceid.ToString()}::{nameof(DispatchBinary)} - " +
                    $"Exception occurred during binary dispatch to channel ({channel}). Exception Message = {e.Message}");

                res = -10;
            }

            ca.RecordReceiveResult(res);
            return res;
        }
    }
}
