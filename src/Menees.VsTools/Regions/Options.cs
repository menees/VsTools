namespace Menees.VsTools.Regions
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.Design;
	using System.Diagnostics.CodeAnalysis;
	using System.Drawing.Design;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	// Note: The MainPackage has a ProvideOptionPage attribute that associates this class with that package.
	[Guid(Guids.RegionOptionsString)]
	[DefaultProperty(nameof(PredefinedRegions))] // Make this get focus in the PropertyGrid first since its category is alphabetically first.
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by VS.")]
	internal sealed class Options : OptionsBase
	{
		#region Internal Constants

		internal const string DefaultCaption = nameof(Regions);

		#endregion

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

		private readonly HashSet<Language> supportRegions = new HashSet<Language> { Language.HTML, Language.SQL, Language.XML, Language.XAML };

		#endregion

		#region Constructors

		public Options()
		{
			this.PredefinedRegions = DefaultPredefinedRegions;
			this.AddInnerBlankLines = true;
		}

		#endregion

		#region Public Browsable Properties (for Options page)

		[Category("Add")]
		[DisplayName("Predefined #regions")]
		[Description("Defines the entries to include in the Add Region dialog. Enter one region name per line in the drop-down editor.")]
		[Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
		[DefaultValue(DefaultPredefinedRegions)]
		public string PredefinedRegions { get; set; }

		[Category("Add")]
		[DisplayName("Add inner blank lines")]
		[Description("Whether to add one blank line after #region and one before #endregion.")]
		[DefaultValue(true)]
		public bool AddInnerBlankLines { get; set; }

		[Category("Add")]
		[DisplayName("Add name after end")]
		[Description("Whether to add the region name after the #endregion tag.")]
		[DefaultValue(false)]
		public bool AddNameAfterEnd { get; set; }

		[Category(nameof(Language))]
		[DisplayName("Support HTML Web Forms regions")]
		[Description("Whether to support <!-- #region --> outlining in HTML files opened in the Web Forms editor.")]
		[DefaultValue(true)]
		public bool SupportHtmlRegions { get => this.IsSupported(Language.HTML); set => this.SetSupported(Language.HTML, value); }

		[Category(nameof(Language))]
		[DisplayName("Support SQL regions")]
		[Description("Whether to support -- #region outlining in SQL files.")]
		[DefaultValue(true)]
		public bool SupportSqlRegions { get => this.IsSupported(Language.SQL); set => this.SetSupported(Language.SQL, value); }

		[Category(nameof(Language))]
		[DisplayName("Support XAML regions")]
		[Description("Whether to support <!-- #region --> outlining in XAML files.")]
		[DefaultValue(true)]
		public bool SupportXamlRegions { get => this.IsSupported(Language.XAML); set => this.SetSupported(Language.XAML, value); }

		[Category(nameof(Language))]
		[DisplayName("Support XML regions")]
		[Description("Whether to support <!-- #region --> outlining in XML files other than XAML.")]
		[DefaultValue(true)]
		public bool SupportXmlRegions { get => this.IsSupported(Language.XML); set => this.SetSupported(Language.XML, value); }

		#endregion

		#region Internal Methods

		internal bool IsSupported(Language language) => this.supportRegions.Contains(language);

		#endregion

		#region Private Methods

		private void SetSupported(Language language, bool value)
		{
			if (value)
			{
				this.supportRegions.Add(language);
			}
			else
			{
				this.supportRegions.Remove(language);
			}
		}

		#endregion
	}
}
