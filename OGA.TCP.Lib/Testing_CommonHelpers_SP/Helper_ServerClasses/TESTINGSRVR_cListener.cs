using System;
using System.Collections.Generic;
using System.Text;

namespace Testing_CommonHelpers_SP.Helpers
{
    /// <summary>
    /// NOT FOR PRODUCTION USE.
    /// THIS IS A COPY OF cListener, INTENDED TO REPLICATE SERVER-SIDE FUNCTIONALITY FOR CLIENT SIDE LIBRARY TESTS.
    /// TESTING HELPER CLASS.
    /// IS A COPY OF cLISTENER.cs
    /// </summary>
    public class TESTINGSRVR_cListener
    {
        #region Private Fields

        private volatile int _listenerID;
        static private volatile int _listener_count;

        private volatile int _spawned_connection_count;

        /// <summary>
        /// Listener reference
        /// </summary>
        private System.Net.Sockets.TcpListener _listener_ref;

        private volatile eListenerState _state;

        /// <summary>
        /// Listening port for the listener instance.
        /// </summary>
        private int _listening_port;
        /// <summary>
        /// Listening address for the listener instance.
        /// </summary>
        private System.Net.IPAddress _listening_ip;

        #endregion


        #region Public Properties

        // NoDelay disables nagle algorithm. lowers CPU% and latency but
        // increases bandwidth
        public bool NoDelay = true;

        // Send would stall forever if the network is cut off during a send, so
        // we need a timeout (in milliseconds)
        public int SendTimeout = 5000;

        public int Spawned_Connection_Count
        {
            get
            {
                return _spawned_connection_count;
            }
        }

        /// <summary>
        /// Local ID of the listener.
        /// This integer value starts at one and increments for each listener created.
        /// </summary>
        public int ListenerID
        {
            get
            {
                return this._listenerID;
            }
        }

        public eListenerState State
        {
            get
            {
                return _state;
            }
        }

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

        public delegate void dNew_Client_Connection(TESTINGSRVR_cListener l, System.Net.Sockets.TcpClient newclient);
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

        public delegate void dStatus_Change(TESTINGSRVR_cListener l, string statusupdate);
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

        public TESTINGSRVR_cListener()
        {
            this._state = eListenerState.Initialized;
            this._del_new_client_connection = null;
            this._listener_ref = null;
            this._listening_ip = null;
            this._listening_port = -1;

            this.Assign_Next_ListenerID();

            this._spawned_connection_count = 0;
        }

        ~TESTINGSRVR_cListener()
        {
            if (this._state != eListenerState.Closed ||
                this._state != eListenerState.Error ||
                this._state != eListenerState.ShuttingDown)
            {
                // The listener is not shutdown.
                this.CloseDown_Listener();
            }

            this._del_new_client_connection = null;
            this._listener_ref = null;
            this._listening_ip = null;
            this._listening_port = 0;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates a thread that runs the listener, and blocks on the accept connection.
        /// </summary>
        /// <returns></returns>
        public int Start_Listener()
        {
            int Result = 0;

            // Check that the listener is not already started.
            if (this._state != eListenerState.Initialized)
            {
                // The listener is not in the correct state to be started.
                // It could already be started, or in error, or is already closed down.
                // Either way, we cannot start it.

                return -4;
            }
            // The listener is initialized and ready to startup.

            // Log a message here.
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Starting Listener.");

            // Activate the listener on the desired IP and port.
            Result = this.Activate_Listener();

            if (Result < 0)
            {
                // An error occurred while activating the listener.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error("An error occurred while activating the listener.");

                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                return Result;
            }
            // Listener instance was activated.
            // This means we have a valid IP address and port.

            // Check that we were given a callback to use as well for connections.
            // Without one, we have no way of dealing with an actual connection made.
            if (this._del_new_client_connection == null)
            {
                // No callback was set.
                // This means that we have nothing todo when a connection actually occurs.
                // This means we serve no purpose, and a logic flaw (outside this class exists).

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    "No connection callback delegate was configured for the listener.");

                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                return -5;
            }
            // If here, we have all that we need for the listener to operation: IP, port, and callback.

            // Log a message here.
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Active Listener created.");

            // Make the call to setup an async connect operation.
            Result = this.Perform_ClientConnect_Async();

            if (Result < 0)
            {
                // An error occurred while setting up the listener's async callback handler.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error("An error occurred while setting up the listener's async callback handler.");

                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                return -6;
            }

            // Log the active listener.
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Active listener's async callback handler armed and waiting for client connections.");

            // Set the listener to the active state.
            Change_Status(eListenerState.Active);

            // We created an async connect method that will wait for a client.
            return 1;
        }

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
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug("Shutting down listener...");

                if (this._listener_ref != null)
                {
                    this._listener_ref.Stop();

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        "Listener closed.");
                }

