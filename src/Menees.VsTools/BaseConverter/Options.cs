namespace Menees.VsTools.BaseConverter
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.Design;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Drawing.Design;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;
	using Menees.VsTools.Editor;
	using Microsoft.VisualStudio.Settings;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using Microsoft.VisualStudio.Shell.Settings;
	using Microsoft.Win32;
	#endregion

	// Note: The MainPackage has ProvideOptionPage and ProvideProfile attributes
	// that associate this class with our package.  Helpful pages:
	// http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.shell.dialogpage(v=vs.110).aspx
	// http://msdn.microsoft.com/en-us/library/bb162586(v=vs.110).aspx
	// http://bloggingabout.net/blogs/perikles/archive/2006/11/22/How-to-dynamically-Import_2F00_Export-setting-in-Visual-Studio-2005_2E00_.aspx
	[Guid(Guids.BaseConverterOptionsString)]
	[DefaultProperty(nameof(TrimLeadingZerosEndian))] // Make this get focus in the PropertyGrid first since its category is alphabetically first.
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by VS.")]
	internal class Options : OptionsBase
	{
		#region Constructors

		public Options()
		{
			this.UseByteSpaceSeparators = true;
			this.UseGroupDelimiterForDecimal = true;
			this.BaseConverterNumberType = NumberType.Int32;
		}

		#endregion

		#region Public Browsable Properties (for Options page)

		[Category("Base Converter")]
		[DisplayName("Trim leading Numeric binary/hex zeros")]
		[Description("Whether leading zeros should be removed on binary and hexadecimal values when using the Numeric byte order.")]
		[DefaultValue(false)]
		public bool TrimLeadingZerosNumeric { get; set; }

		[Category("Base Converter")]
		[DisplayName("Trim leading Endian binary/hex zeros")]
		[Description("Whether leading zeros should be removed on binary and hexadecimal values when using the Little Endian and Big Endian byte orders.")]
		[DefaultValue(false)]
		public bool TrimLeadingZerosEndian { get; set; }

		[Category("Base Converter")]
		[DisplayName("Use space as binary/hex byte delimiter")]
		[Description("Whether spaces should be used to separate bytes in binary and hexadecimal values.")]
		[DefaultValue(true)]
		public bool UseByteSpaceSeparators { get; set; }

		[Category("Base Converter")]
		[DisplayName("Use group delimiter for decimal values")]
		[Description("Whether the system's group delimiter should be used to separate groups of digits in the integral portion of decimal values.")]
		[DefaultValue(true)]
		public bool UseGroupDelimiterForDecimal { get; set; }

		#endregion

		#region Public Non-Browsable Properties (for other state persistence)

		[Browsable(false)]
		[Category("Base Converter")]
		[DisplayName("Byte Order")]
		[DefaultValue(NumberByteOrder.Numeric)]
		public NumberByteOrder BaseConverterByteOrder { get; set; }

		[Browsable(false)]
		[Category("Base Converter")]
		[DisplayName("Number Type")]
		[DefaultValue(NumberType.Int32)]
		public NumberType BaseConverterNumberType { get; set; }

		#endregion
	}
}
