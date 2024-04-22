using OGA.TCP.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OGA.TCP.Server.Lib_Tests.Helpers
{
    /// <summary>
    /// Testing helper class that will stand up a pair of connected TcpClient instances.
    /// The created pair can then be used for endpoint testing.
    /// </summary>
    public class cClientServer_Test_Helper
    {
        public TcpClient Clientside_Connection { get; private set; }
        public TcpClient Serverside_Connection { get; private set; }

        public int Generate_Connected_TcpClient_Pair(int port)
        {
            // Setup a listener.
            Testing_CommonHelpers_SP.Helpers.cListener_Helper l = new Testing_CommonHelpers_SP.Helpers.cListener_Helper();
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


        private void CALLBACK_Server_NewConnection(Testing_CommonHelpers_SP.Helpers.cListener_Helper l, TcpClient newclient)
        {
            // The listener gave us a connection with a client.
            // Post the server side of the connection.
            this.Serverside_Connection = newclient;
        }
    }
}
