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
	private readonly Options options;

	private string solutionPath;
	private string solutionRepoPath;

	#endregion

	#region Constructors

	public CopyInfoHandler(DTE dte, MainPackage package)
	{
		this.dte = dte;
		this.package = package;

		ThreadHelper.ThrowIfNotOnUIThread();
		this.options = MainPackage.GeneralOptions;
	}

	#endregion

	#region Public Methods

	public bool CanExecute(Command command)
	{
		bool isOsAvailable = false;
		bool isTargetAvailable = false;

		ThreadHelper.ThrowIfNotOnUIThread();
		switch (command)
		{
			case Command.CopySolutionRelativePath:
			case Command.CopyProjectRelativePath:
			case Command.CopyRepoRelativePath:
			case Command.CopyParentPath:
			case Command.CopyFullPath:
			case Command.CopyDocSolutionRelativePath:
			case Command.CopyDocProjectRelativePath:
			case Command.CopyDocRepoRelativePath:
			case Command.CopyDocParentPath:
			case Command.CopyDocFullPath:
				isOsAvailable = this.options.ShowCopyInfoStandardCommands;
				break;

			case Command.CopyUnixSolutionRelativePath:
			case Command.CopyUnixProjectRelativePath:
			case Command.CopyUnixRepoRelativePath:
			case Command.CopyUnixParentPath:
			case Command.CopyUnixFullPath:
			case Command.CopyDocUnixSolutionRelativePath:
			case Command.CopyDocUnixProjectRelativePath:
			case Command.CopyDocUnixRepoRelativePath:
			case Command.CopyDocUnixParentPath:
			case Command.CopyDocUnixFullPath:
				isOsAvailable = this.options.ShowCopyInfoUnixCommands;
				break;

			case Command.CopyNameOnly:
			case Command.CopyDocNameOnly:
				isOsAvailable = this.options.ShowCopyInfoStandardCommands || this.options.ShowCopyInfoUnixCommands;
				break;
		}

		switch (command)
		{
			case Command.CopySolutionRelativePath:
			case Command.CopyProjectRelativePath:
			case Command.CopyUnixSolutionRelativePath:
			case Command.CopyUnixProjectRelativePath:
			case Command.CopyDocSolutionRelativePath:
			case Command.CopyDocProjectRelativePath:
			case Command.CopyDocUnixSolutionRelativePath:
			case Command.CopyDocUnixProjectRelativePath:
				isTargetAvailable = this.dte.Solution.FullName.IsNotEmpty();
				break;

			case Command.CopyRepoRelativePath:
			case Command.CopyUnixRepoRelativePath:
			case Command.CopyDocRepoRelativePath:
			case Command.CopyDocUnixRepoRelativePath:
				isTargetAvailable = this.FindGitRepo(this.dte.Solution.FullName);
				break;

			case Command.CopyParentPath:
			case Command.CopyFullPath:
			case Command.CopyNameOnly:
			case Command.CopyUnixParentPath:
			case Command.CopyUnixFullPath:
			case Command.CopyDocParentPath:
			case Command.CopyDocFullPath:
			case Command.CopyDocNameOnly:
			case Command.CopyDocUnixParentPath:
			case Command.CopyDocUnixFullPath:
				isTargetAvailable = true;
				break;
		}

		bool result = isOsAvailable && isTargetAvailable;
		return result;
	}

	public void Execute(Command command)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		this.lines.Clear();

		// TODO: Detect doc command. [Bill, 3/30/2025]
		switch (command)
		{
			case Command.CopySolutionRelativePath:
			case Command.CopyUnixSolutionRelativePath:
			case Command.CopyDocSolutionRelativePath:
			case Command.CopyDocUnixSolutionRelativePath:
				this.GetRelativePaths(
					command == Command.CopyDocSolutionRelativePath || command == Command.CopyDocUnixSolutionRelativePath,
					command == Command.CopyUnixSolutionRelativePath || command == Command.CopyDocUnixSolutionRelativePath,
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
			case Command.CopyDocProjectRelativePath:
			case Command.CopyDocUnixProjectRelativePath:
				this.GetRelativePaths(
					command == Command.CopyDocProjectRelativePath || command == Command.CopyDocUnixProjectRelativePath,
					command == Command.CopyUnixProjectRelativePath || command == Command.CopyDocUnixProjectRelativePath,
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
			case Command.CopyDocRepoRelativePath:
			case Command.CopyDocUnixRepoRelativePath:
				if (this.FindGitRepo(this.dte.Solution.FullName))
				{
					this.GetRelativePaths(
						command == Command.CopyDocRepoRelativePath || command == Command.CopyDocUnixRepoRelativePath,
						command == Command.CopyUnixRepoRelativePath || command == Command.CopyDocUnixRepoRelativePath,
						_ => this.solutionRepoPath,
						_ => this.solutionRepoPath);
				}

				break;

			case Command.CopyParentPath:
			case Command.CopyUnixParentPath:
			case Command.CopyDocParentPath:
			case Command.CopyDocUnixParentPath:
				this.GetPaths(
					command == Command.CopyDocParentPath || command == Command.CopyDocUnixParentPath,
					command == Command.CopyUnixParentPath || command == Command.CopyDocUnixParentPath,
					Path.GetDirectoryName);
				break;

			case Command.CopyFullPath:
			case Command.CopyUnixFullPath:
			case Command.CopyDocFullPath:
			case Command.CopyDocUnixFullPath:
				this.GetPaths(
					command == Command.CopyDocFullPath || command == Command.CopyDocUnixFullPath,
					command == Command.CopyUnixFullPath || command == Command.CopyDocUnixFullPath,
					path => path);
				break;

			case Command.CopyNameOnly:
			case Command.CopyDocNameOnly:
				this.GetPaths(
					command == Command.CopyDocNameOnly,
					false,
					Path.GetFileName);
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

	private string GetRelativePath(string fullBaseName, string fullItemName, bool asUnix)
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
					result = this.GetUnixPath(result);
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
					result = this.GetUnixPath(result);
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

	private string GetUnixPath(string path)
	{
		string result = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (result.Length >= 2 && char.IsLetter(result[0]) && result[1] == ':')
		{
			char driveLetter = result[0];
			string remainingPath = result.Substring(2);
			result = this.options.UnixDriveFormat switch
			{
				UnixDriveFormat.MountLowerLetter => $"/mnt/{char.ToLower(driveLetter)}{remainingPath}",
				UnixDriveFormat.UpperLetterColon => $"{char.ToUpper(driveLetter)}:{remainingPath}",
				_ => $"/{char.ToLower(driveLetter)}{remainingPath}",
			};
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

	private List<object> GetSelectedObjects(bool fromActiveDocument)
	{
		List<object> result;

		ThreadHelper.ThrowIfNotOnUIThread();
		if (!fromActiveDocument)
		{
			result = this.GetSelectedObjects();
		}
		else
		{
			// Unless "Show Miscellaneous Files in Solution Explorer" is enabled,
			// we can't get ProjectItems from the normal IVsHierarchy, so for doc
			// commands we'll use the DTE's ActiveDocument directly.
			Document document = this.dte.ActiveDocument;
			result = [document.ProjectItem];
		}

		return result;
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
		else if (hierarchyPointer == IntPtr.Zero
			&& projectItemId == VSConstants.VSITEMID_ROOT
			&& multiItemSelect is null
			&& selectionContainerPointer != IntPtr.Zero)
		{
			result.Add(this.dte.Solution);
		}
		else
		{
			// This block is typically only reached when no solution is open.
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

	private void GetRelativePaths(bool fromActiveDocument, bool asUnix, Func<Project, string> getProjectBase, Func<ProjectItem, string> getItemBase)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		foreach (object selectedObject in this.GetSelectedObjects(fromActiveDocument))
		{
			(string fullItemName, string fullBaseName) = selectedObject switch
			{
				// FileNames is an old 1-based COM collection.
				ProjectItem projectItem => (projectItem.FileCount > 0 ? projectItem.FileNames[1] : null, getItemBase(projectItem)),
				Project project => (project.FullName, getProjectBase(project)),
				Solution solution => (solution.FullName, solution.FullName),
				_ => (null, null),
			};

			string relativePath = this.GetRelativePath(fullBaseName, fullItemName, asUnix);
			if (relativePath.IsNotEmpty())
			{
				this.lines.Add(relativePath);
			}
		}
	}

	private void GetPaths(bool fromActiveDocument, bool asUnix, Func<string, string> convertPath)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		foreach (object selectedObject in this.GetSelectedObjects(fromActiveDocument))
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
					finalName = this.GetUnixPath(finalName);
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
