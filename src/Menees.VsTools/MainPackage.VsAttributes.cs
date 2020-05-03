namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.ComponentModel.Design;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Menees.VsTools.Editor;
	using Menees.VsTools.Tasks;
	using Microsoft;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.OLE.Interop;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using Microsoft.Win32;

	#endregion

#pragma warning disable SA1515 // SingleLineCommentsMustBePrecededByBlankLine
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("#110", "#111", Version, IconResourceID = 400)] // Info for Help/About dialog
	[ProvideMenuResource("Menus.ctmenu", 1)] // This attribute is needed to let the shell know that this package exposes some menus.
	[ProvideToolWindow(typeof(BaseConverter.Window), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.PropertyBrowser)]
	[ProvideToolWindow(typeof(TasksWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.SolutionExplorer)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.CodeWindow_string, PackageAutoLoadFlags.BackgroundLoad)] // See comments in Menees.VsTools.vsct
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.EmptySolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[Guid(Guids.MeneesVsToolsPackageString)]
	[CLSCompliant(false)]
	[ProvideOptionPage(typeof(Options), Title, "General", 113, 112, false)]
	[ProvideOptionPage(typeof(BaseConverter.Options), Title, BaseConverter.Window.DefaultCaption, 113, 115, false)]
	[ProvideOptionPage(typeof(Tasks.Options), Title, TasksWindow.DefaultCaption, 113, 116, false)]
	[ProvideOptionPage(typeof(HighlightOptions), Title, HighlightOptions.DefaultCaption, 113, 117, false)]
	[ProvideOptionPage(typeof(Sort.Options), Title, Sort.Options.DefaultCaption, 113, 118, false)]
	[ProvideOptionPage(typeof(Regions.Options), Title, Regions.Options.DefaultCaption, 113, 119, false)]
#pragma warning restore SA1515
	public sealed partial class MainPackage
	{
	}
}
