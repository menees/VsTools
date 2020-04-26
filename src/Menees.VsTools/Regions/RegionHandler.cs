#region Using Directives

// If EnvDTE is used inside the namespace, then its EnvDTE.Language interface is used before VsTools.Language enum.
#pragma warning disable SA1200 // Using directives should be placed correctly
using EnvDTE;
#pragma warning restore SA1200 // Using directives should be placed correctly

#endregion

namespace Menees.VsTools.Regions
{
	#region Using Directives

	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using Microsoft.VisualStudio.Shell;

	#endregion

	internal static class RegionHandler
	{
		#region Private Data Members

		private static readonly HashSet<Language> VsBuiltInRegionSupport = new HashSet<Language>
		{
			Language.CSharp,
			Language.VB,
			Language.CPlusPlus,
			Language.JavaScript, // VS 2017 supported
			Language.TypeScript, // VS 2017 supported
			Language.HTML, // VS 2013 update 4 supported in new htmlx editor (but not old HTML web forms editor).
			Language.PowerShell, // VS 2015 supported
			Language.Python, // VS 2015 supported
			Language.XML, // VS 2017 supported (with no name or tooltip showing)
			Language.XAML, // VS 2017 supported (if tags have no spaces around them)
		};

		#endregion

		#region Public Methods

		public static bool IsSupportedLanguage(Language language)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// This method needs to be blazing fast because it's called by the UI constantly to update toolbar button states.
			bool result = VsBuiltInRegionSupport.Contains(language) || MainPackage.RegionOptions.IsSupported(language);
			return result;
		}

