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
	using System.Windows.Media;
	using Microsoft.VisualStudio.Text.Classification;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	[ClassificationFormatCategory(VsFormatCategory.FindResults)]
	internal static class FindResultsFormats
	{
		#region Private Data Members

		private const string ClassificationPrefix = ClassificationFormatBase.ClassificationBasePrefix + "Find Results";

		#endregion

		#region FileName

		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = FileNameFormat.ClassificationName)]
		[Name(FileNameFormat.ClassificationName)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class FileNameFormat : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "FileName";

			public FileNameFormat()
				: base(FileNameFormat.ClassificationName)
			{
				this.ForegroundColor = Colors.Green;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(FileNameFormat.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition FileNameType { get; set; }
		}

		#endregion

		#region Detail

		[UserVisible(true)]
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = DetailFormat.ClassificationName)]
		[Name(DetailFormat.ClassificationName)]
		[Order(Before = Priority.Default)]
		internal sealed class DetailFormat : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "Detail";

			public DetailFormat()
				: base(DetailFormat.ClassificationName)
			{
				this.ForegroundColor = Colors.DimGray;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(DetailFormat.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition DetailType { get; set; }
		}

		#endregion

		#region Match

		[UserVisible(true)]
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = MatchFormat.ClassificationName)]
		[Name(MatchFormat.ClassificationName)]
		[Order(Before = Priority.Default)]
		internal sealed class MatchFormat : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "Match";

			public MatchFormat()
				: base(MatchFormat.ClassificationName)
			{
				this.ForegroundColor = Colors.Blue;
				this.IsBold = true;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(MatchFormat.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition MatchType { get; set; }
		}

		#endregion
	}
}
