namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Classification;

	#endregion

	internal sealed class FindResultsClassifier : ClassifierBase
	{
		#region Private Data Members

		// This pattern looks for optional whitespace, DRIVE:\ or \\, a valid NAMEPART (disallowing the chars returned by
		// Path.GetInvalidFileNameChars()), an optional \ separator, and then allows zero or more repeats of the NAMEPART[\] portion.
		private const string FilenamePrefixPattern = @"^\s*(\w\:\\|\\\\)([^\""\<\>\|\u0000\u0001\u0002\u0003\u0004\u0005\u0006\a\b\t\n\v\f\r" +
			@"\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f\:\*\?\\\/]\\?)*";

		// FilenameOnlyRegex must match to the end of the line.  FilenameAndLineNumberRegex requires either
		// a subsequent "(Line):" (for Find) or "(Line,Col):" (for Replace) before the match details.
		private static readonly Regex FilenameOnlyRegex = new Regex(FilenamePrefixPattern + "$", RegexOptions.Compiled);
		private static readonly Regex FilenameAndLineNumberRegex = new Regex(FilenamePrefixPattern + @"\(\d+(\,\d+)?\)\:", RegexOptions.Compiled);

		private static readonly Regex FindAllPattern = new Regex("(?n)Find all \"(?<pattern>.+?)\",", RegexOptions.Compiled);
		private static readonly Regex ReplaceAllPattern = new Regex("(?n)Replace all \".+?\", \"(?<pattern>.+?)\",", RegexOptions.Compiled);

		private static readonly object ResourceLock = new object();
		private static IClassificationType matchType;
		private static IClassificationType fileNameType;
		private static IClassificationType detailType;

		private FindArgs findArgs;

		#endregion

		#region Constructors

		public FindResultsClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
			: base(buffer)
		{
			lock (ResourceLock)
			{
				if (matchType == null)
				{
					matchType = registry.GetClassificationType(FindResultsFormats.MatchFormat.ClassificationName);
					fileNameType = registry.GetClassificationType(FindResultsFormats.FileNameFormat.ClassificationName);
					detailType = registry.GetClassificationType(FindResultsFormats.DetailFormat.ClassificationName);
				}
			}
		}

		#endregion

		#region Protected Methods

		protected override void GetClassificationSpans(List<ClassificationSpan> result, SnapshotSpan span, Options options)
		{
			bool showFileNames = options.HighlightFindResultsFileNames;
			bool showMatches = options.HighlightFindResultsMatches;
			bool showDetails = options.HighlightFindResultsDetails;

			if (showFileNames || showMatches || showDetails)
			{
				foreach (ITextSnapshotLine line in GetSpanLines(span))
				{
					string text = line.GetText();
					if (!string.IsNullOrEmpty(text))
					{
						// The first line in the window always contains the Find arguments, so we parse and highlight it specially.
						bool firstLine = line.LineNumber == 0;
						if (firstLine)
						{
							this.findArgs = FindArgs.TryParse(text);
						}

						// If we couldn't parse the find args (on this or an earlier call), then we don't need to iterate through the rest of the lines.
						if (this.findArgs == null)
						{
							break;
						}

						if (firstLine)
						{
							if (showMatches)
							{
								AddClassificationSpan(result, line, this.findArgs.PatternIndex, this.findArgs.PatternLength, matchType);
							}
						}
						else
						{
							this.HighlightResultLine(result, line, text, showFileNames, showMatches, showDetails);
						}
					}
				}
			}
		}

		#endregion

		#region Private Methods

		private static void AddClassificationSpan(List<ClassificationSpan> result, ITextSnapshotLine line, int start, int length, IClassificationType type)
		{
			SnapshotPoint startPoint = line.Start + start;
			SnapshotPoint endPoint = startPoint + length;
			SnapshotSpan snapshotSpan = new SnapshotSpan(startPoint, endPoint);
			ClassificationSpan classificationSpan = new ClassificationSpan(snapshotSpan, type);
			result.Add(classificationSpan);
		}

		private void HighlightResultLine(
			List<ClassificationSpan> result,
			ITextSnapshotLine line,
			string text,
			bool showFileNames,
			bool showMatches,
			bool showDetails)
		{
			try
			{
				if (this.findArgs.ListFileNamesOnly)
				{
					if (showFileNames && FilenameOnlyRegex.IsMatch(text))
					{
						AddClassificationSpan(result, line, 0, text.Length, fileNameType);
					}
				}
				else
				{
					Match fileNameMatch = FilenameAndLineNumberRegex.Match(text);
					if (fileNameMatch.Success)
					{
						if (showFileNames)
						{
							AddClassificationSpan(result, line, fileNameMatch.Index, fileNameMatch.Length, fileNameType);
						}

						// Note: If a match line has no leading whitespace, then there's no space between the 'file(num):' and the match line text.
						int start = fileNameMatch.Index + fileNameMatch.Length;
						Match patternMatch = this.findArgs.MatchExpression.Match(text, start);
						while (patternMatch.Success)
						{
							if (showDetails && start < patternMatch.Index)
							{
								AddClassificationSpan(result, line, start, patternMatch.Index - start, detailType);
							}

							if (showMatches)
							{
								AddClassificationSpan(result, line, patternMatch.Index, patternMatch.Length, matchType);
							}

							start = patternMatch.Index + patternMatch.Length;
							patternMatch = patternMatch.NextMatch();
						}

						if (showDetails && start < text.Length)
						{
							AddClassificationSpan(result, line, start, text.Length - start, detailType);
						}
					}
				}
			}
#pragma warning disable CC0004 // Catch block cannot be empty. Comment explains.
			catch (RegexMatchTimeoutException)
			{
				// We set a short Regex timeout because we don't want highlighting to add significant time.
				// It's better to skip highlighting than to make the results take forever to display.
			}
#pragma warning restore CC0004 // Catch block cannot be empty
		}

		#endregion

		#region Private Types

		private sealed class FindArgs
		{
			#region Constructors

			private FindArgs()
			{
				// This should only be called by TryParse.
			}

			#endregion

			#region Public Properties

			public int PatternIndex { get; private set; }

			public int PatternLength { get; private set; }

			public Regex MatchExpression { get; private set; }

			public bool ListFileNamesOnly { get; private set; }

			#endregion

			#region Public Methods

			public static FindArgs TryParse(string text)
			{
				FindArgs result = null;

				// VS 2019 16.5 totally changed the Find Results window and options. Update 16.5.4 restored some functionality to its List View,
				// but now it truncates the pattern after 20 characters. It still doesn't escape patterns, so comma and double quote are ambiguous.
				Match match = FindAllPattern.Match(text);
				if (!match.Success)
				{
					match = ReplaceAllPattern.Match(text);
				}

				if (match.Success && match.Groups.Count == 2)
				{
					int afterMatchIndex = match.Index + match.Value.Length;
					int listFileNamesOnly = text.IndexOf("List filenames only", afterMatchIndex, StringComparison.OrdinalIgnoreCase);
					int regularExpressions = text.IndexOf("Regular expressions", afterMatchIndex, StringComparison.OrdinalIgnoreCase);
					int wholeWord = text.IndexOf("Whole word", afterMatchIndex, StringComparison.OrdinalIgnoreCase);
					int matchCase = text.IndexOf("Match case", afterMatchIndex, StringComparison.OrdinalIgnoreCase);

					Group group = match.Groups[1];
					string pattern = group.Value;
					const string Ellipsis = "...";
					bool truncated = false;
					if (pattern.EndsWith(Ellipsis))
					{
						pattern = pattern.Substring(0, pattern.Length - Ellipsis.Length);
						truncated = true;
					}

					result = new FindArgs
					{
						ListFileNamesOnly = listFileNamesOnly >= 0,
						PatternIndex = group.Index,
						PatternLength = pattern.Length,
					};

					try
					{
						if (regularExpressions < 0)
						{
							pattern = Regex.Escape(pattern);

							// VS seems to only apply the "Whole word" option when "Regular expressions" isn't used, so we'll do the same.
							// We can't apply it at the end of a truncated pattern because the truncation might have occurred mid-word.
							if (wholeWord >= 0)
							{
								const string WholeWordBoundary = @"\b";
								pattern = WholeWordBoundary + pattern + (truncated ? string.Empty : WholeWordBoundary);
							}
						}

						// We don't want to spend too much time searching each line.
						TimeSpan timeout = TimeSpan.FromMilliseconds(100);
						result.MatchExpression = new Regex(pattern, matchCase >= 0 ? RegexOptions.None : RegexOptions.IgnoreCase, timeout);
					}
					catch (ArgumentException)
					{
						// We did our best to parse out the pattern and build a suitable regex.  But it's possible that the
						// parsed pattern was wrong (e.g., if it contained an unescaped ", " substring).  So if Regex
						// throws an ArgumentException, we just can't highlight this time.
						result = null;
					}
				}

				return result;
			}

			#endregion
		}

		#endregion
	}
}
