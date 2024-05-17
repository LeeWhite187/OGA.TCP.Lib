using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using OGA.TCP.Messages;
using OGA.TCP.Shared;

namespace OGA.TCP.SessionLayer
{
    /// <summary>
    /// Provides connectivity to a WSHost websocket, creating an easy abstraction for message exchange.
    /// This class has been declared abstract, so usage of it is forced to provide an implementation for determining the websocket connection url.
    /// Implementations of this abstract must override, Get_ConnectionUrl(), with a method that populates the connection url.
    /// Implementations of this abstract may override: Dispose(), IsInternetAvailable(), Determine_AuthToken(), Send_RegistrationMessage(), FireMessageReceivedEvent(), DispatchConnected().
    /// </summary>
    public abstract class WSClient_v1_Abstract : Client_v1_Abstract, IDisposable
    {
        #region Private Fields

        static public string CONST_HTTP_TokenMarker = "Authorization";

        protected System.Net.WebSockets.ClientWebSocket cws;
 
        /// <summary>
        /// Full url of the WSHost listener, this client will connect with.
        /// </summary>
        protected Uri wsconnection_url;

        #endregion


        #region Public Properties

        /// <summary>
        /// Set this to the lowercase short name of the transport: tcp, ws, etc...
        /// </summary>
        override public string TransportShortName { get; } = "ws";

        /// <summary>
        /// Set this to the name of the transport: TCPSocket, Websocket, etc...
        /// </summary>
        override public string TransportLongName { get; } = "WSSocket";

        /// <summary>
        /// Set this to the lowercase string-literal of the libver property that is passed during connection registration.
        /// For websocket clients, this is: "wslibver".
        /// For tcpsocket clients, this is: "tcplibver".
        /// </summary>
        override public string PropName_ClientLibVer { get; } = "wslibver";

        /// <summary>
        /// Auth token string for the client to give the WSHost, at connection time.
        /// </summary>
        public string AuthToken { get; set; }

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

                    if (cws == null)
                        return false;

                    return cws.State == WebSocketState.Open;
                }
                catch(Exception e)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Ignores object state and disposed boolean. Just reports the raw socket being open or not.
        /// This was added as a separate check, just for the socket state, to prevent any glitching from a race condition that may set the disposed flag on an alternate thread.
        /// </summary>
        override public bool TransportIsOpen
        {
            get
            {
                try
                {
                    if (cws == null)
                        return false;

                    return cws.State == WebSocketState.Open;
                }
                catch(Exception e)
                {
                    return false;
                }
            }
        }

        #endregion


        #region ctor / dtor

        /// <summary>
        /// Constructor requires a logger instance.
        /// </summary>
        public WSClient_v1_Abstract(NLog.ILogger logger = null) : base(logger)
        {
            _classname = nameof(WSClient_v1_Abstract);
        }

        #endregion


        #region Connection Management

        /// <summary>
        /// This method is called, each time a new websocket instance is created, to determine what the auth token should be for the connection.
        /// By default, this method uses the auth token in the public properties of the instance.
        /// But, this method can be overridden, in case an implementation requires looking this value up, based on some other rules.
        /// NOTE: This method is called from the Connection Loop's thread. Don't stall it.
        /// </summary>
        /// <returns></returns>
        protected virtual string Determine_AuthToken()
        {
            // By default, return the local auth token property...
            return this.AuthToken ?? "";
        }

        /// <summary>
        /// This is a hook, in the Setup Before Connection logic flow, to provide a call point for determining any dynamic connection info, such as host, port, or url.
        /// This is especially used by websocket clients, whose connection url is determined by server load balancing and region.
        /// For a simple TCP socket client connecting to a static, target server, this method will simply return success (1).
        /// NOTE: This method is called each time the client attempts to connect.
        /// </summary>
        /// <returns></returns>
        override protected async Task<int> Get_ConnectionInfo()
        {
            // For a websocket connection, we will call the get connection url method...
            return await this.Get_ConnectionUrl();
        }

