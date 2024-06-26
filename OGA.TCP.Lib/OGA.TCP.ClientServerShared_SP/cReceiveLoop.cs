﻿using NLog;
using OGA.TCP.Messages;
using OGA.TCP.Shared.Encoding;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP
{
    public class cReceiveLoop : IDisposable
    {
		#region Private Fields

        protected NLog.ILogger Logger;

		/// <summary>
		/// Tracks how many endpoints have been created for the process.
		/// </summary>
		static protected volatile int _instance_counter;

        protected string _classname;

        protected bool _alreadydisposed;

		protected object _comms_begun_lock;
		protected volatile bool _Comms_Begun;
		protected volatile bool _comms_ended;

		protected System.DateTime _last_received_timestampUTC;

		protected volatile int _received_byte_count;
		protected volatile int _number_of_expected_bytes;
		protected volatile int _currentmessagelength;

		/// <summary>
		/// Need a local reference to the TCPClient so we can check if connected because the network stream class doesn't provide such a message.
		/// </summary>
		protected TcpClient _client;
		/// <summary>
		/// Need a local reference to the underlying stream to exchange data.
		/// </summary>
		protected System.Net.Sockets.NetworkStream _conn_networkstream;

		static protected int _default_buffer_size = 2048;

		protected cEndpoint_Metrics _metrics;

		protected cBuffer _buffer;

		protected CancellationTokenSource _readcts;

		/// <summary>
		/// 0 - non , 1 - length, 2 - data
		/// </summary>
		protected int frameread_section = 0;

		#endregion


		#region Public Properties

        /// <summary>
        /// Maximum allowed message size for a single frame.
        /// Prevent allocation attacks. Each packet is prefixed with a length header, so an attacker could send a fake packet with length=2GB,
        /// causing the server to allocate 2GB and run out of memory quickly.
        /// -> simply increase max packet size if you want to send around bigger files!
        /// -> 1MB per message should be more than enough.
        /// </summary>
        public int MaxMessageSize { get; set; } = OGA.TCP.Constants.CONST_MAX_MessageSize;

		/// <summary>
		/// Number of milliseconds allowed to read the frame body, once the frame size has been read.
		/// This provides us with the ability to protect from a mid-frame connection loss, or malformed client message.
		/// </summary>
		public int FrameReadTimeout { get; set; } = 5000;

		public eLoop_ConnectionStatus State { get; protected set; }

        /// <summary>
        /// Instance Id of the receive loop.
        /// </summary>
        public int InstanceId { get; protected set; }

		public System.DateTime Last_Received_TimestampUTC
		{
			get
			{
				return _last_received_timestampUTC;
			}
		}

		public cEndpoint_Metrics Metrics
		{
			get
			{
				// Get a copy of the metrics.
				cEndpoint_Metrics met = new cEndpoint_Metrics();
				met.CopyFrom(this._metrics);

				return met;
			}
		}

		#endregion


		#region Delegates and Handlers

		public delegate void dWent_Bad(cReceiveLoop mep);
		protected dWent_Bad _del_dwent_bad;
		/// <summary>
		/// Assign a handler to this delegate to accept events when a candidate connection proves good to go.
		/// </summary>
		public dWent_Bad OnConnection_Went_Bad
		{
			set
			{
				this._del_dwent_bad = value;
			}
		}

		public delegate void dStatus_Change(cReceiveLoop mep, string statusupdate);
		protected dStatus_Change _del_Status_Change;
		/// <summary>
		/// Assign a handler to this delegate to receive status changes.
		/// </summary>
		public dStatus_Change OnStatus_Change
		{
			set
			{
				this._del_Status_Change = value;
			}
		}

		public delegate void dMessage_Received(cReceiveLoop mep, string rawmsg);
		protected dMessage_Received _del_message_received;

        /// <summary>
        /// Assign a handler to this delegate to process received messages.
        /// </summary>
        public dMessage_Received OnMessage_Received
		{
			set
			{
				this._del_message_received = value;
			}
		}

		#endregion


		#region ctor / dtor

		public cReceiveLoop(TcpClient client, NLog.ILogger logger = null)
		{
            _instance_counter++;
            this.InstanceId = _instance_counter;

			this._classname = nameof(cReceiveLoop);

			this._readcts = new CancellationTokenSource();

			if(client == null || client.Connected == false)
				throw new Exception("TCPClient instance is null.");

			// Accept the tcp client from the caller.
			this._client = client;

            this.Logger = logger;

            // Create a network stream.
			this._conn_networkstream = this._client.GetStream();

			this._metrics = new cEndpoint_Metrics();

            this._alreadydisposed = false;

			// Instanciate the buffers we will use for data exchange.
			this._buffer = new cBuffer();

			// Declare a comms begun lock.
			this._comms_begun_lock = new object();
			// Clear the comms started and comms ended flag so we know we can begin comms when required.
			this._Comms_Begun = false;
			this._comms_ended = false;

			// Setup our send and receive buffer to the default size.
			this.Resize_Buffer_if_Needed(_default_buffer_size);

			// Reset the last received timestamp to the initialization time for this endpoint.
			this._last_received_timestampUTC = System.DateTime.UtcNow;
			this._metrics.Last_Received_Message_Time = System.DateTime.UtcNow;

			// Reset buffer pointers for a new message...
			Reset_Buffer_Pointers();

			UpdateState(eLoop_ConnectionStatus.Initialized);
		}

		protected virtual void Dispose(bool disposing)
        {
            if (!_alreadydisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

				// Close down the receiver...
				CloseDown();

				// Disconnect all callbacks, now that we have closed down...
				// We do this after close down, to ensure that any status change or closure callback can be called.
				Teardown_Delegates();

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _alreadydisposed = true;
            }
        }

		// TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		~cReceiveLoop()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: false);
		}

		public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

		protected void Teardown_Delegates()
		{
			// Dereference delegates.
			this._del_dwent_bad = null;
			this._del_Status_Change = null;
			this._del_message_received = null;
		}

		#endregion


		#region Control Methods

		/// <summary>
		/// Public method used to startup the loop after construction.
		/// It assumes that a viable connection was given to it, and it can use the connection for message exchange.
		/// </summary>
		/// <returns></returns>
		public int Begin_Comms()
		{
			int Result = 0;

			if (this._alreadydisposed)
            {
				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
					"Caller wants to start a message Loop that is disposed.");

				return -1;
			}

			lock (this._comms_begun_lock)
			{
				// Check if we have begun comms already.
				// We don't want two background workers waiting on reads.
				if (this._Comms_Begun)
				{
					// Comms has already started.
					return -1;
				}
				// We have not made it through the begin comms method yet.

				if (this.State != eLoop_ConnectionStatus.Initialized)
				{
					// Not initialized.
					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
						"Client connection started, but not in the initialized state.");

					// Force the endpoint to the error state.
					this.UpdateState(eLoop_ConnectionStatus.Error);

					// Close things down.
					this.CloseDown();

					return -1;
					// the endpoint is in a bad state and cannot being comms.
				}
				// The state is good.
				// Check on our resources.

				// Clear the comms ended flag.
				// Other logic will use this to know when we have stopped communicating, so they can stop processing.
				// This flag is used by logic that would attempt to process messages from class resources that are torn down, or scheduled to be torn down during shutdown.
				this._comms_ended = false;

				// Attempt to setup the socket.
				Result = this.Setup_Connection();

				// See if it was setup properly.
				if (Result < 0)
				{
					// An error occurred while attempting to setup the connection.

					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
						"An error occurred while attempting to setup the connection.");

					return -10;
				}
				// At this point, the socket and network stream are open as far as we know.


				// Reset buffer pointers for a new message...
				Reset_Buffer_Pointers();

				// Update the last ping time to the current time.
				// We do this because we should have other message types to exchange when just beginning communications.
				this._last_received_timestampUTC = System.DateTime.UtcNow;
				this._metrics.Last_Received_Message_Time = System.DateTime.UtcNow;

				// Update our status.
				this.UpdateState(eLoop_ConnectionStatus.Newly_Opened);

				// Queue up an async read to wait for data.
				Result = this.Queue_Async_Read();

				// See if an error occurred.
				if (Result < 0)
				{
					// An error occurred.

					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
						"An error occurred while queuing up an async reader.");

					return -2;
				}
				// We queued off the initial read operation to wait for data.

				// Set a flag that we have successfully made it through the begin comms method.
				// This ensures that we won't try again.
				this._Comms_Begun = true;

				// TODO: Add any additional startup work here...

				// Return success to the caller.
				return 1;
			}
		}

		public int CloseDown()
		{
			// Update our status that we are closing down.
			// If we are already in a terminal state, we'd stay there, such as Lost, Error or Closed.
			// The special case, here, is if we have NOT been started, and are still Initialized.
			// If still Initialized, we stay in that state.
			this.UpdateState(eLoop_ConnectionStatus.Shutting_Down, true);

			// Clean up other resources...

			this.Logger?.Debug(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(CloseDown)} - " +
				"Cleaning up other resources...");

			this._comms_ended = true;
			this._Comms_Begun = false;

			// Release the read cancellation token...
			if(this._readcts != null)
			{
				try { this._readcts?.Cancel(); } catch (Exception e) { }
				System.Threading.Thread.Sleep(100);
				try { this._readcts?.Dispose(); } catch (Exception e) { }
				this._readcts = null;
			}

			// Dereference the buffers.
			try { this._buffer?.Dispose(); } catch (Exception e) { }
			this._buffer = null;

			// Release connection references...
			// But, don't dispose them.
			// They were given to us by a parent endpoint. So, we are not the owner.
			this._conn_networkstream = null;
			this._client = null;

			this.Logger?.Debug(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(CloseDown)} - " +
				"Resources released.");

			this.UpdateState(eLoop_ConnectionStatus.Closed);

			return 1;
		}

		private int Setup_Connection()
		{
			// Check that the socket exists.
			if (this._client == null)
			{
				// The client is not instanciated.

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Setup_Connection)} - " +
					"Client is not instanciated.");

				// Force the loop to the error state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				// Close things down.
				this.CloseDown();

				return -1;
			}
			// The client exists.
			// Check that the client is open.
			if (this._client.Connected != true)
			{
				// The client is not open.

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Setup_Connection)} - " +
					"Client is not open.");

				// Force the loop to the error state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				// Close things down.
				this.CloseDown();

				return -1;
			}
			// The client reports open.
			// It might not actually be open, but we won't know until we send data.

			// Check that the network stream is open.
			if (this._conn_networkstream == null)
			{
				// The network stream is not open.

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Begin_Comms)} - " +
					"Network stream is not open.");

				// Force the loop to the error state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				// Close things down.
				this.CloseDown();

				return -1;
			}

			// Initialize the buffer.
			this.Resize_Buffer_if_Needed(_default_buffer_size);

			// If here, success.
			return 1;
		}
		
		#endregion


		#region Message Receiving

		/// <summary>
		/// Called each time the endpoint should wait on new data to be received.
		/// Returns zero or positives for success.
		/// Returns negatives for errors.
		/// </summary>
		/// <returns></returns>
		private int Queue_Async_Read()
		{
			this.Logger?.Trace(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(Queue_Async_Read)} - " +
				"Attempting to start an async read...");

			try
			{
				// Added a couple of short-circuits here to NOT start another read from the client in case the endpoint is down, broken, errored out, lost, or shutting down.
				// This prevents the instance from throwing an error for a read without the resources to do so (because they are being recycled).
				if (this._buffer == null)
				{
					// The buffer is not instanciated for use.
					// Check if we are shutting down.
					if (this.State == eLoop_ConnectionStatus.Shutting_Down ||
						this.State == eLoop_ConnectionStatus.Closed ||
						this.State == eLoop_ConnectionStatus.Lost ||
						this.State == eLoop_ConnectionStatus.Error)
					{
						// The loop is down or going down.
						// It cannot reliably processing incoming data in this state.
						// We will not queue up another read.
						return 1;
					}
				}

				// Create a timeout if we are waiting on the message data...
				// NOTE: We only create a timeout, here, if we already know the message length, and are waiting on the actual message frame.
				if(this.frameread_section == 2)
				{
					// We have already read in the frame size.
					// Now, we are attempting to read the frame data, itself.
					// So, we will start a timeout, to ensure we do either read the frame, or throw an error.

					// Re arm the read token...
					try { this._readcts?.Cancel(); } catch (Exception) { }
					System.Threading.Thread.Sleep(100);
					try { this._readcts?.Dispose(); } catch (Exception) { }
					this._readcts = new CancellationTokenSource();

					// And, arm the frame read timeout logic...
					_ = Task.Run(() => this.Arm_FrameReadTimeout());
				}

				// Start an async read to pull in what's left of the expected message.
				var iar = this._conn_networkstream.BeginRead(this._buffer.Buffer,
															 this._received_byte_count,
															 this._number_of_expected_bytes - this._received_byte_count,
															 this.CALLBACK_Receive_Read_Data,
															 this);

				this.Logger?.Trace(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Queue_Async_Read)} - " +
					"Async read was queued.");

				return 1;
			}
			catch (System.ObjectDisposedException ode)
			{
				// Not sure what exception occurred here.

				if (ode.ObjectName == "System.Net.Sockets.NetworkStream")
				{
					// The stream got disposed.
					// We don't own the networkstream instance, ourselves.
					// It is owned by the TcpClient instance.
					// And the TcpClient instance is disposed by our parent.
					// So, we must conclude that we are shutting down.
					// We will not regard this as an error.
					// But, will log it and do the normal closure things,

					// Log a message here.
					this.Logger?.Info(ode,
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(Queue_Async_Read)} - " +
						"The networkstream was disposed while attempting to register a callback to wait for a message.");
				}
				else
				{
					// ODE is not for the networkstream.
					// So, we will log it as error.
					// Not sure what exception occurred here.

					// Log a message here.
					this.Logger?.Error(ode,
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(Queue_Async_Read)} - " +
						"An exception was caught while attempting to register a callback to wait for a message.");
				}

				// Change to the error state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				return -1;
			}
			catch (System.IO.IOException ioe)
			{
				// Not sure what exception occurred here.

				// Log a message here.
				this.Logger?.Error(ioe,
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Queue_Async_Read)} - " +
					"An exception was caught while attempting to register a callback to wait for a message.");

				// Change to the error state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				return -2;
			}
			catch (Exception e)
			{
				// Not sure what exception occurred here.

				// We have situations where the loop starts closing things down while we are waiting on data.
				// This exception gets triggered when that happens.
				// So, we will add a check here to see if comms are going down. And if so, we will silently fail.
				if (this._comms_ended)
				{
					// The loop instance is being closed down.
					// We will ignore any error here.

					return 0;
				}
				// If here, the loop instance is not being closed down, and had an exception.
				// We will regard this as a legitimate exception.

				// Log a message here.
				this.Logger?.Error(e,
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Queue_Async_Read)} - " +
					"An exception was caught while attempting to register a callback to wait for a message.");

				// Change to the error state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				return -3;
			}
		}

        private async Task Arm_FrameReadTimeout()
        {
			try
			{
				// Start the timeout...
				if(this._readcts == null || this._readcts.IsCancellationRequested)
					return;

				await Task.Delay(FrameReadTimeout, this._readcts.Token);

				// See if it was cancelled (the read finished).
				if(this._readcts == null || this._readcts.IsCancellationRequested)
				{
					// The token was cancelled.
					// This means, the read callback returned, or the receiver is closing down.

					// In either case, we will simply leave, here...
					return;
				}
				// If here, the task delay expired.
				// Which means, we've waited too long to read the message frame.
				// So, we must declare a connection lost, and close it.

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Arm_FrameReadTimeout)} - " +
					"Failed to read entire data frame.");
				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Arm_FrameReadTimeout)} - " +
					"Closing down the message endpoint.");

				// Sent a bad state...
				this.UpdateState(eLoop_ConnectionStatus.Lost, true);

				// Disconnect the client.
				this.CloseDown();

				// Call the handler for a connection gone bad, in case it is hooked up.
				this.Call_del_dwent_bad();

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Arm_FrameReadTimeout)} - " +
					"Endpoint closed down.");
			}
			catch(Exception e)
			{
				int x = 0;
			}
        }

        /// <summary>
        /// Internal callback used to respond to data received from the connection.
        /// Will handle decoding and spawn any handling of each message.
        /// </summary>
        /// <param name="iar"></param>
        private void CALLBACK_Receive_Read_Data(System.IAsyncResult iar)
		{
			int Result = 0;
			int Resulta = 0;
			int tempint = 0;
			int messagecount = 0;

			// We are in the BeginRead callback.
			// This means, we are no longer waiting for it to return.
			// So, we can cancel our timeout logic...
			// This cancellation will disarm the read timeout logic.
			if(this._readcts != null)
			{
				try { this._readcts?.Cancel(); } catch (Exception) { }
				System.Threading.Thread.Sleep(100);
				try { this._readcts?.Dispose(); } catch (Exception) { }
				//this._readcts = null;
			}

			// See if we have begun shutting down, and need to stop processing incoming data.
			if(this._comms_ended)
			{
				// Swallow an end read because we are shutting down and need the end read for symmetry.
				try
				{
					// Get a throwaway reference to the current instance.
					cReceiveLoop cconn = (cReceiveLoop)iar.AsyncState;

					// See how the read turned out.
					Result = this._conn_networkstream?.EndRead(iar) ?? 0;
				}
				catch (Exception e) { }

				// The connection was closed by the other end.
				// We will update our state accordingly.

				// Sent a bad state.
				this.UpdateState(eLoop_ConnectionStatus.Closed);

				// Disconnect the client.
				this.CloseDown();

				// Call the handler for a connection gone bad, in case it is hooked up.
				this.Call_del_dwent_bad();

				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Endpoint closed down.");

				return;
			}

			this.Logger?.Info(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Received Data Callback was called to handle data read.");

			try
			{
				// Get a throwaway reference to the current instance.
				cReceiveLoop cconn = (cReceiveLoop)iar.AsyncState;

				// See how the read turned out.
				Result = this._conn_networkstream.EndRead(iar);

				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"The network stream async read returned for processing.");

				// See if the connection was closed.
				if (Result == 0)
				{
					// The connection was closed by the other end.
					// We will update our state accordingly.

					this.Logger?.Info(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"The network stream received a null frame, indicating the connection was closed by the other end.");

					this.Logger?.Info(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Closing down the message endpoint.");

					// Sent a bad state.
					this.UpdateState(eLoop_ConnectionStatus.Closed);

					// Disconnect the client.
					this.CloseDown();

					// Call the handler for a connection gone bad, in case it is hooked up.
					this.Call_del_dwent_bad();

					this.Logger?.Info(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Endpoint closed down.");

					return;
				}
			}
			catch (System.ObjectDisposedException ode)
			{
				// Not sure what exception occurred here.

				if (ode.ObjectName == "System.Net.Sockets.NetworkStream")
				{
					// The stream got disposed.
					// We don't own the networkstream instance, ourselves.
					// It is owned by the TcpClient instance.
					// And the TcpClient instance is disposed by our parent.
					// So, we must conclude that we are shutting down.
					// We will not regard this as an error.
					// But, will log it and do the normal closure things,

					this.Logger?.Debug(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Receive data callback observed Networkstream is disposed. Closing down the message endpoint...");
				}
				else
				{
					// ODE is not for the networkstream.
					// So, we will log it as error.

					// Log a message here.
					this.Logger?.Error(ode,
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"An exception was caught while attempting to read data from the connection.");
				}

				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Closing down the message endpoint.");

				// Sent a bad state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				// Disconnect the server.
				this.CloseDown();

				// Call the handler for a connection gone bad, in case it is hooked up.
				this.Call_del_dwent_bad();

				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Endpoint closed down.");

				return;
			}
			catch (System.IO.IOException ioe)
			{
				// Not sure what exception occurred here.

				// Log a message here.
				this.Logger?.Error(ioe,
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"An exception was caught while attempting to read data from the connection.");

				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Closing down the message endpoint.");

				// Sent a bad state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				// Disconnect the server.
				this.CloseDown();

				// Call the handler for a connection gone bad, in case it is hooked up.
				this.Call_del_dwent_bad();

				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Endpoint closed down.");

				return;
			}
			catch (Exception e)
			{
				// Not sure what exception occurred here.

				// Log a message here.
				this.Logger?.Error(e,
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"An exception was caught while attempting to read data from the connection.");

				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Closing down the message endpoint.");

				// Sent a bad state.
				this.UpdateState(eLoop_ConnectionStatus.Error);

				// Disconnect the server.
				this.CloseDown();

				// Call the handler for a connection gone bad, in case it is hooked up.
				this.Call_del_dwent_bad();

				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Endpoint closed down.");

				return;
			}
			// If here, the connection is open and we read something from it.
			// We need to see what we got.

			// We end up in this method for a number of combinations of data we've read:
			//	Maybe we got part of the message length.
			//	Maybe we got the whole message length.
			//	Maybe we got some of the message header as well.
			//	Maybe the whole message header.
			//	Maybe part of the message payload.
			//	Maybe we got a length, header, and payload, and some of the next message.
			// In any case, we need to handle all of these by this same method.

			// Promote our connection status if newly opened.
			this.PromoteStatus_from_NewlyOpen_to_Open();

			// Update our received byte counter.
			this._received_byte_count = this._received_byte_count + Result;

			// Update our last received timestamp.
			this._last_received_timestampUTC = System.DateTime.UtcNow;
			this._metrics.Last_Received_Message_Time = System.DateTime.UtcNow;

			// See if we've received a message length yet.
			if (this._currentmessagelength == -1)
			{
				// We have not marked a received message length yet.
				// Check if we have enough data for it.

				// Set the state that we're looking for the length...
				this.frameread_section = 1;

				this.Logger?.Debug(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"No message length received yet.");

				if (this._received_byte_count < 4)
				{
					// Not enough yet for a message length.
					// We need to start a new begin read to pull some more data.

					// Set the state that we're looking for the length...
					this.frameread_section = 1;

					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Not enough received for a message length.");

					// Queue off another read.
					// Set the number of expected bytes to 4, so we get a full message length field.
					this._number_of_expected_bytes = 4;

					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Queueing another Async read...");

					Result = this.Queue_Async_Read();

					// See if an error occurred.
					if (Result < 0)
					{
						// An error occurred.

						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Error occurred trying to queue an async read. Result=" + Result.ToString() + ".");
						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Closing down the message endpoint.");

						// Sent a bad state.
						this.UpdateState(eLoop_ConnectionStatus.Closed);

						// Disconnect the client.
						this.CloseDown();

						// Call the handler for a connection gone bad, in case it is hooked up.
						this.Call_del_dwent_bad();

						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Endpoint closed down.");

						return;
					}
					// We queued off the initial read operation to wait for data.

					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Another Async read was queued to read in rest of message length parameter.");

					return;
				}
				else if (this._received_byte_count == 4)
				{
					// We have exactly enough data to parse the message length parameter.

					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"We have exactly enough data to parse the message length parameter.");
					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Attempting to deserialize the the message length.");

					// Get a local reference to the receive buffer that we can pass around by reference.
					byte[] buf = this._buffer.Buffer;

					// Parse out the message length.
					Resulta = cCustom_Serializer.Deserialize_Integer32(ref buf, 0, out tempint);

					// See if an error occurred.
					if (Result < 0)
					{
						// An error occurred.

						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Error occurred trying to deserialize the message length. Result=" + Result.ToString() + ".");
						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Closing down the message endpoint.");

						// Sent a bad state.
						this.UpdateState(eLoop_ConnectionStatus.Error);

						// Disconnect the client.
						this.CloseDown();

						// Call the handler for a connection gone bad, in case it is hooked up.
						this.Call_del_dwent_bad();

						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Endpoint closed down.");

						return;
					}
					// We received the message length parameter.

					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"We have deserialized the the message length.");

					// Do a special case check here in case the message length is zero.
					if (tempint == 0)
					{
						// The sender gave us an empty frame.
						// We regard this as a simple ping (or keepalive).
						// A zero-length message is regarded as a simple ping (or keep-alive) from the other end.
						// We will give no reply for this message.
						// Since neither the sender or us threw an exception, we know the connection is still open.
						// We capture this ping type here and return.
						// There is another full ping/pong mechanism we will use as well.

						this.Logger?.Debug(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Message length was zero. This is a special ping we need to process as such.");

						// We need to update our last message received time here.
						this._last_received_timestampUTC = System.DateTime.UtcNow;
						this._metrics.Last_Received_Message_Time = System.DateTime.UtcNow;
						this._metrics.Received_Message_Count++;

						// We successfully processed the keepalive message from the sender.
						// We can get ready to read another message from the socket.

						// Reset buffer pointers for a new message...
						Reset_Buffer_Pointers();

						this.Logger?.Debug(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"We processed a zero-length message, and treated it as a simple ping.");

						this.Logger?.Trace(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Queueing another Async read...");

						Result = this.Queue_Async_Read();

						// See if an error occurred.
						if (Result < 0)
						{
							// An error occurred.

							this.Logger?.Error(
								$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
								"Error occurred trying to queue an async read. Result=" + Result.ToString() + ".");
							this.Logger?.Error(
								$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
								"Closing down the message endpoint.");

							// Sent a bad state.
							this.UpdateState(eLoop_ConnectionStatus.Closed);

							// Disconnect the client.
							this.CloseDown();

							// Call the handler for a connection gone bad, in case it is hooked up.
							this.Call_del_dwent_bad();

							this.Logger?.Error(
								$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
								"Endpoint closed down.");

							return;
						}
						// We queued off a subsequent read operation to wait for a new message.

						this.Logger?.Trace(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Another Async read was queued to read the next message.");

						return;
					}

					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Message length is non-zero.");
					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Doing a sanity check for the received message length. MessageLength=" + tempint.ToString() + ".");

					// We need to give it a sanity check to make sure it's not too big.
					if (tempint > this.MaxMessageSize)
					{
						// The message length is too large or too small for a legitimate message.
						// We will regard this as an error and close out the connection.

						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Message length failed the sanity check for size. MessageLength=" + tempint.ToString() + ".");
						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Closing down the message endpoint.");

						// Sent a bad state.
						this.UpdateState(eLoop_ConnectionStatus.Error);

						// Disconnect the client.
						this.CloseDown();

						// Call the handler for a connection gone bad, in case it is hooked up.
						this.Call_del_dwent_bad();

						this.Logger?.Debug(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Endpoint closed down.");

						return;
					}
					// The message length is valid to use.

					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Message length passed the sanity check for size, and is good to use.");

					// Accept the message length.
					this._currentmessagelength = tempint;

					// Now, we need to queue off another read to pull in the message header and payload data.
					this._number_of_expected_bytes = this._currentmessagelength + cCustom_Serializer.size_of_Int32;

					// We need to check the receive buffer size if it's large enough.
					this.Resize_Buffer_if_Needed(this._number_of_expected_bytes);

					this.Logger?.Debug(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Queueing another Async read to receive the message header and payload.");

					// Set the state that we're looking for the data section...
					this.frameread_section = 2;

					// Make the call to setup another read.
					Result = this.Queue_Async_Read();

					// See if an error occurred.
					if (Result < 0)
					{
						// An error occurred.

						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Error occurred trying to queue an async read. Result=" + Result.ToString() + ".");
						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Closing down the message endpoint.");

						// Sent a bad state.
						this.UpdateState(eLoop_ConnectionStatus.Error);

						// Disconnect the client.
						this.CloseDown();

						// Call the handler for a connection gone bad, in case it is hooked up.
						this.Call_del_dwent_bad();

						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Endpoint closed down.");

						return;
					}
					// We queued off a subsequent read operation to wait for more.

					this.Logger?.Info(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Another Async read was queued to read in the message frame.");

					return;
				}
				else
				{
					// We have more than enough for the message length.
					// Currently, this is a logic flaw as we hard set the desired read to four bytes for a new message.

					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Closing down the message endpoint because we received more than the expected number of bytes for a new message length.");

					// Sent a bad state.
					this.UpdateState(eLoop_ConnectionStatus.Error);

					// Regard this as an error and leave.
					// Disconnect the client.
					this.CloseDown();

					// Call the handler for a connection gone bad, in case it is hooked up.
					this.Call_del_dwent_bad();

					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Endpoint closed down.");

					return;
				}
			}
			else
			{
				// The message length is known.
				// We need to see if we have the entire message or not.
				// If so, we will begin processing it.
				// If not, we will queue off another read for the rest of it.

				this.Logger?.Debug(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Message length is known.");

				// See if what we have received so far is less than what we expect.
				if (this._received_byte_count < this._number_of_expected_bytes)
				{
					// We have not received all of the message.
					// We've already set the number of expected bytes to read.
					// So, we only need to queue off another read.

					this.Logger?.Debug(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Not all message frame has been received.");

					this.Logger?.Debug(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Queueing another Async read...");

					// Set the state that we're looking for the data section...
					this.frameread_section = 2;

					Result = this.Queue_Async_Read();

					// See if an error occurred.
					if (Result < 0)
					{
						// An error occurred.

						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Error occurred trying to queue an async read. Result=" + Result.ToString() + ".");
						this.Logger?.Error(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Closing down the message endpoint.");

						// Sent a bad state.
						this.UpdateState(eLoop_ConnectionStatus.Closed);

						// Disconnect the client.
						this.CloseDown();

						// Call the handler for a connection gone bad, in case it is hooked up.
						this.Call_del_dwent_bad();

						this.Logger?.Info(
							$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
							"Endpoint closed down.");

						return;
					}
					// We queued off a subsequent read operation to wait for more.

					this.Logger?.Debug(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Another Async read was queued to read the rest of the message header and payload.");

					return;
				}
				// We have received enough message content to process the message.

				this.Logger?.Trace(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"We have received enough message content to process the message.");
				this.Logger?.Trace(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Attempting to process the received message.");

				// Process the received message waiting in the buffer.
				Result = this.Process_Received_MessageBuffer();

				// See if an error occurred.
				if (Result < 0)
				{
					// An error occurred.

					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Error occurred trying to process received messagebuffer. Result=" + Result.ToString() + ".");
					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Closing down the message endpoint.");

					// Sent a bad state.
					this.UpdateState(eLoop_ConnectionStatus.Closed);

					// Disconnect the client.
					this.CloseDown();

					// Call the handler for a connection gone bad, in case it is hooked up.
					this.Call_del_dwent_bad();

					this.Logger?.Info(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Endpoint closed down.");

					return;
				}
				// We successfully processed the received message, and published it.
				// We can get ready to read another message from the socket.

				// Reset buffer pointers for a new message...
				Reset_Buffer_Pointers();

				this.Logger?.Trace(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
					"Received message was processed.");

				// Do effort to queue another read.
				if (messagecount > 0)
				{
					// Log that we are calling read async for the next message.
					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Queueing another Async read to receive the next message.");
				}
				else
				{
					// Log that we are calling read async for the rest of the current message.
					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Queueing another Async read to receive the rest of the message.");
				}

				// Make the call to setup another read.
				Result = this.Queue_Async_Read();

				// See if an error occurred.
				if (Result < 0)
				{
					// An error occurred.

					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Error occurred trying to queue an async read. Result=" + Result.ToString() + ".");
					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Closing down the message endpoint.");

					// Sent a bad state.
					this.UpdateState(eLoop_ConnectionStatus.Closed);

					// Disconnect the client.
					this.CloseDown();

					// Call the handler for a connection gone bad, in case it is hooked up.
					this.Call_del_dwent_bad();

					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Endpoint closed down.");

					return;
				}
				// We queued off a subsequent read operation to wait for more.

				if (messagecount > 0)
				{
					// Log that we are calling read async for the next message.
					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Another Async read was queued to read the next message.");
				}
				else
				{
					// Log that we are calling read async for the rest of the current message.
					this.Logger?.Trace(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(CALLBACK_Receive_Read_Data)} - " +
						"Another Async read was queued to read the rest of the message.");
				}

				return;
			}
		}

        /// <summary>
        /// Private method to handle a complete message received from the wire.
        /// It will recover the message from the buffer, and send it along for processing.
        /// Returns 1 for success.
        /// Returns negatives for error.
        /// </summary>
        /// <returns></returns>
        private int Process_Received_MessageBuffer()
		{
			int bytepointer = 0;
			int payloadlength = 0;

			this.Logger?.Trace(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_Received_MessageBuffer)} - " +
				"Processing a received message buffer.");

			// At this point, we have received a message that we need to recover from the buffer.
			// We will first retrieve the message length from the buffer.
			// This will tell us how much data to convert.
			// Then, we will convert those bytes to string, and deserialize that from json to message envelope.

			// We have the entire message frame in a single buffer.
			// The buffer holds the message size and payload.

			// We have already pulled out the message length.
			// So, advance the buffer pointer by the field size of the message length value.
			bytepointer = bytepointer + cCustom_Serializer.size_of_Int32;

			// Calculate how much of the buffer is occupied by the payload.
			payloadlength = this._currentmessagelength;

			// Convert the payload bytes to a json string...
			var rawmsg = Encoding.UTF8.GetString(this._buffer.Buffer, bytepointer, payloadlength);
			if(string.IsNullOrEmpty(rawmsg))
			{
				// We failed to recover the message and envelope.

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_Received_MessageBuffer)} - " +
					"Failed to recover the raw message from the receive buffer.");

				return -3;
			}
			// We have the message and envelope.
			// We can dispatch it for further processing by the application.

			this.Logger?.Trace(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_Received_MessageBuffer)} - " +
				"Message recovered from receive buffer.");

			// Increment the read message counter.
			this._metrics.Received_Message_Count++;
			this._metrics.Last_Received_Message_Time = System.DateTime.Now;

			this.Logger?.Trace(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_Received_MessageBuffer)} - " +
				"Read message count is now=" + this._metrics.Received_Message_Count.ToString() + ".");

			this.Logger?.Trace(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(Process_Received_MessageBuffer)} - " +
				"Calling the dispatch method...");

			// Pass the message to the delegate...
			// We do this by firing a delegate that our owner gave us.
			if(this._del_message_received != null)
			{
				// Tell the owner that a message has arrived.
				try
				{
					this._del_message_received(this, rawmsg);
				}
				catch (Exception) { }
			}

			this.Logger?.Info(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(CloseDown)} - " +
				"Received Message dispatched for processing.");

			// Return success to the caller.
			return 1;
		}

		#endregion


		#region Buffer Handling

		protected void Resize_Buffer_if_Needed(int needed_size)
		{
			this.Logger?.Trace(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(CloseDown)} - " +
				"Resizing buffer.");

			if (this._buffer == null)
			{
				this.Logger?.Trace(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(CloseDown)} - " +
					"Creating buffer for first time.");

				this._buffer = new cBuffer();
			}
			// The buffer exists.

			this._buffer.Resize_Buffer_if_Needed(needed_size);

			this.Logger?.Trace(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(CloseDown)} - " +
				"Buffer has been resized.");

			// The buffer is adequately sized now.
			return;
		}

		private void Reset_Buffer_Pointers()
		{
			// Reset our buffer offset and receive progress.
			this._received_byte_count = 0;
			// We want a length count first, so just four bytes please.
			this._number_of_expected_bytes = 4;
			// Reset the message length parameter.
			this._currentmessagelength = -1;

			// Set the search state to frame length...
			this.frameread_section = 1;
		}

		#endregion


		#region Delegate Callback Handling

        private void Call_del_dwent_bad()
        {
			try
			{
				if (this._del_dwent_bad != null)
				{
					this.Logger?.Debug(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(Call_del_dwent_bad)} - " +
						"Calling the went bad delegate...");

					// The delegate exists.
					this._del_dwent_bad(this);
				}
			}
			catch (Exception) { }
        }

		#endregion


		#region Status Change Methods

		protected void UpdateState(eLoop_ConnectionStatus newstate)
		{
			UpdateState(newstate, true);
		}
		protected void UpdateState(eLoop_ConnectionStatus newstate, bool publish_change)
		{
			string state_change_string = "";

			if (this.State == newstate)
			{
				// No change.
				return;
			}

			// Also, we will not allow changes from aborted or completed, except to retired.
			// We will consider these as terminal states.
			if (this.State == eLoop_ConnectionStatus.Closed)
			{
				// No state change allowed from here.

				// For logging purposes, we will ignore attempts to change to Error, Lost, or Shutting_Down.
				if(newstate == eLoop_ConnectionStatus.Shutting_Down ||
					newstate == eLoop_ConnectionStatus.Error ||
					newstate == eLoop_ConnectionStatus.Lost)
				{
					// We will not log this prevented transition.
					return;
				}

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
					"State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");

				return;
			}
			if (this.State == eLoop_ConnectionStatus.Lost)
			{
				// No state change allowed from here.

				// For logging purposes, we will ignore attempts to change to Error, Closed, or Shutting_Down.
				if(newstate == eLoop_ConnectionStatus.Shutting_Down ||
					newstate == eLoop_ConnectionStatus.Closed ||
					newstate == eLoop_ConnectionStatus.Lost)
				{
					// We will not log this prevented transition.
					return;
				}

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
					"State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");
				return;
			}
			if (this.State == eLoop_ConnectionStatus.Error)
			{
				// No state change allowed from here.

				// For logging purposes, we will ignore attempts to change to Lost, Closed, or Shutting_Down.
				if(newstate == eLoop_ConnectionStatus.Shutting_Down ||
					newstate == eLoop_ConnectionStatus.Closed ||
					newstate == eLoop_ConnectionStatus.Lost)
				{
					// We will not log this prevented transition.
					return;
				}

				this.Logger?.Error(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
					"State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");
				return;
			}
			// The state is changing.

			// Validate transitions...
			if(this.State == eLoop_ConnectionStatus.Initialized)
			{
				if(newstate != eLoop_ConnectionStatus.Newly_Opened &&
					newstate != eLoop_ConnectionStatus.Error)
				{
					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
						"State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");
					return;
				}
			}
			if(this.State == eLoop_ConnectionStatus.Newly_Opened)
			{
				if(newstate != eLoop_ConnectionStatus.Open &&
					newstate != eLoop_ConnectionStatus.Error &&
					newstate != eLoop_ConnectionStatus.Lost &&
					newstate != eLoop_ConnectionStatus.Shutting_Down)
				{
					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
						"State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");
					return;
				}
			}
			if(this.State == eLoop_ConnectionStatus.Open)
			{
				if(newstate != eLoop_ConnectionStatus.Lost &&
					newstate != eLoop_ConnectionStatus.Error &&
					newstate != eLoop_ConnectionStatus.Shutting_Down)
				{
					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
						"State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");
					return;
				}
			}
			if(this.State == eLoop_ConnectionStatus.Shutting_Down)
			{
				if(newstate != eLoop_ConnectionStatus.Closed)
				{
					this.Logger?.Error(
						$"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
						"State change attempted but prevented, from, " + this.State.ToString() + ", to, " + newstate.ToString() + ".");
					return;
				}
			}

			// Create the state change string that we will pass along.
			state_change_string = "Status changed from " + this.State.ToString() + " to " + newstate.ToString() + ".";

			// Capture the new state.
			this.State = newstate;

			this.Logger?.Debug(
				$"{_classname}:{this.InstanceId.ToString()}::{nameof(UpdateState)} - " +
				state_change_string);

			if (publish_change == false)
			{
				// We are not to publish status changes.
				// This is usually because we are being closed down by an external owner who already knows what's up with us.
				return;
			}

			// Call the status change handler if registered.
			if (this._del_Status_Change != null)
			{
				// Call the status change handler.
				try
				{
					this._del_Status_Change(this, state_change_string);
				}
				catch (Exception) { }
			}
		}
		protected void PromoteStatus_from_NewlyOpen_to_Open()
		{
			if (this.State == eLoop_ConnectionStatus.Newly_Opened)
			{
				this.Logger?.Info(
					$"{_classname}:{this.InstanceId.ToString()}::{nameof(PromoteStatus_from_NewlyOpen_to_Open)} - " +
					"Promoting status from Newly Open to Open.");

				this.UpdateState(eLoop_ConnectionStatus.Open);
			}
		}

		#endregion
    }
}
