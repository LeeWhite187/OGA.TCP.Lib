using Microsoft.Extensions.Hosting;
using OGA.TCP.Server.Model;
using OGA.TCP.Server.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP.Server
{
    /// <summary>
    /// Server-side TCP connection manager with integrated listener.
    /// Will listen and host connections on a single listening port.
    /// </summary>
    public class TCPConnMgr_wListener : ConnectionMgr_Abstract
    {
        #region ctor / dtor

        static TCPConnMgr_wListener()
        {
            _classname = nameof(TCPConnMgr_wListener);
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public TCPConnMgr_wListener() : base()
        {
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Override this method with logic to validate config and resource availability needed to run.
        /// This gets called by Startup().
        /// </summary>
        /// <returns></returns>
        override protected int DoStartupChecks()
        {
            // Check the host and port, so our listener can use them...
            if(this.ListeningPort <= 0)
            {
                // We currently don't allow for dynamic porting.
                // And it can't be negative.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(DoStartupChecks)} - " +
                    $"ListingPort Not Set. Cannot startup.");

                return -1;
            }
            if(string.IsNullOrEmpty(this.ListeningAddress))
            {
                // We currently don't allow for dynamic porting.
                // And it can't be negative.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(DoStartupChecks)} - " +
                    $"ListingPort Not Set. Cannot startup.");

                return -1;
            }

            // Ensure the listening address is correct...
            if(this.ListeningAddress.ToLower() == "localhost")
            {
                // We will change it to 127.0.0.1.
                this.ListeningAddress = "127.0.0.1";
            }

            return 1;
        }

        #endregion


        #region Public Methods

        ///// <summary>
        ///// Override of the Add connection method, to bolt on our incoming message channel handlers.
        ///// </summary>
        ///// <param name="newconn"></param>
        ///// <returns></returns>
        //public override int AddConnection(Endpoint_Abstract newconn)
        //{
        //    // Call the base, to add the connection as unregistered or registered, depending if it has a connectionId.
        //    var res = base.AddConnection(newconn);
        //    if(res != 1)
        //    {
        //        return res;
        //    }
        //    // We've added the connection.
        //    // Now, we need to hookup any channel handlers to the websocket endpoint.

        //    try
        //    {
        //        // Hookup message dispatch delegates...
        //        // We will use a generic handler when possible. Those are obvious from the lambda created in this method.


        //        // Hookup a handler for processing inbound IB Chat messages...
        //        // We will leverage a generic handler, and given it a little context with this lambda.
        //        DelMessageReceived ibchathandler = (ws, messagetype, jsonmsg, corelationid) =>
        //        {
        //            // NOTE: We are using the generic handler that wraps our message in an inbound envelope.
        //            // Doing so, saves the CMS from having to lookup reply routing.

        //            // This call returns the standard 1, 0, -1 values that we need for resolving handled, unhandled, or handled with error.
        //            return GenericHandler_for_InboundMessage_Handling_wSourceEnv<MessageDTO>(
        //                ws, messagetype, jsonmsg, corelationid, "IB Chat", this._queueclient.Forward_InBand_ChatMessage_fromClient);
        //            //// This call returns the standard 1, 0, -1 values that we need for resolving handled, unhandled, or handled with error.
        //            //return GenericHandler_for_InboundMessage_Handling<MessageDTO>(
        //            //    ws, messagetype, jsonmsg, "IB Chat", this._queueclient.Forward_InBand_ChatMessage_fromClient);
        //        };
        //        newconn.Add_ChannelHandler(WS_ChannelList.CONST_WebSocketChannel_Chat_InBand, ibchathandler);


        //        // Hookup a handler for processing inbound RTToy messages...
        //        // We will leverage a generic handler, and given it a little context with this lambda.
        //        DelMessageReceived rttoyhandler = (ws, messagetype, jsonmsg, corelationid) =>
        //        {
        //            // This call returns the standard 1, 0, -1 values that we need for resolving handled, unhandled, or handled with error.
        //            return GenericHandler_for_InboundMessage_Handling<DistributionEnvelope>(
        //                ws, messagetype, jsonmsg, corelationid, "RTToy", this._queueclient.Forward_RTToyMessage_fromClient);
        //        };
        //        newconn.Add_ChannelHandler(WS_ChannelList.CONST_WebSocketChannel_Toy_RealTimeControl, rttoyhandler);


        //        // Hookup a handler for processing inbound Diagnostic P2P messages...
        //        // We will leverage a generic handler, and given it a little context with this lambda.
        //        DelMessageReceived diagp2phandler = (ws, messagetype, jsonmsg, corelationid) =>
        //        {
        //            int x = 0;

        //            // Do any processing for diagnostic p2P messages...
        //            // Specifically: If it's a timing message, we will append a timestamp for its stop, here.
        //            jsonmsg = this.Decorate_Diagnostic_P2P_Envelope(messagetype, jsonmsg);

        //            // This call returns the standard 1, 0, -1 values that we need for resolving handled, unhandled, or handled with error.
        //            return GenericHandler_for_InboundMessage_Handling<DiagP2PEnvelopeDTO>(
        //                ws, messagetype, jsonmsg, corelationid, "DiagP2P", this._queueclient.Forward_DiagP2PMessage_fromClient);
        //        };
        //        newconn.Add_ChannelHandler(WS_ChannelList.CONST_WebSocketChannel_Diag_P2P, diagp2phandler);


        //        // Hookup a handler for processing inbound OOB Chat messages...
        //        // We will leverage a generic handler, and given it a little context with this lambda.
        //        DelMessageReceived oobchathandler = (ws, messagetype, jsonmsg, corelationid) =>
        //        {
        //            // This call returns the standard 1, 0, -1 values that we need for resolving handled, unhandled, or handled with error.
        //            return GenericHandler_for_InboundMessage_Handling<OutofBandEnvelope>(
        //                ws, messagetype, jsonmsg, corelationid, "OOB Chat", this._queueclient.Forward_OutOfBandChatMessageQueue_fromClient);
        //        };
        //        newconn.Add_ChannelHandler(WS_ChannelList.CONST_WebSocketChannel_Chat_OutofBand, oobchathandler);

        //        return 1;
        //    }
        //    catch (Exception e)
        //    {
        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
        //            $"{_classname}:{_instance_counter.ToString()}::{nameof(AddConnection)} - " +
        //            $"Exception occurred while attempting to add connection.");

        //        return -2;
        //    }
        //}

        #endregion


        #region Incoming Message Handlers


        #endregion


        #region Listener Management

        private cListener _listener;

        /// <summary>
        /// This method is called when the connection manager is being shut down.
        /// Override this method with any logic that will close down and dereference any listeners this connection manager depends on or contains.
        /// </summary>
        override protected void CloseListeners()
        {
            this._listener?.CloseDown_Listener();
            this._listener = null;
        }

        /// <summary>
        /// Starts up the internal connection listener.
        /// This method is called on connection manager startup, to enable any internal or external listeners.
        /// Override this method with any logic that will setup and start any listeners this connection manager depends on or contains.
        /// </summary>
        /// <returns></returns>
        override protected int StartListener()
        {
            try
            {
                this._listener = new cListener();
                this._listener.OnNew_Client_Connection = this.ListenerCALLBACK_OnNew_Client_Connection;
                this._listener.OnStatus_Change = this.ListenerCALLBACK_OnStatus_Change;
                this._listener.Listening_IP = System.Net.IPAddress.Parse(this.ListeningAddress);
                this._listener.Listening_Port = this.ListeningPort;
                this._listener.Start_Listener();

                return 1;
            }
            catch (Exception ex)
            {
                return -2;
            }
        }

        /// <summary>
        /// Hooked up the listener status change callback.
        /// But, we're not actively doing anything with it for now.
        /// </summary>
        /// <param name="l"></param>
        /// <param name="statusupdate"></param>
        private void ListenerCALLBACK_OnStatus_Change(cListener l, string statusupdate)
        {
            int x = 0;
        }

        /// <summary>
        /// This callback triggers when the socket listener returns a new live connection instance.
        /// This callback will only occur if the listener is live and received a connected client.
        /// So, we don't have to check those things.
        /// </summary>
        /// <param name="l"></param>
        /// <param name="newclient"></param>
        private async void ListenerCALLBACK_OnNew_Client_Connection(cListener l, TcpClient newclient)
        {
            bool success = false;

            TCPEndpoint nce = null;

            // Wrap this in a try-finally, so we can close down any resources on failure...
            try
            {
                // Quick sanity check on the client...
                // This isn't required based on the listener logic.
                // But, we do it just in case that changes.
                if(newclient == null || !newclient.Connected)
                {
                    // The received connection is null or not connected.
                    // We will ignore it.

                    // We will close client connection in the finally.

                    return;
                }

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(ListenerCALLBACK_OnNew_Client_Connection)} - " +
                    $"Received new client connection from listener. Adding it to connection management...");

                // Create a new TCPEndpoint instance...
                nce = new TCPEndpoint(newclient);

                // Wrap the AddConnection call in a try-catch, because its override may throw...
                try
                {
                    // Add the endpoint to our listing...
                    var resadd = this.AddConnection(nce);
                    if(resadd != 1)
                    {
                        // Failed to add the new connection.

                        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                            $"{_classname}:{_instance_counter.ToString()}::{nameof(ListenerCALLBACK_OnNew_Client_Connection)} - " +
                            $"Failed to add new client connection (from listener). Closing down received client connection...");

                        // We will close the endpoint and client connection in the finally.

                        return;
                    }
                }
                catch (Exception ex) { }
                // All good so far.

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(ListenerCALLBACK_OnNew_Client_Connection)} - " +
                    $"Starting new client connection...");

                // Start the endpoint, and give it its own thread...
                _= Task.Run(() => nce.Start_Async());

                // Set the success flag, so we let the new connection live...
                success = true;

                OGA.SharedKernel.Logging_Base.Logger_Ref?.Debug(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(ListenerCALLBACK_OnNew_Client_Connection)} - " +
                    $"New client connection has been started.");

                return;
            }
            catch(Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(ListenerCALLBACK_OnNew_Client_Connection)} - " +
                    $"Exception caught while accepting new connection.");

                return;
            }
            finally
            {
                if(!success)
                {
                    // Close down instances since we did not successfully start up a new connection...

                    try
                    {
                        nce.Dispose();
                    } catch(Exception ex) { }
                    nce = null;

                    try
                    {
                        newclient?.Dispose();
                    } catch(Exception ex) { }
                    newclient = null;
                }
            }
        }

        #endregion
    }
}
