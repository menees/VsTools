namespace Menees.VsTools.BaseConverter
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	internal sealed class Converter
	{
		#region Private Data Members

		private const int BitsPerByte = 8;
		private const int Shift1Byte = BitsPerByte;
		private const int Shift2Bytes = 2 * BitsPerByte;
		private const int Shift3Bytes = 3 * BitsPerByte;

		private readonly Options options;
		private string binaryValue = string.Empty;
		private string decimalValue = string.Empty;
		private string hexValue = string.Empty;

		#endregion

		#region Constructors

		public Converter(string textValue, Options options, NumberBase numBase, NumberByteOrder byteOrder, NumberType numberType)
		{
			this.options = options;

			if (!string.IsNullOrEmpty(textValue))
			{
				switch (numBase)
				{
					case NumberBase.Binary:
						this.InitializeFromBinary(textValue, byteOrder, numberType);
						break;
					case NumberBase.Decimal:
						this.InitializeFromDecimal(textValue, byteOrder, numberType);
						break;
					case NumberBase.Hex:
						this.InitializeFromHex(textValue, byteOrder, numberType);
						break;
				}
			}
		}

		#endregion

		#region Public Properties

		public string BinaryValue => this.binaryValue;

		public string DecimalValue => this.decimalValue;

		public string HexValue => this.hexValue;

		#endregion

		#region Private Methods

		private static object GetConvertedValueForBytes(byte[] machineValueBytes, NumberType dataType)
		{
			object convertedValue = null;

			if (machineValueBytes != null)
			{
				switch (dataType)
				{
					case NumberType.Byte:
						convertedValue = machineValueBytes[0];
						break;

					case NumberType.Decimal:
						convertedValue = ToDecimal(machineValueBytes);
						break;

					case NumberType.Double:
						convertedValue = BitConverter.ToDouble(machineValueBytes, 0);
						break;

					case NumberType.Int32:
						convertedValue = BitConverter.ToInt32(machineValueBytes, 0);
						break;

					case NumberType.Int64:
						convertedValue = BitConverter.ToInt64(machineValueBytes, 0);
						break;

					case NumberType.Int16:
						convertedValue = BitConverter.ToInt16(machineValueBytes, 0);
						break;

					case NumberType.SByte:
						unchecked
						{
							convertedValue = (sbyte)machineValueBytes[0];
						}

						break;

					case NumberType.Single:
						convertedValue = BitConverter.ToSingle(machineValueBytes, 0);
						break;

					case NumberType.UInt32:
						convertedValue = BitConverter.ToUInt32(machineValueBytes, 0);
						break;

					case NumberType.UInt64:
						convertedValue = BitConverter.ToUInt64(machineValueBytes, 0);
						break;

					case NumberType.UInt16:
						convertedValue = BitConverter.ToUInt16(machineValueBytes, 0);
						break;
				}
			}

			return convertedValue;
		}

		private static decimal ToDecimal(byte[] bytes)
		{
			// Originally, from Nick Darnell on http://msdn2.microsoft.com/en-us/library/system.bitconverter_methods.aspx
			int[] bits = new int[sizeof(decimal) / sizeof(int)];

			unchecked
			{
				bits[0] = ((bytes[0] | (bytes[1] << Shift1Byte)) | (bytes[2] << Shift2Bytes)) | (bytes[3] << Shift3Bytes); // lo
				bits[1] = ((bytes[4] | (bytes[5] << Shift1Byte)) | (bytes[6] << Shift2Bytes)) | (bytes[7] << Shift3Bytes); // mid
				bits[2] = ((bytes[8] | (bytes[9] << Shift1Byte)) | (bytes[10] << Shift2Bytes)) | (bytes[11] << Shift3Bytes); // hi
				bits[3] = ((bytes[12] | (bytes[13] << Shift1Byte)) | (bytes[14] << Shift2Bytes)) | (bytes[15] << Shift3Bytes); // flags
			}

			return new decimal(bits);
		}

		private static byte[] GetBytes(decimal d)
		{
			// Originally, from Nick Darnell on http://msdn2.microsoft.com/en-us/library/system.bitconverter_methods.aspx
			byte[] bytes = new byte[sizeof(decimal)];

			int[] bits = decimal.GetBits(d);
			int lo = bits[0];
			int mid = bits[1];
			int hi = bits[2];
			int flags = bits[3];
			bytes[0] = (byte)lo;
			bytes[1] = (byte)(lo >> Shift1Byte);
			bytes[2] = (byte)(lo >> Shift2Bytes);
			bytes[3] = (byte)(lo >> Shift3Bytes);
			bytes[4] = (byte)mid;
			bytes[5] = (byte)(mid >> Shift1Byte);
			bytes[6] = (byte)(mid >> Shift2Bytes);
			bytes[7] = (byte)(mid >> Shift3Bytes);
			bytes[8] = (byte)hi;
			bytes[9] = (byte)(hi >> Shift1Byte);
			bytes[10] = (byte)(hi >> Shift2Bytes);
			bytes[11] = (byte)(hi >> Shift3Bytes);
			bytes[12] = (byte)flags;
			bytes[13] = (byte)(flags >> Shift1Byte);
			bytes[14] = (byte)(flags >> Shift2Bytes);
			bytes[15] = (byte)(flags >> Shift3Bytes);

			return bytes;
		}

		private static bool ValidateAndNormalizeByteText(ref string textValue, NumberByteOrder byteOrder, NumberBase numBase, int numRequiredBytes)
		{
			// Ignore whitespace (leading, trailing, and between digit groups) and non-printable control characters.
			textValue = new string(textValue.Where(ch => !char.IsWhiteSpace(ch) && !char.IsControl(ch)).ToArray());

			// Ignore 0x prefix for hex
			if (numBase == NumberBase.Hex && textValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				textValue = textValue.Substring(2);
			}

			int numCharsPerByte = numBase == NumberBase.Binary ? BitsPerByte : 2;
			int numCharsTotal = textValue.Length;

			// I'm depending on the truncation of integer division here.
			int numWholeBytes = numCharsTotal / numCharsPerByte;
			int numLeftoverCharacters = numCharsTotal % numCharsPerByte;
			bool validLength = numWholeBytes == numRequiredBytes && numLeftoverCharacters == 0;

			// If necessary, we can pad up to the number of required bytes, but we can never truncate.
			if (!validLength && numWholeBytes < numRequiredBytes)
			{
				int numRequiredChars = numCharsPerByte * numRequiredBytes;
				if (byteOrder == NumberByteOrder.Numeric)
				{
					textValue = new string('0', numRequiredChars - numCharsTotal) + textValue;
					validLength = true;
				}
				else
				{
					// For big and little endian, we'll left pad up to the next full byte, then we'll right pad the rest of the data.
					// This seems the most reasonable strategy since entering a single byte's data is a "Numeric" entry, which
					// normally zero pads on the left.  But since Endianness deals with storage and we enter data from left to
					// right, it seems reasonable to zero pad on the right to fill up the remaining unspecified bytes. For example,
					// if "F" is entered for a Hex Int32, we'll left pad that up to "0F" and then right pad it to "0F000000".
					int numLeftPadChars = numLeftoverCharacters == 0 ? 0 : numCharsPerByte - numLeftoverCharacters;
					textValue = new string('0', numLeftPadChars) + textValue + new string('0', numRequiredChars - numCharsTotal - numLeftPadChars);

					validLength = true;
				}
			}

			return validLength;
		}

		private static string TrimLeadingZeros(string result)
		{
			int nonZeroCharPos = -1;
			for (int i = 0; i < result.Length; i++)
			{
				char ch = result[i];
				if (ch != '0' && ch != ' ')
				{
					nonZeroCharPos = i;
					break;
				}
			}

			if (nonZeroCharPos > 0)
			{
				result = result.Substring(nonZeroCharPos);
			}
			else if (nonZeroCharPos < 0)
			{
				result = "0";
			}

			return result;
		}

		private static bool TryParse(string byteString, NumberBase numBase, out byte byteValue)
		{
			bool result;

			if (numBase == NumberBase.Hex)
			{
				result = byte.TryParse(byteString, NumberStyles.HexNumber, null, out byteValue);
			}
			else
			{
				result = true;
				byteValue = 0;
				const int UpperBound = BitsPerByte - 1;
				for (int i = UpperBound; i >= 0; i--)
				{
					char ch = byteString[UpperBound - i];
					if (ch == '1')
					{
						byteValue |= (byte)(1 << i);
					}
					else if (ch != '0')
					{
						result = false;
						break;
					}
				}
			}

			return result;
		}

		private static bool MachineOrderMatchesByteOrder(NumberByteOrder byteOrder)
		{
			// As far as this function is concerned, numeric and big endian are the same because they
			// both order from most significant down to least significant.
			bool result = (BitConverter.IsLittleEndian && byteOrder == NumberByteOrder.LittleEndian) ||
								(!BitConverter.IsLittleEndian && byteOrder != NumberByteOrder.LittleEndian);
			return result;
		}

		private static int GetNumBytesForNumberType(NumberType numberType)
		{
			int result = 0;

			switch (numberType)
			{
				case NumberType.Byte:
				case NumberType.SByte:
					result = sizeof(byte);
					break;

				case NumberType.Int16:
				case NumberType.UInt16:
					result = sizeof(short);
					break;

				case NumberType.Int32:
				case NumberType.UInt32:
				case NumberType.Single:
					result = sizeof(int);
					break;

				case NumberType.Double:
				case NumberType.Int64:
				case NumberType.UInt64:
					result = sizeof(double);
					break;

				case NumberType.Decimal:
					result = sizeof(decimal);
					break;
			}

			return result;
		}

		private static string[] SplitIntoByteStrings(string textValue, int numCharsPerByte)
		{
			Debug.Assert(textValue.Length % numCharsPerByte == 0, "Text must use an integral number of bytes.");

			int numBytes = textValue.Length / numCharsPerByte;
			List<string> byteStrings = new List<string>(numBytes);
			for (int groupIndex = 0; groupIndex < numBytes; groupIndex++)
			{
				string byteString = textValue.Substring(groupIndex * numCharsPerByte, numCharsPerByte);
				byteStrings.Add(byteString);
			}

			return byteStrings.ToArray();
		}

		private static byte[] EnsureRequestedByteOrder(byte[] machineValueBytes, NumberByteOrder byteOrder)
		{
			if (!MachineOrderMatchesByteOrder(byteOrder))
			{
				Array.Reverse(machineValueBytes);
			}

			return machineValueBytes;
		}

		private static byte[] GetMachineBytesFromText(string textValue, NumberByteOrder byteOrder, NumberBase numBase, int requiredNumBytes)
		{
			Debug.Assert(numBase == NumberBase.Binary || numBase == NumberBase.Hex, "GetMachineBytesFromText is only for binary and hex values.");

			byte[] result = null;
			bool validLength = ValidateAndNormalizeByteText(ref textValue, byteOrder, numBase, requiredNumBytes);
			if (validLength)
			{
				bool valid = true;
				string[] byteStrings = SplitIntoByteStrings(textValue, numBase == NumberBase.Binary ? BitsPerByte : 2);
				List<byte> byteList = new List<byte>(requiredNumBytes);
				foreach (string byteString in byteStrings)
				{
					if (TryParse(byteString, numBase, out byte byteValue))
					{
						byteList.Add(byteValue);
					}
					else
					{
						valid = false;
						break;
					}
				}

				if (valid)
				{
					result = byteList.ToArray();

					// Regardless of input byte order we must return bytes that match machine byte order
					// (i.e., BitConverter.IsLittleEndian) because our output will be passed to BitConverter.
					result = EnsureRequestedByteOrder(result, byteOrder);
				}
			}

			return result;
		}

		private void InitializeFromDecimal(string textValue, NumberByteOrder byteOrder, NumberType numberType)
		{
			const NumberStyles IntegerStyles = NumberStyles.Integer | NumberStyles.AllowThousands;

			byte[] machineValueBytes = null;
			switch (numberType)
			{
				case NumberType.Byte:
					byte byteValue;
					if (byte.TryParse(textValue, IntegerStyles, null, out byteValue))
					{
						machineValueBytes = new byte[1] { byteValue };
					}

					break;

				case NumberType.Int16:
					short shortValue;
					if (short.TryParse(textValue, IntegerStyles, null, out shortValue))
					{
						machineValueBytes = BitConverter.GetBytes(shortValue);
					}

					break;

				case NumberType.Int32:
					int intValue;
					if (int.TryParse(textValue, IntegerStyles, null, out intValue))
					{
						machineValueBytes = BitConverter.GetBytes(intValue);
					}

					break;

				case NumberType.Int64:
					long longValue;
					if (long.TryParse(textValue, IntegerStyles, null, out longValue))
					{
						machineValueBytes = BitConverter.GetBytes(longValue);
					}

					break;

				case NumberType.Single:
					float singleValue;
					if (float.TryParse(textValue, out singleValue))
					{
						machineValueBytes = BitConverter.GetBytes(singleValue);
					}

					break;

				case NumberType.Double:
					double doubleValue;
					if (double.TryParse(textValue, out doubleValue))
					{
						machineValueBytes = BitConverter.GetBytes(doubleValue);
					}

					break;

				case NumberType.Decimal:
					decimal decimalValue;
					if (decimal.TryParse(textValue, out decimalValue))
					{
						machineValueBytes = GetBytes(decimalValue);
					}

					break;

				case NumberType.SByte:
					sbyte sbyteValue;
					if (sbyte.TryParse(textValue, IntegerStyles, null, out sbyteValue))
					{
						unchecked
						{
							byte castValue = (byte)sbyteValue;
							machineValueBytes = new byte[1] { castValue };
						}
					}

					break;

				case NumberType.UInt16:
					ushort ushortValue;
					if (ushort.TryParse(textValue, IntegerStyles, null, out ushortValue))
					{
						machineValueBytes = BitConverter.GetBytes(ushortValue);
					}

					break;

				case NumberType.UInt32:
					uint uintValue;
					if (uint.TryParse(textValue, IntegerStyles, null, out uintValue))
					{
						machineValueBytes = BitConverter.GetBytes(uintValue);
					}

					break;

				case NumberType.UInt64:
					ulong ulongValue;
					if (ulong.TryParse(textValue, IntegerStyles, null, out ulongValue))
					{
						machineValueBytes = BitConverter.GetBytes(ulongValue);
					}

					break;
			}

			if (machineValueBytes != null)
			{
				this.decimalValue = textValue;
				byte[] requestedOrderBytes = EnsureRequestedByteOrder(machineValueBytes, byteOrder);
				this.hexValue = this.GenerateHexString(requestedOrderBytes, byteOrder);
				this.binaryValue = this.GenerateBinaryString(requestedOrderBytes, byteOrder);
			}
		}

		private void InitializeFromBinary(string textValue, NumberByteOrder byteOrder, NumberType numberType)
		{
			int requiredNumBytes = GetNumBytesForNumberType(numberType);
			byte[] machineValueBytes = GetMachineBytesFromText(textValue, byteOrder, NumberBase.Binary, requiredNumBytes);
			object convertedValue = GetConvertedValueForBytes(machineValueBytes, numberType);

			if (convertedValue != null)
			{
				this.decimalValue = this.FormatDecimalValue(convertedValue);
				this.binaryValue = textValue;
				byte[] requestedOrderBytes = EnsureRequestedByteOrder(machineValueBytes, byteOrder);
				this.hexValue = this.GenerateHexString(requestedOrderBytes, byteOrder);
			}
		}

		private void InitializeFromHex(string textValue, NumberByteOrder byteOrder, NumberType numberType)
		{
			int requiredNumBytes = GetNumBytesForNumberType(numberType);
			byte[] machineValueBytes = GetMachineBytesFromText(textValue, byteOrder, NumberBase.Hex, requiredNumBytes);
			object convertedValue = GetConvertedValueForBytes(machineValueBytes, numberType);

			if (convertedValue != null)
			{
				this.decimalValue = this.FormatDecimalValue(convertedValue);
				this.hexValue = textValue;
				byte[] requestedOrderBytes = EnsureRequestedByteOrder(machineValueBytes, byteOrder);
				this.binaryValue = this.GenerateBinaryString(requestedOrderBytes, byteOrder);
			}
		}

		private string FormatDecimalValue(object convertedValue)
		{
			string formatStr = "{0}";
			if (this.options.UseGroupDelimiterForDecimal)
			{
				TypeCode code = Type.GetTypeCode(convertedValue.GetType());
				switch (code)
				{
					case TypeCode.Single:
					case TypeCode.Double:
					case TypeCode.Decimal:
						// For floating-point types, we have to use a custom format to
						// get the grouping separator and all significant decimal digits.
						// This allows 29 digits after the decimal point for System.Decimal.
						// http://stackoverflow.com/questions/295877/format-a-number-with-commas-but-keep-decimals
						formatStr = "{0:#,##0.#############################}";
						break;

					default:
						// For integral types, the "N" format will use the group separator.
						formatStr = "{0:N0}";
						break;
				}
			}

			string result = string.Format(formatStr, convertedValue);
			return result;
		}

		private string GenerateBinaryString(byte[] requestedOrderBytes, NumberByteOrder byteOrder)
		{
			StringBuilder sb = new StringBuilder(BitsPerByte * requestedOrderBytes.Length);

			foreach (byte currentByte in requestedOrderBytes)
			{
				if (sb.Length > 0 && this.options.UseByteSpaceSeparators)
				{
					sb.Append(' ');
				}

				for (int i = BitsPerByte - 1; i >= 0; i--)
				{
					bool isBitSet = (currentByte & (1 << i)) != 0;
					sb.Append(isBitSet ? "1" : "0");
				}
			}

			string result = sb.ToString();
			if (this.ShouldTrimLeadingZeros(byteOrder))
			{
				result = TrimLeadingZeros(result);
			}

			return result;
		}

		private string GenerateHexString(byte[] requestedOrderBytes, NumberByteOrder byteOrder)
		{
			StringBuilder sb = new StringBuilder(2 * requestedOrderBytes.Length);

			foreach (byte b in requestedOrderBytes)
			{
				if (sb.Length > 0 && this.options.UseByteSpaceSeparators)
				{
					sb.Append(' ');
				}

				sb.AppendFormat("{0:X2}", b);
			}

			string result = sb.ToString();
			if (this.ShouldTrimLeadingZeros(byteOrder))
			{
				result = TrimLeadingZeros(result);
			}

			return result;
		}

		private bool ShouldTrimLeadingZeros(NumberByteOrder byteOrder)
		{
			bool result = byteOrder == NumberByteOrder.Numeric ? this.options.TrimLeadingZerosNumeric : this.options.TrimLeadingZerosEndian;
			return result;
		}

		#endregion
	}
}
