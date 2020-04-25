namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Drawing;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Windows.Forms;
	using EnvDTE;
	using EnvDTE80;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.TextManager.Interop;
	using Sys = System.Threading.Tasks;

	#endregion

	internal sealed class CommentTaskProvider : TaskProvider, IVsTaskListEvents
	{
		#region Private Data Members

		// We're not using TaskProviderGuids.Comments because we don't want our tasks in the VS task list.
		private static readonly Guid ProviderId = new Guid("24079DE0-6866-4EF5-9095-530705917139");
		private static readonly Guid CategoryId = new Guid("DBC6A6BD-302C-4095-941A-1B126548C66A");

		private readonly System.Windows.Forms.Timer foregroundTimer;
		private readonly System.Threading.Timer backgroundTimer;
		private readonly SolutionMonitor solutionMonitor;
		private readonly DocumentMonitor documentMonitor;
		private readonly FileMonitor fileMonitor;
		private readonly FileItemManager manager;
		private readonly List<CommentToken> foregroundTokens = new List<CommentToken>();
		private readonly BackgroundOptions backgroundOptions = new BackgroundOptions();

		private bool disposed;
		private int isBackgroundTimerExecuting;
		private bool foregroundTokensChanged;
		private List<CommentToken> backgroundTokens;
		private bool appliedOptionsPending;

		#endregion

		#region Constructors

		public CommentTaskProvider(MainPackage package)
			: base(package.ServiceProvider)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.ServiceProvider = package.ServiceProvider;

			// In some cases the task list may not be available (e.g., during devenv.exe /build).
			if (!(this.ServiceProvider.GetService(typeof(SVsTaskList)) is IVsTaskList taskList))
			{
				this.disposed = true;
			}
			else
			{
				Options options = MainPackage.GeneralOptions;
				options.Applied += this.Options_Applied;
				this.backgroundOptions.Update(options);

				// Register a custom category so Visual Studio will invoke our IVsTaskListEvents callbacks.
				VSTASKCATEGORY[] assignedCategory = new VSTASKCATEGORY[1];
				int hr = this.VsTaskList.RegisterCustomCategory(CategoryId, (uint)TaskCategory.Comments + 1, assignedCategory);
				ErrorHandler.ThrowOnFailure(hr);
				this.CustomCategory = (TaskCategory)assignedCategory[0];

				// The TaskProvider.ProviderGuid Property help says:
				// "The task list groups all tasks from multiple providers with the same GUID into a single list."
				// So the ProviderGuid is really just the group we're providing tasks for and not unique to us.
				this.ProviderGuid = ProviderId;
				this.ProviderName = "Tasks (" + MainPackage.Title + ")";

				// Hide this provider since we're using our own Tasks tool window.
				this.AlwaysVisible = false;
				this.DisableAutoRoute = false;
				this.MaintainInitialTaskOrder = false;

				this.foregroundTimer = new System.Windows.Forms.Timer();
				this.foregroundTimer.Interval = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;
				this.foregroundTimer.Tick += this.ForegroundTimer_Tick;
				this.ScanDelay = TimeSpan.FromSeconds(2);

				this.CacheCommentTokens();
				this.solutionMonitor = new SolutionMonitor(this);
				this.documentMonitor = new DocumentMonitor(this);
				this.fileMonitor = new FileMonitor(this);
				this.manager = new FileItemManager(this, this.fileMonitor, this.backgroundOptions);

				// Enable the timers last.  The BackgroundTimerCallback will fire after ScanDelay (on a worker thread),
				// so we have to ensure that everything is initialized before its first callback.
				this.foregroundTimer.Enabled = true;
				this.backgroundTimer = new System.Threading.Timer(this.BackgroundTimerCallback, null, this.ScanDelay, this.ScanDelay);
			}
		}

		#endregion

		#region Public Events

		public event EventHandler<TasksChangedEventArgs> TasksChanged;

		#endregion

		#region Public Properties

		public TaskCategory CustomCategory { get; }

		public IServiceProvider ServiceProvider { get; }

		public TimeSpan ScanDelay { get; }

		public IReadOnlyList<CommentToken> BackgroundTokens => this.backgroundTokens;

		#endregion

		#region IVsTaskListEvents Methods

		int IVsTaskListEvents.OnCommentTaskInfoChanged()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.CacheCommentTokens();
			return VSConstants.S_OK;
		}

		#endregion

		#region Internal Methods

		internal static Dictionary<TKey, TValue> CloneAndClear<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
		{
			Dictionary<TKey, TValue> result = null;

			lock (dictionary)
			{
				if (dictionary.Count > 0)
				{
					result = new Dictionary<TKey, TValue>(dictionary, dictionary.Comparer);
					dictionary.Clear();
				}
			}

			return result;
		}

		[Conditional("DEBUG")]
		internal static void Debug(string format, params object[] args)
		{
			string message = string.Format(format, args);
			System.Diagnostics.Debug.WriteLine(message, nameof(CommentTaskProvider));
		}

		#endregion

		#region Protected Methods

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing && !this.disposed)
			{
#pragma warning disable VSTHRD108 // Assert thread affinity unconditionally. If !disposing, then we're on a finalizer thread.
				ThreadHelper.ThrowIfNotOnUIThread();
#pragma warning restore VSTHRD108 // Assert thread affinity unconditionally

				this.backgroundTimer.Dispose();
				this.foregroundTimer.Dispose();
				this.solutionMonitor.Dispose();
				this.documentMonitor.Dispose();
				this.fileMonitor.Dispose();

				int hr = this.VsTaskList.UnregisterCustomCategory((VSTASKCATEGORY)this.CustomCategory);
				ErrorHandler.ThrowOnFailure(hr);
				this.disposed = true;
			}
		}

		#endregion

		#region Private Methods

		private void ForegroundTimer_Tick(object sender, EventArgs e)
		{
			var handler = this.TasksChanged;
			if (handler != null)
			{
				// We can only safely add and remove visible tasks from the foreground thread.
				IReadOnlyDictionary<CommentTask, bool> changedTasks = this.manager.GetChangedTasks();
				if (changedTasks != null)
				{
					TasksChangedEventArgs args = new TasksChangedEventArgs(
						changedTasks.Where(pair => pair.Value).Select(pair => pair.Key),
						changedTasks.Where(pair => !pair.Value).Select(pair => pair.Key));
					handler(this, args);
				}
			}
		}

		private void BackgroundTimerCallback(object state)
		{
			if (Interlocked.CompareExchange(ref this.isBackgroundTimerExecuting, 1, 0) == 0)
			{
				// Make sure only one thread at a time is running (in case this handler takes longer than the timer's interval).
				try
				{
					// If the user disables this in Options, then immediately stop scanning (without waiting for a restart).
					if (this.backgroundOptions.EnableCommentScans)
					{
						bool updateAll = this.appliedOptionsPending;
						this.appliedOptionsPending = false;
						lock (this.foregroundTokens)
						{
							if (this.foregroundTokensChanged)
							{
								this.backgroundTokens = new List<CommentToken>(this.foregroundTokens);
								this.foregroundTokensChanged = false;
								updateAll = true;
							}
						}

						if (this.backgroundTokens.Count > 0 || updateAll)
						{
							var foregroundUpdate = ThreadHelper.JoinableTaskFactory.RunAsync(this.ForegroundUpdateAsync);
#pragma warning disable VSTHRD102 // Implement internal logic asynchronously. Some of the VS calls must be made from the foreground thread.
							foregroundUpdate.Join();
#pragma warning restore VSTHRD102 // Implement internal logic asynchronously

							IReadOnlyDictionary<string, bool> changedFiles = this.fileMonitor.GetChangedFiles();
							if (changedFiles != null)
							{
								Debug(
									"Updating Files ({0}): {1}",
									changedFiles.Count,
									string.Join(", ", changedFiles.Select(pair => Path.GetFileName(pair.Key) + '=' + pair.Value)));
								this.manager.UpdateFiles(changedFiles);
							}

							this.manager.UpdateTasks(updateAll);
						}
					}
				}
#pragma warning disable CC0004 // Catch block cannot be empty. Comment explains it.
				catch (InvalidComObjectException)
				{
					// If this fires on a background thread while the main VS thread is cleaning up,
					// then a RCW might get separated from its COM object before this.disposed is set.
				}
#pragma warning restore CC0004 // Catch block cannot be empty
				finally
				{
					Interlocked.Exchange(ref this.isBackgroundTimerExecuting, 0);
				}
			}
		}

		private async Sys.Task ForegroundUpdateAsync()
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			IEnumerable<HierarchyItem> allHierarchyItems = this.solutionMonitor.GetChangedHierarchy();
			if (allHierarchyItems != null)
			{
				Debug("Updating Hierarchy ({0})", allHierarchyItems.Count());
				this.manager.UpdateHierarchy(allHierarchyItems);
			}

			IReadOnlyDictionary<string, DocumentItem> changedDocuments = this.documentMonitor.GetChangedDocuments();
			if (changedDocuments != null)
			{
				Debug(
					"Updating Documents ({0}): {1}",
					changedDocuments.Count,
					string.Join(", ", changedDocuments.Select(pair => Path.GetFileName(pair.Key) + '=' + (pair.Value != null))));
				this.manager.UpdateDocuments(changedDocuments);
			}
		}

		private void CacheCommentTokens()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// The VS options dialog allows tokens to be entered that differ only by case (e.g., todo and TODO),
			// and it allows them to each have a different priority assigned.  However, the VS comment task provider
			// doesn't match case-insensitively, so only the last token and priority are used within a case group.
			// We'll be smarter than that and detect whether case-sensitivity is required for a token.
			Dictionary<string, TaskPriority> tempTokens = new Dictionary<string, TaskPriority>();

			if (this.VsTaskList is IVsCommentTaskInfo tokenInfo
				&& ErrorHandler.Succeeded(tokenInfo.TokenCount(out int tokenCount)) && tokenCount > 0)
			{
				var tokens = new IVsCommentTaskToken[tokenCount];
				if (ErrorHandler.Succeeded(tokenInfo.EnumTokens(out IVsEnumCommentTaskTokens tokenEnum))
					&& ErrorHandler.Succeeded(tokenEnum.Next((uint)tokenCount, tokens, out uint count)) && count == tokenCount)
				{
					var priority = new VSTASKPRIORITY[1];
					foreach (var token in tokens)
					{
						if (ErrorHandler.Succeeded(token.Text(out string text)) &&
							ErrorHandler.Succeeded(token.Priority(priority)) &&
							!string.IsNullOrWhiteSpace(text))
						{
							tempTokens.Add(text, (TaskPriority)priority[0]);
						}
					}
				}
			}

			lock (this.foregroundTokens)
			{
				this.foregroundTokens.Clear();
				var groups = tempTokens.GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase);
				foreach (var group in groups)
				{
					bool isCaseSensitive = group.Count() > 1;
					foreach (var pair in group)
					{
						this.foregroundTokens.Add(new CommentToken(pair.Key, pair.Value, isCaseSensitive));
					}
				}

				this.foregroundTokensChanged = true;
			}
		}

		private void Options_Applied(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (this.backgroundOptions.Update(MainPackage.GeneralOptions))
			{
				this.appliedOptionsPending = true;
			}
		}

		#endregion
	}
}
