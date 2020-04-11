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

				// Example line to parse with all Find args enabled:
				// Find all "X", Match case, Whole word, Regular expressions, Subfolders, Keep modified files open, List filenames only, Find Results 1, "C:\...
				const string FindLinePrefix = "Find all \"";
				const string ReplaceLinePrefix = "Replace all \"";
				string linePrefix = text.StartsWith(FindLinePrefix) ? FindLinePrefix : text.StartsWith(ReplaceLinePrefix) ? ReplaceLinePrefix : null;
				if (!string.IsNullOrEmpty(linePrefix))
				{
					// The Find Results header line contains an unescaped search term/expression, which can be a problem
					// if it contains commas, double quotes, or text that also appears as one of the options.  To try to make
					// parsing as reliable as possible, we'll validate the line start, and then we'll work backward through the
					// known arg terms until we find the earliest one present.  Then the unescaped search term/expression
					// should be in double quotes immediately before that arg.
					int findResults = text.LastIndexOf(", Find Results "); // This should always be present.
					int listFileNamesOnly = text.LastIndexOf(", List filenames only, ");
					int keepOpen = text.LastIndexOf(", Keep modified files open, "); // This is always present for Find; it's optional for Replace.
					int subfolders = text.LastIndexOf(", Subfolders, ");
					int regularExpressions = text.LastIndexOf(", Regular expressions, ");
					int wholeWord = text.LastIndexOf(", Whole word, ");
					int matchCase = text.LastIndexOf(", Match case, ");
					int block = text.LastIndexOf(", Block, "); // When "Current Block (...)" is selected
					int[] afterPatternChoices = new[]
						{
							findResults, listFileNamesOnly, keepOpen, subfolders, regularExpressions, wholeWord, matchCase, block,
							int.MaxValue, // Include at least one value that always >= 0 since Min() requires that.
						};
					int afterPattern = afterPatternChoices.Where(index => index >= 0).Min();

					// VS won't let you search for an empty string, and the pattern should always have a double quote added after it.
					int patternIndex = linePrefix.Length;
					if (afterPattern < int.MaxValue && afterPattern > (patternIndex + 1))
					{
						string pattern = text.Substring(patternIndex, afterPattern - (patternIndex + 1));

						// For Replace All, the Find and Replace terms are both listed.  We only want the Replace term
						// since it's all that we'll be able to highlight in the matched/replaced lines that are returned.
						if (linePrefix == ReplaceLinePrefix)
						{
							// The Find and Replace terms are unescaped, so it's possible that one contains this separator.
							// It's unlikely, but if it happens, then our highlights may be off or non-existent.
							const string Separator = "\", \"";
							int separatorIndex = pattern.IndexOf(Separator);
							if (separatorIndex >= 0)
							{
								separatorIndex += Separator.Length;
								pattern = pattern.Substring(separatorIndex);
								patternIndex += separatorIndex;
							}
							else
							{
								// Something is wrong.  The Find and Replace terms weren't formatted like we expected.
								pattern = null;
							}
						}

						if (!string.IsNullOrEmpty(pattern))
						{
							result = new FindArgs
							{
								ListFileNamesOnly = listFileNamesOnly >= 0,
								PatternIndex = patternIndex,
								PatternLength = pattern.Length,
							};

							try
							{
								if (regularExpressions < 0)
								{
									pattern = Regex.Escape(pattern);

									// VS seems to only apply the "Whole word" option when "Regular expressions" isn't used, so
									// we'll do the same.  Otherwise, VS would return lines that we couldn't highlight a match in!
									if (wholeWord >= 0)
									{
										const string WholeWordBoundary = @"\b";
										pattern = WholeWordBoundary + pattern + WholeWordBoundary;
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
					}
				}

				return result;
			}

			#endregion
		}

		#endregion
	}
}
