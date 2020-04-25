namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using EnvDTE;
	using EnvDTE80;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.ComponentModelHost;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;

	#endregion

	internal sealed class SolutionMonitor : IVsSolutionEvents, IVsSolutionEvents2, IVsSolutionEvents3, IVsSolutionEvents4,
		IVsTrackProjectDocumentsEvents2, IDisposable
	{
		#region Private Data Members

		private readonly CommentTaskProvider provider;
		private readonly IVsSolution2 solution;
		private readonly IVsTrackProjectDocuments2 projectTracker;

		private uint? solutionEventsCookie;
		private bool isScanRequired;
		private uint? projectEventsCookie;

		#endregion

		#region Constructors

		public SolutionMonitor(CommentTaskProvider provider)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.provider = provider;
			this.solution = (IVsSolution2)this.provider.ServiceProvider.GetService(typeof(SVsSolution));
			int hr = this.solution.AdviseSolutionEvents(this, out uint cookie);
			ErrorHandler.ThrowOnFailure(hr);
			this.solutionEventsCookie = cookie;

			// Note: I tried using IVsHierarchyItemCollectionProvider (via componentModel.GetService<IVsHierarchyItemCollectionProvider>()),
			// but it had problems.  It only worked for the first solution opened, and it caused the nodes to display in a different order in
			// the VS Solution Explorer!  Using IVsTrackProjectDocumentsEvents2 works better.
			this.projectTracker = (IVsTrackProjectDocuments2)this.provider.ServiceProvider.GetService(typeof(SVsTrackProjectDocuments));
			hr = this.projectTracker.AdviseTrackProjectDocumentsEvents(this, out cookie);
			ErrorHandler.ThrowOnFailure(hr);
			this.projectEventsCookie = cookie;

			this.provider.Options.Updated += (s, e) => { this.RequireScan(); };

			this.RequireScan();
		}

		#endregion

		#region Private Properties

		private IVsHierarchy SolutionHierarchy
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				IVsHierarchy result = this.solution as IVsHierarchy;
				return result;
			}
		}

		#endregion

		#region IVsSolutionEvents* Methods

		public int OnAfterAsynchOpenProject(IVsHierarchy hierarchy, int added)
		{
			if (added != 0)
			{
				this.RequireScan();
			}

			return VSConstants.S_OK;
		}

		public int OnAfterChangeProjectParent(IVsHierarchy hierarchy)
		{
			// I'm not sure whether this is necessary.  But this rarely occurs, so another scan probably won't hurt.
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterCloseSolution(object unkReserved)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterClosingChildren(IVsHierarchy hierarchy)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterLoadProject(IVsHierarchy stubHierarchy, IVsHierarchy realHierarchy)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterMergeSolution(object unkReserved)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterOpeningChildren(IVsHierarchy hierarchy)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterOpenProject(IVsHierarchy hierarchy, int added)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterOpenSolution(object unkReserved, int newSolution)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterRenameProject(IVsHierarchy hierarchy)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		// We're requiring a scan in the OnAfterCloseSolution method.
		public int OnBeforeCloseSolution(object unkReserved) => VSConstants.S_OK;

		// We're requiring a scan in the OnAfterClosingChildren method.
		public int OnBeforeClosingChildren(IVsHierarchy hierarchy) => VSConstants.S_OK;

		// We're requiring a scan in the OnAfterOpeningChildren method.
		public int OnBeforeOpeningChildren(IVsHierarchy hierarchy) => VSConstants.S_OK;

		public int OnBeforeUnloadProject(IVsHierarchy realHierarchy, IVsHierarchy stubHierarchy)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnQueryChangeProjectParent(IVsHierarchy hierarchy, IVsHierarchy newParentHier, ref int cancel) => VSConstants.S_OK;

		public int OnQueryCloseProject(IVsHierarchy hierarchy, int removing, ref int cancel) => VSConstants.S_OK;

		public int OnQueryCloseSolution(object unkReserved, ref int cancel) => VSConstants.S_OK;

		public int OnQueryUnloadProject(IVsHierarchy realHierarchy, ref int cancel) => VSConstants.S_OK;

		#endregion

		#region IVsTrackProjectDocumentsEvents* Methods

		public int OnAfterAddDirectoriesEx(
			int countProjects,
			int countDirectories,
			IVsProject[] projects,
			int[] firstIndices,
			string[] rgpszMkDocuments,
			VSADDDIRECTORYFLAGS[] flags)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterAddFilesEx(
			int countProjects,
			int countFiles,
			IVsProject[] projects,
			int[] firstIndices,
			string[] rgpszMkDocuments,
			VSADDFILEFLAGS[] flags)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterRemoveDirectories(
			int countProjects,
			int countDirectories,
			IVsProject[] projects,
			int[] firstIndices,
			string[] rgpszMkDocuments,
			VSREMOVEDIRECTORYFLAGS[] flags)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterRemoveFiles(
			int countProjects,
			int countFiles,
			IVsProject[] projects,
			int[] firstIndices,
			string[] rgpszMkDocuments,
			VSREMOVEFILEFLAGS[] flags)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterRenameDirectories(
			int countProjects,
			int countDirs,
			IVsProject[] orojects,
			int[] firstIndices,
			string[] rgszMkOldNames,
			string[] rgszMkNewNames,
			VSRENAMEDIRECTORYFLAGS[] flags)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterRenameFiles(
			int countProjects,
			int countFiles,
			IVsProject[] projects,
			int[] firstIndices,
			string[] rgszMkOldNames,
			string[] rgszMkNewNames,
			VSRENAMEFILEFLAGS[] flags)
		{
			this.RequireScan();
			return VSConstants.S_OK;
		}

		public int OnAfterSccStatusChanged(
			int countProjects,
			int countFiles,
			IVsProject[] projects,
			int[] firstIndices,
			string[] rgpszMkDocuments,
			uint[] rgdwSccStatus) => VSConstants.S_OK;

		public int OnQueryAddDirectories(
			IVsProject project,
			int countDirectories,
			string[] rgpszMkDocuments,
			VSQUERYADDDIRECTORYFLAGS[] flags,
			VSQUERYADDDIRECTORYRESULTS[] summaryResult,
			VSQUERYADDDIRECTORYRESULTS[] results) => VSConstants.S_OK;

		public int OnQueryAddFiles(
			IVsProject project,
			int countFiles,
			string[] rgpszMkDocuments,
			VSQUERYADDFILEFLAGS[] flags,
			VSQUERYADDFILERESULTS[] summaryResult,
			VSQUERYADDFILERESULTS[] results) => VSConstants.S_OK;

		public int OnQueryRemoveDirectories(
			IVsProject project,
			int countDirectories,
			string[] rgpszMkDocuments,
			VSQUERYREMOVEDIRECTORYFLAGS[] flags,
			VSQUERYREMOVEDIRECTORYRESULTS[] summaryResult,
			VSQUERYREMOVEDIRECTORYRESULTS[] results) => VSConstants.S_OK;

		public int OnQueryRemoveFiles(
			IVsProject project,
			int countFiles,
			string[] rgpszMkDocuments,
			VSQUERYREMOVEFILEFLAGS[] flags,
			VSQUERYREMOVEFILERESULTS[] summaryResult,
			VSQUERYREMOVEFILERESULTS[] results) => VSConstants.S_OK;

		public int OnQueryRenameDirectories(
			IVsProject project,
			int countDirs,
			string[] rgszMkOldNames,
			string[] rgszMkNewNames,
			VSQUERYRENAMEDIRECTORYFLAGS[] flags,
			VSQUERYRENAMEDIRECTORYRESULTS[] summaryResult,
			VSQUERYRENAMEDIRECTORYRESULTS[] results) => VSConstants.S_OK;

		public int OnQueryRenameFiles(
			IVsProject project,
			int countFiles,
			string[] rgszMkOldNames,
			string[] rgszMkNewNames,
			VSQUERYRENAMEFILEFLAGS[] flags,
			VSQUERYRENAMEFILERESULTS[] summaryResult,
			VSQUERYRENAMEFILERESULTS[] results) => VSConstants.S_OK;

		#endregion

		#region Public Methods

		public void Dispose()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			this.RequireScan(false);

			if (this.solution != null && this.solutionEventsCookie != null)
			{
				int hr = this.solution.UnadviseSolutionEvents(this.solutionEventsCookie.Value);
				ErrorHandler.Succeeded(hr); // Ignored because this returns a bool; it doesn't throw.
				this.solutionEventsCookie = null;
			}

			if (this.projectTracker != null && this.projectEventsCookie != null)
			{
				int hr = this.projectTracker.UnadviseTrackProjectDocumentsEvents(this.projectEventsCookie.Value);
				ErrorHandler.Succeeded(hr); // Ignored because this returns a bool; it doesn't throw.
				this.projectEventsCookie = null;
			}
		}

		public IReadOnlyList<HierarchyItem> GetChangedHierarchy()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			IReadOnlyList<HierarchyItem> result = null;

			if (this.isScanRequired)
			{
				HierarchyVisitor visitor = new HierarchyVisitor(this.SolutionHierarchy, this.provider.Options);
				result = visitor.Items;
				this.RequireScan(false);
			}

			return result;
		}

		#endregion

		#region Private Methods

		private void RequireScan(bool required = true, [CallerMemberName] string caller = null)
		{
			this.isScanRequired = required;
			CommentTaskProvider.Debug("SolutionMonitor.{0} {1} scan.", caller, required ? "enabled" : "disabled");
		}

		#endregion
	}
}
