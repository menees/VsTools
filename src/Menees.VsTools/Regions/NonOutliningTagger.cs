namespace Menees.VsTools.Regions
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Tagging;

	#endregion

	internal sealed class NonOutliningTagger : ITagger<IOutliningRegionTag>
	{
		#region Constructors

		private NonOutliningTagger()
		{
			// This should only be called by the Instance property initializer.
		}

		#endregion

		#region Public Events

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged
		{
			add
			{
				// Don't do anything since we'll never send notifications.
			}

			remove
			{
				// Don't do anything since we'll never send notifications.
			}
		}

		#endregion

		#region Public Properties

		public static NonOutliningTagger Instance { get; } = new NonOutliningTagger();

		#endregion

		#region Public Methods

		public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
			=> Enumerable.Empty<ITagSpan<IOutliningRegionTag>>();

		#endregion
	}
}