		public static void AddRegion(DTE dte, string[] predefinedRegions)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			Document doc = dte.ActiveDocument;
			Language language = Utilities.GetLanguage(doc);
			if (IsSupportedLanguage(language) && predefinedRegions.Length > 0)
			{
				string regionName = GetRegionName(predefinedRegions);
				if (!string.IsNullOrEmpty(regionName))
				{
					TextSelection sel = (TextSelection)doc.Selection;
					EditPoint startpoint = sel.TopPoint.CreateEditPoint();
					EditPoint endpoint = sel.BottomPoint.CreateEditPoint();
					bool hasSelection = true;
					if (startpoint.EqualTo(endpoint))
					{
						hasSelection = false;
					}

					if (!startpoint.AtStartOfLine)
					{
						startpoint.StartOfLine();
					}

					bool closeUndoContext = false;
					if (!dte.UndoContext.IsOpen)
					{
						closeUndoContext = true;
						dte.UndoContext.Open("Add Region" + TextDocumentHandler.UndoContextSuffix, false);
					}

					try
					{
						string padding = GetPadding(startpoint, hasSelection);
						MakeRegion(doc, regionName, startpoint, endpoint, padding, language, hasSelection);
					}
					finally
					{
						if (closeUndoContext)
						{
							dte.UndoContext.Close();
						}
					}
				}
			}
		}

		// Expands all regions in the current document
		public static void ExpandAllRegions(DTE dte, Language language)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (IsSupportedLanguage(language))
			{
				bool suppressedUI = false;
				if (!dte.SuppressUI)
				{
					dte.SuppressUI = true; // Disable UI while we do this
					suppressedUI = true;
				}

				try
				{
					if (language == Language.Python)
					{
						// The Python Tools language provider outlines a #region as "#region Test[...]".
						// Using FindText(...) to force a region expansion won't work for Python because
						// the "#region" part is still in the visible, non-hidden, non-collapsed text.
						// So we'll use the ExpandAllOutlining command instead.  It will also expand
						// non-region collapses (e.g., if someone has collapsed a function definition),
						// so it's not really just an "ExpandAllRegions" command like we want.  :-(
						// Note: This command is only available if outlining is enabled and something
						// in the document is collapsed.
						const string ExpandAllCommand = "Edit.ExpandAllOutlining";
						EnvDTE.Command command = dte.Commands.Item(ExpandAllCommand, 0);
						if (command.IsAvailable)
						{
							dte.ExecuteCommand(ExpandAllCommand);
						}
					}
					else
					{
						TextSelection selection = (TextSelection)dte.ActiveDocument.Selection; // Hook up to the ActiveDocument's selection
						selection.StartOfDocument(); // Shoot to the start of the document

						string regionBeginRegex = GetRegionBeginRegex(language);

						// Loop through the document finding all instances of #region. This action has the side benefit
						// of actually zooming us to the text in question when it is found and ALSO expanding it since it
						// is an outline.
						const int FindOptions = (int)vsFindOptions.vsFindOptionsMatchInHiddenText +
							(int)vsFindOptions.vsFindOptionsMatchCase +
							(int)vsFindOptions.vsFindOptionsRegularExpression;
						while (selection.FindText(regionBeginRegex, FindOptions))
						{
							// The FindText command will expand the #region block if "#region" is hidden.
						}

						selection.StartOfDocument(); // Shoot us back to the start of the document
					}
				}
				finally
				{
					if (suppressedUI)
					{
						dte.SuppressUI = false; // Reenable the UI
					}
				}
			}
		}

		// Collapses all regions in the current document
		public static void CollapseAllRegions(DTE dte, Language language, MainPackage package, bool showErrors = true)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (IsSupportedLanguage(language))
			{
				dte.SuppressUI = true; // Disable UI while we do this
				try
				{
					// Outling must be enabled.  If Outlining is turned off then the rest of this method will get stuck in an infinite loop.
					// It can be turned off by default from the C# advanced text editor properties, or it can be turned off by running
					// the Edit.StopOutlining command (e.g., in the Command window or via Edit -> Outlining -> Stop Outlining).
					// If the Edit.StartAutomaticOutlining command is available, then that means outlining needs to be turned back on.
					const string StartOutliningCommand = "Edit.StartAutomaticOutlining";
					EnvDTE.Command command = dte.Commands.Item(StartOutliningCommand);
					if (command.IsAvailable)
					{
						dte.ExecuteCommand(StartOutliningCommand);
					}

					const string ToggleOutliningExpansion = "Edit.ToggleOutliningExpansion";
					command = dte.Commands.Item(ToggleOutliningExpansion);
					const int MaxAttempts = 3;
					int maxAttempts = command.IsAvailable ? MaxAttempts : 0;

					string regionBeginRegex = GetRegionBeginRegex(language);

					// Sometimes VS can't collapse some regions, so we'll try the whole operation a few times if necessary.
					bool failedToCollapse = true;
					for (int attempt = 1; attempt <= maxAttempts && failedToCollapse; attempt++)
					{
						failedToCollapse = false;
						ExpandAllRegions(dte, language); // Force the expansion of all regions

						TextSelection selection = (TextSelection)dte.ActiveDocument.Selection; // Hook up to the ActiveDocument's selection
						selection.EndOfDocument(); // Shoot to the end of the document

						// Find the first occurence of #region from the end of the document to the start of the document.
						int currentFindOffset = 0;
						int previousFindOffset = int.MaxValue;
						const int FindOptions = (int)vsFindOptions.vsFindOptionsBackwards +
							(int)vsFindOptions.vsFindOptionsMatchCase +
							(int)vsFindOptions.vsFindOptionsRegularExpression;
						while (selection.FindText(regionBeginRegex, FindOptions))
						{
							currentFindOffset = selection.TopPoint.AbsoluteCharOffset;
							if (currentFindOffset >= previousFindOffset)
							{
								// I don't want to get stuck in an infinite loop.  I'd rather throw if something unexpected happens.
								throw new InvalidOperationException(string.Format(
									"FindText did not go backward!  Previous offset: {0}; Current offset: {1}.",
									previousFindOffset,
									currentFindOffset));
							}

							// We can ignore matches where #region is used inside a string or single line comment.
							// However, this still won't detect if it's used inside a multiline comment with the opening
							// delimiter on another line.
							selection.SelectLine();
							string lineText = selection.Text ?? string.Empty;

							// Make sure the region begin token is the first non-whitespace on the line.
							Match match = Regex.Match(lineText.TrimStart(), regionBeginRegex);
							if (match.Success && match.Index == 0)
							{
								// The SelectLine call above will leave the end anchor on the next line.  If there's no blank line between
								// a #region line and an XML doc comment after it, then having the end anchor on the line with the
								// XML doc comment will cause the comment to collapse instead of the #region.  So we'll avoid that
								// by moving back to the find offset.
								selection.MoveToAbsoluteOffset(currentFindOffset);

								// Try to increase the chances that the ToggleOutliningExpansion command will be available.
								selection.StartOfLine(vsStartOfLineOptions.vsStartOfLineOptionsFirstText);

								// Collapse this #region.  Sometimes VS reports that the Edit.ToggleOutliningExpansion command
								// isn't available even though it should be.  Poke it and give it a little bit of time to sync up.
								if (!command.IsAvailable)
								{
									const int WaitMilliseconds = 20;
									System.Threading.Thread.Sleep(WaitMilliseconds);
									int tempOffset = selection.TopPoint.AbsoluteCharOffset;
									selection.CharRight();
									selection.MoveToAbsoluteOffset(tempOffset);
									System.Threading.Thread.Sleep(WaitMilliseconds);
								}

								if (command.IsAvailable)
								{
									// If #region is found in a multiline comment, then this will collapse the enclosing block.
									dte.ExecuteCommand(ToggleOutliningExpansion);
								}
								else
								{
									// We couldn't collapse a #region.
									failedToCollapse = true;
								}
							}

							// Move to the start of the last FindText match, so we can continue searching backward from there.
							selection.MoveToAbsoluteOffset(currentFindOffset);
							previousFindOffset = currentFindOffset;
						}

						selection.StartOfDocument(); // All done, head back to the start of the doc
					}

					if (failedToCollapse && package != null && showErrors)
					{
						package.ShowMessageBox(
							"Some regions couldn't be collapsed because Visual Studio's Edit.ToggleOutliningExpansion command wasn't available.",
							true);
					}
				}
				finally
				{
					dte.SuppressUI = false; // Reenable the UI
				}
			}
		}

		#endregion

		#region Private Methods

		private static string GetRegionName(string[] predefinedRegions)
		{
			ListDialog dialog = new ListDialog();
			string result = dialog.Execute("Add Region", "Enter a name or number:", "Region", predefinedRegions, null);
			return result;
		}

		private static string GetPadding(EditPoint startPoint, bool hasSelection)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string temp = startPoint.GetText(startPoint.LineLength);
			int tempLength = temp.Length;

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < tempLength; i++)
			{
				char ch = temp[i];
				if (ch == ' ' || ch == '\t')
				{
					sb.Append(ch);
				}
				else
				{
					break;
				}
			}

			// If there was no selection and no padding was calculated
			// and the current temp is blank, then use the padding from
			// the line above it.
			string result = sb.ToString();
			if (!hasSelection && result.Length == 0 && tempLength == 0)
			{
				EditPoint newStartPoint = startPoint.CreateEditPoint();
				newStartPoint.LineUp(1);

				// Make sure we can still go up a line.
				if (newStartPoint.Line == startPoint.Line - 1)
				{
					result = GetPadding(newStartPoint, hasSelection);
				}
			}

			return result;
		}

		private static void MakeRegion(
			Document doc,
			string regionName,
			EditPoint startPoint,
			EditPoint endPoint,
			string padding,
			Language language,
			bool hasSelection)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			string beginRegionToken = string.Empty;
			string endRegionToken = string.Empty;
			string regionNameQuote = string.Empty;
			string tokenPrefix = string.Empty;
			string tokenSuffix = string.Empty;
			switch (language)
			{
				case Language.CSharp:
				case Language.PowerShell:
				case Language.Python:
					// PowerShell and Python don't need a '#' token prefix because we always put '#' in front of the begin/end tokens.
					beginRegionToken = "region";
					endRegionToken = "endregion";
					break;

				case Language.VB:
					beginRegionToken = "Region";
					endRegionToken = "End Region";
					regionNameQuote = "\"";
					break;

				case Language.CPlusPlus:
					beginRegionToken = "pragma region";
					endRegionToken = "pragma endregion";
					break;

				case Language.JavaScript:
				case Language.TypeScript:
					beginRegionToken = "region";
					endRegionToken = "endregion";
					tokenPrefix = "// ";
					break;

				case Language.XML:
				case Language.XAML:
				case Language.HTML:
					beginRegionToken = "region";
					endRegionToken = "endregion";
					tokenPrefix = "<!-- ";
					tokenSuffix = " -->";
					break;

				case Language.SQL:
					beginRegionToken = "region";
					endRegionToken = "endregion";
					tokenPrefix = "-- ";
					break;
			}

			// See if this file consistently uses no space after a single-line comment start token.
			if (!string.IsNullOrEmpty(tokenPrefix) && string.IsNullOrEmpty(tokenSuffix))
			{
				EditPoint commentSearch = startPoint.CreateEditPoint();
				if (!commentSearch.FindPattern(tokenPrefix, (int)vsFindOptions.vsFindOptionsFromStart))
				{
					string trimmedTokenPrefix = tokenPrefix.Trim();
					string pattern = Regex.Escape(trimmedTokenPrefix) + @"\S"; // Token prefix immediately followed by non-whitespace.
					if (commentSearch.FindPattern(pattern, (int)(vsFindOptions.vsFindOptionsFromStart | vsFindOptions.vsFindOptionsRegularExpression)))
					{
						tokenPrefix = trimmedTokenPrefix;
					}
				}
			}

			const string StartRegionFormat = "{0}{5}#{3} {4}{1}{4}{6}{2}{2}";
			string endRegionFormat;
			if (!endPoint.AtStartOfLine || !hasSelection)
			{
				endRegionFormat = "{1}{1}{0}{3}#{2}{4}";
			}
			else
			{
				endRegionFormat = "{1}{0}{3}#{2}{4}{1}";
			}

			EditPoint restoreEndPoint = endPoint.CreateEditPoint();
			string startRegionText = string.Format(StartRegionFormat, padding, regionName, "\r\n", beginRegionToken, regionNameQuote, tokenPrefix, tokenSuffix);
			startPoint.Insert(startRegionText);
			EditPoint restoreStartPoint = startPoint.CreateEditPoint();

			if (!hasSelection)
			{
				// The insert statement above moved StartPoint to the end of the inserted text.
				// Since there was no selection, now EndPoint is BEFORE StartPoint, so we need
				// to move it back after StartPoint.
				endPoint = startPoint;
				restoreEndPoint = restoreStartPoint;
			}

			endPoint.Insert(string.Format(endRegionFormat, padding, "\r\n", endRegionToken, tokenPrefix, tokenSuffix));

			// Restore the selection or caret position to the same logical area inside the new region.
			TextSelection selection = (TextSelection)doc.Selection;
			selection.MoveToPoint(restoreStartPoint);
			selection.MoveToPoint(restoreEndPoint, true);
		}

		private static string GetRegionBeginRegex(Language language)
		{
			string result = string.Empty;

			switch (language)
			{
				case Language.CSharp:
				case Language.PowerShell:
				case Language.Python:
					result = @"\#region";
					break;

				case Language.VB:
					result = @"\#Region";
					break;

				case Language.CPlusPlus:
					result = @"\#pragma\ region";
					break;

				case Language.JavaScript:
				case Language.TypeScript:
					// Must begin with '//' comment and then optional whitespace.
					result = @"//\s*\#region";
					break;

				case Language.XML:
				case Language.XAML:
				case Language.HTML:
					// Must begin with '<!--' comment and then optional whitespace and an optional '#'.
					result = @"\<\!\-\-\s*\#?region";
					break;

				case Language.SQL:
					// Must begin with '--' comment and then optional whitespace.
					result = @"--\s*\#region";
					break;
			}

			return result;
		}

		#endregion
	}
}
