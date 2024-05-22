using OGA.TCP.Server.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OGA.TCP.Server.Services
{
    public abstract class ConnectionMgr_Abstract
    //public abstract class ConnectionMgr_Abstract<TEndpoint> where TEndpoint : Endpoint_Abstract
    {
        #region Private Fields

        /// <summary>
        /// Number of instances of this tyme that have beem created.
        /// </summary>
        static protected int _instance_counter;

        /// <summary>
        /// Name of the instance class type.
        /// Used for logging.
        /// </summary>
        static protected string _classname;

        /// <summary>
        /// Local list of registered connections, keyed by their connectionid.
        /// </summary>
        private ConcurrentDictionary<string, Endpoint_Abstract> _connections;

        /// <summary>
        /// Amount of time (in seconds) that an unregistered connection is allowed to live before being cleaned up.
        /// This means that a client must register the connection before it gets this old.
        /// </summary>
        protected int _unregisteredConnectionTTL;

        /// <summary>
        /// When set, new connections are allowed.
        /// Use the method calls to toggle this flag.
        /// </summary>
        protected bool _allowNewConnections;

        #endregion


        #region Public Properties

        /// <summary>
        /// Provides a static public reference, so real time comms can occur without the overhead, indirection, and delay of DI.
        /// </summary>
        static public ConnectionMgr_Abstract Instance { get; set; }

        /// <summary>
        /// Create a unique Connection Host name that we will go by.
        /// This will be used to route message traffic to us, for our client connections.
        /// </summary>
        public string ConnHost_Name { get; set; }

        /// <summary>
        /// Set this value so client connection entries are decorated with the correct address.
        /// Conn Manager types with integrated listeners will use this as the listening address.
        /// </summary>
        public string ListeningAddress { get; set; }

        /// <summary>
        /// Set this value so client connection entries are decorated with the correct port.
        /// Conn Manager types with integrated listeners will use this as the listening port.
        /// </summary>
        public int ListeningPort { get; set; }

        /// <summary>
        /// Set this flag for normal operation.
        /// Clear it if we are breakpointing other things, and the stoppage may trigger connection closure.
        /// </summary>
        public bool We_Require_Clients_to_Be_Chatty { get; set; }


        /// <summary>
        /// Number of active, but unregisterd connections on the WSHost.
        /// </summary>
        public int UnRegisteredConnectionCount
        {
            get => this._connections.Where(m=>!m.Value.ClientInfo.IsRegistered).Count();
        }

        /// <summary>
        /// Number of registered connections on the WSHost.
        /// </summary>
        public int ConnectionCount
        {
            get => this._connections.Where(m=>m.Value.ClientInfo.IsRegistered).Count();
        }

        #endregion


        #region ctor / dtor

        /// <summary>
        /// Public constructor
        /// </summary>
        public ConnectionMgr_Abstract() : base()
        {
            _instance_counter++;

            ListeningAddress = "";

            _connections = new ConcurrentDictionary<string, Endpoint_Abstract>();

            We_Require_Clients_to_Be_Chatty = true;

            _allowNewConnections = true;

            _unregisteredConnectionTTL = 60;

            ConnHost_Name = "";
        }

        #endregion


        #region Management Methods

        /// <summary>
        /// Public method that will close down the connection manager, and any connections and listeners it holds.
        /// </summary>
        public void CloseDown()
        {
            // Wrap in a try-catch to ensure any override doesn't throw and unwind us..
            try
            {
                CloseListeners();
            } catch(Exception ex) { }

            DisableNewConnections();

            Close_All_Connections();

            Purge_OldUnregisteredConnections();

            Purge_LostConnections();
        }

        /// <summary>
        /// Override this method with startup activities for your type.
        /// </summary>
        /// <returns></returns>
        public virtual int Startup()
        {
            // Validate any properties and config...
            try
            {
                var reschecks = DoStartupChecks();
                if(reschecks != 1)
                {
                    // Failed to pass startup checks.
                    // We will consider this fatal, and leave.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Startup)} - " +
                        $"Failed to pass startup checks. Cannot start connection manager.");

                    return -1;
                }
            }
            catch(Exception ex)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ex,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Startup)} - " +
                    $"Exception occurred while doing startup checks. Cannot start connection manager.");

                return -1;
            }

            // Startup listeners, if we control them...
            try
            {
                var resstart = this.StartListener();
                if(resstart != 1)
                {
                    // Failed to start listener.
                    // We will consider this fatal, and leave.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Startup)} - " +
                        $"Failed to startup listener. Cannot start connection manager.");

                    return -2;
                }
            }
            catch(Exception ex)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ex,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Startup)} - " +
                    $"Exception occurred while attempting to startup listener. Cannot start connection manager.");

                return -2;
            }

            return 1;
        }

        /// <summary>
        /// Override this method with logic to validate config and resource availability needed to run.
        /// This gets called by Startup().
        /// </summary>
        /// <returns></returns>
        protected virtual int DoStartupChecks()
        {
            return 1;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// This method accepts new connections from an external listener, such as a websocket's Http request handler.
        /// For websockets, call this method from an HTTP request handler, to introduce a newly connected websocket.
        /// For TCPsockets, call this method from the tcp listener's new connection handler.
        /// It's likely not necessary. But, you can override this method to include any additional message handlers that will process incoming messages.
        /// NOTE: Be sure to call the base.AddConnection() method before adding additional handlers.
        /// </summary>
        /// <param name="newconn"></param>
        /// <returns></returns>
        public virtual int AddConnection(Endpoint_Abstract newconn)
        {
            try
            {
                if (!_allowNewConnections)
                {
                    // No new connections can be added.
                    return -10;
                }

                if (newconn == null)
                    return 0;

                // Add the connection inside a lock...
                lock (_connections)
                {
                    var res = _connections.TryAdd(newconn.WSId, newconn);
                    if (!res)
                    {
                        // Failed to add the new connection to the list.

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{_instance_counter.ToString()}::{nameof(AddConnection)} - " +
                            $"Failed to add connection to the list.");

                        return -1;
                    }
                }

                // Hookup any delegates so we can monitor the connection...
                newconn.OnConnectionClosed = HandleConnectionClosed;
                newconn.OnConnectionRegistration = HandleConnectionRegistration;

                // Hookup message dispatch delegates that we know of...
                newconn.OnMessageReceived = Handle_ReceivedNoChannelMessage_fromClient;

                newconn.Cfg_We_Require_Clients_to_Be_Chatty = this.We_Require_Clients_to_Be_Chatty;

                return 1;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(AddConnection)} - " +
                    $"Exception occurred while attempting to add connection.");

                return -2;
            }
        }

        /// <summary>
        /// Call this method to prevent acceptance of new connections.
        /// </summary>
        public void DisableNewConnections()
        {
            // Clear the allow new connections flag, so no new connections can be added...
            _allowNewConnections = false;
        }
        /// <summary>
        /// Call this method to accept new connections.
        /// </summary>
        public void AllowNewConnections()
        {
            // Set the allow new connections flag, so no new connections can be added...
            _allowNewConnections = true;
        }

        public void Purge_OldUnregisteredConnections()
        {
            try
            {
                List<KeyValuePair<string, Endpoint_Abstract>> cl = new List<KeyValuePair<string, Endpoint_Abstract>>();

                lock (_connections)
                {
                    // Get a list of overripe unregistered connections...
                    cl = _connections.Where(m=>!m.Value.ClientInfo.IsRegistered &&
                                    (m.Value.ClientInfo.UnRegisteredAge.TotalSeconds > _unregisteredConnectionTTL)).ToList();

                    if(cl == null)
                        // No open connections.
                        return;
                    if(cl.Count == 0)
                        // No open connections.
                        return;

                    // We have the list to close.
                    // Before we close down the connections, we need to remove them from the connections list.
                    // We do this, so we can quickly remove the entries while in the lock.
                    // And, we can asynchronously close the connections outside the lock.
                    foreach (var c in cl)
                        this._connections.Remove(c.Key, out _);
                }
                // We are outside the lock, with the list of connections to close.

                // Close down each old connection...
                // Do it on a separate thread.
                Task.Run(() =>
                {
                    try
                    {
                        foreach(var c in cl)
                        {
                            try
                            {
                                c.Value.Dispose();
                            }
                            catch(Exception ex) { }
                        }
                    }
                    catch(Exception ex) { }
                });
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Purge_OldUnregisteredConnections)} - " +
                    $"Exception occurred while purging old connections.");
            }
        }

        public void Purge_LostConnections()
        {
            try
            {
                List<KeyValuePair<string, Endpoint_Abstract>> cl = new List<KeyValuePair<string, Endpoint_Abstract>>();

                lock (_connections)
                {
                    // Get a list of lost connections...
                    cl = _connections.Where(m => m.Value.IsConnected == false).ToList();

                    if(cl == null)
                        // No open connections.
                        return;
                    if(cl.Count == 0)
                        // No open connections.
                        return;

                    // We have the list to close.
                    // Before we close down the connections, we need to remove them from the connections list.
                    // We do this, so we can quickly remove the entries while in the lock.
                    // And, we can asynchronously close the connections outside the lock.
                    foreach (var c in cl)
                        this._connections.Remove(c.Key, out _);
                }
                // We are outside the lock, with the list of connections to close.

                // Close down each lost connection...
                // Do it on a separate thread.
                Task.Run(() =>
                {
                    try
                    {
                        foreach(var c in cl)
                        {
                            try
                            {
                                c.Value.Dispose();
                            }
                            catch(Exception ex) { }
                        }
                    }
                    catch(Exception ex) { }
                });
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Purge_LostConnections)} - " +
                    $"Exception occurred while purging lost connections.");
            }
        }

        /// <summary>
        /// Call this method to close down any current connections.
        /// </summary>
        public void Close_All_Connections()
        {
            try
            {
                List<KeyValuePair<string, Endpoint_Abstract>> cl = new List<KeyValuePair<string, Endpoint_Abstract>>();

                lock (_connections)
                {
                    // Get a list of all connections...
                    cl = _connections.ToList();

                    if(cl == null)
                        // No open connections.
                        return;
                    if(cl.Count == 0)
                        // No open connections.
                        return;

                    // We have the list to close.
                    // Before we close down the connections, we need to remove them from the connections list.
                    // We do this, so we can quickly remove the entries while in the lock.
                    // And, we can asynchronously close the connections outside the lock.
                    foreach (var c in cl)
                        this._connections.Remove(c.Key, out _);
                }
                // We are outside the lock, with the list of connections to close.

                // Close down each connection...
                // Do it on a separate thread.
                Task.Run(() =>
                {
                    try
                    {
                        foreach(var c in cl)
                        {
                            try
                            {
                                c.Value.Dispose();
                            }
                            catch(Exception ex) { }
                        }
                    }
                    catch(Exception ex) { }
                });
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Close_All_Connections)} - " +
                    $"Exception occurred while closing all connections.");
            }
        }

        #endregion


        #region Connection Query Methods

        /// <summary>
        /// Retrieves the current list of managed connections.
        /// This is used by the central connection listing service, to get bulk data.
        /// </summary>
        /// <returns></returns>
        public List<ConnectionEntry_v1> QRYGet_CurrentConnections()
        {
            try
            {
                List<KeyValuePair<string, Endpoint_Abstract>> cl;
                lock (_connections)
                {
                    try
                    {
                        // Get a list of connected endpoints...
                        cl = _connections.Where(m => m.Value.IsConnected).ToList();
                        if (cl == null)
                            return new List<ConnectionEntry_v1>();
                    }
                    catch (Exception)
                    {
                        return new List<ConnectionEntry_v1>();
                    }
                }
                // If here, we have at least one active connection.

                // Create a conn entry for each one...
                var cel = new List<ConnectionEntry_v1>();
                foreach(var c in cl)
                {
                    if (c.Value == null)
                        continue;

                    // Create an entry for the current connection...
                    var ce = new ConnectionEntry_v1();
                    c.Value.Populate_ConnectionEntry(ce);

                    // Add our TCP/WS Host name and port to the connection entry, so the client Mapping Service knows what host to send messages to.
                    ce.Hostname = this.ConnHost_Name;
                    ce.Host_Port = this.ListeningPort;

                    // Add it to the running list...
                    cel.Add(ce);
                }

                return cel;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(QRYGet_CurrentConnections)} - " +
                    $"Exception occurred while getting current connections.");

                return new List<ConnectionEntry_v1>();
            }
        }

        /// <summary>
        /// Called to get all connections for a user, so messages can be sent to each connection the user has.
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public List<Endpoint_Abstract> QRYGetConnections_ByUserId(Guid userid)
        {
            // Since connections can have no user logged in, they will have an empty Guid.
            // So, we need to prevent returning these if the caller gives us an empty Guid.
            if (userid == Guid.Empty)
                // Return an empty list...
                return new List<Endpoint_Abstract>();

            // The userid is set.
            // We will look for matches.
            List<Endpoint_Abstract> cl = new List<Endpoint_Abstract>();
            lock (_connections)
            {
                try
                {
                    // Get a list of connections for the given userid...
                    // Since a user can be associated with more than one device, there can be more than one active connection for a user.
                    // So, we will return all found for a user, and not just the most recent... like we would for a deviceId.
                    cl = _connections.Values.Where(m => m.ClientInfo.UserId == userid).ToList();
                    return cl;
                }
                catch (Exception e)
                {
                    return new List<Endpoint_Abstract>();
                }
            }
        }

        /// <summary>
        /// Called to get the connection that belongs to a particular deviceId.
        /// This method will fork a task that will close down any older entries for the same deviceId.
        /// </summary>
        /// <param name="deviceid"></param>
        /// <returns></returns>
        public Endpoint_Abstract? QRYGetConnection_ByClientDeviceId(string deviceid)
        {
            Endpoint_Abstract valtoreturn;

            // Since connections can have an empty deviceid before registration.
            // So, we need to prevent returning these if the caller gives us a blank deviceid.
            if (string.IsNullOrEmpty(deviceid))
                return null;

            // The device is set.
            List<KeyValuePair<string, Endpoint_Abstract>> connlist;
            try
            {
                lock (_connections)
                {
                    try
                    {
                        // Get all entries for the given DeviceId, in descending connection time...
                        connlist = _connections
                                    .Where(m => !string.IsNullOrEmpty(m.Value.ClientInfo.DeviceId) &&
                                           m.Value.ClientInfo.DeviceId == deviceid)
                                    .OrderByDescending(n=>n.Value.ClientInfo.ConnectionTimeUTC)
                                    .ToList();

                        if (connlist == null)
                            return null;
                        if (connlist.Count == 0)
                            return null;
                    }
                    catch (Exception e)
                    {
                        connlist = new List<KeyValuePair<string, Endpoint_Abstract>>();
                    }
                    // While still in the lock, we need to split out the entry to keep and any to close.

                    if (connlist == null)
                        return null;
                    if (connlist.Count == 0)
                        return null;

                    // Save off the first entry, to give back to the caller...
                    valtoreturn = connlist[0].Value;

                    // Remove it from the found set...
                    connlist.RemoveAt(0);
                }
                // Now that we are out of the lock, we can close down the older connections.

                // Return the connection we found, if no older ones exist...
                if (connlist == null)
                    return valtoreturn;
                if (connlist.Count == 0)
                    return valtoreturn;


                // At least one older connection exists for the device.
                // Close the older, remaining entries on an async thread...
                Task.Run(() =>
                {
                    try
                    {
                        foreach(var c in connlist)
                        {
                            try
                            {
                                c.Value.Dispose();
                            }
                            catch(Exception ex) { }
                        }
                    }
                    catch(Exception ex) { }
                });

                // Return the most recent entry...
                return valtoreturn;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(QRYGetConnection_ByClientDeviceId)} - " +
                    $"Exception occurred while attempting to get connection list for deviceid ({deviceid}).");

                return null;
            }
        }

        public Endpoint_Abstract? QRYGetConnection_ByConnId(string connid)
        {
            Endpoint_Abstract? ws;

            try
            {
                if (string.IsNullOrEmpty(connid))
                {
                    // Invalid connectionid.
                    return null;
                }

                // Look in the connections list for the given connection id...
                lock (_connections)
                {
                    // Attempt to get the connection with the given id...
                    if (!_connections.TryGetValue(connid, out ws))
                    {
                        // Could not find the connection.

                        return null;
                    }
                    // If here, we have a reference.

                    // See if it's a valid instance...
                    if (ws == null)
                    {
                        // Not a valid instance.

                        // Remove it from the list, because the entry is null...
                        _connections.TryRemove(connid, out ws);

                        // Not found.
                        return null;
                    }
                    // If here, we have a valid reference.

                    return ws;
                }
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(QRYGetConnection_ByConnId)} - " +
                    $"Exception occurred while attempting to lookup connection by connectionId ({connid}).");

                return null;
            }
        }

        #endregion


        #region Listener Management

        /// <summary>
        /// This method is called when the connection manager is being shut down.
        /// Override this method with any logic that will close down and dereference any listeners this connection manager depends on or contains.
        /// </summary>
        protected virtual void CloseListeners()
        {
            return;
        }


        /// <summary>
        /// This method is called on connection manager startup, to enable any internal or external listeners.
        /// Override this method with any logic that will setup and start any listeners this connection manager depends on or contains.
        /// </summary>
        protected virtual int StartListener()
        {
            return 1;
        }

        #endregion


        #region Local Event Handlers

        /// <summary>
        /// Handles Connection Registration events, including registration changes during an open connection.
        /// </summary>
        /// <param name="ws"></param>
        /// <param name="oldvals"></param>
        /// <param name="newvals"></param>
        protected void HandleConnectionRegistration(Endpoint_Abstract ws, ClientInfo oldvals, ClientInfo newvals)
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(HandleConnectionRegistration)} - " +
                $"Connection registration occurred for Connectionid = {ws.WSId}.");

            // A registration can occur multiple times over the course of an active websocket connection.
            // This is because a user login event can occur during an active websocket connection.
            // And, such an event could change the userid.
            // So, we will allow for this to happen at any time, and it update our data for the connection.

            // NOTE: The WSEndpoint has already validated that the ConnectionId and DeviceId are defined and unchanged.
            // So, we only need to deal with this being a new connection registration, or a UserId change event.
            // As well, the WSEndpoint has already assigned the ConnectionId, DeviceId, and UserId values before sending us the event.
            // So, we can continue without updating the WSEndpoint with them.

            try
            {
                // Attempt to add the connection to the connection list...
                lock (_connections)
                    _connections.TryAdd(ws.WSId, ws);
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(HandleConnectionRegistration)} - " +
                    $"Exception occurred while adding connection to connection list, for Connectionid = {ws.WSId}.");

                return;
            }

            // Update the central connections listing that a client connection was created or updated...
            try
            {
                // Create a connection entry that we will forward to the client Mapping Service...
                var ce = new ConnectionEntry_v1();
                ws.Populate_ConnectionEntry(ce);

                // Add our TCP/WS Host name and port to the connection entry, so the client Mapping Service knows what host to send messages to.
                ce.Hostname = this.ConnHost_Name;
                ce.Host_Port = this.ListeningPort;

                // Pass along the registration data, and what the old values were (as context)...
                Send_NewClientConnection_to_ClientMappingService(ce, oldvals);
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(HandleConnectionRegistration)} - " +
                    $"Exception occurred while notifying the Client Mapping Service that we promoted a connection to registered state for Connectionid = {ws.WSId}.");

                return;
            }

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(HandleConnectionRegistration)} - " +
                $"Connection registration was sent to the Client Mapping Service for Connectionid = {ws.WSId}.");
        }

        /// <summary>
        /// This handler accepts messages that were not assigned to a channel.
        /// We don't currently do anything with these.
        /// But, if they are desired, you can override this method in a derived class, to receive them.
        /// </summary>
        /// <param name="ws"></param>
        /// <param name="messagetype"></param>
        /// <param name="jsonmsg"></param>
        /// <param name="corelationid"></param>
        /// <returns></returns>
        protected virtual int Handle_ReceivedNoChannelMessage_fromClient(Endpoint_Abstract ws, string messagetype, string jsonmsg, string corelationid)
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(Handle_ReceivedNoChannelMessage_fromClient)} - " +
                $"Received message: ({jsonmsg})");

            // MOST OF WHAT IS IN THIS METHOD IS PLACEHOLDER LOGIC.
            // EACH DEFINED MESSAGE CHANNEL NEEDS ITS OWN HANDLER, LIKE THE ONE FOR IN BAND CHAT MESSAGES.
            // AND, THIS HANDLER NEEDS TO PROCESS MESSAGES THAT DON"T HAVE A CHANNEL.
            // FOR NOW, THERE ARE NO MESSAGE TYPES THAT ARE DEFINED WITHOUT A CHANNEL.
            // SO, THIS METHOD DOESN"T REALLY DO MUCH, AND IS MOSTLY PLACEHOLDER.

            // Determine if the message is:
            //  Out-of-band chat message (these do NOT show up in the chat stream: user status changes, IsTyping events, etc...
            //  Data Requests for things like: contacts, chat users, chat messages, session data, etc...
            //  Runtime Security events like: logout commands (to force a device log out), privilege changes, auth token updates
            //  App level events like: 

            bool MESSAGETYPENOTDEFINED = false;
            if(MESSAGETYPENOTDEFINED)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Handle_ReceivedNoChannelMessage_fromClient)} - " +
                    $"Received message without channel assignment: ({jsonmsg})");

                return -9999;
            }
            //// Handle in-band chat message types...
            //if (messagetype.ToLower() == nameof(MessageDTO).ToLower())
            //{
            //    MessageDTO dto = null;

            //    // Deserialize the message back to an instance...
            //    try
            //    {
            //        dto = JsonConvert.DeserializeObject<MessageDTO>(jsonmsg);
            //    }
            //    catch (Exception e)
            //    {
            //        // Failed to deserialize the chat message.

            //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
            //            $"{nameof(ConnectionMgr)}:{_instance_counter.ToString()}::{nameof(Handle_ReceivedMessage_fromClient)} - " +
            //            $"Failed to deserialize received message into a chat message ({nameof(MessageDTO)}).");

            //        return -1;
            //    }
            //    // The message is deserialized for usage.

            //    // Send the message to the in-band chat message queue...
            //    var res = _queueclient.Forward_InBand_ChatMessage_fromClient(dto);

            //    return res;
            //}
            //// Handle out-of-band chat message types...
            //else if (messagetype == nameof(Bliss.Shared.Chat.DTO.UserTypingDTO) ||
            //         messagetype == nameof(Bliss.Shared.Chat.DTO.UserStateDTO) ||
            //         messagetype == nameof(Bliss.Shared.Chat.DTO.FriendRequestDTO))
            //{
            //    // Process the raw string data of the message so we can send it to the outgoing queue...


            //    // Send the message to the out-of-band chat message queue...
            //    var qc = new QueueClient();
            //    var res = qc.PushMessage_to_OutOfBandChatMessageQueue_Async(msg).GetAwaiter().GetResult();

            //    return res;
            //}
            //else if (messagetype == nameof(Bliss.Shared.Queues.DTO.QueueItemDTO))
            //{
            //    // Process the raw string data of the message so we can send it to the outgoing queue...


            //    // Send the message to the request/reply message queue...
            //    var qc = new QueueClient();
            //    var res = qc.PushMessage_to_DataRequestMessageQueue_Async(qmc).GetAwaiter().GetResult();

            //    return res;
            //}
            else
            {
                // Unknown or unhandled message type.
                // We will log an error and return.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Handle_ReceivedNoChannelMessage_fromClient)} - " +
                    $"Received a message from the websocket client of an unknown or unhandled type ({messagetype}).");

                // Return that the message was unhandled...
                return 0;
            }
        }

        protected void HandleConnectionClosed(Endpoint_Abstract ws)
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(HandleConnectionClosed)} - " +
                $"Connection Closed");

            // Remove the connection from the listing, so no more messages can be sent to it...
            RemoveConnection(ws.WSId);

            // See if the connection has a connection Id...
            if (!ws.ClientInfo.IsRegistered)
            {
                // The client connection is not registered yet.
                // So, we don't need to tell the User Mapping Service about its closure.

                return;
            }
            // The client connection is registered.

            string connid = ws.WSId;

            // Update the Client Mapping Service that the connection was closed...
            try
            {
                Send_ConnectionClosed_to_ClientMappingService(connid);
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(HandleConnectionClosed)} - " +
                    $"Exception occurred while notifying the Client Mapping Service that a connection closed occurred for Connectionid = {connid}.");

                return;
            }

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(HandleConnectionClosed)} - " +
                $"Connection closed was sent to the User Mapping Service for Connectionid = {connid}.");
        }

        #endregion


        #region Method Overrides

        /// <summary>
        /// This method must be overridden to send new client connection messages to a service like the client mapping service.
        /// It accepts the new registration data, as well as, the previous values.
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="oldvals"></param>
        protected virtual void Send_NewClientConnection_to_ClientMappingService(ConnectionEntry_v1 dto, ClientInfo oldvals)
        {
            int x = 0;
        }

        /// <summary>
        /// This method must be overridden to send connection closed messages to a service like the client mapping service.
        /// </summary>
        /// <param name="connid"></param>
        protected virtual void Send_ConnectionClosed_to_ClientMappingService(string connid)
        {
            int x = 0;
        }

        #endregion


        #region Private Methods

        protected int RemoveConnection(string connectionid)
        {
            try
            {
                // We were given a connection Id, so we can only remove websockets from the connected list.
                lock (_connections)
                {
                    // Remove the connection from the list...
                    if (_connections.TryRemove(connectionid, out _))
                        return 1;
                    else
                        return 0;
                }
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(RemoveConnection)} - " +
                    $"Exception occurred while attempting to remove connection ({connectionid}).");

                return -1;
            }
        }

        #endregion
    }
}
