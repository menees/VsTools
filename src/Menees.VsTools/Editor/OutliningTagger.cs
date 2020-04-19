namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Windows.Threading;
	using Menees.VsTools.Tasks;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Tagging;

	#endregion

	// Note: This started from MSDN's "Walkthrough: Outlining" example (https://msdn.microsoft.com/en-us/library/ee197665.aspx).
	internal sealed class OutliningTagger : ITagger<IOutliningRegionTag>
	{
		#region Private Data Members

		private const string RegionToken = "#region";
		private static readonly CommentToken StartToken = new CommentToken(RegionToken, TaskPriority.Normal, false);
		private static readonly CommentToken EndToken = new CommentToken("#endregion", TaskPriority.Normal, false);
		private static readonly CommentToken AltEndToken = new CommentToken("#end region", TaskPriority.Normal, false);

		private readonly ITextBuffer buffer;
		private readonly IReadOnlyList<Regex> startExpressions;
		private readonly IReadOnlyList<Regex> endExpressions;

		private readonly object resourceLock = new object();
		private SnapshotRegions snapshotRegions;

		#endregion

		#region Constructors

		public OutliningTagger(ITextBuffer buffer, ScanInfo scanInfo)
		{
			this.buffer = buffer;

			if (scanInfo != null)
			{
				// Our RegionHandler's GetRegionBeginRegex only looks for single line comment tokens
				// when doing Collapse/ExpandAllRegions, so we'll use the same restriction here.
				this.startExpressions = scanInfo.GetTokenRegexes(StartToken, true).ToList();
				this.endExpressions = scanInfo.GetTokenRegexes(EndToken, true).Concat(scanInfo.GetTokenRegexes(AltEndToken, true)).ToList();
			}
			else
			{
				// If we don't have scan info, then we can't provide tags.
				this.startExpressions = CollectionUtility.EmptyArray<Regex>();
				this.endExpressions = CollectionUtility.EmptyArray<Regex>();
			}

			this.snapshotRegions = new SnapshotRegions(buffer.CurrentSnapshot, CollectionUtility.EmptyArray<Region>());
			this.buffer.Changed += this.BufferChanged;
			this.BackgroundReparse();
		}

		#endregion

		#region Public Events

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		#endregion

		#region Public Methods

		public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count > 0)
			{
				SnapshotRegions currentSnapshotRegions;
				lock (this.resourceLock)
				{
					currentSnapshotRegions = this.snapshotRegions;
				}

				ITextSnapshot currentSnapshot = currentSnapshotRegions.Snapshot;
				SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End)
					.TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
				int startLineNumber = entire.Start.GetContainingLine().LineNumber;
				int endLineNumber = entire.End.GetContainingLine().LineNumber;
				foreach (var region in currentSnapshotRegions.Regions)
				{
					if (region.StartLine <= endLineNumber && region.EndLine >= startLineNumber)
					{
						var startLine = currentSnapshot.GetLineFromLineNumber(region.StartLine);

						string startLineText = startLine.GetText();
						Match match = this.startExpressions.Select(regex => regex.Match(startLineText)).FirstOrDefault(m => m.Success);
						if (match != null)
						{
							string text = match.Groups[ScanInfo.RegexCommentGroupName].Value;
							if (text.StartsWith(RegionToken))
							{
								text = text.Substring(RegionToken.Length).Trim();
							}

							if (string.IsNullOrWhiteSpace(text))
							{
								text = RegionToken;
							}

							// The region starts at the beginning of the start expression and goes until the *end* of the line that contains the end expression.
							var endLine = currentSnapshot.GetLineFromLineNumber(region.EndLine);
							var regionSpan = new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End);
							string hoverText = region.GetHoverText(currentSnapshot, startLineText, match);
							var regionTag = new OutliningRegionTag(false, false, text, hoverText);

							yield return new TagSpan<IOutliningRegionTag>(regionSpan, regionTag);
						}
					}
				}
			}
		}

		#endregion

		#region Private Methods

		private static SnapshotSpan AsSnapshotSpan(Region region, ITextSnapshot snapshot)
		{
			var startLine = snapshot.GetLineFromLineNumber(region.StartLine);
			var endLine = (region.StartLine == region.EndLine) ? startLine
				: snapshot.GetLineFromLineNumber(region.EndLine);
			SnapshotSpan result = new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End);
			return result;
		}

		private void BufferChanged(object sender, TextContentChangedEventArgs e)
		{
			// If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
			if (e.After == this.buffer.CurrentSnapshot)
			{
				this.BackgroundReparse();
			}
		}

		private void BackgroundReparse()
		{
			ThreadHelper.JoinableTaskFactory.StartOnIdle(this.Reparse);
		}

		private void Reparse()
		{
			ITextSnapshot newSnapshot = this.buffer.CurrentSnapshot;
			List<Region> newRegions = new List<Region>();

			// Keep the current (deepest) partial region, which will have references to any parent partial regions.
			PartialRegion currentRegion = null;

			foreach (var line in newSnapshot.Lines)
			{
				string text = line.GetText();

				// Lines that match a start regex denote the start of a new region.
				Match startMatch = this.startExpressions.Select(regex => regex.Match(text)).FirstOrDefault(m => m.Success);
				if (startMatch != null)
				{
					int matchStartIndex = startMatch.Index;
					int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;
					int newLevel = currentLevel + 1;

					// Levels are the same, and we have an existing region;
					// End the current region and start the next.
					if (currentLevel == newLevel && currentRegion != null)
					{
						newRegions.Add(new Region
						{
							Level = currentRegion.Level,
							StartLine = currentRegion.StartLine,
							StartOffset = currentRegion.StartOffset,
							EndLine = line.LineNumber,
						});

						currentRegion = new PartialRegion
						{
							Level = newLevel,
							StartLine = line.LineNumber,
							StartOffset = matchStartIndex,
							PartialParent = currentRegion.PartialParent,
						};
					}
					else
					{
						// This is a new (sub)region
						currentRegion = new PartialRegion
						{
							Level = newLevel,
							StartLine = line.LineNumber,
							StartOffset = matchStartIndex,
							PartialParent = currentRegion,
						};
					}
				}
				else
				{
					// Lines that match an end regex denote the end of a region
					Match endMatch = this.endExpressions.Select(regex => regex.Match(text)).FirstOrDefault(m => m.Success);
					if (endMatch != null)
					{
						int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;
						int closingLevel = currentLevel;

						// The regions match
						if (currentRegion != null && currentLevel == closingLevel)
						{
							newRegions.Add(new Region
							{
								Level = currentLevel,
								StartLine = currentRegion.StartLine,
								StartOffset = currentRegion.StartOffset,
								EndLine = line.LineNumber,
							});

							currentRegion = currentRegion.PartialParent;
						}
					}
				}
			}

			SnapshotRegions newSnapshotRegions = new SnapshotRegions(newSnapshot, newRegions);
			SnapshotRegions oldSnapshotRegions;
			lock (this.resourceLock)
			{
				oldSnapshotRegions = this.snapshotRegions;
				this.snapshotRegions = newSnapshotRegions;
			}

			this.CheckIfTagsChanged(oldSnapshotRegions, newSnapshotRegions);
		}

		private void CheckIfTagsChanged(SnapshotRegions oldSnapshotRegions, SnapshotRegions newSnapshotRegions)
		{
			ITextSnapshot oldSnapshot = oldSnapshotRegions.Snapshot;
			IReadOnlyList<Region> oldRegions = oldSnapshotRegions.Regions;
			ITextSnapshot newSnapshot = newSnapshotRegions.Snapshot;
			IReadOnlyList<Region> newRegions = newSnapshotRegions.Regions;

			// Determine the changed spans and send a changed event with the new spans.
			List<Span> oldSpans = new List<Span>(
				oldRegions.Select(r => AsSnapshotSpan(r, oldSnapshot).TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive).Span));
			List<Span> newSpans = new List<Span>(newRegions.Select(r => AsSnapshotSpan(r, newSnapshot).Span));

			NormalizedSpanCollection oldSpanCollection = new NormalizedSpanCollection(oldSpans);
			NormalizedSpanCollection newSpanCollection = new NormalizedSpanCollection(newSpans);

			// The changed regions are regions that appear in one set or the other, but not both.
			NormalizedSpanCollection removed = NormalizedSpanCollection.Difference(oldSpanCollection, newSpanCollection);

			int changeStart = int.MaxValue;
			int changeEnd = -1;

			if (removed.Count > 0)
			{
				changeStart = removed[0].Start;
				changeEnd = removed[removed.Count - 1].End;
			}

			if (newSpans.Count > 0)
			{
				changeStart = Math.Min(changeStart, newSpans[0].Start);
				changeEnd = Math.Max(changeEnd, newSpans[newSpans.Count - 1].End);
			}

			if (changeStart <= changeEnd)
			{
				var handler = this.TagsChanged;
				if (handler != null)
				{
					var args = new SnapshotSpanEventArgs(new SnapshotSpan(newSnapshot, Span.FromBounds(changeStart, changeEnd)));
					handler(this, args);
				}
			}
		}

		#endregion

		#region Private Types

		private class PartialRegion
		{
			#region Public Properties

			public int StartLine { get; set; }

			public int StartOffset { get; set; }

			public int Level { get; set; }

			public PartialRegion PartialParent { get; set; }

			#endregion
		}

		private sealed class Region : PartialRegion
		{
			#region Public Properties

			public int EndLine { get; set; }

			#endregion

			#region Public Methods

			public string GetHoverText(ITextSnapshot snapshot, string startLineText, Match match)
			{
				IEnumerable<char> leadingWhitespaceChars = startLineText.TakeWhile((ch, index) => index < match.Index && char.IsWhiteSpace(ch));
				string leadingWhitespace = leadingWhitespaceChars.Any() ? new string(leadingWhitespaceChars.ToArray()) : string.Empty;

				StringBuilder sb = new StringBuilder();
				int afterMatchIndex = match.Index + match.Length;
				if (afterMatchIndex < startLineText.Length)
				{
					sb.Append(startLineText, afterMatchIndex, startLineText.Length - afterMatchIndex);
				}

				const int MaxLines = 20;
				int lineCount = 0;
				for (int lineNumber = this.StartLine + 1; lineNumber < this.EndLine && lineCount < MaxLines; lineNumber++, lineCount++)
				{
					if (sb.Length > 0)
					{
						sb.AppendLine();
					}

					ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
					string text = line.GetText();
					if (text.StartsWith(leadingWhitespace))
					{
						text = text.Substring(leadingWhitespace.Length);
					}

					sb.Append(text);
				}

				// Remove trailing whitespace (e.g., empty lines).
				while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1]))
				{
					sb.Length -= 1;
				}

				if (lineCount >= MaxLines)
				{
					sb.AppendLine().Append("...");
				}

				return sb.ToString();
			}

			#endregion
		}

		private sealed class SnapshotRegions
		{
			#region Constructors

			public SnapshotRegions(ITextSnapshot snapshot, IReadOnlyList<Region> regions)
			{
				this.Snapshot = snapshot;
				this.Regions = regions;
			}

			#endregion

			#region Public Properties

			public ITextSnapshot Snapshot { get; }

			public IReadOnlyList<Region> Regions { get; }

			#endregion
		}

		#endregion
	}
}
