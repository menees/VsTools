namespace Menees.VsTools.Sort
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	// Note: The MainPackage has a ProvideOptionPage attribute that associates this class with that package.
	[Guid(Guids.SortOptionsString)]
	[DefaultProperty(nameof(OnlyShowSortMembersDialogWhenShiftIsPressed))] // Make this get focus in the PropertyGrid first.
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by VS.")]
	internal sealed class Options : OptionsBase
	{
		#region Internal Constants

		internal const string DefaultCaption = nameof(Sort);

		#endregion

		#region Constructors

		public Options()
		{
		}

		#endregion

		#region Public Browsable Properties (for Options page)

		[Category("Members")]
		[DisplayName("Sort Members order")]
		[Description("A comma-separated list of member properties to order by. Prefix a property with '-' to order it descending. " +
			"The default ordering is: Kind, Access, IsStatic, KindModifier, ConstModifier, OverrideModifier, Name, ParameterCount.")]
		[DefaultValue(null)]
		public string SortMembersOrder { get; set; }

		[Category("Members")]
		[DisplayName("Only show Sort Members dialog when shift is pressed")]
		[Description("Provides a way to suppress the display of the Sort Members dialog unless the Shift key is pressed. " +
			"If the dialog is suppressed, then selected members will be sorted.")]
		[DefaultValue(false)]
		public bool OnlyShowSortMembersDialogWhenShiftIsPressed { get; set; }

		[Category("Lines")]
		[DisplayName("Only show Sort Lines dialog when shift is pressed")]
		[Description("Provides a way to suppress the display of the Sort Lines dialog unless the Shift key is pressed. " +
			"If the dialog is suppressed, then the lines in the selected text will be sorted.")]
		[DefaultValue(false)]
		public bool OnlyShowSortLinesDialogWhenShiftIsPressed { get; set; }

		#endregion

		#region Public Non-Browsable Properties (for other state persistence)

		[Browsable(false)]
		public LineOptions LineOptions { get; set; }

		#endregion
	}
}
