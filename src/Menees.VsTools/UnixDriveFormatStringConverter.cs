namespace Menees.VsTools;

#region Using Directives

using System.ComponentModel;
using System.Linq;

#endregion

internal sealed class UnixDriveFormatStringConverter : StringConverter
{
	#region Public Constants

	public const UnixDriveFormat DefaultFormat = UnixDriveFormat.LowerLetter;
	public const string DefaultFormatText = "/x/   (Git Bash)";

	#endregion

	#region Private Data Members

	// This isn't a dictionary because (a) we need to search it by keys and values, (b) it's so small
	// that we'll never notice the performance hit of a sequential search vs. a hashed search, and
	// (c) we want to preserve the order for GetStandardValues.
	private static readonly (string Text, UnixDriveFormat Format)[] Associations =
	[
		(DefaultFormatText, DefaultFormat),
		("/mnt/x/   (WSL)", UnixDriveFormat.MountLowerLetter),
		("X:/   (Windows)", UnixDriveFormat.UpperLetterColon),
	];

	#endregion

	#region Public Methods

	public static string ToString(UnixDriveFormat format)
	{
		string result = Associations.Where(a => a.Format == format).Select(a => a.Text).FirstOrDefault();
		if (string.IsNullOrEmpty(result))
		{
			result = ToString(DefaultFormat);
		}

		return result;
	}

	public static UnixDriveFormat ToFormat(string text)
	{
		// Callers depend on this returning the default UnixDriveFormat if the text is null or unrecognized.
		UnixDriveFormat result = Associations.Where(a => a.Text == text).Select(a => a.Format).FirstOrDefault();
		return result;
	}

	public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;

	public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

	public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		=> new(Associations.Select(a => a.Text).ToArray());

	#endregion
}
