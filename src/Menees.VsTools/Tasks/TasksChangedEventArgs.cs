namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	internal sealed class TasksChangedEventArgs : EventArgs
	{
		#region Constructors

		internal TasksChangedEventArgs(IEnumerable<CommentTask> addedTasks, IEnumerable<CommentTask> removedTasks)
		{
			this.AddedTasks = addedTasks;
			this.RemovedTasks = removedTasks;
		}

		#endregion

		#region Public Properties

		public IEnumerable<CommentTask> AddedTasks { get; }

		public IEnumerable<CommentTask> RemovedTasks { get; }

		#endregion
	}
}
