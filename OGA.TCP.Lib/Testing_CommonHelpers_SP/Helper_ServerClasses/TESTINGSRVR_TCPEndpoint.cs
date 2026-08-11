using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OGA.Common.Process;
using OGA.TCP.Messages;
using OGA.TCP.Server.Model;
using OGA.TCP.Shared.Encoding;

namespace OGA.TCP.Server
{
    /// <summary>
    /// NOT FOR PRODUCTION USE — TESTING COPY.
    /// This is a copy of the server library's TCPEndpoint, compiled into the client test projects so
    ///     client tests can run against live server behavior on the .NET Framework targets the server
    ///     library does not support (the owner retains netfx client support for fielded implementations).
    /// SYNC EXPECTATION: protocol or behavior changes in the real class MUST be mirrored here; drift
    ///     breaks the client suite loudly, which is the intended detector.
    /// REMOVAL: when the client library drops its .NET Framework targets, delete the TESTINGSRVR_ copies
    ///     and reference the real server classes — see the design spec's OI-06.
    /// Provides tcpsocket connectivity to a single connected client.
    /// Is compatible with clients of LibVersion={1,2,3}
    /// </summary>
    public class TESTINGSRVR_TCPEndpoint : TESTINGSRVR_Endpoint_Abstract, IDisposable
    {
        #region Private Fields

        /// <summary>
        /// Need a local reference to the TCPClient so we can check if connected because the network stream class doesn't provide such a message.
        /// </summary>
        private TcpClient _client;
        /// <summary>
        /// Need a local reference to the underlying stream to exchange data.
        /// </summary>
        private System.Net.Sockets.NetworkStream _conn_networkstream;

        /// <summary>
        /// Helper class that deals with the cruft of receiving data from a tcp socket.
        /// </summary>
        private cReceiveLoop _receiveLoop;

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
        /// NoDelay disables nagle algorithm. lowers CPU% and latency but increases bandwidth.
        /// </summary>
        public bool Cfg_NoDelay { get; set; } = true;

        /// <summary>
        /// Send would stall forever if the network is cut off during a send, so we need a timeout (in milliseconds).
        /// This value gets applied to the actual Tcpclient instance, to serve as its timeout for sends.
        /// </summary>
        public int Cfg_SendTimeout { get; set; } = 5000;

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
                    if(_alreadydisposed)
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

        #endregion


        #region ctor / dtor

        /// <summary>
        /// This is the normal constructor for creating a TCPEndpoint instance.
        /// It requires the web listener already having created the websocket instance from the initial http request.
        /// </summary>
        /// <param name="client"></param>
        public TESTINGSRVR_TCPEndpoint(TcpClient client) : base()
        {
            _classname = nameof(TESTINGSRVR_TCPEndpoint);

            this._client = client;

            this._metrics = new cEndpoint_Metrics();
        }

        #endregion


        #region Connection Management

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
                // set socket options after the socket was created in Connect()
                // (not after the constructor because we clear the socket there)
                this._client.NoDelay = this.Cfg_NoDelay;
                this._client.SendTimeout = this.Cfg_SendTimeout;

                // Create a network stream.
                this._conn_networkstream = this._client.GetStream();

                // Create the sender and receiver instances. Give them the client and stream references we just got and made.
                this._receiveLoop = new cReceiveLoop(this._client);
                this._receiveLoop.OnConnection_Went_Bad = this.CALLBACK_Receiver_Conn_Went_Bad;
                this._receiveLoop.OnMessage_Received = this.CALLBACK_Receiver_Message_Received;
                this._receiveLoop.OnStatus_Change = this.CALLBACK_Receiver_Status_Change;
                this._receiveLoop.MaxFrameSize = this.MaxFrameSize;

                // Since the BeginRead method of a TCP socket doesn't accept a cancellation token,
                //  and we have a receiver cancellation token source that we are using for other transports,
                //  we will tie the receive loop's dispose method into the cancellation token's callbac.
                this._receive_cts.Token.Register(this._receiveLoop.Dispose);

                var res = this._receiveLoop.Begin_Comms();
                if(res != 1)
                {
                    // Failed to start receive loop.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
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
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
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

                // Wait a tick before dereferencing the transport instance...
                // This gives the client a little time to receive the close message before we dispose.
                System.Threading.Thread.Sleep(100);

#if (NET452 || NET48)
#else
                try
                {
                    this._client?.Dispose();
                }
                catch (Exception) { }
#endif
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
            b.AppendLine($"***End of Configuration***");

            return b.ToString();
        }


        #endregion


        #region Send Methods

