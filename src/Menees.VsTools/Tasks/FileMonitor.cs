namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Microsoft;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;

	#endregion

	internal sealed class FileMonitor : IVsFileChangeEvents, IDisposable
	{
		#region Private Data Members

		private readonly IVsFileChangeEx fileChangeService;
		private readonly CommentTaskProvider provider;
		private readonly Dictionary<string, uint> cookies = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, bool> changedFiles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		#endregion

		#region Constructors

		public FileMonitor(CommentTaskProvider provider)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.provider = provider;
			this.fileChangeService = (IVsFileChangeEx)this.provider.ServiceProvider.GetService(typeof(SVsFileChangeEx));
			Assumes.Present(this.fileChangeService);
		}

		#endregion

		#region Public Methods

		public void Add(string fileName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (!this.cookies.TryGetValue(fileName, out _))
			{
				// We have to handle Adds in case someone deletes a file and then recreates it.
				const _VSFILECHANGEFLAGS Flags = _VSFILECHANGEFLAGS.VSFILECHG_Add
					| _VSFILECHANGEFLAGS.VSFILECHG_Del
					| _VSFILECHANGEFLAGS.VSFILECHG_Size
					| _VSFILECHANGEFLAGS.VSFILECHG_Time;
				int hr = this.fileChangeService.AdviseFileChange(fileName, (uint)Flags, this, out uint cookie);

				// AdviseFileChange will fail with an E_INVALIDARG result if fileName isn't a valid file name
				// (e.g., if it's a moniker like RDT_PROJ_MK::{42D00E44-28B8-4CAA-950E-909D5273945D}).
				if (ErrorHandler.Succeeded(hr))
				{
					this.cookies.Add(fileName, cookie);
				}
			}
		}

		public void Remove(string fileName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (this.cookies.TryGetValue(fileName, out uint cookie))
			{
				this.Unadvise(cookie);
				this.cookies.Remove(fileName);
			}
		}

		public IReadOnlyDictionary<string, bool> GetChangedFiles()
		{
			Dictionary<string, bool> result = CommentTaskProvider.CloneAndClear(this.changedFiles);
			return result;
		}

		public void Dispose()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			foreach (var pair in this.cookies)
			{
				this.Unadvise(pair.Value);
			}

			this.cookies.Clear();
		}

		// This should never be called because we don't call AdviseDirChange (just AdviseFileChange).
		int IVsFileChangeEvents.DirectoryChanged(string pszDirectory) => VSConstants.E_NOTIMPL;

		int IVsFileChangeEvents.FilesChanged(uint countChanges, string[] rgpszFile, uint[] rggrfChange)
		{
			lock (this.changedFiles)
			{
				for (int i = 0; i < countChanges; i++)
				{
					string fileName = rgpszFile[i];
					_VSFILECHANGEFLAGS flags = (_VSFILECHANGEFLAGS)rggrfChange[i];
					bool exists = !flags.HasFlag(_VSFILECHANGEFLAGS.VSFILECHG_Del);
					this.changedFiles[fileName] = exists;
				}
			}

			return VSConstants.S_OK;
		}

		#endregion

		#region Private Methods

		private void Unadvise(uint cookie)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			int hr = this.fileChangeService.UnadviseFileChange(cookie);
			Debug.Assert(ErrorHandler.Succeeded(hr) || Environment.HasShutdownStarted, "The unadvise called failed.");
		}

		#endregion
	}
}
