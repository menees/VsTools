namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio.Shell;

	#endregion

	[DebuggerDisplay("{Text}, {Priority}")]
	internal sealed class CommentToken
	{
		#region Constructors

		public CommentToken(string text, TaskPriority priority, bool isCaseSensitive)
		{
			this.Text = text;
			this.Priority = priority;
			this.IsCaseSensitive = isCaseSensitive;
		}

		#endregion

		#region Public Properties

		public string Text { get; }

		public TaskPriority Priority { get; }

		public bool IsCaseSensitive { get; }

		#endregion
	}
}
