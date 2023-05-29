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
	[ProvideOptionPage(typeof(Projects.Options), Title, Projects.Options.DefaultCaption, 113, 120, false)]
	/*
		The ProvideOptionPage.SupportsProfiles property requires SupportsAutomation. If both are true and all
		the options hierarchy is marked ComVisible, then import/export profiles can be theoretically be automatic.
		However, that doesn't work for me. I always get an error trying to export each Options category:
			"Failed to export settings for 'Xxx' [code 6896]"
		So, I have to manually use ProvideProfile, which means I can't group the pages in the import/export dialog.
		(Via reflection it looks like ProvileProfile.GroupName is only used for registry key nesting not UI grouping.)
		This is ugly, but it's better than having no support for import/export.
		https://github.com/VsixCommunity/Community.VisualStudio.Toolkit/discussions/237
		https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/support-for-user-settings
		https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.provideprofileattribute.-ctor
	*/
	[ProvideProfile(typeof(Options), Title, "General", 113, 212, true)]
	[ProvideProfile(typeof(BaseConverter.Options), Title, BaseConverter.Window.DefaultCaption, 113, 215, true)]
	[ProvideProfile(typeof(Tasks.Options), Title, TasksWindow.DefaultCaption, 113, 216, true)]
	[ProvideProfile(typeof(HighlightOptions), Title, HighlightOptions.DefaultCaption, 113, 217, true)]
	[ProvideProfile(typeof(Sort.Options), Title, Sort.Options.DefaultCaption, 113, 218, true)]
	[ProvideProfile(typeof(Regions.Options), Title, Regions.Options.DefaultCaption, 113, 219, true)]
	[ProvideProfile(typeof(Projects.Options), Title, Projects.Options.DefaultCaption, 113, 220, true)]
	public sealed partial class MainPackage
	{
	}
}
