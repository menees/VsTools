namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;

	#endregion

	internal sealed class BackgroundOptions
	{
		#region Private Data Members

		private readonly object locker = new object();

		#endregion

		#region Public Events

		public event EventHandler Updated;

		#endregion

		#region Public Properties

		public bool EnableCommentScans { get; private set; }

		public IReadOnlyList<Regex> ExcludeFilesExpressions { get; private set; }

		public IReadOnlyList<Regex> ExcludeProjectsExpressions { get; private set; }

		public int MaxDegreeOfParallelism { get; private set; }

		#endregion

		#region Public Methods

		public bool Update(Options options)
		{
			bool updated = false;

			lock (this.locker)
			{
				if (this.EnableCommentScans != options.EnableCommentScans)
				{
					this.EnableCommentScans = options.EnableCommentScans;
					updated = true;
				}

				if (this.ExcludeFilesExpressions != options.ExcludeFilesExpressions)
				{
					this.ExcludeFilesExpressions = options.ExcludeFilesExpressions;
					updated = true;
				}

				if (this.ExcludeProjectsExpressions != options.ExcludeProjectsExpressions)
				{
					this.ExcludeProjectsExpressions = options.ExcludeProjectsExpressions;
					updated = true;
				}

				if (this.MaxDegreeOfParallelism != options.MaxDegreeOfParallelism)
				{
					this.MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
					updated = true;
				}
			}

			if (updated)
			{
				this.Updated?.Invoke(this, EventArgs.Empty);
			}

			return updated;
		}

		#endregion
	}
}
