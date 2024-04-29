using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;

namespace OGA.TCP.Server
{
    /// <summary>
    /// Binds to an IP address and port and listens for client connections.
    /// Spawns a new connection for each connected client.
    /// Each instance of this class can listen on one IP/port combination.
    /// You must create a second instance to listen to a different port or IP address.
    /// </summary>
    public class cListener : IDisposable
    {
        #region Private Fields

        private string _classname;

        private volatile int _listenerID;
        static private volatile int _instance_counter;

        /// <summary>
        /// Listener reference
        /// </summary>
        private System.Net.Sockets.TcpListener _listener_ref;

        /// <summary>
        /// Listening port for the listener instance.
        /// </summary>
        private int _listening_port;
        /// <summary>
        /// Listening address for the listener instance.
        /// </summary>
        private System.Net.IPAddress _listening_ip;

        /// <summary>
        /// Send would stall forever if the network is cut off during a send, so we need a timeout (in milliseconds).
        /// </summary>
        private int _sendtimeout = 5000;

        private bool disposedValue;

        #endregion


        #region Public Properties

        /// <summary>
        /// NoDelay disables nagle algorithm. lowers CPU% and latency but increases bandwidth.
        /// </summary>
        public bool NoDelay { get; set; } = true;

        /// <summary>
        /// Send would stall forever if the network is cut off during a send, so we need a timeout (in milliseconds).
        /// </summary>
        public int SendTimeout
        {
            get => this._sendtimeout;
            set
            {
                if (value < 1000)
                    _sendtimeout = 1000;
                _sendtimeout = value;
            }
        }

        /// <summary>
        /// Number of created connections by this listener.
        /// </summary>
        public int Spawned_Connection_Count { get; private set; }

        /// <summary>
        /// Local ID of the listener.
        /// This integer value starts at one and increments for each listener created.
        /// </summary>
        public int InstanceId { get; private set; }

        /// <summary>
        /// Current listener state.
        /// </summary>
        public eListenerState State { get; private set; }

        /// <summary>
        /// Listening port for the server instance.
        /// </summary>
        public int Listening_Port
        {
            get
            {
                return this._listening_port;
            }
            set
            {
                this._listening_port = value;
            }
        }

        /// <summary>
        /// Listening address for the server instance.
        /// </summary>
        public System.Net.IPAddress Listening_IP
        {
            get
            {
                return this._listening_ip;
            }
            set
            {
                this._listening_ip = value;
            }
        }

        #endregion


        #region Delegates and Handlers

        public delegate void dNew_Client_Connection(cListener l, System.Net.Sockets.TcpClient newclient);
        private dNew_Client_Connection _del_new_client_connection;
        /// <summary>
        /// Assign a handler to this delegate if the listener does not create sessions for each connecting client.
        /// </summary>
        public dNew_Client_Connection OnNew_Client_Connection
        {
            set
            {
                this._del_new_client_connection = value;
            }
        }

        public delegate void dStatus_Change(cListener l, string statusupdate);
        private dStatus_Change _del_Status_Change;

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

        #endregion


        #region ctor / dtor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public cListener()
        {
            _instance_counter++;
            this.InstanceId = _instance_counter;

            this._classname = nameof(cListener);

            this.State = eListenerState.Initialized;
            this._del_new_client_connection = null;
            this._listener_ref = null;
            this._listening_ip = null;
            this._listening_port = -1;

            this.Spawned_Connection_Count = 0;
        }

        /// <summary>
        /// TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        /// </summary>
        ~cListener()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        /// <summary>
        /// Local dispose method.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // The listener is not shutdown.
                this.CloseDown_Listener();

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        /// <summary>
        /// Public dispose method.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Starts the listener.
        /// This call does not block. It creates a thread that runs the listener, and blocks on the accept connection.
        /// </summary>
        /// <returns></returns>
        public int Start_Listener()
        {
            // Ensure we are not already disposed...
            if(this.disposedValue)
            {
                // The listener is already disposed.
                // We cannot start it again.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Start_Listener)} - " +
                    $"Already disposed. Cannot start.");

                return -1;
            }
            // Check that the listener is not already started.
            if (this.State != eListenerState.Initialized)
            {
                // The listener is not in the correct state to be started.
                // It could already be started, or in error, or is already closed down.
                // Either way, we cannot start it.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Start_Listener)} - " +
                    $"Not in Initialized state. Cannot start.");

                return -2;
            }
            // The listener is initialized and ready to startup.

