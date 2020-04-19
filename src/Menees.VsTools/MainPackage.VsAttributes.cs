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

#pragma warning disable SA1515
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	// Tells the PkgDef creation utility (CreatePkgDef.exe) that this class is a package.
	// Registers the information needed to show this package in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#111", MainPackage.Version, IconResourceID = 400)]
	[ProvideMenuResource("Menus.ctmenu", 1)] // This attribute is needed to let the shell know that this package exposes some menus.
	[ProvideOptionPage(
		typeof(Options),
		categoryName: MainPackage.Title,
		pageName: "General",
		categoryResourceID: 113,
		pageNameResourceID: 112,
		supportsAutomation: false,
		SupportsProfiles = true,
		ProfileMigrationType = ProfileMigrationType.PassThrough)] // Registers an Options page
	[ProvideProfile(
		typeof(Options),
		categoryName: MainPackage.Title,
		objectName: MainPackage.Title,
		categoryResourceID: 113,
		objectNameResourceID: 113,
		isToolsOptionPage: true,
		DescriptionResourceID = 114,
		MigrationType = ProfileMigrationType.PassThrough)] // Registers settings persistence. Affects Import/Export Settings.
	[ProvideAutoLoad(VSConstants.UICONTEXT.CodeWindow_string, PackageAutoLoadFlags.BackgroundLoad)] // See comments in Menees.VsTools.vsct
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.EmptySolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[Guid(Guids.MeneesVsToolsPackageString)]
	[CLSCompliant(false)]
#pragma warning disable SA1515
	public sealed partial class MainPackage
	{
	}
}
