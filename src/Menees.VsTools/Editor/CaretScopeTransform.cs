namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.Text.Formatting;

	#endregion

	internal sealed class CaretScopeTransform : ILineTransformSource
	{
		#region Private Data Members

		private IWpfTextView textView;

		#endregion

		#region Constructors

		internal CaretScopeTransform(IWpfTextView textView)
		{
			this.textView = textView;
			this.textView.Caret.PositionChanged += this.OnCaretPositionChanged;
		}

		#endregion

		#region Public Methods

		public LineTransform GetLineTransform(ITextViewLine line, double yposition, ViewRelativePosition placement)
		{
			// Vertically compress lines that are far from the caret (based on buffer lines, not view lines).
			ITextSnapshot snapshot = this.textView.TextSnapshot;
			int caretLineNumber = snapshot.GetLineNumberFromPosition(this.textView.Caret.Position.BufferPosition);
			int lineNumber = snapshot.GetLineNumberFromPosition(line.Start);
			int delta = Math.Abs(caretLineNumber - lineNumber);

			// Idea: Provide options to control these factors. [Bill, 7/17/2015]
			// Idea: Optionally, compress whitespace and non-alphanumeric lines more. [Bill, 7/17/2015]
			const int Group1Lines = 3;
			const int Group2Lines = Group1Lines + 5;
			const int Group3Lines = Group2Lines + 10;
			const double Group1ScaleFactor = 1.0;
			const double Group2ScaleFactor = 0.9;
			const double Group3ScaleFactor = 0.8;
			const double Group2ScaleDecrement = (Group1ScaleFactor - Group2ScaleFactor) / (Group2Lines - Group1Lines);
			const double Group3ScaleDecrement = (Group2ScaleFactor - Group3ScaleFactor) / (Group3Lines - Group2Lines);

			double scale;
			if (delta <= Group1Lines)
			{
				scale = Group1ScaleFactor;
			}
			else if (delta <= Group2Lines)
			{
				scale = Group1ScaleFactor - ((delta - (double)Group1Lines) * Group2ScaleDecrement);
			}
			else if (delta <= Group3Lines)
			{
				scale = Group2ScaleFactor - ((delta - (double)Group2Lines) * Group3ScaleDecrement);
			}
			else
			{
				scale = Group3ScaleFactor;
			}

			LineTransform result = new LineTransform(0.0, 0.0, scale);
			return result;
		}

		#endregion

		#region Private Event Handlers

		private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs args)
		{
			SnapshotPoint oldPosition = args.OldPosition.BufferPosition;
			SnapshotPoint newPosition = args.NewPosition.BufferPosition;

			ITextSnapshot snapshot = this.textView.TextSnapshot;
			if (snapshot.GetLineNumberFromPosition(newPosition) != snapshot.GetLineNumberFromPosition(oldPosition))
			{
				// Is the caret on a line that has been formatted by the view?
				ITextViewLine line = this.textView.Caret.ContainingTextViewLine;

				if (line.VisibilityState != VisibilityState.Unattached)
				{
					// Force the view to redraw so that (top of) the caret line has exactly the same position.
					this.textView.DisplayTextLineContainingBufferPosition(line.Start, line.Top, ViewRelativePosition.Top);
				}
			}
		}

		#endregion
	}
}