        /// <summary>
        /// TCP-specific frame send: prepends the five-byte preamble (4-byte little-endian body length, then the
        ///     frame-type byte from the FrameTypes registry) and pushes preamble plus body as one network write.
        /// A zero-length body is legal: it produces a preamble-only frame, used as a lightweight keepalive probe.
        /// No need for any try-catch, as the call to this method is safely wrapped.
        /// </summary>
        /// <param name="frametype">The frame's type byte, from the FrameTypes registry.</param>
        /// <param name="body">The frame's body bytes. May be empty, never null.</param>
        /// <returns></returns>
        override protected async Task<int> RawTransportSend(byte frametype, byte[] body)
        {
            // Compose the raw buffer that will be pushed down the network stack.
            // We push the preamble and the body into a single buffer, so the send is one network call:
            //  two array copies plus one write are faster than separate writes for preamble and body.
            int bodylength = body?.Length ?? 0;
            int bytes_pushed_into_buffer = bodylength + 5;
            byte[] frame = new byte[bytes_pushed_into_buffer];

            // Serialize the body length into the first four bytes...
            int Result = cCustom_Serializer.Serialize_Integer32(bodylength, ref frame, 0);
            if (Result < 0)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                    "Error occurred while forming the message frame.");

                return -2;
            }

            // Stamp the frame type as the fifth byte...
            frame[4] = frametype;

            // Copy over the body, when present...
            if (bodylength > 0)
                Array.Copy(body, 0, frame, 5, bodylength);

            // We have a frame in the buffer that can be pushed to the wire.

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
		private int Push_Buffer_to_Wire(byte[] buffer, int start, int length)
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
				// This must land on the backing field: the Metrics property hands out snapshot copies.
				this._metrics.Sent_Message_Count++;
				this._metrics.Last_Sent_Message_Time = DateTime.UtcNow;

				OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
					"Message buffer was sent over the wire.");

				// Return success to the caller.
				return length;
			}
            catch (System.Net.Sockets.SocketException se)
            {
                // IO Exception occurred.
                // We can no longer trust the connection.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(se,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
                    "Socket Exception occurred while attempting to send a message to the wire.");

                this.UpdateState(eEndpoint_ConnectionStatus.Lost);

                return -6;
            }
			catch (System.IO.IOException ioe)
			{
				// IO Exception occurred.
				// We can no longer trust the connection.

				OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ioe,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
					"IO Exception occurred while attempting to send a message to the wire.");

				this.UpdateState(eEndpoint_ConnectionStatus.Lost);

				return -3;
			}
			catch (System.ObjectDisposedException ode)
			{
				// Object disposed Exception occurred.
				// We can no longer trust the connection.

				OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ode,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
					"Object Dispose Exception occurred while attempting to send a message to the wire.");

				this.UpdateState(eEndpoint_ConnectionStatus.Lost);

				return -4;
			}
			catch (Exception e)
			{
				// Error occurred.

				OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(Push_Buffer_to_Wire)} - " +
					"Standard Exception occurred while attempting to send a message to the wire.");

				this.UpdateState(eEndpoint_ConnectionStatus.Lost);

				return -5;
			}
		}

        #endregion


        #region Receiver Callbacks

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
        override protected async Task<int> ReceiveLoop_from_Client()
        {
            // Since we are using the BeginRead and EndRead method calls of the TCPclient socket, we don't actually need an active receiver thread.
            // And, we have no logic for this method.
            // So, we are just filling out an empty block, to satisfy the abstract method override.

            return 1;
        }

        private void CALLBACK_Receiver_Status_Change(cReceiveLoop rcloop, string statusupdate)
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
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

                this.DoCommonClosureThings();
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
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Status_Change)} - " +
                    "received a callback from the Receiver with an unknown state. " +
                    "State=" + rcloop.State.ToString() + ".");

                return;
            }
        }

        private void CALLBACK_Receiver_Conn_Went_Bad(cReceiveLoop mep)
        {
            // Clear the send flag, to prevent outgoing messages...
            this._allowsend = false;

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Conn_Went_Bad)} - " +
                $"Connection  was closed or lost.");

            this.UpdateState(eEndpoint_ConnectionStatus.Error);

            this.DoCommonClosureThings();
        }

        private void CALLBACK_Receiver_Message_Received(cReceiveLoop mep, byte frametype, byte[] body)
        {
            // Update our received timestamp...
            LastReceivedTimeUTC = DateTime.UtcNow;

            // Increment the received message counter...
            Interlocked.Increment(ref this._receivedmessage_counter);

            // Send it off for processing....
            ///  1 = Message was handled.
            ///  0 = Message could not be deserialized or handled. Ignoring and continuing on.
            /// -1 = Registration failed. The receive loop cannot continue, and the connection must close down.
            int res = Process_ReceivedMessage_from_Client(frametype, body);
            if (res == 0)
            {
                // Message process and dispatch had a problem, but we can keep going.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Message_Received)} - " +
                    "Failed to process and dispatch received message.");
            }
            else if (res == -1)
            {
                // Message processing failed.
                // We will consider this fatal to the current connection.

                // We failed to process the received message.
                // We must recycle this connection, and try again.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receiver_Message_Received)} - " +
                    "Messaging processing failed in a fatal way, and we need to recycle this connection.");

                // Do all the common connection closure things...
                this.DoCommonClosureThings();

                return;
            }
        }

        #endregion
    }
}
