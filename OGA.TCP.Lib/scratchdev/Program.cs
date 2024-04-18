using System;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace scratchdev
{
    internal class Program
    {
        static public int ServerListeningPort = 5003;
        static public string ServerListeningAddr = "192.168.1.127";
        static public CancellationTokenSource _cts;

        static public int _connid;

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            _cts = new CancellationTokenSource();

            // Start the server...
            var ress = StartServer();
            if(ress != 1)
            {
                // Failed to start server.
                return;
            }

            // Start a client...
            var rescl = SetupClient();
            if(rescl != 1)
            {
                // Failed to start client.
                return;
            }

            while(true)
            {
                // Client send a message...
                var clmsg = ClientSendMessage("Hello");
                if(clmsg != 1)
                {
                    // Failed to send message.
                    return;
                }

                //System.Threading.Thread.Sleep(1000);

                //// server send a message...
                //var smsg = ServerSendMessage("Hello");
                //if(smsg != 1)
                //{
                //    // Failed to send message.
                //    return;
                //}

                System.Threading.Thread.Sleep(5000);
            }


            while (true)
            {
                System.Threading.Thread.Sleep(1000000);
            }
        }

        
        static private int ServerSendMessage(string msg)
        {
            try
            {
                byte[] mb = Encoding.UTF8.GetBytes(msg);

                return 1;
            }
            catch(Exception e)
            {
                return -2;
            }
        }

        static private int ClientSendMessage(string msg)
        {
            try
            {
                byte[] mb = Encoding.UTF8.GetBytes(msg);

                return 1;
            }
            catch(Exception e)
            {
                return -2;
            }
        }

        static private int SetupClient()
        {
            try
            {

                return 1;
            }
            catch(Exception e)
            {
                return -2;
            }
        }

        static private int StartServer()
        {
            try
            {

                return 1;
            }
            catch(Exception e)
            {
                return -2;
            }
        }




        private static void OnClientConnected()
        {
            Console.WriteLine("Client reports: Connected");

            return;
        }

        private static void OnClientData(ArraySegment<byte> segment)
        {
            Console.WriteLine("Client received data");

            string utfString = Encoding.UTF8.GetString(segment.Array, 0, segment.Array.Length);
            Console.WriteLine(utfString);

            return;
       }

        private static void OnClientDisconnected()
        {
            Console.WriteLine("Client reports: Disconnected");

            return;
        }

        private static void ServerOnDisconnected(int connid)
        {
            Console.WriteLine($"Server reports client " + connid.ToString() + " disconnected.");

            Program._connid = 0;

            return;
        }

        private static void ServerOnData(int arg1, ArraySegment<byte> segment)
        {
            Console.WriteLine($"Server received data from client " + arg1.ToString() + " :");

            string utfString = Encoding.UTF8.GetString(segment.Array, 0, segment.Array.Length);
            Console.WriteLine(utfString);

            return;
        }

        private static void ServerOnConnected(int connid)
        {
            Console.WriteLine($"Server reports client " + connid.ToString() + " connected.");

            Program._connid = connid;

            return;
        }
    }
}
