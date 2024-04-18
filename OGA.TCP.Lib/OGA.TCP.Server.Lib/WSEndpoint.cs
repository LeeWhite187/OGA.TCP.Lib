using Microsoft.AspNetCore.Connections;
using Newtonsoft.Json;
using OGA.Common.Process;
using OGA.TCP.Messages;
using OGA.TCP.Server.Model;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OGA.TCP.Server
{
    /// <summary>
    /// Provides websocket connectivity to a single connected client.
    /// Is compatible with clients of LibVersion={1,2}
    /// </summary>
    public class WSEndpoint : Endpoint_Abstract, IDisposable
    {
        #region Private Fields

        private System.Net.WebSockets.WebSocket _webSocket;

        #endregion


        #region Public Properties

        /// <summary>
        /// Determines if a receiver loop is spawned.
        /// This should be set for websockets, clear for tcpsockets.
        /// </summary>
        override public bool Cfg_TransportRequiresReceiverLoop { get; } = true;

        /// <summary>
        /// Set this to the lowercase short name of the transport: tcp, ws, etc...
        /// </summary>
        override public string TransportShortName { get; } = "ws";

        /// <summary>
        /// Set this to the name of the transport: TCPSocket, Websocket, etc...
        /// </summary>
        override public string TransportLongName { get; } = "Websocket";

        /// <summary>
        /// Set this to the lowercase string-literal of the libver property that is passed during connection registration.
        /// For websocket clients, this is: "wslibver".
        /// For tcpsocket clients, this is: "tcplibver".
        /// </summary>
        override public string PropName_ClientLibVer { get; } = "wslibver";

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

                    if (_webSocket == null)
                        return false;

                    return _webSocket.State == WebSocketState.Open;
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
        /// This is the normal constructor for creating a WSEndpoint instance.
        /// It requires the web listener already having created the websocket instance from the initial http request.
        /// </summary>
        /// <param name="webSocket"></param>
        public WSEndpoint(System.Net.WebSockets.WebSocket webSocket) : base()
        {
            _classname = nameof(WSEndpoint);

            _webSocket = webSocket;
        }

        #endregion


        #region Public Methods

        #endregion


        #region Connection Management

        /// <summary>
        /// In the implementation of this method, perform a close on the transport, and dispose of it.
        /// Don't dereference the transport instance, yet.
        /// </summary>
        override protected async Task CloseandDisposeTransport()
        {
            if (_webSocket != null)
            {
                // Close and dispose the connection...
                try
                {
                    // We will call the close output async, so we are not waiting for a reply...
                    // https://learn.microsoft.com/en-us/dotnet/api/system.web.websockets.aspnetwebsocket.closeasync?view=netframework-4.8#remarks
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    await _webSocket?.CloseOutputAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                              // We've waffled back and forth on which to call, here: CloseAsync or CloseOutputAsync.
                              // What we've determined is that: CloseAsync would be the more correct. BUT. Big BUT.
                              // Using CloseAsync REQUIRES that BOTH client and server use it, or the one side that did, will hang indefinitely.
                              // And since hanging indefinitely is a VERY bad failure mode for production code, this is not a tolerable side-effect.
                              // So, we use the CloseOutputAsync and suffer the transient WebSocketException it may cause the other end, which should close down anyway.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                catch (Exception) { }

                try { _webSocket?.Dispose(); } catch (Exception) { }
            }
        }

        /// <summary>
        /// In the implementation of this method, perform a dereference of the transport instance.
        /// Don't dispose it or anything else, here.
        /// </summary>
        override protected void DereferenceTransport()
        {
            this._webSocket = null;
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

            //await _webSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
            //return 1;

            // Send the message...
            await this._webSocket.SendAsync(data, System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None).ContinueWith(task =>
            {
                // If here, the send method finished.

                // See how it did...
                if (task.IsFaulted)
                {
                    // An exception occurred while attempting to send data to the websoket.
                    // We will log and leave.

                    var msg = $"There was an error sending data to the {(this.TransportLongName?.ToLower() ?? "")}, ConnectionID = {(this.WSId ?? "")}. Returning...";

                    // Log the error with exception if possible...
                    Exception? te = null;
                    try
                    {
                        te = task?.Exception?.GetBaseException() ?? null;
                    }
                    catch(Exception dr) { }
                    if (te != null)
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(te,
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                                    msg);
                    else
                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                            msg);

                    return -2;
                }
                else
                {
                    // The send did not fault.
                    // So, it should have sent.

                    // Verify our state...
                    if(this._webSocket.State != System.Net.WebSockets.WebSocketState.Open)
                    {
                        // client does not report connected.

                        var msg = $"The connection was lost. Returning...";

                        // Log the error with exception if possible...
                        Exception? te = null;
                        try
                        {
                            te = task?.Exception?.GetBaseException() ?? null;
                        }
                        catch(Exception) { }
                        if (te != null)
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(te,
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                                msg);
                        else
                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                                msg);

                        return -1;
                    }
                    else
                    {
                        // If here, we can assume the send was successful.

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(RawTransportSend)} - " +
                            $"Successfully sent message to client. ConnectionID = {(this.WSId ?? "")}.");

                        return 1;
                    }
                }
            });

            return 1;
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
        override protected async Task<int> ReceiveLoop_from_Client()
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                "Receive loop method has been called.");

            // Check if connected...
            if (_alreadydisposed)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                    $"{(this.TransportLongName ?? "Socket")} is already disposed.");

                return -1;
            }

            // Do any setup....

            OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                $"{(this.TransportLongName ?? "Socket")} receive loop is starting...");

            var buffer = new ArraySegment<byte>(new byte[2048]);
            WebSocketReceiveResult result;
            MemoryStream ms = null;

            // Enter the loop...
            try
            {
                // Run the outer loop...
                while (this.IsConnected && _receive_cts != null && !_receive_cts.IsCancellationRequested)
                {
                    // Loop inside a try, to ensure we don't leave unless we want to...
                    try
                    {
                        // Check if we are connected...
                        if (_webSocket.State != WebSocketState.Open)
                        {
                            // We are not open.
                            // We cannot accept messages.
                            // Leave the receive loop if we are not connected...

                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                                $"Receive loop detected a closed websocket. Leaving the receive loop...");

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
                                result = await _webSocket.ReceiveAsync(buffer, _receive_cts.Token);

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
                                        await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", _receive_cts.Token);
                                    }
                                    catch(WebSocketException wse)
                                    {
                                        int x = 0;
                                    }
                                    catch(Exception e)
                                    {
                                        int x = 0;
                                    }

                                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                                        "Client sent us a closure message, so we need to close and recycle this connection.");

                                    // We need to close the connection, so the connection loop will try a reconnect...
                                    try { _webSocket?.Dispose(); } catch(Exception) { }
                                    _webSocket = null;

                                    // Signal that the connection was closed...
                                    DispatchConnectionClosed();

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
                                        await _webSocket.CloseOutputAsync(WebSocketCloseStatus.ProtocolError, "", _receive_cts.Token);
                                    }
                                    catch(WebSocketException wse)
                                    {
                                        int x = 0;
                                    }
                                    catch(Exception e)
                                    {
                                        int x = 0;
                                    }

                                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                                        "Client sent us a closure message, so we need to close and recycle this connection.");

                                    // We need to close the connection, so the connection loop will try a reconnect...
                                    try { _webSocket?.Dispose(); } catch(Exception) { }
                                    _webSocket = null;

                                    // Signal that the connection was closed...
                                    DispatchConnectionClosed();

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

                                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                                    "We received a close request from the client. Leaving the receive loop...");

                                break;
                            }

                            // If not a close, process the message...
                            ms.Seek(0, SeekOrigin.Begin);
                            using (var reader = new StreamReader(ms, Encoding.UTF8))
                            {
                                // Read in the raw message...
                                string rawmsg = await reader.ReadToEndAsync();

                                // Update our received timestamp...
                                LastReceivedTimeUTC = DateTime.UtcNow;

                                // Increment the received message counter...
                                Interlocked.Increment(ref this._receivedmessage_counter);

                                // Send it off for processing....
                                ///  1 = Message was handled.
                                ///  0 = Message could not be deserialized or handled. Ignoring and continuing on.
                                /// -1 = Registration failed. The receive loop cannot continue, and the connection must close down.
                                int res = Process_ReceivedMessage_from_Client(rawmsg);
                                if (res == 0)
                                {
                                    // Message process and dispatch had a problem, but we can keep going.

                                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                                        "Failed to process and dispatch received message.");
                                }
                                else if (res == -1)
                                {
                                    // Message processing failed.
                                    // We will consider this fatal to the current connection.

                                    // We failed to complete post-connection work.
                                    // We must recycle this connection, and try again.

                                    OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                                        $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                                        "Messaging processing failed in a fatal way, and we need to recycle this connection.");

                                    // Do all the common connection closure things...
                                    this.DoCommonClosureThings();

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

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                            "Receive loop was cancelled.");

                        return 1;
                    }
                    catch when (_receive_cts.IsCancellationRequested)
                    {
                        // We were cancelled.
                        // We can leave.

                        // Clear the send flag, to prevent outgoing messages...
                        this._allowsend = false;

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                            "Receive loop was cancelled.");

                        return 1;
                    }
                    catch (WebSocketException wse)
                    {
                        // Clear the send flag, to prevent outgoing messages...
                        this._allowsend = false;

                        // Get the exception type...
                        var gg = wse.InnerException?.GetType().Name ?? "";

                        if (gg == nameof(ConnectionResetException))
                        {
                            // The connection was closed.

                            OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                                $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                                "The connection was closed.");

                            return 1;
                        }

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Warn(
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                            $"{(this.TransportLongName ?? "Socket")} Exception occurred during receive loop. Likely from a connection closure.");

                        return 1;
                    }
                    catch (Exception e)
                    {
                        // Clear the send flag, to prevent outgoing messages...
                        this._allowsend = false;

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                            $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                            "*************Generic exception occurred while during receive loop. " +
                            "This one is marked, so we can see if it ever occurs, as it may indicate a flaw. " +
                            $"And, we're not quite sure if the block this exception is in should force connection closure, or allow a retry.");

                        return 1;
                        //// Pausing for a bit, before attempting to connect again...
                        //await Task.Delay(_Startup_Connect_Retry_Delay, _receive_cts.Token);
                    }
                }
                // Bottom of the outer loop.
                // We have left the loop.

                return 1;
            }
            catch (Exception ef)
            {
                // Clear the send flag, to prevent outgoing messages...
                this._allowsend = false;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(ef,
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                    "Exception occurred while looping, most likely from the cancellation token being disposed or null.");

                return -1;
            }
            finally
            {
                ms?.Dispose();

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Trace(
                    $"{_classname}:{this.InstanceId.ToString()}::{nameof(ReceiveLoop_from_Client)} - " +
                    "Receive loop method is returning.");
            }
        }

        #endregion
    }
}
