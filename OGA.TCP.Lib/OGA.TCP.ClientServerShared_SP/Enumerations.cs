using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP
{
    public enum eEndpoint_ConnectionStatus
    {
        /// <summary>
        /// Endpoint was initialized but the connection is not open yet.
        /// The endpoint will change to a newly opened when a connection is successful.
        /// NOTE: THIS STATE IS ONLY SEEN IF CONSTRUCTED WITHOUT A SOCKET.
        /// </summary>
        Initialized,
        /// <summary>
        /// The endpoint has been constructed with a live socket, but no data has been exchanged yet.
        /// In this state, it is not known if the endpoint was given a viable connection.
        /// So, we need to wait for a successful data send or receive before considering it OPEN.
        /// </summary>
        Newly_Opened,
        /// <summary>
        /// The endpoint is open, and a proper data exchange has begun.
        /// If here, we are exchanging data via messages until:
        ///		the connection is closed - moves to Error.
        ///		a malformed message is received - moves to Closed.
        ///		an exception is caught while sending or receiving data - moves to Closed.
        /// </summary>
        Open,
        /// <summary>
        /// The endpoint is being closed gracefully.
        /// We are in the process of closing down both ends.
        /// In this state, a separate flag indicates if:
        ///		This endpoint is waiting on a close reply from the other end.
        ///		The remote endpoint sets its flag indicating it is waiting on the close reply.
        ///	Once a close reply is received, the endpoint changes to closed.
        /// </summary>
        Shutting_Down,
        /// <summary>
        /// Connection was lost without a graceful close.
        /// Maybe we received a null.
        /// Maybe we got an io exception on a read or write.
        /// </summary>
        Lost,
        /// <summary>
        /// A garbled message was received, or an oversized frame.
        /// Or, we are unable to exchange data with the remote endpoint for some framing reason.
        /// We can no longer use this endpoint instance.
        /// </summary>
        Error,
        /// <summary>
        /// The endpoint has been closed by both ends.
        /// </summary>
        Closed
    }

    public enum eLoop_ConnectionStatus
    {
        /// <summary>
        /// Loop was initialized but the connection is not open yet.
        /// The Loop will change to a newly opened when a connection is successful.
        /// NOTE: THIS STATE IS ONLY SEEN IF CONSTRUCTED WITHOUT A SOCKET.
        /// </summary>
        Initialized,
        /// <summary>
        /// The Loop has been constructed with a live socket, but no data has been exchanged yet.
        /// In this state, it is not known if the endpoint was given a viable connection.
        /// So, we need to wait for a successful data send or receive before considering it OPEN.
        /// </summary>
        Newly_Opened,
        /// <summary>
        /// The endpoint is open, and a proper data exchange has begun.
        /// If here, we are exchanging data via messages until:
        ///		the connection is closed - moves to Error.
        ///		a malformed message is received - moves to Closed.
        ///		an exception is caught while sending or receiving data - moves to Closed.
        /// </summary>
        Open,
        /// <summary>
        /// The endpoint is being closed gracefully.
        /// We are in the process of closing down both ends.
        /// In this state, a separate flag indicates if:
        ///		This endpoint is waiting on a close reply from the other end.
        ///		The remote endpoint sets its flag indicating it is waiting on the close reply.
        ///	Once a close reply is received, the endpoint changes to closed.
        /// </summary>
        Shutting_Down,
        /// <summary>
        /// Connection was lost without a graceful close.
        /// Maybe we received a null.
        /// Maybe we got an io exception on a read or write.
        /// </summary>
        Lost,
        /// <summary>
        /// A garbled message was received, or an oversized frame.
        /// Or, we are unable to exchange data with the remote endpoint for some framing reason.
        /// We can no longer use this endpoint instance.
        /// </summary>
        Error,
        /// <summary>
        /// The endpoint has been closed by both ends.
        /// </summary>
        Closed
    }
}
