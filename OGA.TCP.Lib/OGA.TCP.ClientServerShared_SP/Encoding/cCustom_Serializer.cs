using System;
using System.Collections.Generic;
using System.Text;

namespace OGA.TCP.Shared.Encoding
{
	public class cCustom_Serializer
	{
		static public int size_of_bool = 1;
		static public int size_of_Int32 = 4;
		static public int size_of_short = 4;
		static public int size_of_float = 4;
		static public int size_of_double = 8;
		static public int size_of_long = 8;
		static public int size_of_DateTime = 8;
		static public int size_of_GUID = 16;
		static public int size_of_Version = 16;


		#region Serializers

		/// <summary>
		/// Static public method used to convert from a string array to a byte array.
		/// </summary>
		/// <param name="Input"></param>
		/// <param name="Output"></param>
		/// <returns></returns>
		static public int Serialize_StringArray(ref string[] input, ref byte[] output, int offset)
		{
			int string_count = 0;
			int string_index = 0;
			int byte_offset = offset;
			int required_array_length;
			int Result = 0;

			// Perform validation checks.

			// Check that the input array has at least one string.
			if (input == null)
			{
				// The input array is null.

				// Zero the count of the number of stored strings.
				string_count = 0;
			}
			else
			{
				// The input array is not null.

				// Get a count of the number of stored strings.
				string_count = input.Length;
			}

			// Create a header structure to hold meta-data.
			cMultiString_MetaData m = new cMultiString_MetaData();

			// Populate the metadata structure with information from the string array.
			m.Populate_Metadata(input);

			// Determine how large the output array needs to be to hold the serialized data.
			required_array_length = m.Stored_Size + m.Total_String_Length;

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it and make it large enough to hold the string array and its metadata with the desired offset.
				output = new byte[byte_offset + required_array_length];

			}
			// The output array exists.

			// See if the given buffer can hold our string array.
			if ((output.Length - byte_offset) < required_array_length)
			{
				// The data won't fit into the given output buffer.

				// Return the number of bytes that are required to hold all data.
				// Return this number as a negative value.
				return -required_array_length;
			}
			// The string data fits into the buffer at the desired offset.

			// Store the metadata.
			Result = m.Serialize(ref output, byte_offset);

			// See if the metadata was stored successfully.
			if (Result < 0)
			{
				// An error occurred.

				// We cannot return an error code as this will be construed as a necessary size.
				// So, we will throw an exception here.
				throw new System.Exception("An error occurred while attempting to store the array's metadata.");
			}
			// The metadata was stored in the array.

			// Update our byte array offset with how many bytes we've stored so far.
			byte_offset = byte_offset + Result;

			// Now, store the string data.
			while (string_index < string_count)
			{
				// Serialize the current string to the array.
				Result = cCustom_Serializer.Serialize_String(input[string_index], ref output, byte_offset);

				// See if the current string was stored successfully.
				if (Result < 0)
				{
					// An error occurred.
					return -1;
				}
				// The current string was stored in the array.

				// Update our byte array offset with how many bytes we've stored so far.
				byte_offset = byte_offset + Result;

				// Advance to the next string.
				string_index++;
			}
			// We have iterated all strings in the array, and stored them in the byte array.

			// Now, we can return how many bytes we've recorded.
			// This would be our current buffer pointer index minus our starting offset.
			return byte_offset - offset;
		}

		/// <summary>
		/// Static public method that serializes a string into an output byte array.
		/// This special version saves the length of the string at the beginning of the given buffer
		///		so that the proper length string can be later retrieved.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_String_with_EmbeddedLength(string input, ref byte[] output, int offset)
		{
			int string_length = 0;
			int byte_offset = offset;

			// Convert the string to bytes.
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);

