using Microsoft.AspNetCore.Http;
using OGA.TCP.Server.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Testing.WSEndpoint_Tests.HelperClasses;

namespace OGA.TCP.Server
{
    /// <summary>
    /// Wraps around the cListener class, similar to a WS Connection Manager.
    /// Provides a means to easily spawn and monitor a server-side, TCP Endpoint during testing.
    /// NOTE: THIS CLASS IS NOT FOR PRODUCTION USAGE.
    /// </summary>
    public class Simple_TCPListener
    {
        private bool disposedValue;
        private CancellationTokenSource _cts;

        static public bool DoSomethingWith_ConnectionRegistration = false;
        static public bool DoSomethingWith_ConnectionClosure = false;
        static public bool AllowQuietClients = true;
        static public bool WeRequireClients_tobe_Chatty = true;

        static public int Keepalive_Timeout = 20;

        public int Port = 5000;
        public string Host = "0.0.0.0";

        public int ClosureCount = 0;

        public TCPEndpoint ServerSide_TCPEndpoint;
        public cListener Listener;

        public Simple_TCPListener()
        {

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                Stop();

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Simple_WSListener()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        public int Start()
        {
            try
            {
                this._cts = new CancellationTokenSource();

                this.Listener = new cListener();
                this.Listener.OnNew_Client_Connection = this.ListenerCALLBACK_OnNew_Client_Connection;
                this.Listener.OnStatus_Change = this.ListenerCALLBACK_OnStatus_Change;
                this.Listener.Listening_IP = System.Net.IPAddress.Parse(Host);
                this.Listener.Listening_Port = Port;
                this.Listener.Start_Listener();

                return 1;
            }
            catch (Exception ex)
            {
                return -2;
            }
        }

        public int Stop()
        {
            this._cts?.Cancel();
            this._cts?.Dispose();
            this._cts = null;
            
            this.Listener.CloseDown_Listener();
            this.Listener = null;

            this.ServerSide_TCPEndpoint?.Dispose();
            this.ServerSide_TCPEndpoint = null;

            return 1;
        }

        private void ListenerCALLBACK_OnStatus_Change(cListener l, string statusupdate)
        {
            int x = 0;
        }

        private async void ListenerCALLBACK_OnNew_Client_Connection(cListener l, TcpClient newclient)
        {
            try
            {
                if(this._cts == null)
                {
                    return;
                }
                if(this._cts?.IsCancellationRequested ?? true)
                {
                    return;
                }
                if(newclient == null)
                {
                    return;
                }


                // Check that an endpoint is not already active...
                if(ServerSide_TCPEndpoint != null)
                {
                    // A tcpsocket endpoint is already active.
                    // We want only one active at a time.

                    // Close down the existing tcpsocket instance...
                    ServerSide_TCPEndpoint.Dispose();
                    ServerSide_TCPEndpoint = null;
                }

                // Hand off the connection to the TCPEndpoint instance...
                ServerSide_TCPEndpoint = new TCPEndpoint(newclient);

                if(DoSomethingWith_ConnectionRegistration)
                {
                    ServerSide_TCPEndpoint.OnConnectionRegistration = this.Handle_ConnectionRegistration;
                }

                if(DoSomethingWith_ConnectionClosure)
                {
                    ServerSide_TCPEndpoint.OnConnectionClosed = this.Handle_ConnectionClosed;

                    DoSomethingWith_ConnectionClosure = false;
                }

                // Give the endpoint a nominal keepalive timeout...
                ServerSide_TCPEndpoint.Cfg_DeadClientTimeout = Keepalive_Timeout;
                ServerSide_TCPEndpoint.Cfg_We_Require_Clients_to_Be_Chatty = WeRequireClients_tobe_Chatty;

                // Tell the websocket to start work...
                await ServerSide_TCPEndpoint.Start_Async();
                // If here, the websocket has returned, and we can close it down...
                ServerSide_TCPEndpoint?.Dispose();

                int x = 0;
            }
            catch(Exception e)
            {

                int x = 0;
            }
        }


        private void Handle_ConnectionClosed(Endpoint_Abstract mep)
        {
            ClosureCount++;
        }

        private void Handle_ConnectionRegistration(Endpoint_Abstract mep, ClientInfo oldvals, ClientInfo newvals)
        {
            OGA.SharedKernel.Logging_Base.Logger_Ref?.Info(
                $"{nameof(Simple_TCPListener)}:-::{nameof(Handle_ConnectionRegistration)} - " +
                "Received connection registration message from client.");

            // Create a connection entry that we will forward to the client Mapping Service...
            var ce = new ConnectionEntry_v1();
            mep.Populate_ConnectionEntry(ce);

            // Add our WS Host name to the connection entry...
            ce.Hostname = "Some WSHost Name";
            ce.Host_Port = 1234;

            var msgjson = Newtonsoft.Json.JsonConvert.SerializeObject(ce);

            // Send a channel signal that the registration was received...
            Task.Run(() => CommonChannel.Callback_Queue.Enqueue(msgjson));
        }
    }
}
