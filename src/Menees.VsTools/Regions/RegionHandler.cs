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
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.ComponentModelHost;
	using Microsoft.VisualStudio.Editor;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.Text.Outlining;
	using Microsoft.VisualStudio.TextManager.Interop;

	#endregion

	internal static class RegionHandler
	{
		#region Private Data Members

		private static readonly HashSet<Language> VsBuiltInRegionSupport = new()
		{
			Language.CSharp,
			Language.VB,
			Language.CPlusPlus,
			Language.JavaScript, // VS 2017 supported
			Language.TypeScript, // VS 2017 supported
			Language.HTML, // VS 2013 update 4 supported in new htmlx editor (but not old HTML web forms editor).
			Language.PowerShell, // VS 2015 supported
			Language.XML, // VS 2017 supported (with no name or tooltip showing)
			Language.XAML, // VS 2017 supported (if tags have no spaces around them)
			Language.Razor, // VS 2019 supported

			// Language.Python, // VS 2015-2019 supported. VS 2022 RC doesn't, even if Python workload is installed.
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

		public static void AddRegion(DTE dte, Options options)
		{
			string[] predefinedRegions = OptionsBase.SplitValues(options.PredefinedRegions);

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
						MakeRegion(doc, regionName, startpoint, endpoint, padding, language, hasSelection, options);
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
		public static void ExpandAllRegions(DTE dte, Language language, MainPackage package)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (IsSupportedLanguage(language))
			{
				dte.SuppressUI = true; // Disable UI while we do this
				try
				{
					HandleRegions(language, package, (outline, region) =>
					{
						if (region is ICollapsed collapsed)
						{
							outline.Expand(collapsed);
						}
					});
				}
				finally
				{
					dte.SuppressUI = false; // Reenable the UI
				}
			}
		}

		// Collapses all regions in the current document
		public static void CollapseAllRegions(DTE dte, Language language, MainPackage package)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (IsSupportedLanguage(language))
			{
				dte.SuppressUI = true; // Disable UI while we do this
				try
				{
					HandleRegions(language, package, (outline, region) =>
					{
						if (!region.IsCollapsed)
						{
							outline.TryCollapse(region);
						}
					});
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
			ListDialog dialog = new();
			string result = dialog.Execute("Add Region", "Enter a name or number:", "Region", predefinedRegions, null);
			return result;
		}

		private static string GetPadding(EditPoint startPoint, bool hasSelection)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string temp = startPoint.GetText(startPoint.LineLength);
			int tempLength = temp.Length;

			StringBuilder sb = new();
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

		private static void GetRegionInfo(
			Language language,
			out string beginRegionToken,
			out string endRegionToken,
			out string regionNameQuote,
			out string tokenPrefix,
			out string tokenSuffix)
		{
			beginRegionToken = string.Empty;
			endRegionToken = string.Empty;
			regionNameQuote = string.Empty;
			tokenPrefix = string.Empty;
			tokenSuffix = string.Empty;

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

				case Language.CSS:
				case Language.Less:
				case Language.Scss:
					// Use multi-line delimiter so region comments will remain in generated CSS.
					beginRegionToken = "region";
					endRegionToken = "endregion";
					tokenPrefix = "/* ";
					tokenSuffix = " */";
					break;

				case Language.XML:
				case Language.XAML:
				case Language.HTML:
				case Language.Razor:
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
		}

		private static void MakeRegion(
			Document doc,
			string regionName,
			EditPoint startPoint,
			EditPoint endPoint,
			string padding,
			Language language,
			bool hasSelection,
			Options options)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			GetRegionInfo(
				language,
				out string beginRegionToken,
				out string endRegionToken,
				out string regionNameQuote,
				out string tokenPrefix,
				out string tokenSuffix);

			// See if this file consistently uses no space after a single-line comment start token.
			if (!string.IsNullOrEmpty(tokenPrefix) && string.IsNullOrEmpty(tokenSuffix))
			{
				EditPoint commentSearch = startPoint.CreateEditPoint();
				commentSearch.StartOfDocument();
				EditPoint commentSearchEnd = startPoint.CreateEditPoint();
				commentSearchEnd.EndOfDocument();
				string documentText = commentSearch.GetText(commentSearchEnd);
				if (documentText.IndexOf(tokenPrefix) < 0)
				{
					string trimmedTokenPrefix = tokenPrefix.Trim();
					string pattern = Regex.Escape(trimmedTokenPrefix) + @"\S"; // Token prefix immediately followed by non-whitespace.
					if (Regex.IsMatch(documentText, pattern))
					{
						tokenPrefix = trimmedTokenPrefix;
					}
				}
			}

			string regionEndName = options.AddNameAfterEnd ? (" " + regionName) : string.Empty;
			string startRegionFormat = "{0}{5}#{3} {4}{1}{4}{6}{2}" + (options.AddInnerBlankLines ? "{2}" : string.Empty);
			string endRegionFormat = options.AddInnerBlankLines ? "{1}" : string.Empty;
			if (!endPoint.AtStartOfLine || !hasSelection)
			{
				endRegionFormat += "{1}{0}{3}#{2}{4}{5}";
			}
			else
			{
				endRegionFormat += "{0}{3}#{2}{4}{5}{1}";
			}

			EditPoint restoreEndPoint = endPoint.CreateEditPoint();
			string startRegionText = string.Format(startRegionFormat, padding, regionName, "\r\n", beginRegionToken, regionNameQuote, tokenPrefix, tokenSuffix);
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

			string endRegionText = string.Format(endRegionFormat, padding, "\r\n", endRegionToken, tokenPrefix, tokenSuffix, regionEndName);
			endPoint.Insert(endRegionText);

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
					result = @"^\#region";
					break;

				case Language.VB:
					result = @"^\#Region";
					break;

				case Language.CPlusPlus:
					result = @"^\#pragma\ region";
					break;

				case Language.JavaScript:
				case Language.TypeScript:
					// Must begin with '//' comment and then optional whitespace.
					result = @"^//\s*\#region";
					break;

				case Language.CSS:
					// Must begin with '/*' comment and then optional whitespace.
					result = @"^/\*\s*\#region";
					break;

				case Language.Less:
				case Language.Scss:
					// Must begin with '//' or '/*' comment and then optional '!' and whitespace.
					result = @"^/[/\*]!?\s*\#region";
					break;

				case Language.XML:
				case Language.XAML:
				case Language.HTML:
				case Language.Razor:
					// Must begin with '<!--' comment and then optional whitespace and an optional '#'.
					result = @"^\<\!\-\-\s*\#?region";
					break;

				case Language.SQL:
					// Must begin with '--' comment and then optional whitespace.
					result = @"^--\s*\#region";
					break;
			}

			return result;
		}

		private static void HandleRegions(Language language, MainPackage package, Action<IOutliningManager, ICollapsible> handleRegion)
		{
			IServiceProvider serviceProvider = package.ServiceProvider;
			IVsTextManager textManager = serviceProvider.GetService<SVsTextManager, IVsTextManager>(true);
			ErrorHandler.ThrowOnFailure(textManager.GetActiveView(1, null, out IVsTextView activeTextView));
			if (activeTextView != null)
			{
				IComponentModel componentModel = serviceProvider.GetService<SComponentModel, IComponentModel>(true);
				IVsEditorAdaptersFactoryService editorAdapter = componentModel.GetService<IVsEditorAdaptersFactoryService>();
				IWpfTextView wpfView = editorAdapter.GetWpfTextView(activeTextView);
				if (wpfView != null)
				{
					IOutliningManagerService outliningService = componentModel.GetService<IOutliningManagerService>();
					IOutliningManager outliningManager = outliningService.GetOutliningManager(wpfView);
					string regionBeginRegex = GetRegionBeginRegex(language);
					if (outliningManager != null && !string.IsNullOrEmpty(regionBeginRegex))
					{
						SnapshotSpan span = new(wpfView.TextSnapshot, 0, wpfView.TextSnapshot.Length);
						foreach (ICollapsible region in outliningManager.GetAllRegions(span))
						{
							ITrackingSpan extent = region.Extent;
							ITextSnapshot regionSnapshot = extent.TextBuffer.CurrentSnapshot;
							string text = extent.GetText(regionSnapshot);
							if (Regex.IsMatch(text, regionBeginRegex))
							{
								handleRegion(outliningManager, region);
							}
						}
					}
				}
			}
		}

		#endregion
	}
}
