namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;
	using EnvDTE;
	using Menees.VsTools.Projects;
	using Menees.VsTools.Regions;
	using Menees.VsTools.Sort;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using Microsoft.VisualStudio.Threading;
	using VSLangProj;

	#endregion

	internal sealed class CommandProcessor
	{
		#region Private Data Members

		private readonly MainPackage package;
		private readonly DTE dte;
		private CopyInfoHandler copyInfoHandler;

		#endregion

		#region Constructors

		public CommandProcessor(MainPackage package)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.package = package;
			this.dte = (DTE)this.GetService(typeof(SDTE));
		}

		#endregion

		#region Private Properties

		private Language ActiveLanguage
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				Language result = Utilities.GetLanguage(this.dte.ActiveDocument);
				return result;
			}
		}

		#endregion

		#region Public Methods

		public bool CanExecute(Command command)
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				bool result = false;

				if (this.dte != null)
				{
					switch (command)
					{
						// These require a text selection.
						case Command.SortLines:
						case Command.Trim:
						case Command.Statistics:
						case Command.StreamText:
						case Command.ExecuteText:
						case Command.CheckSpelling:
							result = new TextDocumentHandler(this.dte).HasNonEmptySelection;
							break;

						// These require a text selection for specific languages.
						case Command.CommentSelection:
						case Command.UncommentSelection:
							result = CommentHandler.CanCommentSelection(this.dte, command == Command.CommentSelection);
							break;

						// These require a document using a supported language.
						case Command.AddRegion:
						case Command.CollapseAllRegions:
						case Command.ExpandAllRegions:
							result = RegionHandler.IsSupportedLanguage(this.ActiveLanguage);
							break;

						// These require an open document with a backing file on disk.
						case Command.ExecuteFile:
						case Command.ToggleReadOnly:
							string fileName = this.GetDocumentFileName();
							result = File.Exists(fileName);
							break;

						case Command.GenerateGuid:
							result = new TextDocumentHandler(this.dte).CanSetSelectedText;
							break;

						case Command.ToggleFiles:
							result = ToggleFilesHandler.IsSupportedLanguage(this.ActiveLanguage);
							break;

						case Command.ListAllProjectProperties:
						case Command.ViewProjectDependencies:
							result = ProjectHandler.GetSelectedProjects(this.dte, null, command == Command.ViewProjectDependencies);
							break;

						case Command.ViewBaseConverter:
						case Command.ViewTasks:
							result = true;
							break;

						case Command.SortMembers:
							result = new MemberSorter(this.dte, false).CanFindMembers;
							break;

						case Command.AddToDoComment:
							result = CommentHandler.CanAddToDoComment(this.dte);
							break;

						case Command.CopySolutionRelativePath:
						case Command.CopyProjectRelativePath:
						case Command.CopyRepoRelativePath:
						case Command.CopyParentPath:
						case Command.CopyFullPath:
						case Command.CopyNameOnly:
						case Command.CopyUnixSolutionRelativePath:
						case Command.CopyUnixProjectRelativePath:
						case Command.CopyUnixRepoRelativePath:
						case Command.CopyUnixParentPath:
						case Command.CopyUnixFullPath:
						case Command.CopyDocSolutionRelativePath:
						case Command.CopyDocProjectRelativePath:
						case Command.CopyDocRepoRelativePath:
						case Command.CopyDocParentPath:
						case Command.CopyDocFullPath:
						case Command.CopyDocNameOnly:
						case Command.CopyDocUnixSolutionRelativePath:
						case Command.CopyDocUnixProjectRelativePath:
						case Command.CopyDocUnixRepoRelativePath:
						case Command.CopyDocUnixParentPath:
						case Command.CopyDocUnixFullPath:
							result = this.GetCopyInfoHandler().CanExecute(command);
							break;
					}
				}

				return result;
			}
			catch (Exception ex)
			{
				MainPackage.LogException(ex);
				throw;
			}
		}

