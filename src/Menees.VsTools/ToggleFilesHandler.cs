namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;
	using EnvDTE;
	using Microsoft.VisualStudio.Shell;

	#endregion

	internal sealed class ToggleFilesHandler
	{
		#region Private Data Members

		private static readonly HashSet<Language> SupportsToggleFiles = new(
			new[] { Language.CSharp, Language.VB, Language.CPlusPlus, Language.HTML, Language.XML, Language.XAML });

		// Use arrays (not HashSets) so we can search for extensions
		// in a preferred order when opening the target file.
		private static readonly string[] CppHeaderExtensions = { ".h", ".tlh", ".hpp", ".hxx", ".hh" };
		private static readonly string[] CppImplementationExtensions = { ".cpp", ".c", ".inl", ".tli", ".cxx", ".cc" };

		private readonly DTE dte;
		private readonly Document document;

		#endregion

		#region Constructors

		public ToggleFilesHandler(DTE dte)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.dte = dte;
			this.document = dte.ActiveDocument;
		}

		#endregion

		#region Public Methods

		public static bool IsSupportedLanguage(Language language)
		{
			bool result = SupportsToggleFiles.Contains(language);
			return result;
		}

		public void ToggleFiles()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (this.document != null)
			{
				Language language = Utilities.GetLanguage(this.document);
				switch (language)
				{
					case Language.CPlusPlus:
						this.ToggleCppDoc();
						break;

					case Language.HTML:
						this.ToggleHtmlDoc();
						break;

					default: // CSharp, Basic, XML, etc.
						this.ToggleCodeDesigner();
						break;
				}
			}
		}

		#endregion

		#region Private Methods

		private static bool AreDocumentWindowsEqual(Window x, Window y)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			bool result = x.Equals(y);

			// For some reason, in VS 11, XAML files have multiple windows in the document.Windows collection all with the same caption.
			// (Based on where focus goes after re-activation, they seem to be the form designer, XAML editor, and something else.  They
			// may be three editor windows on a single VS tab, but I don't know how to distinguish between them.)  Since we're trying to
			// toggle tabs, we need to treat the same-named windows as equal to correctly toggle from "the XAML designer tab" to code.
			if (!result && x.Caption == y.Caption && string.Equals(Path.GetExtension(x.Caption), ".xaml", StringComparison.OrdinalIgnoreCase))
			{
				result = true;
			}

			return result;
		}

		private void ToggleCppDoc()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			string fullName = this.document.FullName;
			string extension = Path.GetExtension(fullName);
			if (extension.Length > 0)
			{
				string[] extensions = null;
				if (CppHeaderExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
				{
					extensions = CppImplementationExtensions;
				}
				else if (CppImplementationExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
				{
					extensions = CppHeaderExtensions;
				}

				if (extensions != null)
				{
					string name = Path.GetFileNameWithoutExtension(fullName);

					// First try to activate an already-open document with a preferred extension.
					if (!this.OpenDocumentWithExtension(name, extensions))
					{
						// Now try looking on disk for a file with a preferred extension.
						string path = Path.GetDirectoryName(fullName);
						string[] otherPaths = OptionsBase.SplitValues(MainPackage.GeneralOptions.CppSearchDirectories);
						this.OpenFileWithExtension(name, extensions, path, otherPaths);
					}
				}
			}
		}

		private void ToggleHtmlDoc()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Execute the NextView command.  It may fail for non-designable HTML docs (e.g. XSLT).
			if (!this.ExecuteVsCommand("View.NextView"))
			{
				// If NextView failed, then try to manually change the tab.
				Window window = this.document.ActiveWindow;
				if (window != null)
				{
					if (window.Object is HTMLWindow html)
					{
						vsHTMLTabs tabCurrent = html.CurrentTab;
						if (tabCurrent == vsHTMLTabs.vsHTMLTabsSource)
						{
							html.CurrentTab = vsHTMLTabs.vsHTMLTabsDesign;
						}
						else
						{
							html.CurrentTab = vsHTMLTabs.vsHTMLTabsSource;
						}
					}
				}
			}
		}

		private void ToggleCodeDesigner()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// If a non-document window is selected (e.g., Solution Explorer,
			// Properties, etc), then try to force a document window to be
			// activated.
			this.ExecuteVsCommand("Window.ActivateDocumentWindow");

			// If the active window is a document, then activate
			// the first document window that is NOT equal to the
			// active window.
			Window activeWindow = this.dte.ActiveWindow;
			if (activeWindow != null && activeWindow.Type == vsWindowType.vsWindowTypeDocument)
			{
				bool found = false;
				foreach (Window window in this.document.Windows)
				{
					if (!AreDocumentWindowsEqual(activeWindow, window))
					{
						window.Activate();
						found = true;
						break;
					}
				}

				if (!found)
				{
					// The active window is a text document, but the active document's window was equal to the active
					// window.  So we'll try the View.ViewCode command first.  If it doesn't change the active window,
					// then we'll try View.ViewDesigner.
					//
					// These commands aren't available when a solution isn't open, and they may not be available at other
					// times.  So this may do nothing, but it's our best shot.
					this.ExecuteVsCommand("View.ViewCode");
					if (AreDocumentWindowsEqual(this.dte.ActiveWindow, activeWindow))
					{
						// The document may not have anything to design.
						this.ExecuteVsCommand("View.ViewDesigner");
					}
				}
			}
		}

		private bool ExecuteVsCommand(string commandName, string arguments = null)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			bool result = false;

			EnvDTE.Command command = this.dte.Commands.Item(commandName, 0);
			if (command.IsAvailable)
			{
				this.dte.ExecuteCommand(commandName, arguments ?? string.Empty);
				result = true;
			}

			return result;
		}

		private bool OpenDocumentWithExtension(string name, string[] extensions)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			bool result = false;

			// See if file is currently open and activate it.  This is faster than hitting the disk, and if
			// the user has manually opened a file with a "later" extension, it will be found before we
			// try to open earlier extensions from the disk.
			foreach (string extension in extensions)
			{
				Document openDoc = this.FindOpenDocument(name, extension);
				if (openDoc != null)
				{
					// The IDE sometimes thinks it has a document open even if it has no windows.  I've seen cases where I manually
					// close a file, but it is still in the Documents collection.  I think this is an IDE bug because it rarely happens.
					// I'll work around it by checking to see if the document still has any windows.  If not, then we'll try to create
					// a new window before activating it.  If we try to Activate a document with no windows the IDE throws an exception.
					// If this fails, I'll just eat the exception and hope we can open the file from disk.
					try
					{
						if (openDoc.Windows.Count == 0)
						{
							openDoc.NewWindow();
						}

						openDoc.Activate();

						// If we got here then everything succeeded.  We activated the correct document, so we're done.
						result = true;
						break;
					}
#pragma warning disable CC0004 // Catch block cannot be empty. Comment explains it.
					catch (COMException)
					{
						// Ignore the error and continue.
					}
#pragma warning restore CC0004 // Catch block cannot be empty
				}
			}

			return result;
		}

		private Document FindOpenDocument(string name, string extension)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			Document result = null;
			string nameWithExtension = name + extension;

			foreach (Document doc in this.dte.Documents)
			{
				if (string.Equals(Path.GetFileName(doc.FullName), nameWithExtension, StringComparison.OrdinalIgnoreCase))
				{
					result = doc;
					break;
				}
			}

			return result;
		}

		private void OpenFileWithExtension(string name, string[] extensions, string basePath, string[] otherPaths)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Try to find a file on disk in the base path and open it.
			if (!this.FindFileWithExtension(basePath, name, extensions))
			{
				// See if there are any additional directories to search.
				if (otherPaths != null)
				{
					// Search each path for the file
					foreach (string originalPath in otherPaths)
					{
						string path = originalPath.Trim();

						// Strip double quotes off
						if (path.StartsWith("\""))
						{
							path = path.Length > 0 ? path.Substring(1) : string.Empty;
						}

						if (path.EndsWith("\""))
						{
							path = path.Length > 0 ? path.Substring(0, path.Length - 1) : string.Empty;
						}

						if (this.FindFileWithExtension(path, name, extensions))
						{
							break;
						}
					}
				}
			}
		}

		private bool FindFileWithExtension(string path, string name, string[] extensions)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			bool result = false;

			foreach (string extension in extensions)
			{
				string fullName = Path.Combine(path, name + extension);
				if (File.Exists(fullName))
				{
					string quotedFileName = string.Format("\"{0}\"", fullName);
					result = this.ExecuteVsCommand("File.OpenFile", quotedFileName);
					break;
				}
			}

			return result;
		}

		#endregion
	}
}
