namespace Menees.VsTools
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
	[Guid(Guids.GeneralOptionsString)]
	[DefaultProperty(nameof(IsMouseWheelZoomEnabled))] // Make this get focus in the PropertyGrid first since its category is alphabetically first.
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by VS.")]
	internal class Options : OptionsBase
	{
		#region Private Data Members

		private const string DefaultPredefinedRegions =
			"Using Directives\r\n" +
			"Private Data Members\r\n" +
			"Constructors\r\n" +
			"Public Properties\r\n" +
			"Internal Properties\r\n" +
			"Protected Properties\r\n" +
			"Private Properties\r\n" +
			"Public Methods\r\n" +
			"Public Events\r\n" +
			"Internal Methods\r\n" +
			"Protected Methods\r\n" +
			"Private Methods\r\n" +
			"Private Event Handlers\r\n" +
			"Private Types";

		private GuidFormat guidFormat;

		#endregion

		#region Constructors

		public Options()
		{
			// Fields
			this.guidFormat = GuidFormatStringConverter.DefaultFormat;

			// Displayed options
			// Note: Features that add to VS capabilities I've turned on by default.
			// However, features that remove/suppress VS behaviors (e.g., IsMouseWheelZoomEnabled)
			// are initialized where VS's standard behavior is retained.  I'll manually change those option
			// values in environments where I want to suppress the standard behavior.
			this.SaveAllBeforeExecuteFile = true;
			this.PredefinedRegions = DefaultPredefinedRegions;
			this.UppercaseGuids = true;
			this.IsMouseWheelZoomEnabled = true;

			// Other dialog state settings
			this.TrimEnd = true;
			this.SortAscending = true;
			this.SortIgnoreWhitespace = true;
		}

		#endregion

		#region Public Browsable Properties (for Options page)

		[Category("Miscellaneous")]
		[DisplayName("Save all files before Execute File")]
		[Description("Whether the Execute File command should save all open documents first.")]
		[DefaultValue(true)]
		public bool SaveAllBeforeExecuteFile { get; set; }

		[Category(nameof(Editor))]
		[DisplayName("Predefined #regions")]
		[Description("Defines the entries to include in the Add Region dialog.  Enter one region name per line in the drop-down editor.")]
		[Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
		[DefaultValue(DefaultPredefinedRegions)]
		public string PredefinedRegions { get; set; }

		[Category("Miscellaneous")]
		[DisplayName("Additional C++ search directories")]
		[Description("Additional search directories when using Toggle Files on C++ files.  Enter one directory per line in the drop-down editor.")]
		[Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
		[DefaultValue(null)]
		public string CppSearchDirectories { get; set; }

		[Category("Miscellaneous")]
		[DisplayName("Only show Trim dialog when shift is pressed")]
		[Description("Provides a way to suppress the display of the Trim dialog unless the Shift key is pressed.")]
		[DefaultValue(false)]
		public bool OnlyShowTrimDialogWhenShiftIsPressed { get; set; }

		// See comments in the setter for why this is a string property.
		[Category("Miscellaneous")]
		[DisplayName("GUID format")]
		[Description("The format to use for generated GUIDs.")]
		[DefaultValue(GuidFormatStringConverter.DefaultFormatText)]
		[TypeConverter(typeof(GuidFormatStringConverter))]
		public string GuidFormatText
		{
			get
			{
				string result = GuidFormatStringConverter.ToString(this.guidFormat);
				return result;
			}

			set
			{
				// Originally, this property returned GuidFormat and its TypeConverter was GuidFormatStringConverter,
				// which worked fine for normal user value selection.  However, in VS 11 Beta, double-clicking the property
				// name in the PropertyGrid (to skip to the next value in the list) caused VS to try to assign the string
				// directly to the property, ignoring the type converter!  That caused an ArgumentException with a
				// PropertyGrid popup message of "Object of type 'System.String' cannot be converted to type
				// 'Menees.VsTools.GuidFormat'."  To work around that, I'm using a public string property (with a type
				// converter to provide the pick list of values) and an internal enum property.
				//
				// Privately, I'm still storing the enum value since that's what's needed 99% of the time.  This also normalizes
				// the input value in case someone maliciously (or stupidly) changes the string value in a saved .settings
				// file to an invalid value and then tries to load it in.  The ToFormat method will just return the default format
				// enum in that case.
				this.guidFormat = GuidFormatStringConverter.ToFormat(value);
			}
		}

		[Category("Miscellaneous")]
		[DisplayName("GUID uppercase")]
		[Description("Whether generated GUIDs should use uppercase hexadecimal characters.")]
		[DefaultValue(true)]
		public bool UppercaseGuids { get; set; }

		[Category(nameof(Editor))]
		[DisplayName("Use VS-style comment indentation")]
		[Description("Whether Comment Selection should use a fixed level of indentation for single-line comments.")]
		[DefaultValue(false)]
		public bool UseVsStyleCommentIndentation { get; set; }

		[Category(nameof(Editor))]
		[DisplayName("Enable mouse wheel zoom in new text windows")]
		[Description("Whether Ctrl+MouseWheel should affect the zoom level for new text windows.")]
		[DefaultValue(true)]
		public bool IsMouseWheelZoomEnabled { get; set; }

		[Category("Miscellaneous")]
		[DisplayName("Sort Members order")]
		[Description("A comma-separated list of member properties to order by.  Prefix a property with '-' to order it descending.  " +
			"The default ordering is: Kind, Access, IsStatic, KindModifier, ConstModifier, OverrideModifier, Name, ParameterCount.")]
		[DefaultValue(null)]
		public string SortMembersOrder { get; set; }

		[Category("Miscellaneous")]
		[DisplayName("Only show Sort Members dialog when shift is pressed")]
		[Description("Provides a way to suppress the display of the Sort Members dialog unless the Shift key is pressed.  " +
			"If the dialog is suppressed, then selected members will be sorted.")]
		[DefaultValue(false)]
		public bool OnlyShowSortMembersDialogWhenShiftIsPressed { get; set; }

		[Category("Miscellaneous")]
		[DisplayName("Build timing")]
		[Description("Whether build timing information should be added to the Build output window.")]
		[DefaultValue(BuildTiming.None)]
		public BuildTiming BuildTiming { get; set; }

		#endregion

		#region Public Non-Browsable Properties (for other state persistence)

		[Browsable(false)]
		[Category("Trim")]
		[DisplayName("Trim Start")]
		[DefaultValue(false)]
		public bool TrimStart { get; set; }

		[Browsable(false)]
		[Category("Trim")]
		[DisplayName("Trim End")]
		[DefaultValue(true)]
		public bool TrimEnd { get; set; }

		[Browsable(false)]
		[Category("Sort Lines")]
		[DisplayName("Case sensitive")]
		[DefaultValue(false)]
		public bool SortCaseSensitive { get; set; }

		[Browsable(false)]
		[Category("Sort Lines")]
		[DisplayName("Compare by ordinal")]
		[DefaultValue(false)]
		public bool SortCompareByOrdinal { get; set; }

		[Browsable(false)]
		[Category("Sort Lines")]
		[DisplayName("Ascending")]
		[DefaultValue(true)]
		public bool SortAscending { get; set; }

		[Browsable(false)]
		[Category("Sort Lines")]
		[DisplayName("Ignore leading and trailing whitespace")]
		[DefaultValue(true)]
		public bool SortIgnoreWhitespace { get; set; }

		[Browsable(false)]
		[Category("Sort Lines")]
		[DisplayName("Ignore punctuation")]
		[DefaultValue(false)]
		public bool SortIgnorePunctuation { get; set; }

		[Browsable(false)]
		[Category("Sort Lines")]
		[DisplayName("Eliminate Duplicates")]
		[DefaultValue(false)]
		public bool SortEliminateDuplicates { get; set; }

		#endregion

		#region Internal Properties

		internal GuidFormat GuidFormat => this.guidFormat;

		#endregion

		#region Public Methods

		public static string[] SplitValues(string multiLineValue)
		{
			string regions = multiLineValue ?? string.Empty;
			string[] result = regions.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			return result;
		}

		#endregion
	}
}
