namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Security;
	using System.Text;
	using System.Text.RegularExpressions;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	[DebuggerDisplay("{fileInfo.Name}")]
	internal sealed class FileItem
	{
		#region Private Data Members

		private static readonly DateTime MinDateTimeUtc = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		private static readonly HashSet<HierarchyItem> EmptySet = new();

		private readonly CommentTaskProvider provider;
		private readonly FileInfo fileInfo;

		private IEnumerable<CommentTask> tasks;
		private DateTime lastModifiedTimeScannedUtc;
		private HashSet<HierarchyItem> hierarchyItems;
		private DocumentItem document;
		private ScanInfo scanInfo;
		private string projectNames;
		private string exactCaseFileName;

		#endregion

		#region Constructors

		internal FileItem(CommentTaskProvider provider, string fileName)
		{
			// Note: The caller should check FileUtility.IsValidPath(fileName) before invoking this constructor.
			this.provider = provider;
			this.FileName = fileName;
			this.hierarchyItems = EmptySet;
			this.lastModifiedTimeScannedUtc = MinDateTimeUtc;

			try
			{
				this.fileInfo = new FileInfo(fileName);
				this.scanInfo = ScanInfo.Get(this);
			}
			catch (Exception ex)
			{
				// We might not have access to the file, or the fileName parameter might contain a non-file name.
				// The latter condition should be very rare now since the caller is using FileUtility.IsValidPath.
				// An invalid file name should only get through to here if it's valid per Windows standard naming
				// rules but invalid on the target file system (e.g., if it's on a Unix system or on a CD).
				if (!IsAccessException(ex) && !(ex is NotSupportedException || ex is ArgumentException))
				{
					throw;
				}

				// Make sure this FileItem is unscannable so nothing will try to use its null this.fileInfo member.
				this.scanInfo = ScanInfo.Unscannable;
			}
		}

		#endregion

		#region Public Properties

		public string FileName { get; }

		public DocumentItem Document
		{
			get
			{
				return this.document;
			}

			set
			{
				if (this.document != value)
				{
					this.document = value;
					this.scanInfo = ScanInfo.Get(this);
				}
			}
		}

		public bool HasHierarchyReference
		{
			get
			{
				// Miscellaneous files will have HierarchyItem entries, but their HierarchyItems won't
				// have a parent reference if the solution or projects don't directly reference them.
				bool result = this.hierarchyItems.Any(item => !item.IsMiscellaneousFile);
				return result;
			}
		}

		public Language DocumentLanguage
		{
			get
			{
				Language result = Language.Unknown;

				DocumentItem doc = this.document;
				if (doc != null)
				{
					result = doc.Language;
				}

				return result;
			}
		}

		public bool AreTaskProjectsCurrent
		{
			get
			{
				bool result = this.tasks == null || this.tasks.All(task => task.Project == this.ProjectNames);
				return result;
			}
		}

		public bool IsScannable
		{
			get
			{
				bool result = this.scanInfo.IsScannable;
				return result;
			}
		}

		#endregion

		#region Private Properties

		private string ProjectNames
		{
			get
			{
				if (this.projectNames == null)
				{
					// Leave out items where the Solution is the parent.  The solution caption changes frequently.
					var projects = this.hierarchyItems.Where(item => item.Parent != null && item.Parent.ItemType == HierarchyItemType.Project);

					// We have to order the names because in AreTaskProjectsCurrent we compare CommentTask.Project to this.ProjectNames,
					// and we don't want the names list to change if HashSet returns the items in a different order for some reason.
					this.projectNames = string.Join(", ", projects.Select(item => item.Parent.Caption).OrderBy(text => text));
				}

				return this.projectNames;
			}
		}

		private string ExactCaseFileName
		{
			get
			{
				if (this.exactCaseFileName == null)
				{
					if (!FileUtility.TryGetExactPath(this.FileName, out this.exactCaseFileName))
					{
						this.exactCaseFileName = this.FileName;
					}
				}

				return this.exactCaseFileName;
			}
		}

		#endregion

		#region Public Methods

		public void ClearHierarchy()
		{
			this.projectNames = null;
			if (this.hierarchyItems != EmptySet)
			{
				this.hierarchyItems.Clear();
			}
		}

		public bool AddHierarchyItem(HierarchyItem item)
		{
			if (this.hierarchyItems == EmptySet)
			{
				this.hierarchyItems = new HashSet<HierarchyItem>();
			}

			bool result = this.hierarchyItems.Add(item);
			if (result)
			{
				this.projectNames = null;
			}

			return result;
		}

		public bool Refresh(RefreshAction action, out IDictionary<CommentTask, bool> itemChanges)
		{
			// When the solution is closed, make sure we treat previously opened items as not scannable so their tasks go away.
			bool isScannable = action != RefreshAction.Remove && this.scanInfo.IsScannable
				&& (this.hierarchyItems.Count > 0 || (this.document?.HasTextDocument ?? false) || !string.IsNullOrEmpty(this.ProjectNames));

			if (isScannable)
			{
				this.fileInfo.Refresh();
				isScannable = this.fileInfo.Exists;
			}

			bool result = false;
			itemChanges = null;

			if (!isScannable)
			{
				if (this.tasks != null)
				{
					itemChanges = this.tasks.ToDictionary(t => t, t => false);
					this.tasks = null;
				}

				// Reset to min time so if the file shows up again we'll re-scan it.
				this.lastModifiedTimeScannedUtc = MinDateTimeUtc;
				result = true;
			}
			else
			{
				bool forceRefresh = action == RefreshAction.Always || !this.AreTaskProjectsCurrent;

				// Make sure that the file has been modified since we last scanned it.
				// Note: lastModifiedUtc can go backward to before lastModifiedTimeScannedUtc
				// due to Undo, so we have to check != instead of just >.
				DateTime lastModifiedUtc = this.Document != null && this.Document.HasTextDocument
					? this.Document.LastModifiedUtc.Value : this.fileInfo.LastWriteTimeUtc;
				if (forceRefresh || lastModifiedUtc != this.lastModifiedTimeScannedUtc)
				{
					// Make sure that it's been at least N seconds since the file was last modified.
					// If someone is making a long sequence of continual edits, then there's no point
					// in wasting scanning overhead while they're actively changing the file.
					DateTime latestAllowedTimeUtc = DateTime.UtcNow - this.provider.ScanDelay;
					if (forceRefresh || latestAllowedTimeUtc >= lastModifiedUtc)
					{
						itemChanges = this.Refresh();
						result = true;
						this.lastModifiedTimeScannedUtc = lastModifiedUtc;
					}
				}
				else
				{
					// We should only get into this branch of the Refresh method if we got a new file, a document change event,
					// or a file change event.  It's possible to get a "document change" event when a document within the solution
					// is first opened even though we've already scanned it (from the solution reference).  When that happens the
					// lastModifiedUtc should equal the lastModifiedTimeScannedUtc, so we'll just return true since the file scan
					// results should already be up-to-date.
					result = lastModifiedUtc == this.lastModifiedTimeScannedUtc;
					Debug.Assert(result, "The file's modified time should have changed or been the same as the last modified scanned time.");
				}
			}

			CommentTaskProvider.Debug("FileItem.Refresh: {0}, Result={1}  ItemChanges={2}", this.FileName, result, itemChanges != null);
			return result;
		}

		#endregion

		#region Internal Methods

		internal static bool IsAccessException(Exception ex)
		{
			bool result = ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException;
			return result;
		}

		internal Stream TryOpenFileStream()
		{
			Stream result = null;

			try
			{
				result = new FileStream(this.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			}
			catch (Exception ex)
			{
				if (!IsAccessException(ex))
				{
					throw;
				}
			}

			return result;
		}

		#endregion

		#region Private Methods

		private IDictionary<CommentTask, bool> Refresh()
		{
			IEnumerable<CommentTask> newTasks = this.GetNewCommentTasks();
			IEnumerable<CommentTask> oldTasks = this.tasks;

			IDictionary<CommentTask, bool> result = null;
			if (newTasks != null || oldTasks != null)
			{
				result = new Dictionary<CommentTask, bool>();
				if (newTasks != null)
				{
					foreach (var task in newTasks)
					{
						result.Add(task, true);
					}
				}

				if (oldTasks != null)
				{
					foreach (var task in oldTasks)
					{
						result.Add(task, false);
					}
				}

				this.tasks = newTasks;
			}

			return result;
		}

		private IList<CommentTask> GetNewCommentTasks()
		{
			var tokenRegexList = this.provider.BackgroundTokens
				.Select(token => new { Token = token, Regexes = this.scanInfo.GetTokenRegexes(token).ToList() })
				.ToList();

			List<CommentTask> result = null;
			BackgroundOptions options = this.provider.Options;

			int lineNumber = 0;
			foreach (string line in this.GetLines())
			{
				lineNumber++;
				if (!string.IsNullOrEmpty(line))
				{
					foreach (var tokenRegex in tokenRegexList)
					{
						foreach (Regex regex in tokenRegex.Regexes)
						{
							MatchCollection matches = regex.Matches(line);
							foreach (Match match in matches)
							{
								string comment = match.Groups[ScanInfo.RegexCommentGroupName].Value.Trim();
								if (!options.ExcludeCommentsExpressions.Any(expr => expr.IsMatch(comment)))
								{
									CommentTask task = new(
										this.provider,
										tokenRegex.Token.Priority,
										this.ProjectNames,
										this.ExactCaseFileName,
										lineNumber,
										comment);

									if (!options.ExcludeFileComments.Contains(task.ExcludeText))
									{
										result ??= new();
										result.Add(task);
									}
								}
							}
						}
					}
				}
			}

			return result;
		}

		private IEnumerable<string> GetLines()
		{
			if (this.document != null && this.document.HasTextDocument)
			{
				foreach (string line in this.document.GetLines())
				{
					yield return line;
				}
			}
			else
			{
				using (Stream stream = this.TryOpenFileStream())
				{
					if (stream != null)
					{
						using (StreamReader reader = new(stream))
						{
							string line;
							while ((line = reader.ReadLine()) != null)
							{
								yield return line;
							}
						}
					}
				}
			}
		}

		#endregion
	}
}