        /// <summary>
        /// Call hook in the Do Setup Before Connecting method, for the transport-specific implementation to create the websocket, tcpclient, etc...
        /// This call is responsible for instantiating the actual tcpclient, websocket, etc.
        /// </summary>
        /// <returns></returns>
        override protected async Task<int> TransportSpecific_CreateNewConnection()
        {
            // Declare the ws instance...
            ClientWebSocket newcws = null;
            newcws = new System.Net.WebSockets.ClientWebSocket();

            // Setup options...
#if (NET452 || NET48)
#else
            newcws.Options.RemoteCertificateValidationCallback += this.CALLBACK_Check_Server_Certificate;
#endif
            newcws.Options.KeepAliveInterval = new TimeSpan(0, 0, 300);


            // Figure out the auth token to use...
            // We call a method, in case there's an implementation that has to do some query or other, to find out the appropriate auth token to use.
            var atk = this.Determine_AuthToken();

            // Add authorization token if we have one...
            if (!string.IsNullOrEmpty(atk))
                newcws.Options.SetRequestHeader(CONST_HTTP_TokenMarker, atk);

            // We are to swap in our new client, here.
            // Copy off the old one, so we can dispose it...
            var oldcws = this.cws;

            // Do a quick copy over, to minimize any transients of other threads seeing the client as inconsistent...
            this.cws = newcws;

            // Dispose of the old one...
            try { oldcws?.Dispose(); } catch (Exception e) { }

            return 1;
        }

        /// <summary>
        /// This abstract method requires an implementation to determine the websocket connection URL.
        /// A couple of implementation possibilities exist, here:
        /// For a fixed websocket connection URL, override this method to assign the url string to 'this.wsconnection_url'.
        /// For connection URLs that are given by a service, such as the WSHost Manager Service,
        ///     override this method with a REST call that submits application data, and receives a url that is pushed into 'this.wsconnection_url'.
        /// </summary>
        /// <returns></returns>
        protected abstract Task<int> Get_ConnectionUrl();

        /// <summary>
        /// Override this method with the transport-specific connect logic.
        /// Return true if successful. False, if not.
        /// </summary>
        /// <returns></returns>
        override protected async Task<bool> TransportSpecific_Connect()
        {
            bool success = false;

            if (cws == null)
                return success;

            // Attempt to start the connection, and handle any error that results...
            await cws.ConnectAsync(wsconnection_url, _cts.Token).ContinueWith(task =>
            {
                // If here, the connect method finished.

                // See how it did...
                if (task.IsFaulted)
                {
                    // An exception occurred while attempting to connect to the websoket service.
                    // We will log and leave.

                    var msg = $"There was an error opening the {(this.TransportLongName?.ToLower() ?? "socket")}, ({this.wsconnection_url.ToString()}). Returning...";

                    // Log the error with exception if possible...
#if (NET452 || NET48)
                    Exception te = null;
#else
                    Exception? te = null;
#endif
                    try
                    {
                        te = task?.Exception?.GetBaseException() ?? null;
                    }
                    catch (Exception) { }
                    if (te != null)
                        this.Logger?.Error(te,
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                            msg);
                    else
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                            msg);
                }
                else
                {
                    // The task did not fault.
                    // So, it may be connected.

                    // Verify that we have a connected state...
                    if(this.cws == null)
                    {
                        // The websocket is null.
                        // We will fall out, below, as not successful.

                        int x = 0;
                    }
                    else if(this.cws.State != System.Net.WebSockets.WebSocketState.Open)
                    {
                        // client does not report connected.

                        var msg = $"The connection attempt failed for ({this.wsconnection_url.ToString()}). Returning...";

                        // Log the error with exception if possible...
#if (NET452 || NET48)
                        Exception te = null;
#else
                        Exception? te = null;
#endif
                        try
                        {
                            te = task?.Exception?.GetBaseException() ?? null;
                        }
                        catch (Exception) { }
                        if (te != null)
                            this.Logger?.Error(te,
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                msg);
                        else
                            this.Logger?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                                msg);
                    }
                    else
                    {
                        // If here, we can assumes connected.

                        this.Logger?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ConnectionLoop)} - " +
                            $"Successful connection with server, ({this.wsconnection_url.ToString()}). ConnectionID = {(this.ConnectionId ?? "")}.");

