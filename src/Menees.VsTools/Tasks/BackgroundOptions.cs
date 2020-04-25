namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
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

		public string ExcludeFromCommentScans { get; private set; }

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

				if (this.ExcludeFromCommentScans != options.ExcludeFromCommentScans)
				{
					this.ExcludeFromCommentScans = options.ExcludeFromCommentScans;
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
