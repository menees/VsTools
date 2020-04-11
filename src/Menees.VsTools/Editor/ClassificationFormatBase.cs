namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel.Composition;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using Microsoft.VisualStudio.Text.Classification;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	// Note: Derived classes must still specify the attributes.
	internal abstract class ClassificationFormatBase : ClassificationFormatDefinition
	{
		#region Internal Constants

		internal const string ClassificationBasePrefix = MainPackage.Title + "/";

		#endregion

		#region Protected Methods

		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Created by MEF.")]
		protected ClassificationFormatBase(string classificationTypeName)
		{
			if (classificationTypeName.StartsWith(ClassificationBasePrefix))
			{
				classificationTypeName = classificationTypeName.Substring(ClassificationBasePrefix.Length);
			}

			// This clever Regex to split a mixed case identifier into words came from:
			// http://stackoverflow.com/questions/155303/net-how-can-you-split-a-caps-delimited-string-into-an-array/155340#155340
			string typeDisplayName = Regex.Replace(classificationTypeName, @"(\B[A-Z0-9])", " $1");

			// Note: This is required for VS to display the format.  It does not work to just put a DisplayNameAttribute on this class.
			this.DisplayName = MainPackage.Title + " " + typeDisplayName;
		}

		#endregion
	}
}
