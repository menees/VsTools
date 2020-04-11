namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	// Note: This started with code generated from http://artistalibre.ru/Tools/EnumTypeConverter.aspx.
	// But then I rewrote 90% of it to use LINQ, store the associated values in a member array, inherit
	// from StringConverter, etc.
	public class GuidFormatStringConverter : StringConverter
	{
		#region Public Constants

		public const GuidFormat DefaultFormat = GuidFormat.Dashes;
		public const string DefaultFormatText = "00000000-0000-0000-0000-000000000000";

		#endregion

		#region Private Data Members

		// This isn't a dictionary because (a) we need to search it by keys and values, (b) it's so small
		// that we'll never notice the performance hit of a sequential search vs. a hashed search, and
		// (c) we want to preserve the order for GetStandardValues.
		private static readonly Tuple<string, GuidFormat>[] Associations = new[]
		{
			new Tuple<string, GuidFormat>("00000000000000000000000000000000", GuidFormat.Numbers),
			new Tuple<string, GuidFormat>(DefaultFormatText, DefaultFormat),
			new Tuple<string, GuidFormat>("{00000000-0000-0000-0000-000000000000}", GuidFormat.Braces),
			new Tuple<string, GuidFormat>("(00000000-0000-0000-0000-000000000000)", GuidFormat.Parentheses),
			new Tuple<string, GuidFormat>("{0x00000000,0x0000,0x0000,{0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00}}", GuidFormat.Structure),
		};

		#endregion

		#region Public Methods

		public static string ToString(GuidFormat format)
		{
			string result = Associations.Where(a => a.Item2 == format).Select(a => a.Item1).FirstOrDefault();
			if (string.IsNullOrEmpty(result))
			{
				result = ToString(DefaultFormat);
			}

			return result;
		}

		public static GuidFormat ToFormat(string text)
		{
			// Callers depend on this returning the default GuidFormat if the text is null or unrecognized.
			GuidFormat result = Associations.Where(a => a.Item1 == text).Select(a => a.Item2).FirstOrDefault();
			return result;
		}

		public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

		public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
			=> new StandardValuesCollection(Associations.Select(a => a.Item1).ToArray());

		#endregion
	}
}
