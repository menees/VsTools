namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	public sealed partial class MainPackage
	{
		#region Internal Constants

		// These are factored into a separate partial file so the core MainPackage.cs file
		// won't be updated in source control for every release, which makes finding real
		// changes much easier.
		//
		// Note: When the version changes (major, minor, build, or revision), also update:
		// - source.extension.vsixmanifest: <Identity Version="*"/>
		internal const string Version = "2019.0.9";

		internal const string Title = "Menees VS Tools";

		#endregion
	}
}
