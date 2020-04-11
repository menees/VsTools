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
	using Microsoft.VisualStudio.Editor;
	using Microsoft.VisualStudio.Text.Classification;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	[ClassificationFormatCategory(VsFormatCategory.OutputWindow)]
	internal static class OutputFormats
	{
		#region Private Data Members

		private const string ClassificationPrefix = ClassificationFormatBase.ClassificationBasePrefix + "Output";

		#endregion

		#region Error

		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = ErrorFormat.ClassificationName)]
		[Name(ErrorFormat.ClassificationName)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class ErrorFormat : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "Error";

			public ErrorFormat()
				: base(ErrorFormat.ClassificationName)
			{
				this.ForegroundColor = Colors.Red;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(ErrorFormat.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition ErrorType { get; set; }
		}

		#endregion

		#region Warning

		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = WarningFormat.ClassificationName)]
		[Name(WarningFormat.ClassificationName)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class WarningFormat : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "Warning";

			public WarningFormat()
				: base(WarningFormat.ClassificationName)
			{
				// This is the same warning color used by MegaBuild.
				this.ForegroundColor = Colors.DarkOrchid;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(WarningFormat.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition WarningType { get; set; }
		}

		#endregion

		#region Information

		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = InformationFormat.ClassificationName)]
		[Name(InformationFormat.ClassificationName)]
		[UserVisible(true)]
		[Order(Before = Priority.Default)]
		internal sealed class InformationFormat : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "Information";

			public InformationFormat()
				: base(InformationFormat.ClassificationName)
			{
				this.ForegroundColor = Colors.Green;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(InformationFormat.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition InformationType { get; set; }
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

		#region Header

		[UserVisible(true)]
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = HeaderFormat.ClassificationName)]
		[Name(HeaderFormat.ClassificationName)]
		[Order(Before = Priority.Default)]
		internal sealed class HeaderFormat : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "Header";

			public HeaderFormat()
				: base(HeaderFormat.ClassificationName)
			{
				// This is the same header color used by MegaBuild.
				this.ForegroundColor = Colors.Blue;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(HeaderFormat.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition HeaderType { get; set; }
		}

		#endregion

		#region Custom1

		[UserVisible(true)]
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = Custom1Format.ClassificationName)]
		[Name(Custom1Format.ClassificationName)]
		[Order(Before = Priority.Default)]
		internal sealed class Custom1Format : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "Custom1";

			public Custom1Format()
				: base(Custom1Format.ClassificationName)
			{
				this.ForegroundColor = Colors.DarkGoldenrod;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(Custom1Format.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition Custom1Type { get; set; }
		}

		#endregion

		#region Custom2

		[UserVisible(true)]
		[Export(typeof(EditorFormatDefinition))]
		[ClassificationType(ClassificationTypeNames = Custom2Format.ClassificationName)]
		[Name(Custom2Format.ClassificationName)]
		[Order(Before = Priority.Default)]
		internal sealed class Custom2Format : ClassificationFormatBase
		{
			public const string ClassificationName = ClassificationPrefix + "Custom2";

			public Custom2Format()
				: base(Custom2Format.ClassificationName)
			{
				this.ForegroundColor = Colors.DarkOrange;
			}

			[Export(typeof(ClassificationTypeDefinition))]
			[Name(Custom2Format.ClassificationName)]
			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
			internal static ClassificationTypeDefinition Custom2Type { get; set; }
		}

		#endregion
	}
}
