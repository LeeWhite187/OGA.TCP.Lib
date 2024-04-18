using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.SessionLayer
{
    public class cEndpoint_Ping_Tracking
    {
        protected NLog.ILogger Logger;

        private bool _enabled;
        private int _ping_delay;
        private int _ping_reply_timeout;
        private ePingState _state;

        #region Public Properties

        /// <summary>
        ///  Time of the last ping attempt, waiting for a reply.
        /// </summary>
        public DateTime Last_Ping_Attempt { get; internal set; }

        /// <summary>
        /// Current state of ping.
        /// </summary>
        public ePingState State
        {
            get
            {
                UpdateState();
                return _state;
            }
        }

        /// <summary>
        /// Number of milliseconds to wait before sending a ping.
        /// </summary>
        public int Ping_Delay
        {
            get
            {
                return _ping_delay;
            }
            set
            {
                if(value <= 0)
                {
                    this._ping_delay = 20;
                }
                else
                {
                    this._ping_delay = value;
                }

                UpdateState();
            }
        }
        /// <summary>
        /// Number of milliseconds to wait after sending a ping that we consider the ping a failure.
        /// </summary>
        public int Ping_Reply_Timeout
        {
            get
            {
                return _ping_reply_timeout;
            }
            set
            {
                if (value <= 0)
                {
                    this._ping_reply_timeout = 20;
                }
                else
                {
                    this._ping_reply_timeout = value;
                }

                UpdateState();
            }
        }

        #endregion


        #region ctor / dtor

        public cEndpoint_Ping_Tracking(NLog.ILogger logger = null)
        {
            this.Logger = logger;

            this._enabled = false;

            this._ping_delay = 5000;
            this._ping_reply_timeout = 10000;
            this.Last_Ping_Attempt = System.DateTime.Now;

            UpdateState();
        }

        #endregion


        #region Public Methods

        public void Enable()
        {
            if (this._enabled)
            {
                // Already enabled.

                this.Logger?.Error("PingTracking already enabled. Cannot be enabled again.");
            }
            // Ping tracking is disabled.
            // Enable and reset it.

            this._enabled = true;

            this.UpdateState();
        }

        public void Disable()
        {
            if (this._enabled)
            {
                // Already enabled.

                this.Logger?.Error("PingTracking already enabled. Cannot be enabled again.");
            }

            this._enabled = false;

            this.UpdateState();
        }

        /// <summary>
        /// This method is called periodically to see what ping tracking says to do.
        /// </summary>
        /// <returns></returns>
        public ePingAction Get_Action()
        {
            if(this._state == ePingState.Disabled)
            {
                // Pings are disabled.
                // Reset things to a baseline.
                Reset();
                return ePingAction.None;
            }

            // Update state...
            UpdateState();

            if(this.State == ePingState.WaitingforReply)
            {
                // We are still waiting for a reply.
                return ePingAction.None;
            }
            else if(this.State == ePingState.Lost)
            {
                return ePingAction.CloseConnection;
            }
            else if (this.State == ePingState.WaitingtoSend)
            {
                // See if it's time to send the ping.
                System.DateTime nowtime = System.DateTime.Now;
                if (nowtime.CompareTo(this.Last_Ping_Attempt.AddMilliseconds(this._ping_delay)) > 0)
                {
                    // Our inter ping time delay has been exceeded.
                    // We are due to send another.

                    return ePingAction.SendPing;
                }
                else
                {
                    // We are waiting to send a ping.

                    // It is not yet time to send a ping.
                    return ePingAction.None;
                }
            }
            else
            {
                // Unknown ping state.

                this.Logger?.Error(
                        "Ping Tracking is in an unknown state {0}. Send connection to LOST.", this.State.ToString());

                // Lose the connection.
                this._state = ePingState.Lost;
                return ePingAction.None;
            }
        }

        public void Ping_was_Sent()
        {
            if (this._enabled)
            {
                // The caller reports that a ping was sent.
                // We will be waiting for a pong reply.
                this._state = ePingState.WaitingforReply;
            }
            else
            {
                this._state = ePingState.Disabled;
            }
            // Update our last attempt time.
            this.Last_Ping_Attempt = System.DateTime.Now;
        }

        /// <summary>
        /// Call this method to tell the ping tracker that a message was received from the remote end.
        /// This method is called for any received message, including the pong reply.
        /// They all tell us that the connection is alive.
        /// Treat each message as a means to reset the ping wait timer.
        /// </summary>
        public void Message_was_Received()
        {
            if(this._enabled)
            {
                this._state = ePingState.WaitingtoSend;
            }
            else
            {
                this._state = ePingState.Disabled;
            }
            this.Last_Ping_Attempt = System.DateTime.Now;
        }

        public void Reset()
        {
            if (this._enabled)
            {
                this._state = ePingState.WaitingtoSend;
            }
            else
            {
                this._state = ePingState.Disabled;
            }
            this.Last_Ping_Attempt = System.DateTime.Now;
        }

        #endregion

        private void UpdateState()
        {
            if (this._enabled)
            {
                if(this._state == ePingState.Disabled)
                {
                    // Just enabled.

                    // Slide the last attempt time to now since we just enabled.
                    this.Last_Ping_Attempt = System.DateTime.Now;

                    this._state = ePingState.WaitingtoSend;
                }
                else if (this._state == ePingState.Lost)
                {
                    // Already lost.
                    // No state changes from this except during the mesage received or disable.
                }
                else if (this._state == ePingState.WaitingforReply)
                {
                    // See if we have waited too long.
                    if (System.DateTime.Now.CompareTo(this.Last_Ping_Attempt.AddMilliseconds(this._ping_reply_timeout)) > 0)
                    {
                        // We did not receive a ping reply to our request in the timeout period.
                        // We have timed out.
                        // We must consider the connection lost.

                        // Update our ping state to lost.
                        this._state = ePingState.Lost;
                    }
                    else
                    {
                        // Stay in the waiting for reply state.
                    }
                }
                else if (this._state == ePingState.WaitingtoSend)
                {
                }
                else
                {
                    // Unknown ping state.

                    this.Logger?.Error(
                            "Ping Tracking is in an unknown state {0}. Send connection to LOST.", this.State.ToString());

                    // Lose the connection.
                    this._state = ePingState.Lost;
                }
            }
            else
            {
                // Just disabled.

                // Slide the last attempt time to now since we just enabled.
                this.Last_Ping_Attempt = System.DateTime.Now;

                this._state = ePingState.Disabled;
            }
        }
    }

    public enum ePingState
    {
        /// <summary>
        /// No ping activity occurs in this case.
        /// </summary>
        Disabled,
        /// <summary>
        /// Waiting for the timer to expire.
        /// </summary>
        WaitingtoSend,
        /// <summary>
        /// A Ping was sent. We are waiting for a reply.
        /// And, will timeout if we don't receive it.
        /// </summary>
        WaitingforReply,
        /// <summary>
        /// We did not receive a ping reply in time.
        /// The connection must be considered lost.
        /// </summary>
        Lost
    }

    public enum ePingAction
    {
        None,
        SendPing,
        CloseConnection
    }
}