#pragma warning disable MEN003 // Method is too long. Large switch statement for handling all commands.
		public void Execute(Command command)
#pragma warning restore MEN003 // Method is too long
		{
			try
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				if (this.dte != null)
				{
					switch (command)
					{
						case Command.AddRegion:
							RegionHandler.AddRegion(this.dte, MainPackage.RegionOptions);
							break;

						case Command.CheckSpelling:
							this.CheckSpelling();
							break;

						case Command.CollapseAllRegions:
							RegionHandler.CollapseAllRegions(this.dte, this.ActiveLanguage, this.package);
							break;

						case Command.CommentSelection:
						case Command.UncommentSelection:
							CommentHandler.CommentSelection(this.dte, command == Command.CommentSelection);
							break;

						case Command.ExecuteFile:
							this.ExecuteFile();
							break;

						case Command.ExecuteText:
							this.ExecuteText();
							break;

						case Command.ExpandAllRegions:
							RegionHandler.ExpandAllRegions(this.dte, this.ActiveLanguage, this.package);
							break;

						case Command.GenerateGuid:
							this.GenerateGuid();
							break;

						case Command.ListAllProjectProperties:
							ProjectHandler.ListAllProjectProperties(this.dte);
							break;

						case Command.ViewProjectDependencies:
							ProjectHandler.ViewProjectDependencies(this.package, this.dte);
							break;

						case Command.SortLines:
							this.SortLines();
							break;

						case Command.Statistics:
							this.Statistics();
							break;

						case Command.StreamText:
							this.StreamText();
							break;

						case Command.ToggleFiles:
							ToggleFilesHandler toggleFilesHandler = new(this.dte);
							toggleFilesHandler.ToggleFiles();
							break;

						case Command.ToggleReadOnly:
							this.ToggleReadOnly();
							break;

						case Command.Trim:
							this.Trim();
							break;

						case Command.ViewBaseConverter:
							this.ViewToolWindow(typeof(BaseConverter.Window));
							break;

						case Command.SortMembers:
							this.SortMembers();
							break;

						case Command.AddToDoComment:
							CommentHandler.AddToDoComment(this.dte);
							break;

						case Command.ViewTasks:
							this.ViewToolWindow(typeof(Tasks.TasksWindow));
							break;

						case Command.CopySolutionRelativePath:
						case Command.CopyProjectRelativePath:
						case Command.CopyRepoRelativePath:
						case Command.CopyParentPath:
						case Command.CopyFullPath:
						case Command.CopyNameOnly:
						case Command.CopyUnixSolutionRelativePath:
						case Command.CopyUnixProjectRelativePath:
						case Command.CopyUnixRepoRelativePath:
						case Command.CopyUnixParentPath:
						case Command.CopyUnixFullPath:
						case Command.CopyDocSolutionRelativePath:
						case Command.CopyDocProjectRelativePath:
						case Command.CopyDocRepoRelativePath:
						case Command.CopyDocParentPath:
						case Command.CopyDocFullPath:
						case Command.CopyDocNameOnly:
						case Command.CopyDocUnixSolutionRelativePath:
						case Command.CopyDocUnixProjectRelativePath:
						case Command.CopyDocUnixRepoRelativePath:
						case Command.CopyDocUnixParentPath:
						case Command.CopyDocUnixFullPath:
							this.GetCopyInfoHandler().Execute(command);
							break;
					}
				}
			}
			catch (Exception ex)
			{
				MainPackage.LogException(ex);
				throw;
			}
		}

		#endregion

		#region Private Methods

		private object GetService(Type serviceType)
		{
			object result = this.package.ServiceProvider.GetService(serviceType);
			return result;
		}

		private string GetDocumentFileName()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			Document doc = this.dte.ActiveDocument;
			string result = doc?.FullName;
			return result;
		}

		private void CheckSpelling()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(this.dte);
			if (handler.HasNonEmptySelection)
			{
				try
				{
					// Launch Word.
					Type wordType = Type.GetTypeFromProgID("Word.Application");
					dynamic wordApp = Activator.CreateInstance(wordType);

					// Add a document.
					dynamic wordDoc = wordApp.Documents.Add();

					// Clear current contents.
					dynamic range = wordApp.Selection.Range;
					range.WholeStory();
					range.Delete();
					range = null;

					// Add the text the user selected.
					wordApp.Selection.Text = handler.SelectedText;

					// Show it
					wordApp.Visible = true;
					wordApp.Activate();
					wordDoc.Activate();

					// Check spelling
					wordDoc.CheckSpelling();

					// Get the edited text back
					wordApp.Selection.WholeStory();
					string newText = wordApp.Selection.Text;

					// Word always adds an extra CR, so strip that off.
					// Also it converts all LFs to CRs, so change
					// that back.
					if (!string.IsNullOrEmpty(newText))
					{
						if (newText.EndsWith("\r"))
						{
							newText = newText.Substring(0, newText.Length - 1);
						}

						newText = newText.Replace("\r", "\r\n");
					}

					handler.SetSelectedTextIfUnchanged(newText, "Check Spelling With MS Word");

					// Tell the doc and Word to go away.
					object saveChanges = false;
					wordDoc.Close(ref saveChanges);
					wordApp.Visible = false;
					wordApp.Quit();
				}
				catch (COMException ex)
				{
					// If we get REGDB_E_CLASSNOTREG, then Word probably isn't installed.
					const uint REGDB_E_CLASSNOTREG = 0x_8004_0154;
					if (unchecked((uint)ex.ErrorCode) == REGDB_E_CLASSNOTREG)
					{
						this.package.ShowMessageBox(
							"Microsoft Word is required in order to check spelling, but it isn't available.\r\n\r\nDetails:\r\n" + ex.Message,
							true);
					}
					else
					{
						throw;
					}
				}
			}
		}

		private void ExecuteFile()
		{
			// Perform the SaveAll first (if necessary) in case the "executing" file has never
			// been saved before, so this will assign it a file name.
			ThreadHelper.ThrowIfNotOnUIThread();
			bool performExecute = true;
			Documents allDocs = this.dte.Documents;
			if (allDocs != null && MainPackage.GeneralOptions.SaveAllBeforeExecuteFile)
			{
				try
				{
					allDocs.SaveAll();
				}
				catch (ExternalException ex)
				{
					performExecute = false;

					// Rethrow the exception unless the user hit Cancel when prompted to save changes for a document.
					// Cancelling throws an HRESULT of 0x80004004, which is the C constant E_ABORT with the description
					// "Operation aborted".
					const uint E_ABORT = 0x_8000_4004;
					if (unchecked((uint)ex.ErrorCode) != E_ABORT)
					{
						throw;
					}
				}
			}

			if (performExecute)
			{
				string fileName = this.GetDocumentFileName();
				if (!string.IsNullOrEmpty(fileName))
				{
					Utilities.ShellExecute(fileName);
				}
			}
		}

		private void ExecuteText()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(this.dte);
			if (handler.HasNonEmptySelection)
			{
				Utilities.ShellExecute(handler.SelectedText);
			}
		}

		private void GenerateGuid()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(this.dte);
			if (handler.CanSetSelectedText)
			{
				Guid guid = Guid.NewGuid();
				Options options = MainPackage.GeneralOptions;

				string format;
				switch (options.GuidFormat)
				{
					case GuidFormat.Numbers:
						format = "N";
						break;

					case GuidFormat.Braces:
						format = "B";
						break;

					case GuidFormat.Parentheses:
						format = "P";
						break;

					case GuidFormat.Structure:
						format = "X";
						break;

					default: // GuidFormat.Dashes
						format = "D";
						break;
				}

				string guidText = guid.ToString(format);
				if (options.UppercaseGuids)
				{
					guidText = guidText.ToUpper();
				}

				// Set the selection to the new GUID
				handler.SetSelectedText(guidText, "Generate GUID");
			}
		}

		private void SortLines()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(this.dte);
			if (handler.HasNonEmptySelection)
			{
				Sort.Options options = MainPackage.SortOptions;

				bool execute = true;
				if (!options.OnlyShowSortLinesDialogWhenShiftIsPressed || Utilities.IsShiftPressed)
				{
					SortLinesDialog dialog = new();
					execute = dialog.Execute(options);
				}

				if (execute)
				{
					if (options.LineOptions.HasFlag(LineOptions.WholeLines))
					{
						handler.SelectWholeLines();
					}

					// Now sort the lines and put them back as the selection
					string text = handler.SelectedText;
					TextLines lines = new(text);
					lines.Sort(options.LineOptions);
					string sortedText = lines.ToString();
					handler.SetSelectedTextIfUnchanged(sortedText, "Sort Lines");
				}
			}
		}

		private void SortMembers()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			MemberSorter sorter = new(this.dte, true);
			if (sorter.HasSelectedMembers)
			{
				sorter.SortMembers(MainPackage.SortOptions);
			}
		}

		private void Statistics()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(this.dte);
			if (handler.HasNonEmptySelection)
			{
				string text = handler.SelectedText;
				StatisticsDialog dialog = new();
				dialog.Execute(text);
			}
		}

		private void StreamText()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(this.dte);
			if (handler.HasNonEmptySelection)
			{
				string text = handler.SelectedText;
				TextLines lines = new(text);
				string streamedText = lines.Stream(handler.Language);
				handler.SetSelectedTextIfUnchanged(streamedText, "Stream Text");
			}
		}

		private void ToggleReadOnly()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			string fileName = this.GetDocumentFileName();
			if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
			{
				FileAttributes attr = File.GetAttributes(fileName);

				if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				{
					attr &= ~FileAttributes.ReadOnly;
				}
				else
				{
					attr |= FileAttributes.ReadOnly;
				}

				File.SetAttributes(fileName, attr);
			}
		}

		private void Trim()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			TextDocumentHandler handler = new(this.dte);
			if (handler.HasNonEmptySelection)
			{
				Options options = MainPackage.GeneralOptions;

				bool execute = true;
				if (!options.OnlyShowTrimDialogWhenShiftIsPressed || Utilities.IsShiftPressed)
				{
					TrimDialog dialog = new();
					execute = dialog.Execute(options);
				}

				if (execute && (options.TrimStart || options.TrimEnd))
				{
					string text = handler.SelectedText;
					TextLines lines = new(text);
					lines.Trim(options.TrimStart, options.TrimEnd);
					string trimmedText = lines.ToString();
					handler.SetSelectedTextIfUnchanged(trimmedText, nameof(this.Trim));
				}
			}
		}

		private void ViewToolWindow(Type toolWindowPaneType)
		{
			// From https://github.com/Microsoft/VSSDK-Analyzers/blob/master/doc/VSSDK003.md
			JoinableTask task = this.package.JoinableTaskFactory.RunAsync(async () =>
			{
				// Get instance number 0 of this tool window. It's single instance so that's the only one.
				// The last flag is set to true so that if the tool window does not exist it will be created.
				ToolWindowPane window = await this.package.ShowToolWindowAsync(toolWindowPaneType, 0, true, this.package.DisposalToken).ConfigureAwait(true);
				if ((window == null) || (window.Frame == null))
				{
					throw new NotSupportedException("Cannot create tool window.");
				}

				await this.package.JoinableTaskFactory.SwitchToMainThreadAsync();
				IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
				ErrorHandler.ThrowOnFailure(windowFrame.Show());
			});

			Utilities.Unused(task); // We just want the task to run in the background.
		}

		private CopyInfoHandler GetCopyInfoHandler()
			=> this.copyInfoHandler ??= new CopyInfoHandler(this.dte, this.package);

		#endregion
	}
}
