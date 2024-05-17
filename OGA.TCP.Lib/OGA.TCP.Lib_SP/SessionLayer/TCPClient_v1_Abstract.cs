using Newtonsoft.Json;
using NLog;
using OGA.TCP.Messages;
using OGA.TCP.Shared;
using OGA.TCP.Shared.Encoding;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP.SessionLayer
{
    /// <summary>
    /// Provides connectivity to a TCPHost websocket, creating an easy abstraction for message exchange.
    /// This class has been declared abstract, so usage of it is forced to provide an implementation for determining the websocket connection url.
    /// Implementations of this abstract must override, Get_ConnectionUrl(), with a method that populates the connection url.
    /// Implementations of this abstract may override: Dispose(), IsInternetAvailable(), Determine_AuthToken(), Send_RegistrationMessage(), FireMessageReceivedEvent(), DispatchConnected().
    /// </summary>
    public abstract class TCPClient_v1_Abstract : Client_v1_Abstract, IDisposable
    {
        #region Private Fields

        /// <summary>
        /// Need a local reference to the TCPClient so we can check if connected because the network stream class doesn't provide such a message.
        /// </summary>
        protected TcpClient _client;
        /// <summary>
        /// Need a local reference to the underlying stream to exchange data.
        /// </summary>
        protected System.Net.Sockets.NetworkStream _conn_networkstream;

        /// <summary>
        /// Helper class that deals with the cruft of receiving data from a tcp socket.
        /// </summary>
        protected cReceiveLoop _receiveLoop;

        /// <summary>
        /// host or IP of remote connection.
        /// </summary>
        protected string tcpconnection_host;
        /// <summary>
        /// Port of remote connection.
        /// </summary>
        protected int tcpconnection_port;

        protected cEndpoint_Metrics _metrics;

        #endregion


        #region Public Properties

        /// <summary>
        /// Determines if a receiver loop is spawned.
        /// This should be set for websockets, clear for tcpsockets.
        /// </summary>
        override public bool Cfg_TransportRequiresReceiverLoop { get; } = false;

        /// <summary>
        /// Set this to the lowercase short name of the transport: tcp, ws, etc...
        /// </summary>
        override public string TransportShortName { get; } = "tcp";

        /// <summary>
        /// Set this to the name of the transport: TCPSocket, Websocket, etc...
        /// </summary>
        override public string TransportLongName { get; } = "TCPSocket";

        /// <summary>
        /// Set this to the lowercase string-literal of the libver property that is passed during connection registration.
        /// For websocket clients, this is: "wslibver".
        /// For tcpsocket clients, this is: "tcplibver".
        /// </summary>
        override public string PropName_ClientLibVer { get; } = "tcplibver";

        /// <summary>
        /// Can be checked for a positive connection.
        /// Your implementation should verify the endpoint is NOT disposed, the underlying transport instance is not null, and indicates connected.
        /// </summary>
        override public bool IsConnected
        {
            get
            {
                try
                {
                    if(this.disposedValue)
                        return false;

                    if (this._client == null)
                        return false;

                    return this._client.Connected == true;
                }
                catch(Exception e)
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// Ignores object state and disposed boolean. Just reports the raw socket being open or not.
        /// </summary>
        override public bool TransportIsOpen
        {
            get
            {
                try
                {
                    if (this._client == null)
                        return false;

                    return this._client.Connected == true;
                }
                catch(Exception e)
                {
                    return false;
                }
            }
        }

        public cEndpoint_Metrics Metrics
        {
            get
            {
                // Get a copy of the metrics.
                cEndpoint_Metrics met = new cEndpoint_Metrics();
                met.CopyFrom(this._metrics);

                var rcvmet = this._receiveLoop.Metrics;

                met.Last_Received_Message_Time = rcvmet.Last_Received_Message_Time;
                met.Last_Unknown_MessageType_Time = rcvmet.Last_Unknown_MessageType_Time;
                met.Received_Message_Count = rcvmet.Received_Message_Count;
                met.Unknown_MessageType_Count = rcvmet.Unknown_MessageType_Count;

                return met;
            }
        }

        /// <summary>
        /// NoDelay disables nagle algorithm. lowers CPU% and latency but increases bandwidth.
        /// </summary>
        public bool Cfg_NoDelay { get; set; } = true;

        /// <summary>
        /// Send would stall forever if the network is cut off during a send, so we need a timeout (in milliseconds).
        /// This value gets applied to the actual Tcpclient instance, to serve as its timeout for sends.
        /// </summary>
        public int Cfg_SendTimeout { get; set; } = 5000;

        /// <summary>
        /// Amount of time, in milliseconds, to allow connection before failing.
        /// </summary>
        public int Cfg_ConnectTimeout { get; set; } = 5000;

        #endregion


        #region ctor / dtor

        /// <summary>
        /// Constructor requires a logger instance.
        /// </summary>
        public TCPClient_v1_Abstract(NLog.ILogger logger = null) : base(logger)
        {
            _classname = nameof(TCPClient_v1_Abstract);

            this._metrics = new cEndpoint_Metrics();
        }

        #endregion


        #region Connection Management

        /// <summary>
        /// This is a hook, in the Setup Before Connection logic flow, to provide a call point for determining any dynamic connection info, such as host, port, or url.
        /// This is especially used by websocket clients, whose connection url is determined by server load balancing and region.
        /// For a simple TCP socket client connecting to a static, target server, this method will simply return success (1).
        /// NOTE: This method is called each time the client attempts to connect.
        /// </summary>
        /// <returns></returns>
        override protected async Task<int> Get_ConnectionInfo()
        {
            // NOTE: This method is called each time the client attempts to create and connect a new transport connection.
            // This is called each time, in case your code is connecting to a service that provides dynamic connection info.
            // Such is the case for some websocket implementations, in that multiple WS host services may be available.
            //  But, you must ask a central clearinghouse for which one to connect to, and you will be given connection info based on closest service, or load balancing.

            // Include in your override method, the logic necessary to retrieve, lookup, or ask for, the transport's connection info.
            // For a websocket connection, this would be logic that gets a connection URL, like the below example call to Get_ConnectionUrl().
            // Or. If the connection URL is fixed, maybe there is nothing to do, here.
            // Tcp socket connection info works similar, it's just not a url, but a host and port instead.

            //return await this.Get_ConnectionUrl();
            // For a tcp socket, this may be a call to get the host and port of listening server.
            return 1;
        }

        /// <summary>
        /// Call hook in the Do Setup Before Connecting method, for the transport-specific implementation to create the websocket, tcpclient, etc...
        /// This call is responsible for instantiating the actual tcpclient, websocket, etc.
        /// </summary>
        /// <returns></returns>
        override protected async Task<int> TransportSpecific_CreateNewConnection()
        {
            // create a TcpClient with perfect IPv4, IPv6 and hostname resolving
            // support.
            //
            // * TcpClient(hostname, port): works but would connect (and block)
            //   already
            // * TcpClient(AddressFamily.InterNetworkV6): takes Ipv4 and IPv6
            //   addresses but only connects to IPv6 servers (e.g. Telepathy).
            //   does NOT connect to IPv4 servers (e.g. Mirror Booster), even
            //   with DualMode enabled.
            // * TcpClient(): creates IPv4 socket internally, which would force
            //   Connect() to only use IPv4 sockets.
            //
            // => the trick is to clear the internal IPv4 socket so that Connect
            //    resolves the hostname and creates either an IPv4 or an IPv6
            //    socket as needed (see TcpClient source)
            TcpClient newclient = new TcpClient(); // creates IPv4 socket
            //newclient.Client = null; // clear internal IPv4 socket until Connect()

            // We are to swap in our new client, here.
            // Copy off the old one, so we can dispose it...
            var oldclient = this._client;

            // Do a quick copy over, to minimize any transients of other threads seeing the client as inconsistent...
            this._client = newclient;

            // Dispose of the old one...
#if (NET452)
            try { oldclient?.Close(); } catch (Exception e) { }
#else
            try { oldclient?.Dispose(); } catch (Exception e) { }
#endif

            return 1;
        }

        /// <summary>
        /// Override this method with the transport-specific connect logic.
        /// Return true if successful. False, if not.
        /// </summary>
        /// <returns></returns>
        override protected async Task<bool> TransportSpecific_Connect()
        {
            bool success = false;

            if (_client == null)
                return success;

            try
            {
                // Attempt to start the connection, and handle any error that results...
                // NOTE: Here, we call the escape hatch (AsTask()) of the valuetask, so we can have a traditional Task<T> that can be continued.

                // We've simplified the connection logic, here, to make it compatible across net framework and core versions.
                // In doing so, we got rid of the ContinueWith clause.
                var timeoutTask = Task.Delay(this.Cfg_ConnectTimeout);
                var connectTask = _client.ConnectAsync(this.tcpconnection_host, this.tcpconnection_port);
                var completedTask = await Task.WhenAny(timeoutTask, connectTask);
                if (completedTask == timeoutTask)
                {
                    // Timed out while connecting.

                    var msg = $"A timeout occurred while opening the {(this.TransportLongName?.ToLower() ?? "socket")}, " +
                                $"({(this._connection_string ?? "<connectionstring not defined>")}). Returning...";

                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(TransportSpecific_Connect)} - " +
                        msg);

                    return success;
                }
                else
                {
                    // If here, the connect method finished.

                    // See how it did...
                    if (connectTask.IsFaulted)
                    {
                        // An exception occurred while attempting to connect to the websoket service.
                        // We will log and leave.

                        var msg = $"There was an error opening the {(this.TransportLongName?.ToLower() ?? "socket")}, " +
                                  $"({(this._connection_string ?? "<connectionstring not defined>")}). Returning...";

                        // Log the error with exception if possible...
#if (NET452 || NET48)
                        Exception te = null;
#else
                        Exception? te = null;
#endif
                        try
                        {
                            te = connectTask?.Exception?.GetBaseException() ?? null;
                        }
                        catch (Exception) { }
                        if (te != null)
                            this.Logger?.Error(te,
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(TransportSpecific_Connect)} - " +
                                msg);
                        else
                            this.Logger?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(TransportSpecific_Connect)} - " +
                                msg);
                    }
                    else
                    {
                        // The task did not fault.
                        // So, it may be connected.

                        // Verify that we have a connected state...
                        if (this._client == null)
                        {
                            // The websocket is null.
                            // We will fall out, below, as not successful.

                            int x = 0;
                        }
                        else if (!this._client.Connected)
                        {
                            // client does not report connected.

                            var msg = $"The connection attempt failed for ({(this._connection_string ?? "<connectionstring not defined>")}). Returning...";

                            // Log the error with exception if possible...
#if (NET452 || NET48)
                            Exception te = null;
#else
                            Exception? te = null;
#endif
                            try
                            {
                                te = connectTask?.Exception?.GetBaseException() ?? null;
                            }
                            catch (Exception) { }
                            if (te != null)
                                this.Logger?.Error(te,
                                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(TransportSpecific_Connect)} - " +
                                    msg);
                            else
                                this.Logger?.Error(
                                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(TransportSpecific_Connect)} - " +
                                    msg);
                        }
                        else
                        {
                            // If here, we can assumes connected.

                            // set socket options after the socket was created in Connect()
                            // (not after the constructor because we clear the socket there)
                            this._client.NoDelay = Cfg_NoDelay;
                            this._client.SendTimeout = Cfg_SendTimeout;

                            this.Logger?.Debug(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(TransportSpecific_Connect)} - " +
                                $"Successful connection with server, " +
                                $"({(this._connection_string ?? "<connectionstring not defined>")}). ConnectionID = {(this.ConnectionId ?? "")}.");

                            success = true;
                        }
                    }
                }

                return success;
            }
            catch (SocketException exception)
            {
                // This happens if (for example) the ip address is correct
                // but there is no server running on that ip/port

                this.Logger?.Error(exception,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(TransportSpecific_Connect)} - " +
                    $"Failed to connect to server, " +
                    $"({(this._connection_string ?? "<connectionstring not defined>")}).");

                // Notify the owner that the connection failed.
                return success;
            }
            catch (Exception exception)
            {
                // Connect might have failed. thread might have been closed.

                this.Logger?.Error(exception,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(TransportSpecific_Connect)} - " +
                    $"Failed to connect to server, " +
                    $"({(this._connection_string ?? "<connectionstring not defined>")}).");


                // Notify the owner that the connection failed.
                return success;
            }
        }

        /// <summary>
        /// This virtual method provides a way for the derived type to perform any transport-specific setup after an initial connection is made.
        /// This method was created for the TCP socket implementation, because it has a two-part client (client and network stream),
        ///     and the network stream instance must be retrieved from the client instance, to expose the read and write methods.
        /// </summary>
        override protected async Task<int> Do_TransportSpecific_PostConnectionWork_Async()
        {
            // Moved the network stream assignment and the receiver and sender instanciations here so the constructor doesn't throw an exception
            //  if it's passed a closed tcpclient.
            try
            {
                // Create a network stream.
                this._conn_networkstream = this._client.GetStream();

                // Create the sender and receiver instances. Give them the client and stream references we just got and made.
                this._receiveLoop = new cReceiveLoop(this._client);
                this._receiveLoop.OnConnection_Went_Bad = this.CALLBACK_Receiver_Conn_Went_Bad;
                this._receiveLoop.OnMessage_Received = this.CALLBACK_Receiver_Message_Received;
                this._receiveLoop.OnStatus_Change = this.CALLBACK_Receiver_Status_Change;
                this._receiveLoop.MaxMessageSize = this.MaxMessageSize;

                // Since the BeginRead method of a TCP socket doesn't accept a cancellation token,
                //  and we have a receiver cancellation token source that we are using for other transports,
                //  we will tie the receive loop's dispose method into the cancellation token's callbac.
                this._receive_cts.Token.Register(this._receiveLoop.Dispose);

                var res = this._receiveLoop.Begin_Comms();
                if(res != 1)
                {
                    // Failed to start receive loop.
                    this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_TransportSpecific_PostConnectionWork_Async)} - " +
                        "Failed to start receive loop.");

                    // Force the endpoint to the error state.
                    this.UpdateState(eEndpoint_ConnectionStatus.Error);

                    return -2;
                }

                return 1;
            }
            catch(Exception e)
            {
                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Do_TransportSpecific_PostConnectionWork_Async)} - " +
                    "Exception occurred, most likely because the tcpclient we received during construction is closed or disposed.");

                // Force the endpoint to the error state.
                this.UpdateState(eEndpoint_ConnectionStatus.Error);

                return -10;
            }
        }

        /// <summary>
        /// In the implementation of this method, perform a close on the transport, and dispose of it.
        /// Don't dereference the transport instance, yet.
        /// </summary>
        override protected async Task CloseandDisposeTransport()
        {
            if (_client != null)
            {
                // Close the connection...
                try
                {
                    this._client?.Close();
                }
                catch (Exception) { }
                try
                {
#if (NET452)
                    this._client?.Close();
#else
                    this._client?.Dispose();
#endif
                }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// In the implementation of this method, perform a dereference of the transport instance.
        /// Don't dispose it or anything else, here.
        /// </summary>
        override protected void DereferenceTransport()
        {
            this._client = null;
        }

        /// <summary>
        /// Creates a loggable string block of the current configuration for the client.
        /// </summary>
        /// <returns></returns>
        override public string ToLogString_Config()
        {
            StringBuilder b = new StringBuilder();

            b.Append(base.ToLogString_Config());
            b.AppendLine($"Cfg_NoDelay = " + Cfg_NoDelay.ToString() + ";");
            b.AppendLine($"Cfg_SendTimeout = " + Cfg_SendTimeout.ToString() + ";");
            b.AppendLine($"Cfg_ConnectTimeout = " + Cfg_ConnectTimeout.ToString() + ";");
            b.AppendLine($"***End of Configuration***");

            return b.ToString();
        }

        #endregion


        #region Send Methods

        /// <summary>
        /// Override this method with the transport-specific means to send the given array.
        /// No need for any try-catch, as the call to this method is safely wrapped.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        override protected async Task<int> RawTransportSend(byte[] data)
        {
			int Result = 0;
			int bytes_pushed_into_buffer = 0;
			byte[] frame;

			// Handle the special case that the caller send us an empty message.
			// This is usually a zer-bypte ping message, and we will send it as a zero-length and empty data section.
			if(data.Length == 0)
			{
				// We retrieved a zero-length message that we need to send.

				// Create a frame of just the header size.
				bytes_pushed_into_buffer = cCustom_Serializer.size_of_Int32;
				frame = new byte[bytes_pushed_into_buffer];

				// Serialize the size.
				Result = cCustom_Serializer.Serialize_Integer32(0, ref frame, 0);
				if (Result < 0)
				{
					this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
						"Error occurred while forming the empty message.");

					return -2;
				}
				// We serialized the empty message.
			}
			else
			{
				// We received a positive length message.
				// Process it as normal.

				// Compose the raw buffer that will be pushed down the network stack.
				// We do this because we must send the data as well as a length, prepending it, so the receiving end can know how much data is in the message.
				// We push both the size and the data into a single buffer so it's a single network call.
				// Two array copies (size and data into a single buffer) and one network write are faster than two network writes (for separate size and data).
				bytes_pushed_into_buffer = data.Length + cCustom_Serializer.size_of_Int32;
				frame = new byte[bytes_pushed_into_buffer];

				// Serialize the size.
				Result = cCustom_Serializer.Serialize_Integer32(data.Length, ref frame, 0);
				if (Result < 0)
				{
					this.Logger?.Error(
                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
						"Error occurred while forming the message frame.");

					return -2;
				}
				// We serialized the message size.

			}

			// Copy over the data.
			Array.Copy(data, 0, frame, 4, data.Length);

			// We have a message in the buffer that can be pushed to the wire.

			// Push the buffer to the wire.
			return this.Push_Buffer_to_Wire(frame, 0, bytes_pushed_into_buffer);
        }

		/// <summary>
		/// TCP specific means to push a buffer to the wire.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="start"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		protected int Push_Buffer_to_Wire(byte[] buffer, int start, int length)
		{
            try
            {
                // Send the message buffer to the wire.
                this._conn_networkstream.Write(buffer, start, length);

                // If we made it here, we successfully sent a message over the wire.
                // Otherwise, we would have thrown an exception.
                // In case this is our first outgoing message, we need to upgrade our connection status.
                this.PromoteStatus_from_NewlyOpen_to_Open();

                // Increment the write message counter.
                this.Metrics.Sent_Message_Count++;

                this.Logger?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
                    "Message buffer was sent over the wire.");

                // Return success to the caller.
                return length;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // IO Exception occurred.
                // We can no longer trust the connection.

                this.Logger?.Error(se,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
                    "Socket Exception occurred while attempting to send a message to the wire.");

                this.UpdateState(eEndpoint_ConnectionStatus.Lost);

                return -6;
            }
            catch (System.IO.IOException ioe)
            {
                // IO Exception occurred.
                // We can no longer trust the connection.

                this.Logger?.Error(ioe,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
                    "IO Exception occurred while attempting to send a message to the wire.");

                this.UpdateState(eEndpoint_ConnectionStatus.Lost);

                return -3;
            }
            catch (System.ObjectDisposedException ode)
            {
                // Object disposed Exception occurred.
                // We can no longer trust the connection.

                this.Logger?.Error(ode,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
                    "Object Dispose Exception occurred while attempting to send a message to the wire.");

                this.UpdateState(eEndpoint_ConnectionStatus.Lost);

                return -4;
            }
            catch (Exception e)
            {
                // Error occurred.

                this.Logger?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
                    "Standard Exception occurred while attempting to send a message to the wire.");

                this.UpdateState(eEndpoint_ConnectionStatus.Lost);

                return -5;
            }
		}

        #endregion


        #region Receiving Methods

        /// <summary>
        /// NOTE: This method executes on its own thread, started by the post connection method.
        /// Runs the receive loop logic to accept all received messages and dispatch them.
        /// This is called each time a new connection is made.
        /// Returns  1 if cancelled.
        /// Returns  0 if failed to parse the received message.
        /// Returns -1 if unable to accept received messages.
        /// Returns -2 if received a close message.
        /// </summary>
        /// <returns></returns>
        override protected async Task<int> ReceiveLoop()
        {
            // Since we are using the BeginRead and EndRead method calls of the TCPclient socket, we don't actually need an active receiver thread.
            // And, we have no logic for this method.
            // So, we are just filling out an empty block, to satisfy the abstract method override.

            return 1;
        }

        protected void CALLBACK_Receiver_Status_Change(cReceiveLoop rcloop, string statusupdate)
        {
            this.Logger?.Info(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Status_Change)} - " +
                "received a callback from the receiver that its state changed.");

            // See if the receiver is in error.
            if (rcloop.State == eLoop_ConnectionStatus.Error ||
                rcloop.State == eLoop_ConnectionStatus.Shutting_Down ||
                rcloop.State == eLoop_ConnectionStatus.Lost ||
                rcloop.State == eLoop_ConnectionStatus.Closed)
            {
                // Receiver is in error or shutting down, or just closed.
                // We can no longer accept messages.

                // Set a corresponding state to the endpoint based on the receiver status, if the endpoint is not yet closed or error.
                if(this.State != eEndpoint_ConnectionStatus.Closed &&
                    this.State != eEndpoint_ConnectionStatus.Error &&
                    this.State != eEndpoint_ConnectionStatus.Shutting_Down &&
                    this.State != eEndpoint_ConnectionStatus.Lost)
                {
                    // The endpoint is not already closing down.

                    // Update the endpoint state.
                    if (rcloop.State == eLoop_ConnectionStatus.Error)
                        this.UpdateState(eEndpoint_ConnectionStatus.Error);
                    else if (rcloop.State == eLoop_ConnectionStatus.Closed)
                        this.UpdateState(eEndpoint_ConnectionStatus.Closed);
                    else if (rcloop.State == eLoop_ConnectionStatus.Lost)
                        this.UpdateState(eEndpoint_ConnectionStatus.Lost);
                    else if (rcloop.State == eLoop_ConnectionStatus.Shutting_Down)
                        this.UpdateState(eEndpoint_ConnectionStatus.Shutting_Down);
                }

                // Do all the common connection closure things...
                // Reset the allow sending flag, to prevent outgoing messages...
                this._allowsend = false;
                // Signal that the connection was lost...
                DispatchConnectionLost();
                // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
                try
                {
                    this.CloseandDisposeTransport().GetAwaiter();
                }
                catch (Exception) { }
                try
                {
                    this.DereferenceTransport();
                }
                catch (Exception) { }
            }
            else if (rcloop.State == eLoop_ConnectionStatus.Newly_Opened)
            {
                // Receiver went active.

                // Update our state to reflect that.
                this.UpdateState(eEndpoint_ConnectionStatus.Newly_Opened);

                return;
            }
            else if (rcloop.State == eLoop_ConnectionStatus.Open)
            {
                // Receiver went active.

                // Update our state to reflect that.
                this.UpdateState(eEndpoint_ConnectionStatus.Open);

                return;
            }
            else if (rcloop.State == eLoop_ConnectionStatus.Initialized)
            {
                // Receiver is initialized.
                // Nothing to report here.
                return;
            }
            else
            {
                // Unknown state for the sender.
                this.Logger?.Info(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Status_Change)} - " +
                    "received a callback from the Receiver with an unknown state. " +
                    "State=" + rcloop.State.ToString() + ".");

                return;
            }
        }

        protected void CALLBACK_Receiver_Conn_Went_Bad(cReceiveLoop mep)
        {
            // Clear the send flag, to prevent outgoing messages...
            this._allowsend = false;

            this.Logger?.Warn(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Conn_Went_Bad)} - " +
                $"Connection  was closed or lost.");

            this.UpdateState(eEndpoint_ConnectionStatus.Error);

            // Do all the common connection closure things...
            // Reset the allow sending flag, to prevent outgoing messages...
            this._allowsend = false;
            // Signal that the connection was lost...
            DispatchConnectionLost();

            // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
            try
            {
                this.CloseandDisposeTransport().GetAwaiter();
            }
            catch (Exception) { }
            try
            {
                this.DereferenceTransport();
            }
            catch (Exception) { }
        }

        protected void CALLBACK_Receiver_Message_Received(cReceiveLoop mep, string rawmsg)
        {
            // Update our received timestamp...
            LastReceivedTime = DateTime.UtcNow;

            // Increment the received message counter...
            Interlocked.Increment(ref this._receivedmessage_counter);

            // Send it off for processing....
            ///  1 = Message was handled.
            ///  0 = Message could not be deserialized or handled. Ignoring and continuing on.
            /// -1 = Registration failed. The receive loop cannot continue, and the connection must close down.
            int res = Process_ReceivedMessage(rawmsg);
            if (res == 0)
            {
                // Message process and dispatch had a problem, but we can keep going.

                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Message_Received)} - " +
                    "Failed to process and dispatch received message.");
            }
            else if (res == -1)
            {
                // Message processing failed.
                // We will consider this fatal to the current connection.

                // We failed to process the received message.
                // We must recycle this connection, and try again.

                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Message_Received)} - " +
                    "Messaging processing failed in a fatal way, and we need to recycle this connection.");

                // Do all the common connection closure things...
                // Reset the allow sending flag, to prevent outgoing messages...
                this._allowsend = false;
                // Signal that the connection was lost...
                DispatchConnectionLost();
                // Wrap in a try-catch to ensure the override doesn't thrown and unwind us...
                try
                {
                    this.CloseandDisposeTransport().GetAwaiter();
                }
                catch (Exception) { }
                try
                {
                    this.DereferenceTransport();
                }
                catch (Exception) { }

                return;
            }
        }

        #endregion
    }
}
