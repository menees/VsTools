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
	// Tells the PkgDef creation utility (CreatePkgDef.exe) that this class is a package.
	// Registers the information needed to show this package in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#111", Version, IconResourceID = 400)]
	[ProvideMenuResource("Menus.ctmenu", 1)] // This attribute is needed to let the shell know that this package exposes some menus.
	[ProvideToolWindow(typeof(BaseConverter.Window), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.PropertyBrowser)] // Registers a tool window.
	[ProvideToolWindow(typeof(TasksWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids.SolutionExplorer)] // Registers a tool window.
	[ProvideAutoLoad(VSConstants.UICONTEXT.CodeWindow_string, PackageAutoLoadFlags.BackgroundLoad)] // See comments in Menees.VsTools.vsct
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.EmptySolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[Guid(Guids.MeneesVsToolsPackageString)]
	[CLSCompliant(false)]
	[ProvideOptionPage(
		typeof(Options),
		categoryName: Title,
		pageName: "General",
		categoryResourceID: 113,
		pageNameResourceID: 112,
		supportsAutomation: false,
		SupportsProfiles = true,
		ProfileMigrationType = ProfileMigrationType.PassThrough)] // Registers an Options page
	[ProvideProfile(
		typeof(Options),
		categoryName: Title,
		objectName: "General",
		categoryResourceID: 113,
		objectNameResourceID: 112,
		isToolsOptionPage: true,
		DescriptionResourceID = 114,
		MigrationType = ProfileMigrationType.PassThrough)] // Registers settings persistence. Affects Import/Export Settings.
	[ProvideOptionPage(
		typeof(BaseConverter.Options),
		categoryName: Title,
		pageName: BaseConverter.Window.DefaultCaption,
		categoryResourceID: 113,
		pageNameResourceID: 115,
		supportsAutomation: false,
		SupportsProfiles = true,
		ProfileMigrationType = ProfileMigrationType.PassThrough)] // Registers an Options page
	[ProvideProfile(
		typeof(BaseConverter.Options),
		categoryName: Title,
		objectName: BaseConverter.Window.DefaultCaption,
		categoryResourceID: 113,
		objectNameResourceID: 115,
		isToolsOptionPage: true,
		DescriptionResourceID = 114,
		MigrationType = ProfileMigrationType.PassThrough)] // Registers settings persistence. Affects Import/Export Settings.
	[ProvideOptionPage(
		typeof(Tasks.Options),
		categoryName: Title,
		pageName: TasksWindow.DefaultCaption,
		categoryResourceID: 113,
		pageNameResourceID: 116,
		supportsAutomation: false,
		SupportsProfiles = true,
		ProfileMigrationType = ProfileMigrationType.PassThrough)] // Registers an Options page
	[ProvideProfile(
		typeof(Tasks.Options),
		categoryName: Title,
		objectName: TasksWindow.DefaultCaption,
		categoryResourceID: 113,
		objectNameResourceID: 116,
		isToolsOptionPage: true,
		DescriptionResourceID = 114,
		MigrationType = ProfileMigrationType.PassThrough)] // Registers settings persistence. Affects Import/Export Settings.
	[ProvideOptionPage(
		typeof(HighlightOptions),
		categoryName: Title,
		pageName: HighlightOptions.DefaultCaption,
		categoryResourceID: 113,
		pageNameResourceID: 117,
		supportsAutomation: false,
		SupportsProfiles = true,
		ProfileMigrationType = ProfileMigrationType.PassThrough)] // Registers an Options page
	[ProvideProfile(
		typeof(HighlightOptions),
		categoryName: Title,
		objectName: HighlightOptions.DefaultCaption,
		categoryResourceID: 113,
		objectNameResourceID: 117,
		isToolsOptionPage: true,
		DescriptionResourceID = 114,
		MigrationType = ProfileMigrationType.PassThrough)] // Registers settings persistence. Affects Import/Export Settings.
#pragma warning restore SA1515
	public sealed partial class MainPackage
	{
	}
}
