namespace Menees.VsTools.Editor
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

	// Note: The MainPackage has ProvideOptionPage and ProvideProfile attributes
	// that associate this class with our package.  Helpful pages:
	// http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.shell.dialogpage(v=vs.110).aspx
	// http://msdn.microsoft.com/en-us/library/bb162586(v=vs.110).aspx
	// http://bloggingabout.net/blogs/perikles/archive/2006/11/22/How-to-dynamically-Import_2F00_Export-setting-in-Visual-Studio-2005_2E00_.aspx
	[Guid(Guids.HighlightOptionsString)]
	[DefaultProperty(nameof(HighlightFindResultsDetails))] // Make this get focus in the PropertyGrid first since its category is alphabetically first.
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by VS.")]
	internal sealed class HighlightOptions : OptionsBase
	{
		#region Internal Constants

		internal const string DefaultCaption = "Highlight";

		#endregion

		#region Private Data Members

		private List<OutputHighlight> outputHighlights;

		#endregion

		#region Constructors

		public HighlightOptions()
		{
			this.HighlightOutputText = true;
			this.OutputHighlights = CreateDefaultOutputHighlights();
			this.HighlightFindResultsDetails = true;
			this.HighlightFindResultsFileNames = true;
			this.HighlightFindResultsMatches = true;
		}

		#endregion

		#region Public Browsable Properties (for Options page)

		[Category("Output Windows")]
		[DisplayName("Highlight output window text")]
		[Description("Whether pattern-matched lines in output windows should be highlighted.")]
		[DefaultValue(true)]
		public bool HighlightOutputText { get; set; }

		[Category("Output Windows")]
		[DisplayName("Output patterns to highlight")]
		[Description("Defines regular expressions used to highlight lines in output windows.")]
		[TypeConverter(typeof(OutputHighlightListTypeConverter))]
		[Editor(typeof(CollectionEditor), typeof(UITypeEditor))]
		public List<OutputHighlight> OutputHighlights
		{
			get
			{
				return this.outputHighlights;
			}

			set
			{
				// Reset to the default highlights if the user deletes them all.
				if (value == null || value.Count == 0)
				{
					this.outputHighlights = CreateDefaultOutputHighlights();
				}
				else
				{
					this.outputHighlights = value;
				}
			}
		}

		[Category("Find Windows")]
		[DisplayName("Highlight Find Results details")]
		[Description("Whether non-matched details in Find Results windows should be highlighted.")]
		[DefaultValue(true)]
		public bool HighlightFindResultsDetails { get; set; }

		[Category("Find Windows")]
		[DisplayName("Highlight Find Results file names")]
		[Description("Whether file names in Find Results windows should be highlighted.")]
		[DefaultValue(true)]
		public bool HighlightFindResultsFileNames { get; set; }

		[Category("Find Windows")]
		[DisplayName("Highlight Find Results matches")]
		[Description("Whether search term/expression matches in Find Results windows should be highlighted.")]
		[DefaultValue(true)]
		public bool HighlightFindResultsMatches { get; set; }

		#endregion

		#region Private Methods

		private static List<OutputHighlight> CreateDefaultOutputHighlights()
		{
			List<OutputHighlight> result = new List<OutputHighlight>();

			ISet<string> knownOutputContentTypes = OutputHighlight.GetKnownOutputContentTypes();

			string[] buildContent = OutputHighlight.ValidateContentTypes(new[] { "BuildOutput", "BuildOrderOutput" }, knownOutputContentTypes);
			if (buildContent.Length > 0)
			{
				result.Add(new OutputHighlight("Code Analysis Success", OutputHighlightType.None, @"\s0 error\(s\), 0 warning\(s\)$", buildContent));
			}

			// The "Ext: ExceptionBreaker (Diagnostic)" pane uses a general Output content type.
			// We have to match this before the normal "exception:" rule.
			result.Add(new OutputHighlight(
				"Previous/Conflicting Exception",
				OutputHighlightType.None,
				@"^\s*(Previous|Conflicting) exception:")
			{ MatchCase = true });

			string[] debugContent = OutputHighlight.ValidateContentTypes(new[] { "DebugOutput" }, knownOutputContentTypes);
			if (debugContent.Length > 0)
			{
				result.Add(new OutputHighlight("Exception", OutputHighlightType.Error, @"(exception:|stack trace:)", debugContent));
				result.Add(new OutputHighlight("Exception At", OutputHighlightType.Error, @"^\s+at\s", debugContent));
			}

			string[] testsContent = OutputHighlight.ValidateContentTypes(new[] { "TestsOutput" }, knownOutputContentTypes);
			if (testsContent.Length > 0)
			{
				result.Add(new OutputHighlight("Test Host Abort", OutputHighlightType.Error, @"^The active (\w+\s)+was aborted\s", testsContent));
				result.Add(new OutputHighlight("Test Host Exception", OutputHighlightType.Error, @"\wException:\s", testsContent) { MatchCase = true });
			}

			string[] tfsContent = OutputHighlight.ValidateContentTypes(new[] { "TFSourceControlOutput" }, knownOutputContentTypes);
			if (tfsContent.Length > 0)
			{
				result.Add(new OutputHighlight("TFS Error Code", OutputHighlightType.Error, @"^TF\d+\:\s", tfsContent));
				result.Add(new OutputHighlight("TFS Unable To Get", OutputHighlightType.Warning, @"\WUnable to perform the get operation\W", tfsContent));
				result.Add(new OutputHighlight("TFS Newer Version", OutputHighlightType.Warning, @"(\W|^)newer version exists in source control$", tfsContent));
				result.Add(new OutputHighlight("TFS Auto Resolve", OutputHighlightType.Information, @"^Automatically resolved conflict\:\W", tfsContent));
				result.Add(new OutputHighlight("TFS Check In", OutputHighlightType.Information, @"^Changeset \d+ successfully checked in\.$", tfsContent));
				result.Add(new OutputHighlight("TFS Check Out", OutputHighlightType.Detail, @"\Whas been automatically checked out\W", tfsContent));
				result.Add(new OutputHighlight("TFS Open For Edit", OutputHighlightType.Detail, @"^\s*opened for edit in\s", tfsContent));
				result.Add(new OutputHighlight("TFS File Path", OutputHighlightType.Detail, @"^\$/.+\:$", tfsContent));
			}

			// These apply to all Output windows, so put them last.  The Header/Footer pattern has to come
			// before the Error pattern because builds use the word "failed" in the output footer.
			result.Add(new OutputHighlight("Header/Footer", OutputHighlightType.Header, @"------ |========== "));
			result.Add(new OutputHighlight("Exception cache is built", OutputHighlightType.None, @"^Exception cache is built\:"));
			result.Add(new OutputHighlight("Error", OutputHighlightType.Error, @"(\W|^)(error|fail|failed|exception)\W"));
			result.Add(new OutputHighlight("Warning", OutputHighlightType.Warning, @"(\W|^)warning\W"));
			result.Add(new OutputHighlight("Information", OutputHighlightType.Information, @"(\W|^)information\W"));

			return result;
		}

		#endregion
	}
}
