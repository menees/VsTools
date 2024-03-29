﻿namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Menees.VsTools.Sort;
	using Menees.VsTools.Tasks;

	#endregion

	internal sealed class TextLines
	{
		#region Private Data Members

		private string text;
		private string[] lines;
		private bool endsInHardReturn;

		#endregion

		#region Constructors

		public TextLines(string text)
		{
			this.SetLines(text);
		}

		#endregion

		#region Public Properties

		public IEnumerable<string> Lines
		{
			get
			{
				foreach (string line in this.lines ?? Enumerable.Empty<string>())
				{
					yield return line;
				}

				if (this.endsInHardReturn)
				{
					yield return string.Empty;
				}
			}
		}

		#endregion

		#region Public Methods

		public override string ToString()
		{
			string result = string.Empty;

			if (this.lines != null)
			{
				StringBuilder sb = new();

				// Convert the lines into a single string
				int numLines = this.lines.Length;
				for (int i = 0; i < numLines; i++)
				{
					if (i != 0)
					{
						sb.Append("\r\n");
					}

					sb.Append(this.lines[i]);
				}

				if (this.endsInHardReturn)
				{
					sb.Append("\r\n");
				}

				result = sb.ToString();
			}

			return result;
		}

		public void Sort(LineOptions lineOptions) => LineComparer.Sort(ref this.lines, lineOptions);

		public void Trim(bool start, bool end)
		{
			int numLines = this.lines.Length;
			for (int i = 0; i < numLines; i++)
			{
				if (start && end)
				{
					this.lines[i] = this.lines[i].Trim();
				}
				else if (start)
				{
					this.lines[i] = this.lines[i].TrimStart();
				}
				else if (end)
				{
					this.lines[i] = this.lines[i].TrimEnd();
				}
			}
		}

		public string Stream(Language language)
		{
			string singleLineCommentDelimiter = null;
			if (ScanInfo.TryGet(language, out ScanInfo scanInfo))
			{
				singleLineCommentDelimiter = scanInfo.TryGetSingleLineCommentDelimiter();
			}

			StringBuilder sb = new();

			int numLines = this.lines.Length;
			bool startOfLine = true;
			for (int i = 0; i < numLines; i++)
			{
				string line = this.lines[i];
				RemoveLinePrefix(ref line, singleLineCommentDelimiter);

				if (string.IsNullOrEmpty(line))
				{
					sb.Append("\r\n");
					startOfLine = true;
				}
				else
				{
					if (startOfLine)
					{
						if (sb.Length > 0)
						{
							sb.Append("\r\n");
						}
					}
					else
					{
						sb.Append(' ');
					}

					sb.Append(line);
					startOfLine = false;
				}
			}

			if (this.endsInHardReturn)
			{
				sb.Append("\r\n");
			}

			return sb.ToString();
		}

		public void Comment(bool comment, string delimiter)
		{
			int numLines = this.lines.Length;
			for (int i = 0; i < numLines; i++)
			{
				string line = this.lines[i];
				if (!string.IsNullOrEmpty(line))
				{
					int whitespaceLength = GetLeadingWhitespaceLength(line);
					if (whitespaceLength < line.Length)
					{
						string delimiterAndSpace = delimiter + ' ';
						if (comment)
						{
							line = line.Insert(whitespaceLength, delimiterAndSpace);
						}
						else
						{
							// Don't use RemoveFirst here because we want to make sure
							// the comment delimiter is the first non-whitespace.
							string afterWhitespace = line.Substring(whitespaceLength);
							if (afterWhitespace.StartsWith(delimiter))
							{
								int removeLength = delimiter.Length;
								if (afterWhitespace.StartsWith(delimiterAndSpace))
								{
									removeLength = delimiterAndSpace.Length;
								}

								line = line.Remove(whitespaceLength, removeLength);
							}
						}

						this.lines[i] = line;
					}
				}
			}
		}

		public void Comment(bool comment, string beginDelimiter, string endDelimiter)
		{
			if (this.lines.Length > 0)
			{
				if (comment)
				{
					this.Comment(beginDelimiter, endDelimiter);
				}
				else
				{
					this.Uncomment(beginDelimiter, endDelimiter);
				}
			}
		}

		public void AddCommentSpace(string delimiter)
		{
			string delimiterAndSpace = delimiter + ' ';
			int numLines = this.lines.Length;
			for (int i = 0; i < numLines; i++)
			{
				string line = this.lines[i];
				if (!string.IsNullOrEmpty(line) && ReplaceFirst(ref line, delimiter, delimiterAndSpace))
				{
					this.lines[i] = line;
				}
			}
		}

		#endregion

		#region Private Methods

		private static void RemoveLinePrefix(ref string line, string singleLineCommentDelimiter)
		{
			if (!string.IsNullOrEmpty(line))
			{
				// Strip outer whitespace first
				line = line.Trim();

				// Trim off any leading > chars that stupid email software puts in replies
				while (line.Length > 0 && line[0] == '>')
				{
					line = line.Substring(1).TrimStart();
				}

				// Trim off any leading single line comment delimiters
				if (!string.IsNullOrWhiteSpace(singleLineCommentDelimiter))
				{
					while (line.Length > 0 && line.StartsWith(singleLineCommentDelimiter))
					{
						line = line.Substring(singleLineCommentDelimiter.Length).TrimStart();
					}
				}
			}
		}

		private static bool RemoveFirst(ref string line, string remove)
		{
			bool result = ReplaceFirst(ref line, remove, string.Empty);
			return result;
		}

		private static bool RemoveLast(ref string line, string remove)
		{
			bool result = ReplaceLast(ref line, remove, string.Empty);
			return result;
		}

		private static bool ReplaceFirst(ref string line, string oldValue, string newValue) => ReplaceMatch(ref line, oldValue, newValue, replaceLast: false);

		private static bool ReplaceLast(ref string line, string oldValue, string newValue) => ReplaceMatch(ref line, oldValue, newValue, replaceLast: true);

		private static bool ReplaceMatch(ref string line, string oldValue, string newValue, bool replaceLast)
		{
			bool result = false;

			int index = replaceLast ? line.LastIndexOf(oldValue) : line.IndexOf(oldValue);
			if (index >= 0)
			{
				string newLine = line.Substring(0, index) + newValue + line.Substring(index + oldValue.Length);
				line = newLine;
				result = true;
			}

			return result;
		}

		private static int GetLeadingWhitespaceLength(string line)
		{
			int result = 0;

			foreach (char ch in line)
			{
				if (ch == ' ' || ch == '\t')
				{
					result++;
				}
				else
				{
					break;
				}
			}

			return result;
		}

		private static string EscapeNestedComments(string text, string beginDelimiter, string endDelimiter)
		{
			string result = text;

			if (!string.IsNullOrEmpty(text))
			{
				int matchBeginIndex = text.IndexOf(beginDelimiter);
				int matchEndIndex = text.IndexOf(endDelimiter);

				if (matchBeginIndex >= 0 || matchEndIndex >= 0)
				{
					StringBuilder sb = new();

					int startIndex = 0;
					while (matchBeginIndex >= 0 || matchEndIndex >= 0)
					{
						// We only need the minimum matched index.
						if (matchBeginIndex >= 0 && matchEndIndex >= 0)
						{
							if (matchBeginIndex <= matchEndIndex)
							{
								matchEndIndex = -1;
							}
							else
							{
								matchBeginIndex = -1;
							}
						}

						int matchIndex;
						string matchedDelimiter;
						if (matchBeginIndex >= 0)
						{
							matchIndex = matchBeginIndex;
							matchedDelimiter = beginDelimiter;
						}
						else
						{
							matchIndex = matchEndIndex;
							matchedDelimiter = endDelimiter;
						}

						sb.Append(text, startIndex, matchIndex - startIndex);
						sb.Append(endDelimiter).Append(beginDelimiter);
						startIndex = matchIndex + matchedDelimiter.Length;

						matchBeginIndex = text.IndexOf(beginDelimiter, startIndex);
						matchEndIndex = text.IndexOf(endDelimiter, startIndex);
					}

					if (startIndex < text.Length)
					{
						sb.Append(text, startIndex, text.Length - startIndex);
					}

					result = sb.ToString();
				}
			}

			return result;
		}

		private static string UnescapeNestedComments(string text, string beginDelimiter, string endDelimiter)
		{
			string result = text;

			if (!string.IsNullOrEmpty(text))
			{
				string escapedDelimiter = endDelimiter + beginDelimiter;
				int matchIndex = text.IndexOf(escapedDelimiter);
				if (matchIndex >= 0)
				{
					StringBuilder sb = new();

					// Alternate replacing the escaped delimiter with the begin and end delimiters.
					bool useBeginDelimiter = true;
					int startIndex = 0;
					while (matchIndex >= 0)
					{
						sb.Append(text, startIndex, matchIndex - startIndex);
						sb.Append(useBeginDelimiter ? beginDelimiter : endDelimiter);
						useBeginDelimiter = !useBeginDelimiter;
						startIndex = matchIndex + escapedDelimiter.Length;
						matchIndex = text.IndexOf(escapedDelimiter, startIndex);
					}

					if (startIndex < text.Length)
					{
						sb.Append(text, startIndex, text.Length - startIndex);
					}

					result = sb.ToString();
				}
			}

			return result;
		}

		private void SetLines(string value)
		{
			this.text = value;
			if (value != null)
			{
				// Get rid of any CRs so we can use Split().
				value = value.Replace("\r\n", "\n");
				value = value.Replace("\r", "\n");

				// If the text ends with a hard return, make a note of it
				// and take it off.  This makes some of the other methods
				// easier (e.g., Sort, Stream).
				this.endsInHardReturn = value.EndsWith("\n");
				if (this.endsInHardReturn)
				{
					value = value.Substring(0, value.Length - 1);
				}

				// Split the lines
				this.lines = value.Split('\n');
			}
		}

		private void Comment(string beginDelimiter, string endDelimiter)
		{
			if (this.lines.Length > 1)
			{
				string escapedText = EscapeNestedComments(this.text, beginDelimiter, endDelimiter);
				int whitespaceLength = GetLeadingWhitespaceLength(escapedText);

				string leadingWhitespace = escapedText.Substring(0, whitespaceLength);
				string newText = leadingWhitespace + beginDelimiter + "\r\n" + escapedText;
				if (this.endsInHardReturn)
				{
					newText += leadingWhitespace + endDelimiter + "\r\n";
				}
				else
				{
					newText += "\r\n" + leadingWhitespace + endDelimiter;
				}

				this.SetLines(newText);
			}
			else
			{
				string line = this.lines[0];
				int whitespaceLength = GetLeadingWhitespaceLength(line);
				if (whitespaceLength < line.Length)
				{
					line = EscapeNestedComments(line, beginDelimiter, endDelimiter);
					line = line.Insert(whitespaceLength, beginDelimiter + ' ') + ' ' + endDelimiter;
					this.lines[0] = line;
				}
			}
		}

		private void Uncomment(string beginDelimiter, string endDelimiter)
		{
			List<string> newLines = new(this.lines);

			string line = newLines[0];
			if (line.Trim() == beginDelimiter)
			{
				newLines.RemoveAt(0);
			}
			else if (RemoveFirst(ref line, beginDelimiter + ' ') || RemoveFirst(ref line, beginDelimiter))
			{
				newLines[0] = line;
			}

			if (newLines.Count > 0)
			{
				int lastLine = newLines.Count - 1;
				line = newLines[lastLine];
				if (line.Trim() == endDelimiter)
				{
					newLines.RemoveAt(lastLine);
				}
				else if (RemoveLast(ref line, ' ' + endDelimiter) || RemoveLast(ref line, endDelimiter))
				{
					newLines[lastLine] = line;
				}
			}

			string newText = UnescapeNestedComments(string.Join("\n", newLines), beginDelimiter, endDelimiter);
			if (this.endsInHardReturn)
			{
				newText += "\r\n";
			}

			this.SetLines(newText);
		}

		#endregion
	}
}
