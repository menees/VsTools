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

	[DebuggerDisplay("{Text}")]
	internal sealed class CommentToken
	{
		#region Constructors

		public CommentToken(string text, bool isCaseSensitive)
		{
			this.Text = text;
			this.IsCaseSensitive = isCaseSensitive;
		}

		#endregion

		#region Public Properties

		public string Text { get; }

		public bool IsCaseSensitive { get; }

		#endregion
	}
}
