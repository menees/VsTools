namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Classification;

	#endregion

	internal abstract class ClassifierBase : IClassifier, IDisposable
	{
		#region Private Data Members

		private readonly ITextBuffer buffer;

		#endregion

		#region Constructors

		protected ClassifierBase(ITextBuffer buffer)
		{
			this.buffer = buffer;
			buffer.ContentTypeChanged += this.TextBuffer_ContentTypeChanged;

			// Indicate that we need to re-classify everything when our VSTools options change (specifically the highlighting options).
			MainPackage.GeneralOptions.Applied += this.PackageOptionsApplied;
		}

		#endregion

		#region Public Events

		// This event gets raised if a non-text change would affect the classification in some way,
		// for example typing /* would cause the classification to change in C# without directly
		// affecting the span.
		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

		#endregion

		#region Public Methods

		/// <summary>
		/// This method scans the given SnapshotSpan for potential matches for this classification.
		/// In this instance, it classifies everything and returns each span as a new ClassificationSpan.
		/// </summary>
		/// <param name="span">The span currently being classified</param>
		/// <returns>A list of ClassificationSpans that represent spans identified to be of this classification</returns>
		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
		{
			List<ClassificationSpan> result = new List<ClassificationSpan>();

			ThreadHelper.ThrowIfNotOnUIThread();
			if (span.Length > 0)
			{
				this.GetClassificationSpans(result, span, MainPackage.GeneralOptions);
			}

			return result;
		}

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this.Dispose(true);
		}

		#endregion

		#region Protected Methods

		protected static IEnumerable<ITextSnapshotLine> GetSpanLines(SnapshotSpan span)
		{
			ITextSnapshot snapshot = span.Snapshot;
			if (!snapshot.TextBuffer.EditInProgress)
			{
				// Note: The snapshot is usually for the entire file.  We only need to (re)classify a small span of it usually,
				// so we should not use snapshot.Lines.  We'll just get the lines covered by the span's [Start, End) range.
				int startLineNumber = snapshot.GetLineNumberFromPosition(span.Start.Position);
				int endLineNumber = snapshot.GetLineNumberFromPosition(span.End.Position - 1);

				for (int lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
				{
					ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
					if (line.Length > 0)
					{
						yield return line;
					}
				}
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				this.buffer.ContentTypeChanged -= this.TextBuffer_ContentTypeChanged;
				MainPackage.GeneralOptions.Applied -= this.PackageOptionsApplied;
			}
		}

		protected abstract void GetClassificationSpans(List<ClassificationSpan> result, SnapshotSpan span, Options options);

		// If we're passed a specific option ID, then return false because we can't read it here to tell if it changed.
		// If we're passed null or empty, then return true as if something changed.
		protected virtual bool ReadOptions(string changedOptionId) => string.IsNullOrEmpty(changedOptionId);

		protected virtual void ContentTypeChanged(ITextBuffer buffer, ContentTypeChangedEventArgs e)
		{
			// This is just provided for derived classes.
		}

		protected void OptionsChanged(string optionId)
		{
			if (this.ReadOptions(optionId))
			{
				// Note: Even though the code in Menees VS Tools doesn't attach to this event, it's still important
				// to raise because Visual Studio attaches to it and uses it to call into the appropriate classifier.
				// This is how the actual editor window will get updated when options are changed.
				var handler = this.ClassificationChanged;
				if (handler != null)
				{
					// Say that the entire snapshot needs to be re-classified.
					ITextSnapshot snapshot = this.buffer.CurrentSnapshot;
					handler(this, new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
				}
			}
		}

		#endregion

		#region Private Methods

		private void TextBuffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e)
		{
			this.ContentTypeChanged(this.buffer, e);
		}

		private void PackageOptionsApplied(object sender, EventArgs e)
		{
			// If any of the Menees VS Tools options change, we'll re-classify.  Typically, only a few options actually
			// affect each classifier, but it's rare for any of the package options to change.  So it doesn't hurt to just
			// re-classify if anything changed.
			this.OptionsChanged(null);
		}

		#endregion
	}
}
