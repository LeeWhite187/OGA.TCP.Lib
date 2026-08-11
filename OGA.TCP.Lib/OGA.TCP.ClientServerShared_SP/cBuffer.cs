using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP
{
	/// <summary>
	/// Grow-only byte buffer wrapper: allocated on first use, resized only when a larger frame requires it,
	///     so steady-state receive traffic reuses one allocation.
	/// </summary>
	public class cBuffer
	{
        /// <summary>
        /// Logger instance. Optional.
        /// </summary>
        protected NLog.ILogger Logger;

		private byte[] _buffer;

		/// <summary>
		/// The backing byte array. Null until the first Resize_Buffer_if_Needed call, and after Dispose.
		/// </summary>
		public byte[] Buffer
		{
			get
			{
				return _buffer;
			}
		}

		/// <summary>
		/// Current allocated length of the backing array.
		/// </summary>
		public int Length
		{
			get
			{
				return _buffer.Length;
			}
		}

		/// <summary>
		/// Constructor accepts an optional logger.
		/// </summary>
		/// <param name="logger">Optional logger instance.</param>
		public cBuffer(NLog.ILogger logger = null)
		{
            this.Logger = logger;
		}

		/// <summary>
		/// Releases the backing array. The instance can be reused; the next resize call re-allocates.
		/// </summary>
		public void Dispose()
		{
			// Clear and empty the buffer.
			this._buffer = null;
		}

		/// <summary>
		/// Public method used to resize the buffer.
		/// Will only grow the buffer if actually needed.
		/// </summary>
		/// <param name="needed_size"></param>
		public void Resize_Buffer_if_Needed(int needed_size)
		{
			if (this._buffer == null)
			{
				Logger?.Debug(
					"Creating buffer for first time.");

				this._buffer = new byte[needed_size];

				return;
			}
			// The buffer exists.

			// See if it is large enough.
			if (this._buffer.Length < needed_size)
			{
				// Log a message here.
				Logger?.Debug(
					"Resizing buffer from " + this._buffer.Length.ToString() + " to " + needed_size.ToString() + " bytes.");

				Array.Resize<byte>(ref this._buffer, needed_size);
			}

			Logger?.Debug(
					"Buffer has been resized.");

			// The buffer is adequately sized now.
			return;
		}
	}
}