            // Verify we were given a callback to use as well for connections...
            // Without one, we have no way of dealing with an actual connection made.
            if (this._del_new_client_connection == null)
            {
                // No callback was set.
                // This means that we have nothing todo when a connection actually occurs.
                // This means we serve no purpose, and a logic flaw (outside this class exists).

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Start_Listener)} - " +
                    $"No connection callback delegate was configured for the listener. Cannot start.");

                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                return -3;
            }
            // If here, we have all that we need for the listener to operation: IP, port, and callback.

            // Log a message here.
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(Start_Listener)} - " +
                $"Starting Listener...");

            // Activate the listener on the desired IP and port.
            var res1 = this.Activate_Listener();
            if (res1 < 0)
            {
                // An error occurred while activating the listener.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Start_Listener)} - " +
                    "An error occurred while activating the listener. Cannot start.");

                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                return res1;
            }
            // Listener instance was activated.
            // This means we have a valid IP address and port.


            // Log a message here.
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(Start_Listener)} - " +
                "Active Listener created.");

            // Make the call to setup an async connect operation.
            var res2 = this.Perform_ClientConnect_Async();
            if (res2 < 0)
            {
                // An error occurred while setting up the listener's async callback handler.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Start_Listener)} - " +
                    "An error occurred while setting up the listener's async callback handler. Cannot start.");

                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                return -4;
            }

            // Log the active listener.
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(Start_Listener)} - " +
                "Active listener's async callback handler armed and waiting for client connections.");

            // Set the listener to the active state.
            Change_Status(eListenerState.Active);

            // We created an async connect method that will wait for a client.
            return 1;
        }

        /// <summary>
        /// Public call to close down the listener.
        /// This is also called during disposal.
        /// </summary>
        public void CloseDown_Listener()
        {
            // DISABLED THIS SHUTDOWN CALL AS IT DOESN'T APPLY TO A LISTENING SOCKET...
            //try
            //{
            //    this._listener_socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);

            //    // Log a message here.
            //    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
            //        "Listener traffic stopped.");
            //}
            //catch (System.Net.Sockets.SocketException se)
            //{
            //    // Log a message here.
            //    OGA.SharedKernel.Logging_Base.Logger_Ref?.ErrorException(
            //        "Attempted to stop traffic on the listener, but encountered an exception.", se);
            //}
            try
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(CloseDown_Listener)} - " +
                    "Shutting down listener...");

                if (this._listener_ref != null)
                {
                    try
                    {
                        this._listener_ref?.Stop();
                    } catch(Exception) { }
                    this._listener_ref = null;

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(CloseDown_Listener)} - " +
                        "Listener closed.");
                }

                // Set the listener to the closed state.
                if (this.State != eListenerState.Error)
                {
                    // Set the listener to the active state.
                    Change_Status(eListenerState.Closed);
                }

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(CloseDown_Listener)} - " +
                    "Listener is closed.");
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(se,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(CloseDown_Listener)} - " +
                    "Attempted to close down the listener, but encountered an exception.");
            }
            finally
            {
                this._del_new_client_connection = null;
                this._del_Status_Change = null;
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Call each time the listener is ready to accept a new connection.
        /// </summary>
        /// <returns></returns>
        private int Perform_ClientConnect_Async()
        {
            try
            {
                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Perform_ClientConnect_Async)} - " +
                    "Registering a callback to wait for client connections.");

                // Register a callback for when a client connects.
                IAsyncResult ar = this._listener_ref.BeginAcceptTcpClient(this.Accept_Callback, this._listener_ref);

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Perform_ClientConnect_Async)} - " +
                    "Registered a callback to wait for client connections.");

                // At this point, we have registered a callback that will handle a connected tcp client.
                // We can leave.

                return 1;
            }
            catch (System.ObjectDisposedException ode)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Perform_ClientConnect_Async)} - " +
                    "An exception was caught while attempting to register a callback to wait for client connections.");

                return -1;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Perform_ClientConnect_Async)} - " +
                    "An exception was caught while attempting to register a callback to wait for client connections.");

                return -2;
            }
            catch (Exception e)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Perform_ClientConnect_Async)} - " +
                    "An exception was caught while attempting to register a callback to wait for client connections.");

                return -3;
            }
        }

        /// <summary>
        /// New Connection callback.
        /// Asynchronously called each time a client connects.
        /// Also, called when the listener is being shutdown.
        /// </summary>
        /// <param name="ar"></param>
        private void Accept_Callback(System.IAsyncResult ar)
        {
            int Result = 0;
            System.Net.Sockets.TcpListener listener = null;
            System.Net.Sockets.TcpClient client = null;

            try
            {
                if(this.disposedValue)
                {
                    // Our listener is already disposed.
                    // This means the callback we are in, has returned, and can be discarded.
                    // Also. The dispose method would have already called, this.CloseDown_Listener().
                    // So, we don't need to do that.
                    // We can simply return.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "Accept callback returned after listener is disposed.");

                    // We won't rearm the listener.
                    // No need to close it down, as we are already disposed.

                    return;
                }
                // See if we are closed, shutting down, or in error.
                if (this.State == eListenerState.Closed ||
                    this.State == eListenerState.ShuttingDown ||
                    this.State == eListenerState.Error)
                {
                    // Our listener has been closed or is in error.
                    // We cannot accept any further connections.
                    // If we are in this method, it is because we had an oustanding beginaccept while the listener was closed or put into error.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "Accept callback was handled while listener is already closed or in error. Not accepting further connections.");

                    // We won't rearm the listener.

                    // Close the listener.
                    this.CloseDown_Listener();

                    return;
                }

                // Check if the received instance is null...
                if(ar?.AsyncState == null)
                {
                    // The received listener is null.
                    // We cannot get a client from a null listener instance.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "Accept callback received null listener. Not accepting further connections.");

                    // We won't rearm the listener.

                    // Close the listener.
                    this.CloseDown_Listener();

                    return;
                }

                // Get a reference to the TCP listener.
                listener = (System.Net.Sockets.TcpListener)ar.AsyncState;

                // Declare a client reference and get the passed back client instance.
                client = listener.EndAcceptTcpClient(ar);

                // Verify the client connection exists...
                if(client == null)
                {
                    // The received connection is null.
                    // We will regard this as a listener closure.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "Accept callback received null listener. Not accepting further connections.");

                    // We won't rearm the listener.

                    // Close the listener.
                    this.CloseDown_Listener();

                    return;
                }
                // Client connection exists.
                // We will attempt to process it.

                // Ensure the client is connected...
                if(!client.Connected)
                {
                    // The client reports it is not connected.
                    // We will not pass along unconnected clients.

                    // Dispose the client...
                    client.Dispose();

                    // We will rearm the listener, below.
                }
                else
                {
                    // The client reports connected.
                    // We will process it.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "A client connected!");

                    // We received a valid client connection, and need to publish it.

                    // We need to publish the client instance.
                    // Publish the client if we have a delegate to handle it.
                    if (this._del_new_client_connection == null)
                    {
                        // We have no delegate to call to publish the connected client.
                        // Since our start method had to have a callback to be successful, our delegate has been removed by external logic.
                        // Or, we are in some partial disposed state.
                        // Either way, we will regard this as a failure, and put the listener into an error state.

                        // Log a message here.
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                            "No delegate configured to publish connected clients to... Placing the listener into error.");

                        // Set the listener to the error state.
                        Change_Status(eListenerState.Error);

                        // Close the listener.
                        this.CloseDown_Listener();

                        return;
                    }
                    // The callback delegate is defined.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "Publishing the connected client to the registered delegate...");

                    // Wrap this in a try-catch to ensure we are not affected by exceptions.
                    try
                    {
                        this._del_new_client_connection(this, client);
                    }
                    catch(Exception e) { }

                    // If here, we have spawned a connection.
                    // And, we have sent a callback for the connection.
                    // Increment the spawned connection counter...
                    this.Spawned_Connection_Count++;
                }

                // Do a sanity check to see if we are closed, shutting down, or in error...
                if (this.State == eListenerState.Closed ||
                    this.State == eListenerState.ShuttingDown ||
                    this.State == eListenerState.Error)
                {
                    // Our listener has been closed or is now in error.
                    // We cannot accept any further connections.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "We have just published our last connection, and are not accepting anymore.");

                    // Close the listener.
                    this.CloseDown_Listener();

                    return;
                }

                // We are to accept another client connection.
                // Setup the listener to receive a new connection...
                Result = this.Perform_ClientConnect_Async();
                if (Result < 0)
                {
                    // An error occurred.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "We have just published our last connection, and are not accepting anymore.");

                    // Close the listener.
                    this.CloseDown_Listener();

                    return;
                }
            }
            catch (System.ObjectDisposedException ode)
            {
                // The listener was disposed already.
                // This means that the listener has been closed while we had an oustanding beginaccept in progress.
                // This happens normally, so we will eat the exception, and close the listener gracefully.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                    "Listener accept callback triggered after instance disposed. Closing down listener...");

                // Close the listener.
                this.CloseDown_Listener();

                return;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(se,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                    "An exception was caught while attempting to register a callback to wait for client connections.");

                // Close the listener.
                this.CloseDown_Listener();

                return;
            }
            catch (Exception e)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                    "An exception was caught while attempting to register a callback to wait for client connections.");

                // Close the listener.
                this.CloseDown_Listener();

                return;
            }
            finally
            {
                // Dereference the connection.
                client = null;
                listener = null;
            }
        }

        /// <summary>
        /// Starts the listener instance.
        /// Will cleanup listeners on failure.
        /// </summary>
        /// <returns></returns>
        private int Activate_Listener()
        {
            bool success = false;

            System.Net.Sockets.TcpListener listener = null;
            try
            {
                if(this.disposedValue)
                {
                    // Our listener is already disposed.
                    // This means the callback we are in, has returned, and can be discarded.
                    // Also. The dispose method would have already called, this.CloseDown_Listener().
                    // So, we don't need to do that.
                    // We can simply return.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "Accept callback returned after listener is disposed.");

                    return -1;
                }

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                    "Creating new listener.");

                // Create an endpoint instance that we will pass to the socket listener.
                System.Net.IPEndPoint localEndPoint = new System.Net.IPEndPoint(this._listening_ip, this._listening_port);

                // Create a TCP listener instance.
                listener = new System.Net.Sockets.TcpListener(localEndPoint);

                listener.Server.NoDelay = NoDelay;
                listener.Server.SendTimeout = _sendtimeout;

                // Set a connection queue size of 10.
                listener.Start(10);

                // Set the configured tcp listener as our listener.
                this._listener_ref = listener;

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                    "Listener is active.");

                // Set the listener to the active state.
                Change_Status(eListenerState.Active);

                // Set the success flag, so we will let the instance be active...
                success = true;

                // Return to the caller.
                return 1;
            }
            catch (Exception e)
            {
                // Exception caught.
                // was probably from an already used port, invalid port number, or unparseable IP address.

                if(e.Message.StartsWith("Only one usage of each socket address"))
                {
                    // A listener is already on the same port/address.

                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                        $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                        "A Listener is already on the same port and socket. Cannot open second listener instance.");

                    // Set the listener to the error state.
                    Change_Status(eListenerState.Error);

                    return -2;
                }

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(Accept_Callback)} - " +
                    "Encountered an exception trying to activate the listener instance.");

                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                return -1;
            }
            finally
            {
                // Do any cleanup if failure occurred...
                if(!success)
                {
                    // We failed to start the listener.

                    // Shut down any listener instances we may have half-created...
                    try
                    {
                        this._listener_ref?.Stop();
                    } catch(Exception) { }
                    this._listener_ref = null;
                    try
                    {
                        listener?.Stop();
                    } catch(Exception) { }
                    listener = null;
                }
            }
        }

        #endregion


        #region Status Handling

        private void Change_Status(eListenerState newstate)
        {
            this.Change_Status(newstate, true);
            return;
        }
        private void Change_Status(eListenerState newstate, bool publish_change)
        {
            string state_change_string = "";

            if (this.State == newstate)
            {
                // No change.
                return;
            }
            // The state is changing.

            // Create the state change string that we will pass along.
            state_change_string = "Status changed from " + this.State.ToString() + " to " + newstate.ToString() + ".";

            // Capture the new state.
            this.State = newstate;

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{_instance_counter.ToString()}::{nameof(Change_Status)} - " +
                "Listener " + state_change_string);

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

        #endregion
    }

    /// <summary>
    /// Listener state
    /// </summary>
    public enum eListenerState
    {
        /// <summary>
        /// Listener is initialized for use.
        /// </summary>
        Initialized = 1,
        /// <summary>
        /// Listener is actively waiting for connections.
        /// </summary>
        Active = 2,
        /// <summary>
        /// Listener is shutting down.
        /// It no longer accepts connections.
        /// </summary>
        ShuttingDown = 3,
        /// <summary>
        /// Listener has been gracefully shutdown.
        /// It no longer accepts connections.
        /// </summary>
        Closed = 4,
        /// <summary>
        /// Listener is in an error state.
        /// It cannot accept connections.
        /// </summary>
        Error
    }
}
