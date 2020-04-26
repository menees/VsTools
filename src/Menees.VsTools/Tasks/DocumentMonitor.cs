namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel.Composition;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Text;
	using EnvDTE;
	using EnvDTE80;
	using Microsoft;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.ComponentModelHost;
	using Microsoft.VisualStudio.Editor;
	using Microsoft.VisualStudio.OLE.Interop;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.TextManager.Interop;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	internal sealed class DocumentMonitor : IDisposable, IVsRunningDocTableEvents, IVsRunningDocTableEvents2
	{
		#region Private Data Members

		private const string TempTxtFile = "Temp.txt";

		private readonly CommentTaskProvider provider;
		private readonly RunningDocumentTable docTable;
		private readonly IVsRunningDocumentTable4 docTable4;
		private readonly IComponentModel componentModel;
		private readonly IVsEditorAdaptersFactoryService adapterFactory;
		private readonly HashSet<ITextDocument> attachedDocuments = new HashSet<ITextDocument>();
		private readonly Dictionary<string, DocumentItem> changedDocuments = new Dictionary<string, DocumentItem>(StringComparer.OrdinalIgnoreCase);

		private uint docTableAdviseCookie;
		private ITextDocumentFactoryService documentFactory;

		#endregion

		#region Constructors

		public DocumentMonitor(CommentTaskProvider provider)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.provider = provider;

			this.docTable = new RunningDocumentTable(this.provider.ServiceProvider);
			this.docTable4 = (IVsRunningDocumentTable4)this.provider.ServiceProvider.GetService(typeof(SVsRunningDocumentTable));
			Assumes.Present(this.docTable4);

			this.docTableAdviseCookie = this.docTable.Advise(this);

			// Creating these components without going through MEF is documented in MSDN under
			// "Using Visual Studio Editor Services in a Non-MEF Component" in the article "Adapting
			// Legacy Code to the New Editor" https://msdn.microsoft.com/en-us/library/dd885359.aspx.
			this.componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
			this.adapterFactory = this.componentModel.GetService<IVsEditorAdaptersFactoryService>();
			this.documentFactory = this.componentModel.GetService<ITextDocumentFactoryService>();

			this.documentFactory.TextDocumentCreated += this.DocumentFactory_TextDocumentCreated;
			this.documentFactory.TextDocumentDisposed += this.DocumentFactory_TextDocumentDisposed;
		}

		#endregion

		#region Public Methods

		public void Dispose()
		{
			if (this.docTable != null && this.docTableAdviseCookie != 0)
			{
				this.docTable.Unadvise(this.docTableAdviseCookie);
				this.docTableAdviseCookie = 0;
			}

			if (this.documentFactory != null)
			{
				this.documentFactory.TextDocumentCreated -= this.DocumentFactory_TextDocumentCreated;
				this.documentFactory.TextDocumentDisposed -= this.DocumentFactory_TextDocumentDisposed;
				this.documentFactory = null;
			}

			lock (this.attachedDocuments)
			{
				foreach (ITextDocument document in this.attachedDocuments)
				{
					this.DetachEventHandlers(document);
				}
			}
		}

		public IReadOnlyDictionary<string, DocumentItem> GetChangedDocuments()
		{
			Dictionary<string, DocumentItem> result = CommentTaskProvider.CloneAndClear(this.changedDocuments);
			return result;
		}

		#endregion

		#region IVsRunningDocTableEvents

		public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;

		public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame frame)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.HandleDocumentVisibilityChange(docCookie, frame, false);
			return VSConstants.S_OK;
		}

		public int OnAfterFirstDocumentLock(uint docCookie, uint rdtLockType, uint readLocksRemaining, uint editLocksRemaining)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if ((readLocksRemaining + editLocksRemaining) == 1)
			{
				this.HandleDocumentLockChange(docCookie, true);
			}

			return VSConstants.S_OK;
		}

		public int OnAfterSave(uint docCookie) => VSConstants.S_OK;

		public int OnBeforeDocumentWindowShow(uint docCookie, int firstShow, IVsWindowFrame frame)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (firstShow != 0)
			{
				this.HandleDocumentVisibilityChange(docCookie, frame, true);
			}

			return VSConstants.S_OK;
		}

		public int OnBeforeLastDocumentUnlock(uint docCookie, uint rdtLockType, uint readLocksRemaining, uint editLocksRemaining)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if ((readLocksRemaining + editLocksRemaining) == 0)
			{
				this.HandleDocumentLockChange(docCookie, false);
			}

			return VSConstants.S_OK;
		}

		#endregion

		#region IVsRunningDocTableEvents2

		public int OnAfterAttributeChangeEx(
			uint docCookie,
			uint grfAttribs,
			IVsHierarchy hierOld,
			uint itemidOld,
			string pszMkDocumentOld,
			IVsHierarchy hierNew,
			uint itemidNew,
			string pszMkDocumentNew)
		{
			// Note: grfAttribs is a mask of values from at least __VSRDTATTRIB, __VSRDTATTRIB2, and __VSRDTATTRIB3.
			if (!string.IsNullOrEmpty(pszMkDocumentOld)
				&& !string.IsNullOrEmpty(pszMkDocumentNew)
				&& !string.Equals(pszMkDocumentOld, pszMkDocumentNew, StringComparison.OrdinalIgnoreCase))
			{
				// We just need to remove the old document here.  The Document_FileActionOccurred
				// event handler will call AddChangedDocument for the new document.
				this.AddChangedDocument(pszMkDocumentOld, null);
			}

			return VSConstants.S_OK;
		}

		#endregion

		#region Private Methods

		private void AddChangedDocument(ITextDocument document)
		{
			if (document != null)
			{
				string filePath = document.FilePath;
				this.AddChangedDocument(filePath, new DocumentItem(document));
			}
		}

		private void AddChangedDocument(string filePath, DocumentItem document)
		{
			// Ignore file paths that are invalid (e.g., RDT_PROJ_MK::{42D00E44-28B8-4CAA-950E-909D5273945D}),
			// relative, or too long.  We only care about documents that have valid full paths.
			if (FileUtility.IsValidPath(filePath, ValidPathOptions.None))
			{
				CommentTaskProvider.Debug(
					"AddChangedDocument: {0}. HasDoc: {1}. HasTextDoc: {2}.",
					filePath,
					document != null,
					document != null && document.HasTextDocument);

				lock (this.changedDocuments)
				{
					this.changedDocuments[filePath] = document;
				}
			}
		}

		private void DetachEventHandlers(ITextDocument document)
		{
			document.FileActionOccurred -= this.Document_FileActionOccurred;
			document.TextBuffer.PostChanged -= this.TextBuffer_PostChanged;
		}

		private void HandleDocumentVisibilityChange(uint docCookie, IVsWindowFrame frame, bool visible)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Don't call this.docTable.GetDocumentInfo.  See notes in HandleDocumentLockChange.
			string moniker = this.docTable4.GetDocumentMoniker(docCookie);
			if (!string.IsNullOrEmpty(moniker))
			{
				DocumentItem document = visible ? new DocumentItem(DocumentItem.GetTextDocument(frame, this.adapterFactory)) : null;
				this.AddChangedDocument(moniker, document);
			}
		}

		private void HandleDocumentLockChange(uint docCookie, bool locked)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// We don't want to call this.docTable.GetDocumentInfo because it forces the document to fully-initialize.
			// It's more efficient to use IVsRunningDocumentTable4 to just get the flags first.
			// http://blogs.msdn.com/b/visualstudio/archive/2013/10/14/asynchronous-solution-load-performance-improvements-in-visual-studio-2013.aspx
			// Note: When debugging this, it's easiest to add a Watch = this.docTable4.GetDocumentMoniker(docCookie)
			uint rawFlags = this.docTable4.GetDocumentFlags(docCookie);
			var flags1 = (_VSRDTFLAGS)rawFlags;
			var flags4 = (_VSRDTFLAGS4)rawFlags;
			if (flags4.HasFlag(_VSRDTFLAGS4.RDT_PendingInitialization)
				&& !flags1.HasFlag(_VSRDTFLAGS.RDT_ProjSlnDocument)
				&& !flags1.HasFlag(_VSRDTFLAGS.RDT_VirtualDocument))
			{
				string moniker = this.docTable4.GetDocumentMoniker(docCookie);
				if (!string.IsNullOrEmpty(moniker))
				{
					DocumentItem document = locked ? new DocumentItem(null) : null;
					this.AddChangedDocument(moniker, document);
				}
			}
		}

		#endregion

		#region Private Event Handlers

		private void Document_FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
		{
			if (e.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)
				|| e.FileActionType.HasFlag(FileActionTypes.ContentLoadedFromDisk))
			{
				// Note: Nothing here provides the previous name of the document if it was renamed.
				// However, the IVsRunningDocTableEvents2.OnAfterAttributeChangeEx event handler
				// should handle the update/removal for the old document name.
				this.AddChangedDocument(e.FilePath, new DocumentItem(sender as ITextDocument));
			}
		}

		private void DocumentFactory_TextDocumentCreated(object sender, TextDocumentEventArgs e)
		{
			ITextDocument document = e.TextDocument;
			if (document.FilePath != TempTxtFile)
			{
				document.FileActionOccurred += this.Document_FileActionOccurred;
				document.TextBuffer.PostChanged += this.TextBuffer_PostChanged;
				this.AddChangedDocument(document);

				lock (this.attachedDocuments)
				{
					this.attachedDocuments.Add(document);
				}
			}
		}

		private void DocumentFactory_TextDocumentDisposed(object sender, TextDocumentEventArgs e)
		{
			ITextDocument document = e.TextDocument;
			if (document.FilePath != TempTxtFile)
			{
				this.AddChangedDocument(document.FilePath, null);

				this.DetachEventHandlers(document);

				lock (this.attachedDocuments)
				{
					this.attachedDocuments.Remove(document);
				}
			}
		}

		private void TextBuffer_PostChanged(object sender, EventArgs e)
		{
			ITextDocument document = DocumentItem.GetTextDocument(sender as ITextBuffer);
			this.AddChangedDocument(document);
		}

		#endregion
	}
}