			// Get the length of the string.
			string_length = input.Length;

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[byte_offset + bytes.Length + cCustom_Serializer.size_of_Int32];
			}
			// The output array exists.

			// See if the string data will fit in the output array at the desired offset.
			if ((output.Length - byte_offset) < (bytes.Length + cCustom_Serializer.size_of_Int32))
			{
				// The string data won't fit.

				// Return the number of bytes that are required to hold the string data.
				// Return this number as a negative value.
				return -(bytes.Length + cCustom_Serializer.size_of_Int32);
			}
			// The string data fits into the array at the desired offset.

			// Store the string length.
			int Result = cCustom_Serializer.Serialize_Integer32(string_length, ref output, byte_offset);

			// See if the length was saved correctly.
			if (Result < 0)
			{
				// An error occurred while recording the string length.

				// We cannot return an error code as this will be construed as a necessary size.
				// So, we will throw an exception here.
				throw new System.Exception("An error occurred while attempting to serialize a string's length.");
			}
			// The string length was saved.

			// Update the byte index.
			byte_offset = byte_offset + Result;

			// Now that we have saved the length, we need to save the string itself.
			Result = cCustom_Serializer.Serialize_String(input, ref output, byte_offset);

			// See if the string was saved correctly.
			if (Result < 0)
			{
				// An error occurred while recording the string data.

				// We cannot return an error code as this will be construed as a necessary size.
				// So, we will throw an exception here.
				throw new System.Exception("An error occurred while attempting to serialize a string.");
			}
			// The string was saved.

			// Update the byte index.
			byte_offset = byte_offset + Result;

			// Return the number of bytes saved to the caller.
			return byte_offset - offset;
		}

		/// <summary>
		/// Static public method that serializes a string into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_String(string input, ref byte[] output, int offset)
		{
			// Convert the string to bytes.
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the string data will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The string data won't fit.

				// Return the number of bytes that are required to hold the string data.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The string data fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		/// <summary>
		/// Static public method that serializes a boolean into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_Boolean(bool input, ref byte[] output, int offset)
		{
			// Convert the boolean to bytes.
			byte[] bytes = BitConverter.GetBytes(input);

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the serialized value will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The serialized value won't fit.

				// Return the number of bytes that are required to hold the serialized value.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The serialized value fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		/// <summary>
		/// Static public method that serializes a 32 bit float into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_Float(float input, ref byte[] output, int offset)
		{
			// Convert the float to bytes.
			byte[] bytes = BitConverter.GetBytes(input);

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the serialized value will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The serialized value won't fit.

				// Return the number of bytes that are required to hold the serialized value.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The serialized value fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		/// <summary>
		/// Static public method that serializes a double into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_Double(double input, ref byte[] output, int offset)
		{
			// Convert the double to bytes.
			byte[] bytes = BitConverter.GetBytes(input);

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the serialized value will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The serialized value won't fit.

				// Return the number of bytes that are required to hold the serialized value.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The serialized value fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		/// <summary>
		/// Static public method that serializes a long into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_Long(long input, ref byte[] output, int offset)
		{
			// Convert the long to bytes.
			byte[] bytes = BitConverter.GetBytes(input);

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the serialized value will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The serialized value won't fit.

				// Return the number of bytes that are required to hold the serialized value.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The serialized value fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		/// <summary>
		/// Static public method that serializes a 16 bit integer into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_Short(short input, ref byte[] output, int offset)
		{
			// Convert the short to bytes.
			byte[] bytes = BitConverter.GetBytes(input);

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the serialized value will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The serialized value won't fit.

				// Return the number of bytes that are required to hold the serialized value.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The serialized integer fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		/// <summary>
		/// Static public method that serializes a 32 bit integer into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_Integer32(int input, ref byte[] output, int offset)
		{
			// Convert the integer to bytes.
			byte[] bytes = BitConverter.GetBytes(input);

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the serialized value will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The serialized value won't fit.

				// Return the number of bytes that are required to hold the serialized value.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The serialized integer fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		/// <summary>
		/// Static public method that serializes a datetime into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_DateTime(DateTime input, ref byte[] output, int offset)
		{
			// Attempt to convert the incoming DateTime value to a byte array for storage.
			long ticks = input.Ticks;

			return cCustom_Serializer.Serialize_Long(ticks, ref output, offset);
		}

		/// <summary>
		/// Static public method that serializes a GUID into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_GUID(System.Guid input, ref byte[] output, int offset)
		{
			// Convert the long to bytes.
			byte[] bytes = input.ToByteArray();

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the serialized value will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The serialized value won't fit.

				// Return the number of bytes that are required to hold the serialized value.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The serialized value fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		// TODO: Need to add a test for the serialize version method.
		/// <summary>
		/// Static public method that serializes a Version into an output byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		static public int Serialize_Version(System.Version input, ref byte[] output, int offset)
		{
			// Convert the version to bytes.
			// A system version object is basically four int32 values: major, minor, revision, and build.
			// Create a byte array to hold each one.
			byte[] bytes = new byte[cCustom_Serializer.size_of_Version];

			// Serialize each component of the version object.
			cCustom_Serializer.Serialize_Integer32(input.Major, ref bytes, 0);
			cCustom_Serializer.Serialize_Integer32(input.Minor, ref bytes, 4);
			cCustom_Serializer.Serialize_Integer32(input.Revision, ref bytes, 8);
			cCustom_Serializer.Serialize_Integer32(input.Build, ref bytes, 12);

			// See if the output array exists yet.
			if (output == null)
			{
				// The output array does not exist.

				// Create it for use.
				output = new byte[offset + bytes.Length];
			}
			// The output array exists.

			// See if the serialized value will fit in the output array at the desired offset.
			if ((output.Length - offset) < bytes.Length)
			{
				// The serialized value won't fit.

				// Return the number of bytes that are required to hold the serialized value.
				// Return this number as a negative value.
				return -bytes.Length;
			}
			// The serialized value fits into the array at the desired offset.

			// Copy the bytes to the output array.
			Array.Copy(bytes, 0, output, offset, bytes.Length);

			// Return the number of bytes to the caller.
			return bytes.Length;
		}

		#endregion


		#region De-Serializers

		/// <summary>
		/// Static public method used to convert from a byte array into a string array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int DeSerialize_StringArray(ref byte[] input, int offset, out string[] output)
		{
			int String_Index = 0;
			int Byte_Index = offset;
			int Result = 0;
			string temp_string = "";

			output = new string[0];

			// Perform validation checks.

			// Check that the input array exists.
			if (input == null)
			{
				// The input array is null.

				// Return to the caller.
				return -1;
			}
			// The input array is not null.

			// Create a metadata instance.
			cMultiString_MetaData m = new cMultiString_MetaData();

			// Attempt to retrieve a header from the byte array.
			Result = m.Deserialize(ref input, Byte_Index);

			// See if the metadata was retrieved successfully.
			if (Result < 0)
			{
				// An error occurred.
				return -1;
			}
			// The metadata was recovered from the array.

			// Move the byte array Index to the byte just after the last deserialized byte.
			Byte_Index = Byte_Index + Result;

			// See if there any strings to be recovered.
			if (m.String_Count < 1)
			{
				// No strings to recover.

				// Return how many bytes were digested from the byte array.
				return Byte_Index - offset;
			}
			// There is at least one string to deserialize.

			// Check that the byte array is large enough to hold all the strings to be collected.
			if (input.Length < (Byte_Index + m.Total_String_Length))
			{
				// The byte array is not large enough to hold all strings and metadata.
				return -1;
			}
			// The byte array is large enough to hold metadata and all string contents.
			// Recover them all.

			// Set the size of the output array large enough to hold all strings to be recovered.
			output = new string[m.String_Count];

			// Parse through the byte array and retrieve each string.
			while (String_Index < m.String_Count)
			{
				// Recover and convert the necessary number of bytes for the current string.
				Result = cCustom_Serializer.Deserialize_String(ref input,
															   Byte_Index,
															   m.Lengths[String_Index],
															   out temp_string);

				// See if the current string was retrieved successfully.
				if (Result < 0)
				{
					// An error occurred.
					return -1;
				}
				// The string was recovered from the array.

				// Update the output array with the recovered string.
				output[String_Index] = temp_string;

				// Move the byte array Index to the byte just after the last deserialized string.
				Byte_Index = Byte_Index + Result;

				// Advance to the next string.
				String_Index++;
			}
			// We have iterated the byte array and recovered all strings.

			// Now, we can return how many bytes we've consumed, starting at the given offset.
			return Byte_Index - offset;
		}

		/// <summary>
		/// Static public method that deserializes a string from a byte array.
		/// This particular method flavor will interrogate the first four bytes for an integer corresponding
		///		to the length of the string.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_String_with_EmbeddedLength(ref byte[] input, int offset, out string output)
		{
			int Result = 0;
			int string_length = -1;
			int Byte_Index = offset;

			output = "";

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in byte array is large enough to hold an integer corresponding to the string length.
			if ((input.Length - offset) < cCustom_Serializer.size_of_Int32)
			{
				// The input array is not large enough an integer that tells us the length of the string.

				return -2;
			}
			// The input array is large enough to hold an integer that tells us the length of the string.

			// Attempt recovery of the string length.
			Result = cCustom_Serializer.Deserialize_Integer32(ref input, offset, out string_length);

			// See if an error occurred.
			if (Result < 0)
			{
				// An error occurred.
				return -3;
			}
			// We recovered an integer for the string's length.

			// Move the byte array Index to the byte just after the last deserialized byte.
			Byte_Index = Byte_Index + Result;

			// Give this length value a sanity check against the number of available bytes we can retrieve.
			if ((Byte_Index + string_length) > input.Length)
			{
				// The recovered string length indicates our string's data will run past the end of the input array.
				// We will assume an error for this.
				return -4;
			}
			// There are enough bytes to hold a string of the indicated length.

			// Make the call to recover the string from the array.
			Result = cCustom_Serializer.Deserialize_String(ref input, Byte_Index, string_length, out output);

			// See if an error occurred.
			if (Result < 0)
			{
				// An error occurred.
				return -5;
			}
			// We recovered the string.

			// Move the byte array Index to the byte just after the last deserialized byte.
			Byte_Index = Byte_Index + Result;

			// Return the number of recovered bytes.
			return Byte_Index - offset;
		}

		/// <summary>
		/// Static public method that deserializes a string from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_String(ref byte[] input, int offset, int length, out string output)
		{
			output = "";

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in byte array is large enough to hold the expected information.
			if ((input.Length - offset) < length)
			{
				// The input array is not large enough to hold the string data beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold the string data at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the string data.
				Array.Reverse(input, offset, length);
			}
			// The serialized string data can now be converted back to a string.

			// Recover the string data from the byte array at the desired offset.
			output = System.Text.Encoding.UTF8.GetString(input, offset, length);

			// Return the number of bytes read.
			return length;
		}

		/// <summary>
		/// Static public method that deserializes a boolean from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_Boolean(ref byte[] input, int offset, out bool output)
		{
			int number_of_bytes_in_bool = 1;
			output = false;

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in array is large enough to hold the expected information.
			if ((input.Length - offset) < number_of_bytes_in_bool)
			{
				// The input array is not large enough to hold a boolean beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold a boolean at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the serialized boolean.
				Array.Reverse(input, offset, number_of_bytes_in_bool);
			}
			// The serialized value can now be converted back to a boolean.

			// Recover the serialized boolean from the array at the desired offset.
			output = BitConverter.ToBoolean(input, offset);

			// Return the number of bytes read.
			return number_of_bytes_in_bool;
		}

		/// <summary>
		/// Static public method that deserializes a float from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_Float(ref byte[] input, int offset, out float output)
		{
			output = 0.0f;

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in array is large enough to hold the expected information.
			if ((input.Length - offset) < cCustom_Serializer.size_of_float)
			{
				// The input array is not large enough to hold a float beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold a float at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the serialized float.
				Array.Reverse(input, offset, cCustom_Serializer.size_of_float);
			}
			// The serialized value can now be converted back to a float.

			// Recover the serialized float from the array at the desired offset.
			output = BitConverter.ToSingle(input, offset);

			// Return the number of bytes read.
			return cCustom_Serializer.size_of_float;
		}

		/// <summary>
		/// Static public method that deserializes a double from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_Double(ref byte[] input, int offset, out double output)
		{
			output = 0.0;

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in array is large enough to hold the expected information.
			if ((input.Length - offset) < cCustom_Serializer.size_of_double)
			{
				// The input array is not large enough to hold a double beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold a double at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the serialized double.
				Array.Reverse(input, offset, cCustom_Serializer.size_of_double);
			}
			// The serialized value can now be converted back to a double.

			// Recover the serialized double from the array at the desired offset.
			output = BitConverter.ToDouble(input, offset);

			// Return the number of bytes read.
			return cCustom_Serializer.size_of_double;
		}

		/// <summary>
		/// Static public method that deserializes a long from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_Long(ref byte[] input, int offset, out long output)
		{
			output = 0;

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in array is large enough to hold the expected information.
			if ((input.Length - offset) < cCustom_Serializer.size_of_long)
			{
				// The input array is not large enough to hold a long beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold a long at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the serialized long.
				Array.Reverse(input, offset, cCustom_Serializer.size_of_long);
			}
			// The serialized value can now be converted back to a long.

			// Recover the serialized long from the array at the desired offset.
			output = BitConverter.ToInt64(input, offset);

			// Return the number of bytes read.
			return cCustom_Serializer.size_of_long;
		}

		/// <summary>
		/// Static public method that deserializes an integer 32 from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_Integer32(ref byte[] input, int offset, out int output)
		{
			output = 0;

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in array is large enough to hold the expected information.
			if ((input.Length - offset) < cCustom_Serializer.size_of_Int32)
			{
				// The input array is not large enough to hold an integer beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold an integer at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the serialized integer.
				Array.Reverse(input, offset, cCustom_Serializer.size_of_Int32);
			}
			// The serialized integer can now be converted back to an integer32.

			// Recover the serialized integer32 from the array at the desired offset.
			output = BitConverter.ToInt32(input, offset);

			// Return the number of bytes read.
			return cCustom_Serializer.size_of_Int32;
		}

		/// <summary>
		/// Static public method that deserializes a short from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_Short(ref byte[] input, int offset, out short output)
		{
			output = 0;

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in array is large enough to hold the expected information.
			if ((input.Length - offset) < cCustom_Serializer.size_of_short)
			{
				// The input array is not large enough to hold a short beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold a short at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the serialized short.
				Array.Reverse(input, offset, cCustom_Serializer.size_of_short);
			}
			// The serialized short can now be converted back to a short.

			// Recover the serialized short from the array at the desired offset.
			output = BitConverter.ToInt16(input, offset);

			// Return the number of bytes read.
			return cCustom_Serializer.size_of_short;
		}

		/// <summary>
		/// Static public method that deserializes a long from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_DateTime(ref byte[] input, int offset, out DateTime output)
		{
			long ticks = 0;
			output = DateTime.MinValue;

			int Result = cCustom_Serializer.Deserialize_Long(ref input, offset, out ticks);

			// See if we got an error.
			if (Result < 0)
			{
				// The called method returned an error.

				// Pass it back to the caller.
				return Result;
			}
			// We received a long from the buffer.

			// Set the datetime to the retrieve tick value.
			output = new DateTime(ticks);

			// Return the number of bytes read.
			return cCustom_Serializer.size_of_long;
		}

		/// <summary>
		/// Static public method that deserializes a Guid from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_GUID(ref byte[] input, int offset, out System.Guid output)
		{
			output = Guid.Empty;

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in array is large enough to hold the expected information.
			if ((input.Length - offset) < cCustom_Serializer.size_of_GUID)
			{
				// The input array is not large enough to hold a Guid beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold a Guid at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the serialized Guid.
				Array.Reverse(input, offset, cCustom_Serializer.size_of_GUID);
			}
			// The serialized value can now be converted back to a Guid.

			// The new GUID constructor does not allow offsetting in a byte array.
			// So if our offset is non-zero, we will need to copy data to an intermediate.
			if (offset == 0 && input.Length == cCustom_Serializer.size_of_GUID)
			{
				// The offset is zero.
				// We can call the GUID constructor directly.

				// Recover the serialized Guid from the array.
				output = new Guid(input);
			}
			else
			{
				// Either the offset is not zero, or the array is larger than 16 bytes.
				// We will need to copy to a zero-offset, specific length buffer before calling the GUID constructor.

				// Copy the desired bytes to a temp buffer.
				byte[] temp = new byte[cCustom_Serializer.size_of_GUID];
				Array.Copy(input, offset, temp, 0, cCustom_Serializer.size_of_GUID);

				// Recover the serialized Guid from the array at the desired offset.
				output = new Guid(temp);
			}

			// Return the number of bytes read.
			return cCustom_Serializer.size_of_GUID;
		}

		// TODO: Need to add a test for the deserialize version method.
		/// <summary>
		/// Static public method that deserializes a System.Version from a byte array.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <param name="output"></param>
		/// <returns></returns>
		static public int Deserialize_Version(ref byte[] input, int offset, out System.Version output)
		{
			int major = 0;
			int minor = 0;
			int revision = 0;
			int build = 0;
			output = new System.Version();

			// Check that the incoming array holds enough information at the desired offset.
			if (input == null)
			{
				// The passed in array is null.
				// No information is contained.

				return -1;
			}
			// The passed in array is not null.

			// See if the passed in array is large enough to hold the expected information.
			if ((input.Length - offset) < cCustom_Serializer.size_of_Version)
			{
				// The input array is not large enough to hold a version beginning at the offset.

				return -2;
			}
			// The input array is large enough to hold a version at the offset.

			// If the system architecture is little-endian (that is, little end first),
			// reverse the byte array.
			if (!BitConverter.IsLittleEndian)
			{
				// Reverse the bytes corresponding to the serialized version.
				Array.Reverse(input, offset, cCustom_Serializer.size_of_Version);
			}
			// The serialized value can now be converted back to a version.

			// We will recover each portion of the version instance and load it into a new version instance.
			cCustom_Serializer.Deserialize_Integer32(ref input, offset + 0, out major);
			cCustom_Serializer.Deserialize_Integer32(ref input, offset + 4, out minor);
			cCustom_Serializer.Deserialize_Integer32(ref input, offset + 8, out revision);
			cCustom_Serializer.Deserialize_Integer32(ref input, offset + 12, out build);

			// Load the pieces into a new version instance.
			System.Version v = new Version(major, minor, revision, build);

			// Set the output to our version reference.
			output = v;

			// Return the number of bytes read.
			return cCustom_Serializer.size_of_Version;
		}

		#endregion

		/// <summary>
		/// Static private method that creates a byte array representing a serialized
		///     empty string array.
		/// </summary>
		/// <returns></returns>
		static private byte[] Create_Serialized_Empty_Array(int offset)
		{
			// Create metadata instance for use.
			cMultiString_MetaData m = new cMultiString_MetaData();
			byte[] bytes = null;

			// Populate the metadata instance with an empty header.
			m.String_Count = 0;
			m.Lengths = new int[0];

			// Serialize the metadata instance.
			int Result = m.Serialize(ref bytes, offset);

			// Return the bytes to the caller.
			return bytes;
		}
	}

	/// <summary>
	/// Class used to hold metadata for a string array as it is serialized in a buffer.
	/// This class stores data such as the string count, and length of each string,
	///		so that they can be deserialized reliably.
	/// </summary>
	public class cMultiString_MetaData
	{
		// Number of represented strings.
		public int String_Count;

		//Length of each represented string.
		public int[] Lengths;

		/// <summary>
		/// The total number of characters across all listed strings.
		/// </summary>
		public int Total_String_Length
		{
			get
			{
				int total = 0;
				int Index = 0;

				// Totalize the lengths.
				while (Index < this.String_Count)
				{
					// Add in the character length of the current string.
					total = total + this.Lengths[Index];

					// Advance to the next string.
					Index++;
				}

				// Return the character sum.
				return total;
			}
		}

		/// <summary>
		/// The necessary number of bytes required to store the data stored in the instance.
		/// </summary>
		public int Stored_Size
		{
			get
			{
				// Return the current length.
				if (Lengths == null)
				{
					// The length array is not instanciated.
					// We don't know how much space it takes.

					// Return just the base size of the metadata array.
					return cCustom_Serializer.size_of_Int32;
				}
				// The length array exists.

				// Return how many bytes are required to store it.
				return Lengths.Length * cCustom_Serializer.size_of_Int32 + cCustom_Serializer.size_of_Int32;
			}
		}

		/// <summary>
		/// Serializes the object instance data into the passed in array beginning at the desired
		///     offset.
		/// </summary>
		/// <param name="output"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		public int Serialize(ref byte[] output, int offset)
		{
			int Stored_Byte_Count = 0;
			int Result = -1;
			int Index = 0;

			// Check if the output array is initialized.
			if (output == null)
			{
				output = new byte[offset + this.Stored_Size];
			}
			// The array exists.

			// Check that the array is large enough to hold the serialized data beginning at the offset.
			if ((output.Length - offset) < this.Stored_Size)
			{
				// The output array is not large enough to hold the serialized data.

				// Return the number of required bytes to be stored.
				return -this.Stored_Size;
			}
			// The output array is large enough to work with.

			// Serialize the structure to the array.

			// Serialize the count.
			Result = cCustom_Serializer.Serialize_Integer32(this.String_Count, ref output, offset + Stored_Byte_Count);

			// See if success occurred.
			if (Result < 0)
			{
				// The count would not fit.
				// We have already checked that everything would fit.
				// So, this is an error we cannot recover from.

				// Return an error message.
				return -1;
			}
			// The string count has been serialized.

			// Increment the serialized byte count.
			Stored_Byte_Count = Stored_Byte_Count + Result;

			// Check if the count is positive.
			if (this.String_Count < 1)
			{
				// There are no strings to work with.
				// We have saved the requisite header information (a zero for the count).

				// Return what we have.
				return Stored_Byte_Count;
			}
			// There is at least one string length to represent.

			// Iterate the lengths array and store each one.
			while (Index < this.Lengths.Length)
			{
				// Store the current length.
				Result = cCustom_Serializer.Serialize_Integer32(this.Lengths[Index], ref output, offset + Stored_Byte_Count);

				// See if success occurred.
				if (Result < 0)
				{
					// The length would not fit.
					// We have already checked that everything would fit.
					// So, this is an error we cannot recover from.

					// Return an error message.
					return -2;
				}
				// The current length has been serialized.

				// Increment the serialized byte count.
				Stored_Byte_Count = Stored_Byte_Count + Result;

				// Increment to the next length.
				Index++;
			}
			// We have serialized all lengths to the byte array.

			// Return what we have.
			return Stored_Byte_Count;
		}

		/// <summary>
		/// Deserializes the object instance data from the passed in array beginning at the desired
		///     offset.
		/// </summary>
		/// <param name="input"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		public int Deserialize(ref byte[] input, int offset)
		{
			int current_byte_offset = offset;
			int Result = -1;
			int Index = 0;

			// Check if the input array is initialized.
			if (input == null)
			{
				// The byte array is null.
				// No data to deserialize.
				return -1;
			}
			// The byte array exists.

			// Check that the byte array is large enough to hold the count of the number of strings contained.
			if ((input.Length - current_byte_offset) < cCustom_Serializer.size_of_Int32)
			{
				// The input array is not large enough to hold the count of the number of strings.

				// Return an error message.
				return -2;
			}
			// The input array is large enough to hold the count of the number of strings stored.

			// Deserialize the string count to the current instance.

			// Deserialize the count.
			Result = cCustom_Serializer.Deserialize_Integer32(ref input, current_byte_offset, out this.String_Count);

			// See if success occurred.
			if (Result < 0)
			{
				// The count was not recovered.
				// We have already checked that the array was large enough.
				// So, this is an error we cannot recover from.

				// Return an error message.
				return -1;
			}
			// The string count was deserialized.

			// Increment the deserialized byte count.
			current_byte_offset = current_byte_offset + Result;

			// Check if the count is positive.
			if (this.String_Count < 1)
			{
				// No strings are defined.
				// Clear the lengths array.
				this.Lengths = new int[0];

				// Return how many bytes we have digested.
				return current_byte_offset - offset;
			}
			// There is at least one string length to retrieve.

			// Set the local lengths array to the proper size.
			this.Lengths = new int[this.String_Count];

			// Check that the byte array has enough remaining data to hold the length of each stored string.
			if ((input.Length - current_byte_offset) < this.Stored_Size)
			{
				// The input array is not large enough to hold the count of the number of strings.

				// Return an error message.
				return -2;
			}
			// The input array is large enough to hold the count of the number of strings stored.

			// Iterate the byte array and retrieve each length.
			while (Index < this.String_Count)
			{
				// Retrieve the current length.
				Result = cCustom_Serializer.Deserialize_Integer32(ref input, current_byte_offset, out this.Lengths[Index]);

				// See if success occurred.
				if (Result < 0)
				{
					// The length would not fit.
					// We have already checked that everything would fit.
					// So, this is an error we cannot recover from.

					// Return an error message.
					return -2;
				}
				// The current length has been deserialized.

				// Increment the deserialized byte count.
				current_byte_offset = current_byte_offset + Result;

				// Increment to the next length.
				Index++;
			}
			// We have deserialized all lengths to the object instance.

			// Return how many bytes we have digested.
			return current_byte_offset - offset;
		}

		/// <summary>
		/// Internal method that interrogates the passed in string array and populates
		///     metadata for each string.
		/// </summary>
		/// <param name="input"></param>
		internal void Populate_Metadata(string[] input)
		{
			int Index = 0;

			if (input == null)
			{
				// Zero the string length.
				this.String_Count = 0;

				// No strings in the array.
				this.Lengths = new int[0];

				return;
			}
			// The given array is not null.
			// We will get its length and the length of each contained string.

			// Set the string length.
			this.String_Count = input.Length;

			// Create a lengths array the same size as the string array.
			this.Lengths = new int[input.Length];

			// Iterate each string and get its length.
			while (Index < this.String_Count)
			{
				// At the current string, store its length.
				this.Lengths[Index] = input[Index].Length;

				// Move to the next string.
				Index++;
			}
			// We have iterated each string and collected its length.

			// Return to the caller.
			return;
		}
	}
}
