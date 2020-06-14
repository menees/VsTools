namespace Menees.VsTools.Projects
{
	#region Private Data Members

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Drawing;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	// Note: The MainPackage has a ProvideOptionPage attribute that associates this class with that package.
	[Guid(Guids.ProjectOptionsString)]
	[DefaultProperty(nameof(ReferencedProjectColor))] // Make this get focus in the PropertyGrid first since its category is alphabetically first.
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by VS.")]
	internal sealed class Options : OptionsBase
	{
		#region Internal Constants

		internal const string DefaultCaption = nameof(Projects);

		#endregion

		#region Private Data Members

		private const string DefaultSelectedProjectIcon = "CodeSchema_Project";
		private const string DefaultReferencedProjectIcon = "CodeSchema_Assembly";

		private string selectedProjectIcon = DefaultSelectedProjectIcon;
		private string referencedProjectIcon = DefaultReferencedProjectIcon;

		#endregion

		#region Constructors

		public Options()
		{
			this.RemoveCommonPrefix = true;
			this.DefaultLayout = GraphLayout.BottomToTop;
			this.SelectedProjectColor = Color.AliceBlue;
			this.ReferencedProjectColor = Color.LavenderBlush;
		}

		#endregion

		#region Public Properties

		[Category("Graph Appearance")]
		[DisplayName("Color for selected projects")]
		[Description("The color to use for projects that were explicitly included in the graph via selection in Solution Explorer.")]
		[DefaultValue(typeof(Color), nameof(Color.AliceBlue))]
		public Color SelectedProjectColor { get; set; }

		[Category("Graph Appearance")]
		[DisplayName("Color for referenced projects")]
		[Description("The color to use for projects that were only implicitly included in the graph via references.")]
		[DefaultValue(typeof(Color), nameof(Color.LavenderBlush))]
		public Color ReferencedProjectColor { get; set; }

		[Category("Graph Appearance")]
		[DisplayName("Use glass style")]
		[Description("Whether nodes should be rendered using color gradients to simulate a shiny glass effect.")]
		[DefaultValue(false)]
		public bool UseGlassStyle { get; set; }

		[Category("Graph Appearance")]
		[DisplayName("Default layout")]
		[Description("The predominate direction the graph nodes and links should be oriented initially.")]
		[DefaultValue(GraphLayout.BottomToTop)]
		public GraphLayout DefaultLayout { get; set; }

		[Category("Graph Content")]
		[DisplayName("Remove common prefix")]
		[Description("Whether any common prefix ending in a non-letter, non-digit should be removed from all items if found (e.g., \"System.\").")]
		[DefaultValue(true)]
		public bool RemoveCommonPrefix { get; set; }

		[Category("Graph Content")]
		[DisplayName("Use hyperlinks")]
		[Description("Whether nodes should include underlined hyperlink references when possible.")]
		[DefaultValue(false)]
		public bool AddHyperLinks { get; set; }

		[Category("Graph Icons")]
		[DisplayName("Use icons")]
		[Description("Whether nodes should include an icon.")]
		[DefaultValue(false)]
		public bool UseIcons { get; set; }

		[Category("Graph Icons")]
		[DisplayName("Icon for selected projects")]
		[Description("The icon to use for projects that were explicitly included in the graph via selection in Solution Explorer.")]
		[DefaultValue(DefaultSelectedProjectIcon)]
		public string SelectedProjectIcon
		{
			get => this.selectedProjectIcon;
			set => this.selectedProjectIcon = !string.IsNullOrEmpty(value) ? value : DefaultSelectedProjectIcon;
		}

		[Category("Graph Icons")]
		[DisplayName("Icon for referenced projects")]
		[Description("The icon to use for projects that were only implicitly included in the graph via references.")]
		[DefaultValue(DefaultReferencedProjectIcon)]
		public string ReferencedProjectIcon
		{
			get => this.referencedProjectIcon;
			set => this.referencedProjectIcon = !string.IsNullOrEmpty(value) ? value : DefaultReferencedProjectIcon;
		}

		#endregion
	}
}
