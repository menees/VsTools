namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;

	#endregion

	[DebuggerDisplay("{Comment}")]
	internal sealed class CommentTask
	{
		#region Private Data Members

		private readonly CommentTaskProvider provider;

		#endregion

		#region Constructors

		// Note: This constructor is only for design-time data use (see Tasks --> DesignData.xaml)!
		public CommentTask()
		{
		}

		internal CommentTask(CommentTaskProvider provider, TaskPriority priority, string project, string fileName, int line, string comment)
		{
			this.provider = provider;
			this.Priority = priority;
			this.Project = project;
			this.FilePath = fileName;
			this.Line = line;
			this.Comment = comment;
		}

		#endregion

		#region Public Properties

		public TaskPriority Priority { get; }

		public string Project { get; }

		public string FilePath { get; }

		public int Line { get; }

		public string Comment { get; }

		public string FileName
		{
			get
			{
				string result = Path.GetFileName(this.FilePath);
				return result;
			}
		}

		// Ignore the file path, line number, project, and priority.
		public string ExcludeText => $"{this.FileName}: {this.Comment}";

		#endregion

		#region Public Methods

		public bool GoToComment()
		{
			bool result = false;

			if (this.provider != null)
			{
				TaskListItem task = new()
				{
					Document = this.FilePath,
					Line = this.Line,
				};

				result = this.provider.Navigate(task, VSConstants.LOGVIEWID_TextView);
			}

			return result;
		}

		#endregion
	}
}