                        success = true;
                    }
                }
            });

            return success;
        }

        /// <summary>
        /// Internal method used by the native websocket, when dealing with a host that doesn't include a good certificate.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        protected bool CALLBACK_Check_Server_Certificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        /// <summary>
        /// In the implementation of this method, perform a close on the transport, and dispose of it.
        /// Don't dereference the transport instance, yet.
        /// </summary>
        override protected async Task CloseandDisposeTransport()
        {
            if (cws != null)
            {
                // Close the connection...
                try
                {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    // We will call the close output async, so we are not waiting for a reply...
                    // https://learn.microsoft.com/en-us/dotnet/api/system.web.websockets.aspnetwebsocket.closeasync?view=netframework-4.8#remarks
                    await cws?.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    // We've waffled back and forth on which to call, here: CloseAsync or CloseOutputAsync.
                    // What we've determined is that: CloseAsync would be the more correct. BUT. Big BUT.
                    // Using CloseAsync REQUIRES that BOTH client and server use it, or the one side that did, will hang indefinitely.
                    // And since hanging indefinitely is a VERY bad failure mode for production code, this is not a tolerable side-effect.
                    // So, we use the CloseOutputAsync and suffer the transient WebSocketException it may cause the other end, which should close down anyway.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                catch (Exception) { }

                try
                {
                    cws?.Dispose();
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
            this.cws = null;
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
            if (data == null || data.Length == 0)
                return 1;

            // Send the message...

#if (NET452 || NET48)
            // The SendAsync method in NET Framework 4.8 requires an ArraySegment.
            // So, we must convert it, here.
            var buffer = new ArraySegment<Byte>(data, 0, data.Length);
            await this.cws.SendAsync(buffer, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None).ContinueWith(task =>
#else
            await this.cws.SendAsync(data, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None).ContinueWith(task =>
#endif
            {
                // If here, the send method finished.

                // See how it did...
                if (task.IsFaulted)
                {
                    // An exception occurred while attempting to send data to the websoket.
                    // We will log and leave.

                    var msg = $"There was an error sending data to the {(this.TransportLongName?.ToLower() ?? "")}, ({this.wsconnection_url.ToString()}). Returning...";

                    // Log the error with exception if possible...
#if (NET452 || NET48)
                    Exception te = null;
#else
                    Exception? te = null;
#endif
                    try
                    {
                        te = task?.Exception?.GetBaseException() ?? null;
                    }
                    catch(Exception dr) { }
                    if (te != null)
                        this.Logger?.Error(te,
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                                    msg);
                    else
                        this.Logger?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                            msg);

                    return -2;
                }
                else
                {
                    // The send did not fault.
                    // So, it should have sent.

                    // Verify our state...
                    if(this.cws.State != System.Net.WebSockets.WebSocketState.Open)
                    {
                        // client does not report connected.

                        var msg = $"The connection was lost. Returning...";

                        // Log the error with exception if possible...
#if (NET452 || NET48)
                        Exception te = null;
#else
                        Exception? te = null;
#endif
                        try
                        {
                            te = task?.Exception?.GetBaseException() ?? null;
                        }
                        catch(Exception) { }
                        if (te != null)
                            this.Logger?.Error(te,
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                                msg);
                        else
                            this.Logger?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                                msg);

                        return -1;
                    }
                    else
                    {
                        // If here, we can assume the send was successful.

                        this.Logger?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                            $"Successfully sent message to server, ({this.wsconnection_url.ToString()}). ConnectionID = {(this.ConnectionId ?? "")}.");

                        return 1;
                    }
                }
            });

            return 1;
        }

#endregion


        #region Receiving Methods

        /// <summary>
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
            this.Logger?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                "Receive loop method has been called.");

            // Check if connected...
            if (this.disposedValue)
            {
                this.Logger?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                    "Websocket is already disposed.");

                return -1;
            }

            // Do any setup....

            this.Logger?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                "Websocket receive loop is starting...");

            var buffer = new ArraySegment<byte>(new byte[2048]);
            WebSocketReceiveResult result;
            MemoryStream ms = null;

            // Locally save the current connectionId that we've started under...
            // We do this, here in the receive loop, so we can know if we're holding onto an old connection instance.
            var registered_connectionId = this.ConnectionId;

            // Enter the loop...
            try
            {
                // Stay in the receive loop until told to leave...
                while(!this.disposedValue && _receive_cts != null && !_receive_cts.IsCancellationRequested)
                {
                    if(this.cws == null)
                    {
                        // Websocket has been closed.
                        break;
                    }

                    // Check if we're processing an old instance...
                    if(this.ConnectionId != registered_connectionId)
                    {
                        // The connection Id has changed.
                        // This means that we've recycled the connection, but this receive loop is still holding onto an old one.
                        // So, we will leave the loop.

                        break;
                    }

                    // Loop inside a try, to ensure we don't leave unless we want to...
                    try
                    {
                        // Check if we are connected...
                        if (cws.State != System.Net.WebSockets.WebSocketState.Open)
                        {
                            // We are not open.
                            // We cannot accept messages.
                            // Leave the receive loop if we are not connected...

                            this.Logger?.Warn(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                                "Receive loop detected a closed websocket. Leaving the receive loop...");

                            break;
                        }
                        else
                        {
                            // We can receive.

                            ms = new MemoryStream();

                            // Loop until we receive the entire message...
                            do
                            {
                                // Collect the available piece...
                                result = await cws.ReceiveAsync(buffer, _receive_cts.Token);

                                // Check if we were given a close message...
                                if(result.MessageType == WebSocketMessageType.Close)
                                {
                                    // We were given a close message.

                                    // Clear the send flag, to prevent outgoing messages...
                                    this._allowsend = false;

                                    try
                                    {
                                        // Reply back with a close message...
                                        // See this for which close method to call:
                                        // https://learn.microsoft.com/en-us/dotnet/api/system.web.websockets.aspnetwebsocket.closeasync?view=netframework-4.8#remarks
                                        await cws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", _receive_cts.Token);
                                    }
                                    catch(WebSocketException wse)
                                    {
                                        int x = 0;
                                    }
                                    catch(Exception e)
                                    {
                                        int x = 0;
                                    }

                                    this.Logger?.Error(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                                        "Server-side sent us a closure message, so we need to close and recycle this connection.");

                                    // We need to close the connection, so the connection loop will try a reconnect...
                                    try { cws?.Dispose(); } catch(Exception) { }
                                    cws = null;

                                    // Signal that the connection was lost...
                                    DispatchConnectionLost();

                                    return -2;
                                }
                                // Check if we received binary data...
                                if(result.MessageType == WebSocketMessageType.Binary)
                                {
                                    // We were given a binary message.
                                    // We cannot currently process binary data.
                                    // So, we will consider this a protocol error.

                                    // Clear the send flag, to prevent outgoing messages...
                                    this._allowsend = false;

                                    try
                                    {
                                        // Reply back with a close message...
                                        // See this for which close method to call:
                                        // https://learn.microsoft.com/en-us/dotnet/api/system.web.websockets.aspnetwebsocket.closeasync?view=netframework-4.8#remarks
                                        await cws.CloseOutputAsync(WebSocketCloseStatus.ProtocolError, "", _receive_cts.Token);
                                    }
                                    catch(WebSocketException wse)
                                    {
                                        int x = 0;
                                    }
                                    catch(Exception e)
                                    {
                                        int x = 0;
                                    }

                                    this.Logger?.Error(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                                        "Server-side sent us a closure message, so we need to close and recycle this connection.");

                                    // We need to close the connection, so the connection loop will try a reconnect...
                                    try { cws?.Dispose(); } catch(Exception) { }
                                    cws = null;

                                    // Signal that the connection was lost...
                                    DispatchConnectionLost();

                                    return -2;
                                }

                                // If here, we will accept the received block of data...
                                ms.Write(buffer.Array, buffer.Offset, result.Count);
                            }
                            while (!result.EndOfMessage);

                            // See if the message is a close request...
                            // If so, leave...
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                // Clear the send flag, to prevent outgoing messages...
                                this._allowsend = false;

                                this.Logger?.Debug(
                                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                                    "We received a close request from the server. Leaving the receive loop...");

                                break;
                            }

                            // If not a close, process the message...
                            ms.Seek(0, SeekOrigin.Begin);
                            using (var reader = new StreamReader(ms, Encoding.UTF8))
                            {
                                // Read in the raw message...
                                string rawmsg = await reader.ReadToEndAsync();

                                // Update our received timestamp...
                                this.LastReceivedTime = DateTime.UtcNow;

                                // Increment the received message counter...
                                System.Threading.Interlocked.Increment(ref this._receivedmessage_counter);

                                // Fire off any message received event...
                                // NOTE: This is just notification that something came in, and doesn't do any handling.
                                // It is meant for app-based, diagnostic message counting, nothing more.
                                // Wrap this override in a try-catch to ensure it doesn't throw and unwind us...
                                try
                                {
                                    this.FireMessageReceivedEvent();
                                }
                                catch(Exception) { }

                                // Send it off for processing...
                                ///  1 = Message was handled.
                                ///  0 = Message could not be deserialized or handled. Ignoring and continuing on.
                                int res = Process_ReceivedMessage(rawmsg);
                                if(res == 0)
                                {
                                    // Message process and dispatch had a problem, but we can keep going.

                                    this.Logger?.Error(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                                        "Failed to process and dispatch received message.");
                                }
                                else if(res == -1)
                                {
                                    // Message processing failed.
                                    // We will consider this fatal to the current connection.

                                    // We failed to complete post-connection work.
                                    // We must recycle this connection, and try again.

                                    // Clear the send flag, to prevent outgoing messages...
                                    this._allowsend = false;

                                    this.Logger?.Error(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                                        "Messaging processing failed in a fatal way, and we need to recycle this connection.");

                                    // We need to close the connection, so the connection loop will try a reconnect...
                                    try { cws?.Dispose(); } catch(Exception) { }
                                    cws = null;

                                    // Signal that the connection was lost...
                                    DispatchConnectionLost();

                                    return 0;
                                }
                                // If here, we processed and dispatched the message.
                                // We can continue on to the next.
                            }
                            // Finished processing the current message.
                            // We will return back to the top of the while to check status and wait for another message.
                        }
                    }
                    catch when (_receive_cts == null)
                    {
                        // We were cancelled.
                        // We can leave.

                        // Clear the send flag, to prevent outgoing messages...
                        this._allowsend = false;

                        this.Logger?.Warn(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                            "Receive loop was cancelled.");

                        return 1;
                    }
                    catch when (_receive_cts.IsCancellationRequested)
                    {
                        // We were cancelled.
                        // We can leave.

                        // Clear the send flag, to prevent outgoing messages...
                        this._allowsend = false;

                        this.Logger?.Warn(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                            "Receive loop was cancelled.");

                        return 1;
                    }
                    catch(WebSocketException wse)
                    {
                        // Clear the send flag, to prevent outgoing messages...
                        this._allowsend = false;

                        // Get the exception type...
                        var gg = wse.InnerException?.GetType().Name ?? "";

                        if (gg == nameof(IOException))
                        {
                            // The connection was closed.

                            this.Logger?.Warn(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                                "The connection was closed.");

                            return 1;
                        }

                        this.Logger?.Warn(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                            "Web Socket Exception occurred during receive loop. Likely from a connection closure.");

                        return 1;
                    }
                    catch(Exception e)
                    {
                        // Clear the send flag, to prevent outgoing messages...
                        this._allowsend = false;

                        this.Logger?.Error(e,
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                            "Exception occurred while connecting to server.");

                        await Task.Delay(this._Startup_Connect_Retry_Delay, _cts.Token);
                    }
                }
                // Bottom of the outer loop.
                // We have left the loop.

                return 1;
            }
            catch(Exception ef)
            {
                // Clear the send flag, to prevent outgoing messages...
                this._allowsend = false;

                this.Logger?.Error(ef,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                    "Exception occurred while looping, most likely from the cancellation token being disposed or null.");

                return -1;
            }
            finally
            {
                ms?.Dispose();

                this.Logger?.Trace(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop)} - " +
                    "Receive loop method is returning.");
            }
        }

        #endregion
    }
}
