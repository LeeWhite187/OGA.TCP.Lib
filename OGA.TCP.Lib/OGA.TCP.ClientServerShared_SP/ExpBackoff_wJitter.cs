using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OGA.TCP.Shared
{
    public class ExpBackoff_wJitter
    {
        private readonly int m_maxRetries;
        private readonly int m_delayMilliseconds;
        private readonly int m_maxDelayMilliseconds;
        private int m_retries, m_pow;
        private bool maxreached;
        private float _jitterheight;
        private Random rnd = new Random();

        public bool EnableJitter { get; set; } = false;

        public bool AtMax { get=> maxreached; }

        /// <summary>
        /// Accepts a jitter factor between 0 and 1.
        /// </summary>
        public float JitterHeight
        {
            get => _jitterheight;
            set
            {
                if (value < 0.0f)
                    _jitterheight = 0.0f;
                else if (value > 1.0f)
                    _jitterheight = 0;
                else
                    _jitterheight = value;
            }
        }

        public ExpBackoff_wJitter(int maxRetries, int delayMilliseconds,
            int maxDelayMilliseconds)
        {
            m_maxRetries = maxRetries;
            m_delayMilliseconds = delayMilliseconds;
            m_maxDelayMilliseconds = maxDelayMilliseconds;
            m_retries = 0;
            m_pow = 1;
            maxreached = false;

            JitterHeight = 0.2f;
        }

        public void Reset()
        {
            m_retries = 0;
            m_pow = 1;
            maxreached = false;
        }

        public async Task<int> DelayAsync(CancellationToken? token = null)
        {
            var delay = CalculateDelay();

            // Wait the corresponding delay...
            if(token == null)
                await Task.Delay(delay);
            else
                await Task.Delay(delay, (CancellationToken)token);

            return 1;
        }
        public int Delay(CancellationToken? token = null)
        {
            var delay = CalculateDelay();

            // Wait the corresponding delay...
            if(token == null)
                System.Threading.Thread.Sleep(delay);
            else
                Perform_Abortable_Delay(delay, (CancellationToken)token);

            return 1;
        }

        public int CalculateDelay()
        {
            // Calculate the delay for this retry...
            int delay = 0;

            // Check if the range is zero...
            if(m_delayMilliseconds == m_maxDelayMilliseconds)
            {
                // Constant value.

                maxreached = true;

                delay = m_maxDelayMilliseconds;
            }
            else
            {
                // A range exists.

                // Increment the try counter...
                ++m_retries;
                if (m_retries < 31)
                {
                    m_pow = m_pow << 1; // m_pow = Pow(2, m_retries - 1)
                }

                // Calculate the delay for this retry...
                if(!maxreached)
                    delay = Math.Min(m_delayMilliseconds * (m_pow - 1) / 2, m_maxDelayMilliseconds);
                else
                    delay = m_maxDelayMilliseconds;

                // Check if the calculated delay has reached the max allowed value...
                // This will indicate that we no longer have to calculate the delay.
                if (delay == m_maxDelayMilliseconds)
                    maxreached = true;
            }


            // Include jitter if enabled...
            if(EnableJitter)
            {
                // Determine the jitter envelope...
                var je = delay * JitterHeight;

                // Get the random jitter...
                int scaled_jitter = (int)Math.Ceiling(je);

                // Get a random jitter...
                int rj = rnd.Next(0, scaled_jitter);

                // Slide the jitter value down to center it at the delay value...
                int centered_jitter = (int)(rj - (scaled_jitter * 0.5f));

                // Now, add the centered jitter value...
                delay = delay + centered_jitter;
            }

            // Constrain at zero...
            if (delay < 0)
                delay = 0;

            return delay;
        }

        /// <summary>
        /// Waits for a specified duration (in msec), or an abort to occur.
        /// This is a new version that allows for both positive and negative asserted aborts.
        /// Returns 1 if the wait expired.
        /// Returns 0 if the wait was cancelled before the wait time expired.
        /// </summary>
        /// <param name="wait_duration">Amount of time to wait, in milliseconds.</param>
        /// <param name="token">Cancellation token for leaving wait early.</param>
        /// <returns></returns>
        private int Perform_Abortable_Delay(int wait_duration, CancellationToken token)
        {
            int sleep_duration = 50;

            // Calculate our return target time.
            DateTime target = System.DateTime.Now.AddMilliseconds(wait_duration);

            // Continue to delay while the token is not cancelled...
            while (!token.IsCancellationRequested)
            {
                // Sleep for a little bit.
                System.Threading.Thread.Sleep(sleep_duration);

                // See if it's time to return.
                if (System.DateTime.Now.CompareTo(target) == 1)
                {
                    return 1;
                }
            }

            return 0;
        }

    }
}
