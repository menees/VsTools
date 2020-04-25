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

		private const string DefaultExcludePatterns = @".+\.Designer\.\w+$" + "\r\n" +
			@"modernizr-\d+\.\d+\.\d+(-vsdoc)?\.js$" + "\r\n" +
			@"jquery-\d+\.\d+\.\d+(-vsdoc)?\.js$";

		private string excludeFromCommentScans;

		#endregion

		#region Constructors

		public Options()
		{
			this.ExcludeFromCommentScans = DefaultExcludePatterns;
		}

		#endregion

		#region Public Browsable Properties (for Options page)

		[Category(nameof(Tasks))]
		[DisplayName("Enable tasks provider (requires restart)")]
		[Description("Whether open documents and files referenced by the current solution should be scanned for task comments.")]
		[DefaultValue(false)] // Off by default since it can have a serious CPU impact on large solutions.
		public bool EnableCommentScans { get; set; }

		[Category(nameof(Tasks))]
		[DisplayName("Exclude file name patterns")]
		[Description("Regular expressions used to exclude solution items or open documents from being scanned for comments.  " +
			"Enter one pattern per line.  Each pattern is matched against the fully-qualified file name.")]
		[Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
		[DefaultValue(DefaultExcludePatterns)]
		public string ExcludeFromCommentScans
		{
			get
			{
				return this.excludeFromCommentScans;
			}

			set
			{
				if (string.IsNullOrEmpty(value))
				{
					this.excludeFromCommentScans = DefaultExcludePatterns;
				}
				else
				{
					this.excludeFromCommentScans = value;
				}
			}
		}

		#endregion

		#region Public Non-Browsable Properties (for other state persistence)

		[Browsable(false)]
		[Category(nameof(Tasks))]
		[DisplayName("Tasks Status Xml")]
		[DefaultValue(null)]
		public string TasksStatusXml { get; set; }

		#endregion
	}
}
