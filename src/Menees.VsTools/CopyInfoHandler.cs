namespace Menees.VsTools;

#region Using Directives

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#endregion

internal class CopyInfoHandler
{
	#region Private Data Members

	private readonly DTE dte;
	private readonly MainPackage package;
	private readonly List<string> lines = [];

	private string solutionPath;
	private string solutionRepoPath;

	#endregion

	#region Constructors

	public CopyInfoHandler(DTE dte, MainPackage package)
	{
		this.dte = dte;
		this.package = package;
	}

	#endregion

	#region Public Methods

	public bool CanExecute(Command command)
	{
		bool result = false;

		ThreadHelper.ThrowIfNotOnUIThread();
		switch (command)
		{
			case Command.CopySolutionRelativePath:
			case Command.CopyProjectRelativePath:
			case Command.CopyUnixSolutionRelativePath:
			case Command.CopyUnixProjectRelativePath:
				result = this.dte.Solution.FullName.IsNotEmpty();
				break;

			case Command.CopyRepoRelativePath:
			case Command.CopyUnixRepoRelativePath:
				result = this.FindGitRepo(this.dte.Solution.FullName);
				break;

			case Command.CopyParentPath:
			case Command.CopyFullPath:
			case Command.CopyNameOnly:
			case Command.CopyUnixParentPath:
			case Command.CopyUnixFullPath:
				result = true;
				break;
		}

		return result;
	}

	public void Execute(Command command)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		this.lines.Clear();

		switch (command)
		{
			case Command.CopySolutionRelativePath:
			case Command.CopyUnixSolutionRelativePath:
				this.GetRelativePaths(
					command == Command.CopyUnixSolutionRelativePath,
					project =>
					{
						ThreadHelper.ThrowIfNotOnUIThread();
						return project.DTE.Solution.FullName;
					},
					item =>
					{
						ThreadHelper.ThrowIfNotOnUIThread();
						return item.ContainingProject.DTE.Solution.FullName;
					});
				break;

			case Command.CopyProjectRelativePath:
			case Command.CopyUnixProjectRelativePath:
				this.GetRelativePaths(
					command == Command.CopyUnixProjectRelativePath,
					project =>
					{
						ThreadHelper.ThrowIfNotOnUIThread();
						return project.FullName;
					},
					item =>
					{
						ThreadHelper.ThrowIfNotOnUIThread();
						return item.ContainingProject.FullName;
					});
				break;

			case Command.CopyRepoRelativePath:
			case Command.CopyUnixRepoRelativePath:
				if (this.FindGitRepo(this.dte.Solution.FullName))
				{
					this.GetRelativePaths(
						command == Command.CopyUnixRepoRelativePath,
						_ => this.solutionRepoPath,
						_ => this.solutionRepoPath);
				}

				break;

			case Command.CopyParentPath:
			case Command.CopyUnixParentPath:
				this.GetPaths(command == Command.CopyUnixParentPath, Path.GetDirectoryName);
				break;

			case Command.CopyFullPath:
			case Command.CopyUnixFullPath:
				this.GetPaths(command == Command.CopyUnixFullPath, path => path);
				break;

			case Command.CopyNameOnly:
				this.GetPaths(false, Path.GetFileName);
				break;
		}

		if (this.lines.Count > 0)
		{
			string value = string.Join(Environment.NewLine, this.lines);
			Clipboard.SetText(value);

#if DEBUG
			// TODO: Remove this. [Bill, 3/24/2025]
			VsShellUtilities.ShowMessageBox(
				this.package,
				value,
				command.ToString(),
				OLEMSGICON.OLEMSGICON_INFO,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
#endif
		}
	}

	#endregion

	#region Private Methods

	private static string GetRelativePath(string fullBaseName, string fullItemName, bool asUnix)
	{
		string result = null;

		if (!string.IsNullOrEmpty(fullItemName))
		{
			// A solution item may not be in a project, so we'll return its full path instead of a project-relative path.
			if (string.IsNullOrEmpty(fullBaseName))
			{
				result = fullItemName;
				if (asUnix)
				{
					result = GetUnixPath(result);
				}
			}
			else
			{
				Uri baseUri = new(fullBaseName);
				Uri itemUri = new(fullItemName);
				Uri relativeUri = baseUri.MakeRelativeUri(itemUri);
				result = Uri.UnescapeDataString(relativeUri.ToString());
				if (asUnix)
				{
					// The "relative" path may still end up a rooted path on another drive, so its drive may need reformatting.
					result = GetUnixPath(result);
				}
				else
				{
					// Uri always uses '/', so we need to flip it back to '\'.
					result = result.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
				}
			}
		}

		return result;
	}

	private static string GetUnixPath(string path)
	{
		string result = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (result.Length >= 2 && char.IsLetter(result[0]) && result[1] == ':')
		{
			result = $"{Path.AltDirectorySeparatorChar}{char.ToLower(result[0])}{result.Substring(2)}";
		}

		return result;
	}

