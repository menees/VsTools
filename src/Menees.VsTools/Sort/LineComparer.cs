namespace Menees.VsTools.Sort
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	internal sealed class LineComparer : IComparer<string>
	{
		#region Private Data Members

		private readonly StringComparison comparison;
		private readonly bool descending;
		private readonly bool ignoreWhitespace;
		private readonly bool ignorePunctuation;
		private readonly bool byLength;

		#endregion

		#region Constructors

		private LineComparer(LineOptions options)
		{
			bool caseSensitive = options.HasFlag(LineOptions.CaseSensitive);
			if (options.HasFlag(LineOptions.ByOrdinal))
			{
				this.comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			}
			else
			{
				this.comparison = caseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
			}

			this.descending = options.HasFlag(LineOptions.Descending);
			this.ignoreWhitespace = options.HasFlag(LineOptions.IgnoreWhitespace);
			this.ignorePunctuation = options.HasFlag(LineOptions.IgnorePunctuation);
			this.byLength = options.HasFlag(LineOptions.ByLength);
		}

		#endregion

		#region Public Methods

		public static void Sort(ref string[] lines, LineOptions lineOptions)
		{
			IComparer<string> comparer = new LineComparer(lineOptions);
			Array.Sort(lines, comparer);

			if (lineOptions.HasFlag(LineOptions.EliminateDuplicates))
			{
				int numLines = lines.Length;
				List<string> newLines = new List<string>(numLines);
				string previousLine = null;
				for (int i = 0; i < numLines; i++)
				{
					string line = lines[i];
					if (previousLine == null || comparer.Compare(line, previousLine) != 0)
					{
						newLines.Add(line);
					}

					previousLine = line;
				}

				lines = newLines.ToArray();
			}
		}

		#endregion

		#region IComparer<string> Members

		public int Compare(string x, string y)
		{
			if (this.ignoreWhitespace)
			{
				x = x.Trim();
				y = y.Trim();
			}

			if (this.ignorePunctuation)
			{
				x = StripPunctuation(x);
				y = StripPunctuation(y);
			}

			int result;
			if (this.byLength)
			{
				result = Comparer<int>.Default.Compare(x?.Length ?? 0, y?.Length ?? 0);
				if (result == 0)
				{
					result = string.Compare(x, y, this.comparison);
				}
			}
			else
			{
				result = string.Compare(x, y, this.comparison);
			}

			if (this.descending)
			{
				result = -result;
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static string StripPunctuation(string value)
		{
			StringBuilder sb = new StringBuilder(value.Length);
			foreach (char ch in value)
			{
				if (!char.IsPunctuation(ch))
				{
					sb.Append(ch);
				}
			}

			string result = sb.Length == value.Length ? value : sb.ToString();
			return result;
		}

		#endregion
	}
}
