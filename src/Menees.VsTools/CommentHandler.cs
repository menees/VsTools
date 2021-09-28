namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using EnvDTE;
	using EnvDTE80;
	using Menees.VsTools.Tasks;
	using Microsoft.VisualStudio.Shell;

	#endregion

	internal static class CommentHandler
	{
		#region Private Data Members

		private static readonly vsCMElement[] ElementScopes = new[]
			{
				// These are block elements in order from innermost to outermost.
				// Some rarely have blocks (e.g., variables with initializer blocks
				// or events with add/remove handlers).
				vsCMElement.vsCMElementVariable,
				vsCMElement.vsCMElementParameter,
				vsCMElement.vsCMElementAttribute,
				vsCMElement.vsCMElementDelegate,
				vsCMElement.vsCMElementEvent,
				vsCMElement.vsCMElementProperty,
				vsCMElement.vsCMElementFunction,
				vsCMElement.vsCMElementClass,
				vsCMElement.vsCMElementStruct,
				vsCMElement.vsCMElementEnum,
				vsCMElement.vsCMElementInterface,
				vsCMElement.vsCMElementModule,
				vsCMElement.vsCMElementUnion,
				vsCMElement.vsCMElementUDTDecl,
				vsCMElement.vsCMElementMacro,
				vsCMElement.vsCMElementNamespace,
			};

		private static readonly Language[] NamespaceLanguages = new[]
		{
			Language.CPlusPlus,
			Language.VB,
			Language.CSharp,
			Language.IDL,
			Language.FSharp,
			Language.TypeScript,
			Language.Python,
		};

		#endregion

		#region Public Methods

		public static bool CanCommentSelection(DTE dte, bool comment)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// First make sure text is selected.  The VS commands don't require that,
			// but visually I don't want the command enabled unless text is selected.
			TextDocumentHandler handler = new(dte);
			bool result = handler.HasNonEmptySelection;
			if (result)
			{
				// My custom logic applies to a subset of the languages that the VS commands
				// support, so I'll use their availability to report my commands's statuses.
				result = CanExecuteVsCommand(dte, comment ? "Edit.CommentSelection" : "Edit.UncommentSelection");
			}

			return result;
		}

		public static void CommentSelection(DTE dte, bool comment)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(dte);
			if (handler.HasNonEmptySelection)
			{
				Language language = handler.Language;
				bool useVsIndentation = GetCommentStyle(
					language,
					MainPackage.GeneralOptions.UseVsStyleCommentIndentation,
					false,
					out string beginDelimiter,
					out string endDelimiter);

				// The Edit.CommentSelection and Edit.UncommentSelection commands do 95% of what I want.
				// However, I want to add/remove a space after the opening (and before the closing) delimiter,
				// and I prefer that single-line comments be indented to match the code.  So I'll fallback to the
				// VS commands for any languages I don't have custom logic for.
				bool useSingleLineStyle = string.IsNullOrEmpty(endDelimiter);
				bool useSingleLineVsIndentation = comment && useSingleLineStyle && useVsIndentation;
				string commandName = comment ? "Comment Selection" : "Uncomment Selection";
				if (string.IsNullOrEmpty(beginDelimiter) || useSingleLineVsIndentation)
				{
					// Nest the undo contexts for the VS command and our additional logic.
					handler.Execute(
						commandName,
						() =>
						{
							ExecuteVsCommentCommand(dte, comment);
							if (!string.IsNullOrEmpty(beginDelimiter) && useSingleLineVsIndentation)
							{
								UpdateSelectedText(handler, commandName, lines => lines.AddCommentSpace(beginDelimiter));
							}
						});
				}
				else if (useSingleLineStyle)
				{
					UpdateSelectedText(handler, commandName, lines => lines.Comment(comment, beginDelimiter));
				}
				else
				{
					UpdateSelectedText(handler, commandName, lines => lines.Comment(comment, beginDelimiter, endDelimiter));
				}
			}
		}

		public static bool CanAddToDoComment(DTE dte)
		{
			TextDocumentHandler handler = new(dte);
			bool result = handler.TextDocument != null;
			return result;
		}

		public static void AddToDoComment(DTE dte)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(dte);
			if (handler.TextDocument != null)
			{
				TextPoint bottomPoint = handler.Selection.BottomPoint;
				bool isAtEndOfLine = bottomPoint.AtEndOfLine || IsAtVisibleEndOfLine(bottomPoint);
				Language language = handler.Language;
				GetCommentStyle(language, false, !isAtEndOfLine, out string beginDelimiter, out string endDelimiter);

				StringBuilder sb = new();
				sb.Append(beginDelimiter);
				if (sb.Length > 0)
				{
					sb.Append(' ');
				}

				Tasks.Options options = MainPackage.TaskOptions;
				sb.Append(options.AddTodoPrefix);

				int noteStartIndex = sb.Length;
				string memberName = GetMemberName(handler, language);
				sb.Append("Finish ").Append(memberName ?? "implementation").Append('.');
				int noteLength = sb.Length - noteStartIndex;

				if (options.AddTodoSuffix != TodoSuffix.None)
				{
					sb.Append(" [");
					switch (options.AddTodoSuffix)
					{
						case TodoSuffix.User:
							sb.Append(Environment.UserName);
							break;

						case TodoSuffix.UserDate:
							sb.Append(Environment.UserName).Append(", ");
							goto case TodoSuffix.Date;

						case TodoSuffix.Date:
							sb.Append(DateTime.UtcNow.ToLocalTime().ToShortDateString());
							break;
					}

					sb.Append(']');
				}

				if (!string.IsNullOrEmpty(endDelimiter))
				{
					sb.Append(' ').Append(endDelimiter);
				}

				string comment = sb.ToString();
				handler.SetSelectedText(comment, "Add TODO Comment");

				// Select the note portion in case the user wants to edit it immediately.
				handler.Selection.MoveToAbsoluteOffset(handler.Selection.ActivePoint.AbsoluteCharOffset - comment.Length + noteStartIndex, false);
				handler.Selection.MoveToAbsoluteOffset(handler.Selection.ActivePoint.AbsoluteCharOffset + noteLength, true);
			}
		}

		#endregion

		#region Private Methods

		private static void UpdateSelectedText(TextDocumentHandler handler, string commandName, Action<TextLines> updateSelection)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string text = handler.SelectedText;
			TextLines lines = new(text);
			updateSelection(lines);
			handler.SetSelectedTextIfUnchanged(lines.ToString(), commandName);
		}

		private static bool CanExecuteVsCommand(DTE dte, string commandName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			EnvDTE.Command command = dte.Commands.Item(commandName, 0);
			bool result = command.IsAvailable;
			return result;
		}

		private static bool ExecuteVsCommand(DTE dte, string commandName, string arguments = "")
		{
			bool result = false;

			ThreadHelper.ThrowIfNotOnUIThread();
			EnvDTE.Command command = dte.Commands.Item(commandName, 0);
			if (command.IsAvailable)
			{
				dte.ExecuteCommand(commandName, arguments);
				result = true;
			}

			return result;
		}

		private static void ExecuteVsCommentCommand(DTE dte, bool comment)
		{
			// I'll use VS's commands when necessary.  For single-line commenting, they provide a fixed
			// indentation style that makes use of the shortest indent level in the selection as well as
			// the current language's indent size and indent style.  If I had to duplicate all that it would
			// be a pain.  I'd have to start by using the following:
			// 		var properties = this.dte.Properties("TextEditor", this.dte.ActiveDocument.Language)
			// 		properties.Item("TabSize").Value
			// 		properties.Item("IndentSize").Value
			// 		properties.Item("InsertTabs").Value
			// 		properties.Item("IndentStyle").Value
			// http://stackoverflow.com/questions/446268/visual-studio-varying-tab-width-options-by-vcproj-or-sln-file
			ThreadHelper.ThrowIfNotOnUIThread();
			ExecuteVsCommand(dte, comment ? "Edit.CommentSelection" : "Edit.UncommentSelection");
		}

		private static bool GetCommentStyle(
			Language language,
			bool useVsIndentation,
			bool preferEndDelimited,
			out string beginDelimiter,
			out string endDelimiter)
		{
			beginDelimiter = null;
			endDelimiter = null;

			if (ScanInfo.TryGet(language, out ScanInfo scanInfo))
			{
				if (preferEndDelimited)
				{
					(beginDelimiter, endDelimiter) = scanInfo.TryGetMultiLineCommentDelimiters();
				}

				// If multiLine was preferred but missing (e.g., VB), then we have to return the singleLine.
				if (string.IsNullOrEmpty(beginDelimiter))
				{
					beginDelimiter = scanInfo.TryGetSingleLineCommentDelimiter();
				}

				// If singleLine was preferred but missing (e.g., HTML, XML), then we have to return the multiLine.
				if (!preferEndDelimited && string.IsNullOrEmpty(beginDelimiter))
				{
					(beginDelimiter, endDelimiter) = scanInfo.TryGetMultiLineCommentDelimiters();
				}
			}

			switch (language)
			{
				case Language.VB:
					// VB's auto-formatter throws out leading indentation and indents all comments to
					// one level under their parent.  So I won't use my default indentation style for it.
					useVsIndentation = true;
					break;

				case Language.VBScript:
					// In VS 2012, the VBScript language service has selection bugs when commenting
					// and uncommenting, so I'll always use my indentation style for it.
					useVsIndentation = false;
					break;
			}

			return useVsIndentation;
		}

		private static string GetMemberName(TextDocumentHandler handler, Language language)
		{
			string result = null;

			ThreadHelper.ThrowIfNotOnUIThread();
			if (handler.Document != null && handler.Selection != null)
			{
				// If this doesn't work, then try "HOWTO: Get the code element at the cursor from a Visual Studio
				// .NET macro or add-in" (http://www.mztools.com/articles/2006/mz2006009.aspx).
				VirtualPoint activePoint = handler.Selection.ActivePoint;
				foreach (vsCMElement scope in ElementScopes)
				{
					try
					{
						CodeElement element = activePoint.CodeElement[scope];
						if (element != null && (scope != vsCMElement.vsCMElementNamespace || NamespaceLanguages.Contains(language)))
						{
							result = element.Name;
							break;
						}
					}
#pragma warning disable CC0004 // Catch block cannot be empty. Comment explains.
					catch (COMException)
					{
						// There's no element of the specified type around the specified point.
					}
#pragma warning restore CC0004 // Catch block cannot be empty
				}
			}

			return result;
		}

		private static bool IsAtVisibleEndOfLine(TextPoint point)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			EditPoint startEdit = point.CreateEditPoint();
			EditPoint endEdit = startEdit.CreateEditPoint();
			endEdit.EndOfLine();
			string text = startEdit.GetText(endEdit);
			bool result = string.IsNullOrEmpty((text ?? string.Empty).Trim());
			return result;
		}

		#endregion
	}
}