	private bool FindGitRepo(string solutionPath)
	{
		if (this.solutionPath != solutionPath)
		{
			this.solutionPath = solutionPath;
			this.solutionRepoPath = null;
			if (solutionPath.IsNotEmpty())
			{
				// Search up the folder hierarchy for a hidden .git subfolder.
				string targetFolder = Path.GetDirectoryName(solutionPath);
				while (targetFolder.IsNotEmpty())
				{
					string gitPath = Path.Combine(targetFolder, ".git");
					DirectoryInfo gitDir = new(gitPath);
					if (gitDir.Exists && gitDir.Attributes.HasFlag(FileAttributes.Hidden))
					{
						this.solutionRepoPath = gitPath;
						break;
					}

					targetFolder = Path.GetDirectoryName(targetFolder);
				}
			}
		}

		return this.solutionRepoPath.IsNotEmpty();
	}

	private List<object> GetSelectedObjects()
	{
		// Started from https://stackoverflow.com/a/45180002/1882616
		ThreadHelper.ThrowIfNotOnUIThread();
		IVsMonitorSelection monitorSelection =
				(IVsMonitorSelection)Package.GetGlobalService(
				typeof(SVsShellMonitorSelection));
		monitorSelection.GetCurrentSelection(
			out IntPtr hierarchyPointer,
			out uint projectItemId,
			out IVsMultiItemSelect multiItemSelect,
			out IntPtr selectionContainerPointer);

		// https://stackoverflow.com/a/59993651/1882616
		List<object> result = [];
		if (multiItemSelect != null && ErrorHandler.Succeeded(multiItemSelect.GetSelectionInfo(out uint itemCount, out _)))
		{
			VSITEMSELECTION[] items = new VSITEMSELECTION[itemCount];
			if (ErrorHandler.Succeeded(multiItemSelect.GetSelectedItems(0, itemCount, items)))
			{
				foreach (VSITEMSELECTION item in items)
				{
					if (TryGetSelectedObject(item.pHier, item.itemid, out object selectedObject))
					{
						result.Add(selectedObject);
					}
				}
			}
		}
		else if (hierarchyPointer != IntPtr.Zero
			&& Marshal.GetTypedObjectForIUnknown(hierarchyPointer, typeof(IVsHierarchy)) is IVsHierarchy selectedHierarchy
			&& TryGetSelectedObject(selectedHierarchy, projectItemId, out object selectedObject))
		{
			result.Add(selectedObject);
		}
		else
		{
			// When the root solution node is selected, we need to get the Solution object from the DTE.
			int selectedCount = this.dte.SelectedItems.Count;
			for (int index = 1; index <= selectedCount; index++)
			{
				SelectedItem selectedItem = this.dte.SelectedItems.Item(index);
				if (selectedItem.ProjectItem != null)
				{
					result.Add(selectedItem.ProjectItem);
				}
				else if (selectedItem.Project != null)
				{
					result.Add(selectedItem.Project);
				}
				else if (selectedItem is not null)
				{
					result.Add(this.dte.Solution);
				}
			}
		}

		static bool TryGetSelectedObject(IVsHierarchy hierarchy, uint itemid, out object selectedObject)
		{
			selectedObject = null;
			if (hierarchy != null && ErrorHandler.Succeeded(hierarchy.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_ExtObject, out object extObject)))
			{
				selectedObject = extObject;
			}

			return selectedObject != null;
		}

		return result;
	}

	private void GetRelativePaths(bool asUnix, Func<Project, string> getProjectBase, Func<ProjectItem, string> getItemBase)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		foreach (object selectedObject in this.GetSelectedObjects())
		{
			(string fullItemName, string fullBaseName) = selectedObject switch
			{
				// FileNames is an old 1-based COM collection.
				ProjectItem projectItem => (projectItem.FileCount > 0 ? projectItem.FileNames[1] : null, getItemBase(projectItem)),
				Project project => (project.FullName, getProjectBase(project)),
				Solution solution => (solution.FullName, solution.FullName),
				_ => (null, null),
			};

			string relativePath = GetRelativePath(fullBaseName, fullItemName, asUnix);
			if (relativePath.IsNotEmpty())
			{
				this.lines.Add(relativePath);
			}
		}
	}

	private void GetPaths(bool asUnix, Func<string, string> convertPath)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		foreach (object selectedObject in this.GetSelectedObjects())
		{
			string fullItemName = selectedObject switch
			{
				// FileNames is an old 1-based COM collection.
				ProjectItem projectItem => projectItem.FileCount > 0 ? projectItem.FileNames[1] : null,
				Project project => project.FullName,
				Solution solution => solution.FullName,
				_ => null,
			};

			if (fullItemName.IsNotEmpty())
			{
				// Do lambda conversion before Unix formatting because APIs like Path.GetDirectoryName
				// will convert Path.AltDirectorySeparatorChar back into Path.DirectorySeparatorChar.
				string finalName = convertPath(fullItemName);
				if (asUnix)
				{
					finalName = GetUnixPath(finalName);
				}

				if (finalName.IsNotEmpty())
				{
					this.lines.Add(finalName);
				}
			}
		}
	}

	#endregion
}
