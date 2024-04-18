using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP
{
	public class cBuffer
	{
        protected NLog.ILogger Logger;

		private byte[] _buffer;

		public byte[] Buffer
		{
			get
			{
				return _buffer;
			}
		}

		public int Length
		{
			get
			{
				return _buffer.Length;
			}
		}

		public cBuffer(NLog.ILogger logger = null)
		{
            this.Logger = logger;
		}

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
