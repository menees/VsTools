namespace Menees.VsTools.BaseConverter
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	#region NumberBase

	internal enum NumberBase
	{
		Binary = 2,
		Decimal = 10,
		Hex = 16,
	}

	#endregion

	#region NumberByteOrder

	// Note: These must match the order of the items in the byteOrder combo box.
	internal enum NumberByteOrder
	{
		Numeric = 0,
		LittleEndian = 1,
		BigEndian = 2,

		// Note: The only important difference between Numeric and BigEndian is
		// how BaseConverter.ValidateAndNormalizeByteText zero pads the values.
		// For Numeric, the values are always left-padded.  For BigEndian, a partial
		// byte is left-padded, then the rest is right-padded.
	}

	#endregion

	#region NumberType

	// Note: These must match the order of the items in the dataType combo box.
	internal enum NumberType
	{
		SByte,
		Byte,
		Int16,
		UInt16,
		Int32,
		UInt32,
		Int64,
		UInt64,
		Single,
		Double,
		Decimal,
	}

	#endregion
}