                // Set the listener to the closed state.
                if (this._state != eListenerState.Error)
                {
                    // Set the listener to the active state.
                    Change_Status(eListenerState.Closed);
                }

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug("Listener is closed.");
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(se,
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

        private void Assign_Next_ListenerID()
        {
            // Increment the endpoint counter.
            TESTINGSRVR_cListener._listener_count++;

            // Assign the unique ID to the endpoint.
            this._listenerID = TESTINGSRVR_cListener._listener_count;
        }

        private void Change_Status(eListenerState newstate)
        {
            this.Change_Status(newstate, true);
            return;
        }

        private void Change_Status(eListenerState newstate, bool publish_change)
        {
            string state_change_string = "";

            if (this._state == newstate)
            {
                // No change.
                return;
            }
            // The state is changing.

            // Create the state change string that we will pass along.
            state_change_string = "Status changed from " + this._state.ToString() + " to " + newstate.ToString() + ".";

            // Capture the new state.
            this._state = newstate;

            if (publish_change == false)
            {
                // We are not to publish status changes.
                // This is usually because we are being closed down by an external owner who already knows what's up with us.
                return;
            }

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug("Listener " + state_change_string);

            // Call the status change handler if registered.
            if (this._del_Status_Change != null)
            {
                // Call the status change handler.
                this._del_Status_Change(this, state_change_string);
            }
        }

        private int Perform_ClientConnect_Async()
        {
            try
            {
                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Registering a callback to wait for client connections.");

                // Register a callback for when a client connects.
                IAsyncResult ar = this._listener_ref.BeginAcceptTcpClient(this.Accept_Callback, this._listener_ref);

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Registered a callback to wait for client connections.");

                // At this point, we have registered a callback that will handle a connected tcp client.
                // We can leave.

                return 1;
            }
            catch (System.ObjectDisposedException ode)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ode,
                    "An exception was caught while attempting to register a callback to wait for client connections.");

                return -1;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(se,
                    "An exception was caught while attempting to register a callback to wait for client connections.");

                return -2;
            }
            catch (Exception e)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    "An exception was caught while attempting to register a callback to wait for client connections.");

                return -3;
            }
        }

        private void Accept_Callback(System.IAsyncResult ar)
        {
            int Result = 0;
            System.Net.Sockets.TcpListener listener = null;
            System.Net.Sockets.TcpClient client = null;

            try
            {
                // See if we are closed or in error.
                if (this._state == eListenerState.Closed ||
                    this._state == eListenerState.Error)
                {
                    // Our listener has been closed or is in error.
                    // We cannot accept any further connections.
                    // If we are in this method, it is because we had an oustanding beginaccept while the listener was closed or put into error.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        "Accept callback was handled while listener is already closed or in error. Not accepting further connections.");

                    // Close the listener.
                    this.CloseDown_Listener();

                    return;
                }
                // Get a reference to the TCP listener.
                listener = (System.Net.Sockets.TcpListener)ar.AsyncState;

                // Declare a client reference and get the passed back client instance.
                client = listener.EndAcceptTcpClient(ar);

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("A client connected!");

                // We received a valid client connection, and need to publish it.

                // We need to publish the client instance.
                // Publish the client if we have a delegate to handle it.
                if (this._del_new_client_connection != null)
                {
                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        "Publishing the connected client to the registered delegate.");

                    this._del_new_client_connection(this, client);
                }
                else
                {
                    // We have no delegate to call to publish the connected client.
                    // We will regard this as a failure, and put the listener into an error state.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                        "No delegate configured to publish connected clients to... Placing the listener into error.");

                    // Set the listener to the error state.
                    Change_Status(eListenerState.Error);

                    // Close the listener.
                    this.CloseDown_Listener();

                    return;
                }

                // See if we are closed or in error.
                if (this._state == eListenerState.Closed ||
                    this._state == eListenerState.Error)
                {
                    // Our listener has been closed or is now in error.
                    // We cannot accept any further connections.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                        "We have just published our last connection, and are not accepting anymore.");

                    // Close the listener.
                    this.CloseDown_Listener();

                    return;
                }
                // If here, we have spawned a connection.

                // Increment the spawned connection counter.
                this._spawned_connection_count++;

                // We are to accept another client connection.
                Result = this.Perform_ClientConnect_Async();

                if (Result < 0)
                {
                    // An error occurred.

                    // Log a message here.
                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
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

                // Close the listener.
                this.CloseDown_Listener();

                return;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // Not sure what exception occurred here.

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(se,
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

        private int Activate_Listener()
        {
            try
            {
                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                    "Creating new listener.");

                // Create an endpoint instance that we will pass to the socket listener.
                System.Net.IPEndPoint localEndPoint = new System.Net.IPEndPoint(this._listening_ip, this._listening_port);

                // Create a TCP listener instance.
                System.Net.Sockets.TcpListener listener = new System.Net.Sockets.TcpListener(localEndPoint);

                listener.Server.NoDelay = NoDelay;
                listener.Server.SendTimeout = SendTimeout;

                // Set a connection queue size of 10.
                listener.Start(10);

                // Set the configured tcp listener as our listener.
                this._listener_ref = listener;

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Info("Listener is active.");

                // Set the listener to the active state.
                Change_Status(eListenerState.Active);

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
                        "A Listener is already on the same port and socket. Cannot open second listener instance.");

                    // Set the listener to the error state.
                    Change_Status(eListenerState.Error);

                    return -2;
                }

                // Log a message here.
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    "Encountered an exception trying to activate the listener instance.");

                // Set the listener to the error state.
                Change_Status(eListenerState.Error);

                return -1;
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
