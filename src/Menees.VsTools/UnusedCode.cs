namespace Menees.VsTools
{
	/* This file's Build Action is set to None instead of Compile.
	* You can temporarily flip it to Compile to get an IntelliSense "compile".
	*/

	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using EnvDTE;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;

	#endregion

	// Using the Output Window with a custom pane.
	private static class OutputWindowHelper
	{
		public const string MeneesVsToolsOutputPaneString = "01780d9b-2b60-4ab0-b561-b3ca3065563c";
		public static readonly Guid MeneesVsToolsOutputPane = new Guid(MeneesVsToolsOutputPaneString);

		private static readonly Lazy<bool> IsVs2013 = new Lazy<bool>(() =>
		{
			bool result = false;

			// This idea came from http://stackoverflow.com/a/11097293/1882616.
			string exe = ApplicationInfo.ExecutableFile;
			if (File.Exists(exe))
			{
				FileVersionInfo info = FileVersionInfo.GetVersionInfo(exe);
				result = info.ProductMajorPart == 12;
			}

			return result;
		});

		private static void OutputString(Package package, DTE dte, string message)
		{
			IVsOutputWindowPane output = package.GetOutputPane(Guids.MeneesVsToolsOutputPane, MainPackage.Title);
			if (output != null)
			{
				// ErrorHandler.ThrowOnFailure(output.Clear());
				ErrorHandler.ThrowOnFailure(output.Activate());

				// Make sure the output window is open/visible.
				Window outputWindow = dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
				outputWindow.Visible = true;
				outputWindow.Activate();

				ErrorHandler.ThrowOnFailure(output.OutputString(message));
			}
		}
	}
}
