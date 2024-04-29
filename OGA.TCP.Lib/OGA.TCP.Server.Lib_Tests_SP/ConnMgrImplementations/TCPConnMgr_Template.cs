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
    /// Template implementation of the connection manager for TCP sockets, with integrated listener.
    /// This implementation bolts on some incoming message handlers for testing.
    /// </summary>
    public class TCPConnMgr_Template : TCPConnMgr_wListener
    {
        #region TEST HANDLER HOOKUPS

        static public string TESTCHANNELNAME = "1234567890";
        private int TESTHANDLER_HandleIncoming_TestMessage(Endpoint_Abstract mep, string messagetype, string jsondata, string corelationid)
        {
            var res = this._delOnMessageReceived(mep, messagetype, jsondata, corelationid);
            return 1;
        }

        public delegate int DelMessageReceived(Endpoint_Abstract mep, string messagetype, string jsondata, string corelationid);
        protected DelMessageReceived _delOnMessageReceived;
        /// <summary>
        /// Public delegate hook for accepting message traffic.
        /// </summary>
        public DelMessageReceived OnMessageReceived
        {
            set
            {
                _delOnMessageReceived = value;
            }
        }
       
        #endregion


        #region ctor / dtor

        static TCPConnMgr_Template()
        {
            _classname = nameof(TCPConnMgr_Template);
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public TCPConnMgr_Template() : base()
        {
        }

        #endregion


        #region Overrides

        ///// <summary>
        ///// Override this method with logic to validate config and resource availability needed to run.
        ///// This gets called by Startup().
        ///// </summary>
        ///// <returns></returns>
        //override protected int DoStartupChecks()
        //{
        //    // Check the host and port, so our listener can use them...
        //    if(this.ListeningPort <= 0)
        //    {
        //        // We currently don't allow for dynamic porting.
        //        // And it can't be negative.

        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
        //            $"{_classname}:{_instance_counter.ToString()}::{nameof(DoStartupChecks)} - " +
        //            $"ListingPort Not Set. Cannot startup.");

        //        return -1;
        //    }
        //    if(string.IsNullOrEmpty(this.ListeningAddress))
        //    {
        //        // We currently don't allow for dynamic porting.
        //        // And it can't be negative.

        //        OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(
        //            $"{_classname}:{_instance_counter.ToString()}::{nameof(DoStartupChecks)} - " +
        //            $"ListingPort Not Set. Cannot startup.");

        //        return -1;
        //    }

        //    // Ensure the listening address is correct...
        //    if(this.ListeningAddress.ToLower() == "localhost")
        //    {
        //        // We will change it to 127.0.0.1.
        //        this.ListeningAddress = "127.0.0.1";
        //    }

        //    return 1;
        //}

        #endregion


        #region Public Methods

        /// <summary>
        /// Override of the Add connection method, to bolt on our incoming message channel handlers.
        /// </summary>
        /// <param name="newconn"></param>
        /// <returns></returns>
        public override int AddConnection(Endpoint_Abstract newconn)
        {
            // Call the base, to add the connection as unregistered or registered, depending if it has a connectionId.
            var res = base.AddConnection(newconn);
            if (res != 1)
            {
                return res;
            }
            // We've added the connection.
            // Now, we need to hookup any channel handlers to the websocket endpoint.

            try
            {
                // Hookup message dispatch delegates...

                // Add a handler for test channel messages...
                newconn.Add_ChannelHandler(TESTCHANNELNAME, this.TESTHANDLER_HandleIncoming_TestMessage);

                return 1;
            }
            catch (Exception e)
            {
                OGA.SharedKernel.Logging_Base.Logger_Ref?.Error(e,
                    $"{_classname}:{_instance_counter.ToString()}::{nameof(AddConnection)} - " +
                    $"Exception occurred while attempting to add connection.");

                return -2;
            }
        }

        #endregion


        #region Incoming Message Handlers


        #endregion
    }
}
