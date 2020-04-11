namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Xml;
	using System.Xml.Linq;
	using Microsoft.Win32;

	#endregion

	[DebuggerDisplay("{string.Join(\", \", delimiters)}")]
	internal sealed class ScanInfo
	{
		#region Public Constants

		public const string RegexCommentGroupName = "comment";

		#endregion

		#region Internal Fields

		internal static readonly ScanInfo Unscannable = new ScanInfo();

		#endregion

		#region Private Data Members

		private static readonly Lazy<Cache> LazyCache = new Lazy<Cache>(() => new Cache());

		private readonly HashSet<Delimiter> delimiters = new HashSet<Delimiter>();
		private readonly HashSet<Language> languages = new HashSet<Language>();

		#endregion

		#region Constructors

		private ScanInfo()
		{
			// This constructor is used for the Unscannable instance, which supports no delimiters and no languages.
		}

		private ScanInfo(IEnumerable<Delimiter> delimiters, Language language)
			: this(delimiters, new[] { language })
		{
		}

		private ScanInfo(IEnumerable<Delimiter> delimiters, IEnumerable<Language> languages)
			: this()
		{
			foreach (Delimiter delimiter in delimiters)
			{
				this.delimiters.Add(delimiter);
			}

			if (languages != null)
			{
				foreach (Language language in languages)
				{
					this.languages.Add(language);
				}
			}
		}

		#endregion

		#region Public Properties

		public bool IsScannable
		{
			get
			{
				bool result = this.delimiters.Count > 0;
				return result;
			}
		}

		#endregion

		#region Public Methods

		public static bool TryGet(Language language, out ScanInfo scanInfo)
		{
			scanInfo = null;

			bool result = language != Language.Unknown
				&& language != Language.PlainText
				&& LazyCache.Value.TryGet(language, out scanInfo)
				&& scanInfo != null
				&& scanInfo.IsScannable;

			return result;
		}

		public IEnumerable<Regex> GetTokenRegexes(CommentToken token, bool preferSingleLineOnly)
		{
			IEnumerable<Delimiter> preferredDelimiters = preferSingleLineOnly && this.delimiters.Any(d => d.IsSingleLine)
				? this.delimiters.Where(d => d.IsSingleLine)
				: this.delimiters;

			foreach (Delimiter delimiter in preferredDelimiters)
			{
				yield return delimiter.GetCachedRegex(token);
			}
		}

		#endregion

		#region Private Methods

		private static ScanInfo Merge(ScanInfo scanInfo1, ScanInfo scanInfo2)
		{
			ScanInfo result = new ScanInfo(
				scanInfo1.delimiters.Concat(scanInfo2.delimiters),
				scanInfo1.languages.Concat(scanInfo2.languages));
			return result;
		}

		#endregion

		#region Private Types

		#region Cache

		private sealed class Cache
		{
			#region Private Data Members

			// Include whitespace and newline characters to string.Split will get rid of indentation and newlines too.
			private static readonly char[] SplitCharacters = new[] { ',', ';', ' ', '\t', '\r', '\n' };

			private readonly ConcurrentDictionary<string, ScanInfo> extensions = new ConcurrentDictionary<string, ScanInfo>(StringComparer.OrdinalIgnoreCase);
			private readonly Dictionary<Language, ScanInfo> languages = new Dictionary<Language, ScanInfo>();

			#endregion

			#region Constructors

			public Cache()
			{
				XElement root = XElement.Parse(Properties.Resources.ScanInfoXml);

				HashSet<string> binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (XElement binary in root.Elements("Binary"))
				{
					ReadExtensions(binary, binaryExtensions);
				}

				foreach (string extension in binaryExtensions)
				{
					this.extensions.TryAdd(extension, ScanInfo.Unscannable);
				}

				foreach (XElement style in root.Elements("Style"))
				{
					HashSet<Delimiter> delimiters = new HashSet<Delimiter>();
					foreach (XElement delimiter in style.Elements("SingleLineDelimiter"))
					{
						delimiters.Add(new Delimiter(delimiter.Value));
					}

					foreach (XElement delimiter in style.Elements("MultiLineDelimiter"))
					{
						delimiters.Add(new Delimiter((string)delimiter.Attribute("Begin"), (string)delimiter.Attribute("End")));
					}

					HashSet<Language> languageValues = null;
					XElement languagesElement = style.Element("Languages");
					if (languagesElement != null)
					{
						languageValues = new HashSet<Language>(Split(
							languagesElement.Value,
							value => (Language)Enum.Parse(typeof(Language), value)));

						foreach (Language language in languageValues)
						{
							this.languages.Add(language, new ScanInfo(delimiters, language));
						}
					}

					List<string> extensionList = new List<string>();
					ReadExtensions(style, extensionList);
					this.MergeExtensions(extensionList, delimiters, languageValues, binaryExtensions);
				}
			}

			#endregion

			#region Public Methods

			public bool TryGet(Language language, out ScanInfo scanInfo)
			{
				bool result = this.languages.TryGetValue(language, out scanInfo);
				return result;
			}

			#endregion

			#region Private Methods

			private static IEnumerable<T> Split<T>(string text, Func<string, T> transformValue)
			{
				if (!string.IsNullOrEmpty(text))
				{
					string[] tokens = text.Split(SplitCharacters, StringSplitOptions.RemoveEmptyEntries);
					foreach (string token in tokens.Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)))
					{
						T value = transformValue(token);
						yield return value;
					}
				}
			}

			private static void ReadExtensions(XElement extensionContainer, ICollection<string> collection)
			{
				XElement extensions = extensionContainer.Element("Extensions");
				if (extensions != null)
				{
					string text = extensions.Value;
					IEnumerable<string> values = Split(text, value => '.' + value);
					foreach (string value in values)
					{
						collection.Add(value);
					}
				}
			}

			private void MergeExtensions(
				List<string> extensionList,
				HashSet<Delimiter> delimiters,
				HashSet<Language> languageValues,
				HashSet<string> binaryExtensions)
			{
				ScanInfo scanInfo = new ScanInfo(delimiters, languageValues);

				foreach (string extension in extensionList)
				{
					if (binaryExtensions.Contains(extension))
					{
						// If we get this error, then ScanInfo.xml has assigned the extension as both binary and non-binary.
						throw new InvalidOperationException(extension + " has already been listed as a binary extension in ScanInfo.xml.");
					}

					if (this.extensions.TryGetValue(extension, out ScanInfo existingScanInfo))
					{
						ScanInfo mergedScanInfo = ScanInfo.Merge(scanInfo, existingScanInfo);
						this.extensions[extension] = mergedScanInfo;
					}
					else
					{
						this.extensions.TryAdd(extension, scanInfo);
					}
				}
			}

			#endregion
		}

		#endregion

		#region Delimiter

		[DebuggerDisplay("{id}")]
		private sealed class Delimiter
		{
			#region Private Data Members

			private static readonly ConcurrentDictionary<string, Regex> RegexCache = new ConcurrentDictionary<string, Regex>();

			private readonly string begin;
			private readonly string end;
			private readonly string id;

			#endregion

			#region Constructors

			public Delimiter(string singleLineDelimiter)
			{
				this.begin = singleLineDelimiter;
				this.id = this.begin;
			}

			public Delimiter(string begin, string end)
			{
				this.begin = begin;
				this.end = end;

				// No begin or end delimiter has a space in it, so we can use that as a valid separator.
				this.id = this.begin + " " + this.end;
			}

			#endregion

			#region Public Properties

			public bool IsSingleLine => string.IsNullOrEmpty(this.end);

			#endregion

			#region Public Methods

			public override string ToString() => this.id;

			public override int GetHashCode()
			{
				int result = StringComparer.OrdinalIgnoreCase.GetHashCode(this.id);
				return result;
			}

			public override bool Equals(object obj)
			{
				bool result = !(obj is Delimiter delimiter) ? false : StringComparer.OrdinalIgnoreCase.Equals(this.id, delimiter.id);
				return result;
			}

			public Regex GetCachedRegex(CommentToken token)
			{
				// Cache the compiled Regex instances so we can reuse them even across different languages and file extensions.
				// This is an important performance optimization for common delimiters like "//" and "/* */".
				string regexCacheKey = this.id + ' ' + token.Text + ' ' + (token.IsCaseSensitive ? '+' : '-');
				Regex result = RegexCache.GetOrAdd(regexCacheKey, key => this.CreateRegex(token));
				return result;
			}

			#endregion

			#region Private Methods

			private Regex CreateRegex(CommentToken token)
			{
				StringBuilder sb = new StringBuilder();
				if (!string.IsNullOrEmpty(this.begin))
				{
					// Allow optional whitespace after the comment begins.
					sb.Append(Regex.Escape(this.begin)).Append(@"[ \t]*");
				}
				else
				{
					// If there's no begin delimiter (e.g., in a PlainText file), then start from
					// the beginning of the line or require a non-word character before the token.
					// https://msdn.microsoft.com/en-us/library/20bw873z.aspx#WordCharacter
					sb.Append(@"(^|[^\w])");
				}

				// Begin a named group to get the comment text.
				sb.Append("(?<" + RegexCommentGroupName + ">");
				if (token.IsCaseSensitive)
				{
					sb.Append("(?-i:");
				}

				sb.Append(Regex.Escape(token.Text));
				if (token.IsCaseSensitive)
				{
					sb.Append(")");
				}

				// Support an optional colon, space, or tab followed by any sequence of characters.
				// But we also have to support simple comments like "-- TODO" or "(* TODO *)".
				const string SeparatorPattern = @"[: \t]+.*";
				if (string.IsNullOrEmpty(this.end))
				{
					// Close the optional separator and the named group then match to the end of the string.
					sb.Append("(").Append(SeparatorPattern).Append(")?)$");
				}
				else
				{
					// Support an optional separator pattern ending with the lazy match token (?) so .* won't match
					// to the end of the string if the end delimiter is matched first.  Then close the optional separator
					// and the named group and then match to the end of the string or to the end delimiter.
					// http://stackoverflow.com/a/6738624/1882616 and http://www.regular-expressions.info/repeat.html
					sb.Append(@"(").Append(SeparatorPattern).Append("?)?)($|").Append(Regex.Escape(this.end)).Append(")");
				}

				// Ignore case overall because even in case-sensitive languages, most tokens need to be match case-insensitively.
				// Also, in some languages the case for comment delimiters needs to be ignored (e.g., REM in .bat files).
				// The options for Compiled and CultureInvariant are just to make it match as fast as possible.
				string regexPattern = sb.ToString();
				Regex result = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
				return result;
			}

			#endregion
		}

		#endregion

		#endregion
	}
}
