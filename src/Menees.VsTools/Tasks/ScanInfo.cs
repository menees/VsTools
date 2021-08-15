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
	using Microsoft.VisualStudio.Shell;
	using Microsoft.Win32;

	#endregion

	[DebuggerDisplay("{string.Join(\", \", delimiters)}")]
	internal sealed class ScanInfo
	{
		#region Public Constants

		public const string RegexCommentGroupName = "comment";

		#endregion

		#region Internal Fields

		internal static readonly ScanInfo Unscannable = new();

		#endregion

		#region Private Data Members

		private static readonly Lazy<Cache> LazyCache = new(() => new Cache());

		private readonly HashSet<Delimiter> delimiters = new();
		private readonly HashSet<Language> languages = new();

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

		public static Func<RegistryKey> GetUserRegistryRoot { get; set; }

		public bool IsScannable
		{
			get
			{
				bool result = this.delimiters.Count > 0;
				return result;
			}
		}

		#endregion

		#region Private Properties

		private bool IsPlainText =>
			this.languages.Count == 1
			&& this.languages.First() == Language.PlainText
			&& this.delimiters.Count == 1
			&& string.IsNullOrEmpty(this.delimiters.First().Begin);

		#endregion

		#region Public Methods

		public static ScanInfo Get(FileItem file)
		{
			Cache cache = LazyCache.Value;
			string extension = Path.GetExtension(file.FileName);
			if (cache.TryGet(extension, out ScanInfo result))
			{
				// Binary extensions will return the Unscannable instance.
				if (result.IsScannable)
				{
					// If the file is open in a document that is using a language that doesn't match one
					// associated with its file extension, then we need to include the language's info too.
					// We won't do this for PlainText because it has no comment delimiter, which can
					// cause ambiguities and duplicates if it's paired with an extension with known
					// delimiters.  For example, .bat files have delimiters, but they open as PlainText.
					Language docLanguage = file.DocumentLanguage;
					if (docLanguage != Language.Unknown
						&& docLanguage != Language.PlainText
						&& !result.languages.Contains(docLanguage))
					{
						if (cache.TryGet(docLanguage, out ScanInfo languageScanInfo))
						{
							// If we matched the extension to PlainText and the VS editor associated a language
							// that uses comment delimters, then we only need the language's ScanInfo. VS now
							// maps lots of text file formats to its HTML language editor, and we don't want to
							// report duplicates by also matching tokens with no comment delimiter.
							result = result.IsPlainText ? languageScanInfo : Merge(result, languageScanInfo);
						}
					}
				}
			}
			else
			{
				// The file's extension is unknown, so try to match it by language if a document is open.
				// This allows us to handle user-assigned extensions (e.g., .scs for C#).
				Language docLanguage = file.DocumentLanguage;
				if (docLanguage != Language.Unknown)
				{
					cache.TryGet(docLanguage, out result);
				}

				if (result == null)
				{
					result = Infer(file, cache);
				}
			}

			return result ?? Unscannable;
		}

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

		public IEnumerable<Regex> GetTokenRegexes(CommentToken token)
		{
			IEnumerable<Regex> result = this.GetTokenRegexes(token, false);
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

		public string TryGetSingleLineCommentDelimiter() => this.delimiters.FirstOrDefault(d => d.IsSingleLine)?.Begin;

		public (string Begin, string End) TryGetMultiLineCommentDelimiters()
		{
			Delimiter delimiter = this.delimiters.FirstOrDefault(d => !d.IsSingleLine);
			return (delimiter?.Begin, delimiter?.End);
		}

		#endregion

		#region Internal Methods

		internal static void ScanLanguageServices(RegistryKey packageUserRoot, Func<string, string, bool> scanLanguage)
		{
			string fullName = packageUserRoot.Name;
			string configName = fullName + @"_Config\Languages\Language Services";
			const string HivePrefix = @"HKEY_CURRENT_USER\";
			if (configName.StartsWith(HivePrefix, StringComparison.OrdinalIgnoreCase))
			{
				using (RegistryKey languageServicesRoot = Registry.CurrentUser.OpenSubKey(configName.Substring(HivePrefix.Length)))
				{
					if (languageServicesRoot != null)
					{
						string[] subKeyNames = languageServicesRoot.GetSubKeyNames();
						foreach (string subKeyName in subKeyNames)
						{
							using (RegistryKey languageSubKey = languageServicesRoot.OpenSubKey(subKeyName))
							{
								if (languageSubKey != null)
								{
									// "Registering a Language Service" explains what the values are for.
									// https://msdn.microsoft.com/en-us/library/bb166421.aspx
									string languageLogViewId = (string)languageSubKey.GetValue(string.Empty);
									if (scanLanguage(subKeyName, languageLogViewId))
									{
										break;
									}
								}
							}
						}
					}
				}
			}
		}

		#endregion

		#region Private Methods

		private static ScanInfo Merge(ScanInfo scanInfo1, ScanInfo scanInfo2)
		{
			ScanInfo result = new(
				scanInfo1.delimiters.Concat(scanInfo2.delimiters),
				scanInfo1.languages.Concat(scanInfo2.languages));
			return result;
		}

		private static ScanInfo Infer(FileItem file, Cache cache)
		{
			ScanInfo result = Unscannable;

			try
			{
				FileInfo fileInfo = new(file.FileName);

				// The largest hand-maintained source code file I've ever encountered was almost 900K (in G. Millennium),
				// and it was over 25000 lines of spaghetti code.  So I'm going to assume any file that's over 1MB
				// in size is either generated text or a binary file.
				const long MaxTextFileSize = 1048576;
				if (fileInfo.Exists && fileInfo.Length > 0 && fileInfo.Length <= MaxTextFileSize)
				{
					string extension = fileInfo.Extension;
					if (TryGetCustomExtensionScanInfo(extension, cache, ref result))
					{
						cache.TryAdd(extension, result);
					}
					else
					{
						byte[] buffer = ReadInitialBlock(file, fileInfo);
						if (buffer != null && IsTextBuffer(buffer))
						{
							// Use Language instead of .xml or .txt extensions here since a user could override those extensions.
							Language language = IsXmlBuffer(buffer) ? Language.XML : Language.PlainText;
							result = cache.Get(language);

							// Since we were actually able to read a buffer from the file, we'll save
							// this ScanInfo for use with other files using the same extension.
							cache.TryAdd(extension, result);
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (!FileItem.IsAccessException(ex))
				{
					throw;
				}

				result = Unscannable;
			}

			return result;
		}

		private static bool TryGetCustomExtensionScanInfo(string extension, Cache cache, ref ScanInfo scanInfo)
		{
			bool result = false;

			if (!string.IsNullOrEmpty(extension) && extension.Length > 1 && extension[0] == '.')
			{
				using (RegistryKey studioUserRoot = GetUserRegistryRoot?.Invoke())
				{
					if (studioUserRoot != null)
					{
						// See if this is a custom file extension mapped to a language service.
						string extLogViewId = null;
						using (RegistryKey extKey = studioUserRoot.OpenSubKey(@"FileExtensionMapping\" + extension.Substring(1)))
						{
							if (extKey != null)
							{
								extLogViewId = (string)extKey.GetValue("LogViewID");
							}
						}

						// If we found a LogViewID, then find which language service it goes with (if any).
						if (!string.IsNullOrEmpty(extLogViewId))
						{
							ScanInfo matchedScanInfo = null;
							ScanLanguageServices(
								studioUserRoot,
								(langName, langGuid) =>
								{
									bool matched = false;
									if (string.Equals(langGuid, extLogViewId, StringComparison.OrdinalIgnoreCase))
									{
										Language language = Utilities.GetLanguage(langName, extension);
										if (cache.TryGet(language, out ScanInfo languageScanInfo))
										{
											matchedScanInfo = languageScanInfo;
											matched = true;
										}
									}

									return matched;
								});

							if (matchedScanInfo != null)
							{
								scanInfo = matchedScanInfo;
								result = true;
							}
						}
					}
				}
			}

			return result;
		}

		private static byte[] ReadInitialBlock(FileItem file, FileInfo fileInfo)
		{
			byte[] buffer = null;

			using (Stream stream = file.TryOpenFileStream())
			{
				if (stream != null)
				{
					const int MaxBufferLength = 4096;
					int bufferLength = Math.Min((int)fileInfo.Length, MaxBufferLength);
					buffer = new byte[bufferLength];

					int offset = 0;
					while (offset < bufferLength)
					{
						int read = stream.Read(buffer, offset, bufferLength - offset);
						if (read == 0)
						{
							// TryOpenFileStream uses a FileShare setting that allows external writes,
							// so the stream size can shrink to be less than fileInfo.Length.  If we hit
							// EndOfFile earlier than expected, just shrink the buffer and return it.
							if (offset > 0)
							{
								Array.Resize(ref buffer, offset);
							}
							else
							{
								buffer = null;
							}

							break;
						}

						offset += read;
					}
				}
			}

			return buffer;
		}

		private static bool IsTextBuffer(byte[] buffer)
		{
			bool? result = null;

			using (MemoryStream stream = new(buffer))
			{
				using (StreamReader reader = CreateStreamReader(stream))
				{
					Encoding initialEncoding = reader.CurrentEncoding;
					int value;
					while ((value = reader.Read()) >= 0)
					{
						if (initialEncoding != null && reader.CurrentEncoding != initialEncoding)
						{
							// If a different encoding was detected via byte-order marks, then it's almost certainly a text file.
							result = true;
							break;
						}
						else
						{
							// We only need to check the encoding after the first read.
							initialEncoding = null;
						}

						// Unicode's control characters also include common whitespace like CR and LF,
						// so we'll exclude them.  http://stackoverflow.com/a/26652983/1882616
						char ch = (char)value;
						if (char.IsControl(ch) && !char.IsWhiteSpace(ch))
						{
							result = false;
							break;
						}
					}

					if (result == null)
					{
						// If we got into the while loop and read anything, then initialEncoding should be null.
						// Since we finished the loop without an explicit false (or true), then all the chars were text.
						result = initialEncoding == null;
					}
				}
			}

			return result ?? false;
		}

		private static bool IsXmlBuffer(byte[] buffer)
		{
			bool? result = null;

			using (MemoryStream stream = new(buffer))
			{
				using (StreamReader reader = CreateStreamReader(stream))
				{
					int value;
					while (result == null && (value = reader.Read()) >= 0)
					{
						char ch = (char)value;
						switch (ch)
						{
							case ' ':
							case '\t':
							case '\r':
							case '\n':
								// We need to skip leading whitespace and keep reading.
								break;

							case '<':
								// This is tentative.  We'll try to validate further below.
								result = true;
								break;

							default:
								result = false;
								break;
						}
					}
				}

				if (result.GetValueOrDefault())
				{
					stream.Seek(0, SeekOrigin.Begin);
					try
					{
						using (XmlReader reader = XmlReader.Create(stream))
						{
							// See if the first node (e.g., element, comment, or processing instruction) is valid XML.
							result = reader.Read();
						}
					}
					catch (XmlException)
					{
						result = false;
					}
				}
			}

			return result ?? false;
		}

		private static StreamReader CreateStreamReader(MemoryStream stream)
		{
			StreamReader result = new(
				stream,
				Encoding.Default,
				detectEncodingFromByteOrderMarks: true,
				bufferSize: stream.Capacity,
				leaveOpen: true);
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

			private readonly ConcurrentDictionary<string, ScanInfo> extensions = new(StringComparer.OrdinalIgnoreCase);
			private readonly Dictionary<Language, ScanInfo> languages = new();

			#endregion

			#region Constructors

			public Cache()
			{
				XElement root = XElement.Parse(Properties.Resources.ScanInfoXml);

				HashSet<string> binaryExtensions = new(StringComparer.OrdinalIgnoreCase);
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
					HashSet<Delimiter> delimiters = new();
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

					List<string> extensionList = new();
					ReadExtensions(style, extensionList);
					this.MergeExtensions(extensionList, delimiters, languageValues, binaryExtensions);
				}
			}

			#endregion

			#region Public Methods

			public bool TryGet(string extension, out ScanInfo scanInfo)
			{
				bool result = this.extensions.TryGetValue(extension, out scanInfo);
				return result;
			}

			public bool TryGet(Language language, out ScanInfo scanInfo)
			{
				bool result = this.languages.TryGetValue(language, out scanInfo);
				return result;
			}

			public bool TryAdd(string extension, ScanInfo scanInfo)
			{
				bool result = this.extensions.TryAdd(extension, scanInfo);
				return result;
			}

			public ScanInfo Get(Language language)
			{
				if (!this.TryGet(language, out ScanInfo result))
				{
					// If this is thrown, then the required language isn't in ScanInfo.xml.
					throw new InvalidOperationException("ScanInfo for a required language wasn't found: " + language);
				}

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
				ScanInfo scanInfo = new(delimiters, languageValues);

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

			private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();
			private readonly string id;

			#endregion

			#region Constructors

			public Delimiter(string singleLineDelimiter)
			{
				this.Begin = singleLineDelimiter;
				this.id = this.Begin;
			}

			public Delimiter(string begin, string end)
			{
				this.Begin = begin;
				this.End = end;

				// No begin or end delimiter has a space in it, so we can use that as a valid separator.
				this.id = this.Begin + " " + this.End;
			}

			#endregion

			#region Public Properties

			public bool IsSingleLine => string.IsNullOrEmpty(this.End);

			public string Begin { get; }

			public string End { get; }

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
				bool result = obj is Delimiter delimiter && StringComparer.OrdinalIgnoreCase.Equals(this.id, delimiter.id);
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
				StringBuilder sb = new();
				if (!string.IsNullOrEmpty(this.Begin))
				{
					// Allow optional whitespace after the comment begins.
					sb.Append(Regex.Escape(this.Begin)).Append(@"[ \t]*");
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
					sb.Append(')');
				}

				// Support an optional colon, space, or tab followed by any sequence of characters.
				// But we also have to support simple comments like "-- TODO" or "(* TODO *)".
				const string SeparatorPattern = @"[: \t]+.*";
				if (string.IsNullOrEmpty(this.End))
				{
					// Close the optional separator and the named group then match to the end of the string.
					sb.Append('(').Append(SeparatorPattern).Append(")?)$");
				}
				else
				{
					// Support an optional separator pattern ending with the lazy match token (?) so .* won't match
					// to the end of the string if the end delimiter is matched first.  Then close the optional separator
					// and the named group and then match to the end of the string or to the end delimiter.
					// http://stackoverflow.com/a/6738624/1882616 and http://www.regular-expressions.info/repeat.html
					sb.Append('(').Append(SeparatorPattern).Append("?)?)($|").Append(Regex.Escape(this.End)).Append(')');
				}

				// Ignore case overall because even in case-sensitive languages, most tokens need to be match case-insensitively.
				// Also, in some languages the case for comment delimiters needs to be ignored (e.g., REM in .bat files).
				// The options for Compiled and CultureInvariant are just to make it match as fast as possible.
				string regexPattern = sb.ToString();
				Regex result = new(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
				return result;
			}

			#endregion
		}

		#endregion

		#endregion
	}
}
