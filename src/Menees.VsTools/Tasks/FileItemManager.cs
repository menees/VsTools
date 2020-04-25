namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Text;

	#endregion

	internal sealed class FileItemManager
	{
		#region Private Data Members

		private readonly CommentTaskProvider provider;
		private readonly FileMonitor monitor;
		private readonly Dictionary<string, FileItem> files = new Dictionary<string, FileItem>(StringComparer.OrdinalIgnoreCase);
		private readonly HashSet<FileItem> changedItems = new HashSet<FileItem>();
		private readonly Dictionary<CommentTask, bool> changedTasks = new Dictionary<CommentTask, bool>();
		private readonly BackgroundOptions options;
		private readonly List<Regex> backgroundExcludePatterns = new List<Regex>();

		#endregion

		#region Constructors

		public FileItemManager(CommentTaskProvider provider, FileMonitor monitor, BackgroundOptions options)
		{
			this.provider = provider;
			this.monitor = monitor;
			this.options = options;
			this.options.Updated += this.Options_Updated;
			this.RefreshExcludePatterns();
		}

		#endregion

		#region Public Methods

		public void UpdateHierarchy(IEnumerable<HierarchyItem> allHierarchyItems)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			foreach (var pair in this.files)
			{
				pair.Value.ClearHierarchy();
			}

			HashSet<string> allHierarchyFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (HierarchyItem item in allHierarchyItems)
			{
				// Virtual projects (e.g., TypeScript Virtual Projects) and virtual items may have no file name.
				string fileName = item.FileName;
				if (!string.IsNullOrEmpty(fileName))
				{
					allHierarchyFiles.Add(fileName);

					if (!this.files.TryGetValue(fileName, out FileItem file))
					{
						file = this.TryAdd(fileName);
					}

					if (file != null)
					{
						file.AddHierarchyItem(item);
					}
				}
			}

			// Note: It's possible for a FileItem for a "Miscellaneous File" to have no direct hierarchy reference
			// and to not have a document yet (e.g., if VS is showing a tab for it but hasn't loaded the doc info).
			// So we'll only remove files with no hierarchy reference, no document, and not in the latest item list.
			List<FileItem> removeItems = this.files
				.Where(pair => !pair.Value.HasHierarchyReference && pair.Value.Document == null && !allHierarchyFiles.Contains(pair.Key))
				.Select(pair => pair.Value).ToList();
			this.Remove(removeItems);

			// It's possible for the new hierarchy to have added, changed, or removed projects, which means existing
			// CommentTasks for FileItems might need to update their Project property values.  We only need to do this
			// check for FileItems that aren't already in this.changedItems, that currently have tasks, and where the
			// Project property value needs to change.
			lock (this.changedItems)
			{
				var itemsWithInvalidTasks = this.files.Where(pair => !this.changedItems.Contains(pair.Value) && !pair.Value.AreTaskProjectsCurrent)
					.Select(pair => pair.Value).ToList();
				if (itemsWithInvalidTasks.Any())
				{
					this.changedItems.UnionWith(itemsWithInvalidTasks);
				}
			}
		}

		public void UpdateDocuments(IReadOnlyDictionary<string, DocumentItem> changedDocuments)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			List<FileItem> removeItems = new List<FileItem>();
			foreach (var pair in changedDocuments)
			{
				string fileName = pair.Key;
				DocumentItem document = pair.Value;

				if (this.files.TryGetValue(fileName, out FileItem file))
				{
					file.Document = document;
					if (document != null)
					{
						lock (this.changedItems)
						{
							this.changedItems.Add(file);
						}
					}
					else if (!file.HasHierarchyReference)
					{
						removeItems.Add(file);
					}
				}
				else if (document != null)
				{
					file = this.TryAdd(fileName);
					if (file != null)
					{
						file.Document = document;
					}
				}
			}

			this.Remove(removeItems);
		}

		public void UpdateFiles(IReadOnlyDictionary<string, bool> changedFiles)
		{
			foreach (var pair in changedFiles)
			{
				// Note: pair.Value represents whether the file still exists, but we'll treat a file deletion as a change
				// in order to force the file's Tasks to be reset (i.e., cleared).  Just because the file doesn't exist
				// on disk any more doesn't change the fact that the solution or an open document still references
				// it.  The file might reappear on the next refresh interval if the user is swapping it out.
				string fileName = pair.Key;

				if (this.files.TryGetValue(fileName, out FileItem file))
				{
					lock (this.changedItems)
					{
						this.changedItems.Add(file);
					}
				}
			}
		}

		public void UpdateTasks(bool updateAll)
		{
			IEnumerable<FileItem> items;
			lock (this.changedItems)
			{
				items = this.changedItems.ToList();
			}

			if (updateAll)
			{
				// We have to include any changedItems to make sure we remove existing tasks
				// from files that no longer exist in this.files.
				items = items.Concat(this.files.Select(pair => pair.Value)).Distinct();
			}

			if (items.Any())
			{
				List<Regex> localExcludePatterns;
				lock (this.backgroundExcludePatterns)
				{
					localExcludePatterns = new List<Regex>(this.backgroundExcludePatterns);
				}

				// Try to use a fourth of the processors, but stay in the 1 to 8 range.
				const int MinParallelism = 1;
				const int MaxParallelism = 8;
				const int ProcessorScaleFactor = 4;
				int maxParallelism = this.options.MaxDegreeOfParallelism
					?? Math.Max(MinParallelism, Math.Min(Environment.ProcessorCount / ProcessorScaleFactor, MaxParallelism));
				try
				{
					RefreshAction generalAction = updateAll ? RefreshAction.Always : RefreshAction.IfNeeded;
					Parallel.ForEach(
						items.ToArray(), // Copy before iterating through it.
						new ParallelOptions { MaxDegreeOfParallelism = maxParallelism },
						item =>
								{
									RefreshAction itemAction;
									if (localExcludePatterns.Any(pattern => pattern.IsMatch(item.FileName)))
									{
										itemAction = RefreshAction.Remove;
									}
									else
									{
										itemAction = generalAction;
									}

									this.RefreshItem(item, itemAction);
							});
				}
				catch (Exception ex)
				{
					MainPackage.LogException(ex);
					if (!(ex is AggregateException))
					{
						throw;
					}
				}
			}
		}

		public IReadOnlyDictionary<CommentTask, bool> GetChangedTasks()
		{
			Dictionary<CommentTask, bool> result = CommentTaskProvider.CloneAndClear(this.changedTasks);
			return result;
		}

		#endregion

		#region Private Methods

		private FileItem TryAdd(string fileName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			FileItem result = null;

			// Reject items that don't have valid file names (e.g., monikers like
			// RDT_PROJ_MK::{42D00E44-28B8-4CAA-950E-909D5273945D}).
			// Also, reject relative paths, long paths, and folder paths (e.g., C:\Dir\).
			if (FileUtility.IsValidPath(fileName, ValidPathOptions.None))
			{
				result = new FileItem(this.provider, fileName);
				this.files.Add(fileName, result);

				lock (this.changedItems)
				{
					this.changedItems.Add(result);
				}

				// Note: The result.IsScannable state can change later if an ITextDocument is assigned,
				// but that would be rare for a file that's initially unscannable.  If that happens, then we
				// still don't need to monitor the file though because the only way we'd be able to get
				// the text lines is through the ITextDocument, and it provides its own notifications.
				if (result.IsScannable)
				{
					this.monitor.Add(fileName);
				}
			}

			return result;
		}

		private void Remove(List<FileItem> items)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			foreach (FileItem item in items)
			{
				this.RefreshItem(item, RefreshAction.Remove);

				string fileName = item.FileName;
				this.files.Remove(fileName);
				if (item.IsScannable)
				{
					this.monitor.Remove(fileName);
				}
			}
		}

		private void RefreshItem(FileItem item, RefreshAction action)
		{
			if (item.Refresh(action, out IDictionary<CommentTask, bool> itemChanges))
			{
				// We need to lock here since multiple threads can call this in parallel (e.g., from Parallel.ForEach).
				lock (this.changedItems)
				{
					this.changedItems.Remove(item);
				}

				if (itemChanges != null && itemChanges.Count > 0)
				{
					lock (this.changedTasks)
					{
						foreach (var pair in itemChanges)
						{
							this.changedTasks[pair.Key] = pair.Value;
						}
					}
				}
			}
		}

		private void Options_Updated(object sender, EventArgs e)
		{
			this.RefreshExcludePatterns();
		}

		private void RefreshExcludePatterns()
		{
			lock (this.backgroundExcludePatterns)
			{
				this.backgroundExcludePatterns.Clear();
				this.backgroundExcludePatterns.AddRange(this.options.ExcludeFilesExpressions);
			}
		}

		#endregion
	}
}
