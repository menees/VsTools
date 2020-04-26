namespace Menees.VsTools.Tasks
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
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using Microsoft.VisualStudio.Shell;

	#endregion

	// Note: The MainPackage has ProvideOptionPage and ProvideProfile attributes
	// that associate this class with our package.  Helpful pages:
	// http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.shell.dialogpage(v=vs.110).aspx
	// http://msdn.microsoft.com/en-us/library/bb162586(v=vs.110).aspx
	// http://bloggingabout.net/blogs/perikles/archive/2006/11/22/How-to-dynamically-Import_2F00_Export-setting-in-Visual-Studio-2005_2E00_.aspx
	[Guid(Guids.TasksOptionsString)]
	[DefaultProperty(nameof(EnableCommentScans))] // Make this get focus in the PropertyGrid first since its category is alphabetically first.
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by VS.")]
	internal sealed class Options : OptionsBase
	{
		#region Private Data Members

		private const string DefaultExcludeFilesPatterns = @".+\.Designer\.\w+$" + "\r\n" +
			@"modernizr-\d+\.\d+\.\d+(-vsdoc)?\.js$" + "\r\n" +
			@"jquery-\d+\.\d+\.\d+(-vsdoc)?\.js$";

		private const string DefaultExcludeProjectsPatterns = @".+\.(sql|vc|vcx)proj$";

		private const int MinParallelism = 1;
		private const int MaxParallelism = 8;
		private const int ProcessorScaleFactor = 4;
		private const string ProcessorScaleFactorPercent = "25%";

		private string excludeFilesPatterns;
		private string excludeProjectsPatterns;
		private int? requestedMaxDegreeOfParallelism;

		#endregion

		#region Constructors

		public Options()
		{
			this.ExcludeFilesPatterns = DefaultExcludeFilesPatterns;
			this.ExcludeProjectsPatterns = DefaultExcludeProjectsPatterns;
		}

		#endregion

		#region Public Browsable Properties (for Options page)

		[Category("Common")]
		[DisplayName("Enable task scanning (requires restart)")]
		[Description("Whether open documents and files referenced by the current solution should be scanned for task comments.")]
		[DefaultValue(false)] // Off by default since it can have a serious CPU impact on large solutions.
		public bool EnableCommentScans { get; set; }

		[Category("Common")]
		[DisplayName("Max degree of parallelism")]
		[Description("The maximum number of concurrent file scans to perform. If blank, then "
			+ ProcessorScaleFactorPercent + " of your logical CPU count will be used.")]
		[DefaultValue(null)]
		public int? RequestedMaxDegreeOfParallelism
		{
			get
			{
				return this.requestedMaxDegreeOfParallelism;
			}

			set
			{
				if (value != null && (value.Value < MinParallelism || value.Value > MaxParallelism))
				{
					throw new ArgumentException($"Value must be between {MinParallelism} and {MaxParallelism}.");
				}

				this.requestedMaxDegreeOfParallelism = value;
			}
		}

		[Category("Exclude")]
		[DisplayName("Exclude file name patterns")]
		[Description("Regular expressions used to exclude solution items or open documents from being scanned for comments. " +
			"Enter one pattern per line. Each pattern is matched against the fully-qualified file name.")]
		[Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
		[DefaultValue(DefaultExcludeFilesPatterns)]
		public string ExcludeFilesPatterns
		{
			get
			{
				return this.excludeFilesPatterns;
			}

			set
			{
				string patterns = string.IsNullOrEmpty(value) ? DefaultExcludeFilesPatterns : value;
				this.ExcludeFilesExpressions = SplitPatterns(patterns);
				this.excludeFilesPatterns = patterns;
			}
		}

		[Category("Exclude")]
		[DisplayName("Exclude project name patterns")]
		[Description("Regular expressions used to exclude projects from being recursively scanned for files. " +
			"Enter one pattern per line. Each pattern is matched against the fully-qualified project file path.")]
		[Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
		[DefaultValue(DefaultExcludeProjectsPatterns)]
		public string ExcludeProjectsPatterns
		{
			get
			{
				return this.excludeProjectsPatterns;
			}

			set
			{
				string patterns = string.IsNullOrEmpty(value) ? DefaultExcludeProjectsPatterns : value;
				this.ExcludeProjectsExpressions = SplitPatterns(patterns);
				this.excludeProjectsPatterns = patterns;
			}
		}

		#endregion

		#region Public Non-Browsable Properties (for other state persistence)

		[Browsable(false)]
		public IReadOnlyList<Regex> ExcludeFilesExpressions { get; private set; }

		[Browsable(false)]
		public IReadOnlyList<Regex> ExcludeProjectsExpressions { get; private set; }

		[Browsable(false)]
		public string TasksStatusXml { get; set; }

		[Browsable(false)]
		public int MaxDegreeOfParallelism
		{
			get
			{
				int result = this.requestedMaxDegreeOfParallelism
					?? Math.Max(MinParallelism, Math.Min(Environment.ProcessorCount / ProcessorScaleFactor, MaxParallelism));
				return result;
			}
		}

		#endregion

		#region Private Methods

		private static IReadOnlyList<Regex> SplitPatterns(string patterns)
		{
			TextLines lines = new TextLines(patterns);

			// If they enter an invalid regular expression, then this will throw an ArgumentException.
			List<Regex> result = lines.Lines.Where(line => !string.IsNullOrEmpty(line))
				.Distinct()
				.Select(line => new Regex(line, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant))
				.ToList();

			return result;
		}

		#endregion
	}
}
