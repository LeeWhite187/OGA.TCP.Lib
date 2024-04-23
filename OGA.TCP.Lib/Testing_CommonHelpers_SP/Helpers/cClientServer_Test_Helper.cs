using OGA.TCP.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Testing_CommonHelpers_SP.Helpers;

namespace OGA.TCP.Server.Lib_Tests.Helpers
{
    /// <summary>
    /// Testing helper class that will stand up a pair of connected TcpClient instances.
    /// The created pair can then be used for endpoint testing.
    /// </summary>
    public class cClientServer_Test_Helper : IDisposable
    {
        private bool disposedValue;

        public TcpClient Clientside_Connection { get; private set; }
        public TcpClient Serverside_Connection { get; private set; }


        public cClientServer_Test_Helper()
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

                Serverside_Connection?.Close();
#if (NET452)
                // NET Framework 4.5.2 doesn't have a Dispose() on TcpClient.
#else
                Serverside_Connection?.Dispose();
#endif
                Serverside_Connection = null;
                Clientside_Connection?.Close();
#if (NET452)
                // NET Framework 4.5.2 doesn't have a Dispose() on TcpClient.
#else
                Clientside_Connection?.Dispose();
#endif
                Clientside_Connection = null;

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~cClientServer_Test_Helper()
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



        public int Generate_Connected_TcpClient_Pair(int port)
        {
            // Setup a listener.
            TESTINGSRVR_cListener l = new TESTINGSRVR_cListener();
            l.Listening_IP = IPAddress.Parse("0.0.0.0");
            l.Listening_Port = port;
            l.OnNew_Client_Connection = this.CALLBACK_Server_NewConnection;
            if(l.Start_Listener() != 1)
            {
                // Failed to start listener.
                return -1;
            }
            // If here, we have a listener waiting for a connection.

            // Setup a client with the client helper class.
            cClient_Helper ch = new cClient_Helper();
            if(ch.Connect_Blocking("127.0.0.1", port) != 1)
            {
                // Failed to connect with waiting server.

                return -2;
            }
            // If here, we have a connected client.

            System.Threading.Thread.Sleep(100);

            // Publish the client side connection.
            this.Clientside_Connection = ch.client;

            System.Threading.Thread.Sleep(100);

            // Kill the listener, since we don't need it anymore.
            l.CloseDown_Listener();

            return 1;
        }


        private void CALLBACK_Server_NewConnection(TESTINGSRVR_cListener l, TcpClient newclient)
        {
            // The listener gave us a connection with a client.
            // Post the server side of the connection.
            this.Serverside_Connection = newclient;
        }
    }
}
