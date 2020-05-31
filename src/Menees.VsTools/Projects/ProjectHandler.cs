namespace Menees.VsTools.Projects
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;
	using System.Xml.Linq;
	using EnvDTE;
	using EnvDTE80;
	using Menees.VsTools.Editor;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using VSLangProj;

	#endregion

	internal static class ProjectHandler
	{
		#region Public Methods

		public static bool GetSelectedProjects(DTE dte, List<Project> selectedProjects, bool allowSolution)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			bool result = false;

			try
			{
				bool checkSolution = false;
				SelectedItems selectedItems = dte.SelectedItems;
				if (selectedItems != null)
				{
					foreach (SelectedItem item in selectedItems)
					{
						// If they've selected anything other than a project in Solution Explorer
						// (e.g., a file or the solution node) then item.Project will be nothing.
						Project project = item.Project;
						if (project != null)
						{
							result = true;
							selectedProjects?.Add(project);
						}
						else if (allowSolution && item.ProjectItem == null)
						{
							// If a SelectedItem isn't a Project or ProjectItem, we have no way to see what it actually is.
							// We'll have to directly ask the Solution Explorer below if the solution is a selected UIHierarchyItem.
							checkSolution = true;
						}
					}
				}

				if (checkSolution)
				{
					// From https://www.mztools.com/articles/2013/MZ2013019.aspx
					var uIItems = (object[])((DTE2)dte).ToolWindows.SolutionExplorer.SelectedItems;
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread. Select's lambda runs on the current thread, which is the main thread.
					Solution solution = uIItems.OfType<UIHierarchyItem>().Select(uiItem => uiItem.Object as Solution).FirstOrDefault(sln => sln != null);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
					if (solution != null)
					{
						selectedProjects?.Clear();

						// This throws out any "project" nodes like "Miscellaneous Files" that don't implement VSProject.
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread. Where's lambda runs on the current thread, which is the main thread.
						foreach (Project project in solution.Projects.Cast<Project>().Where(p => p.Object is VSProject))
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
						{
							result = true;
							selectedProjects?.Add(project);
						}
					}
				}
			}
#pragma warning disable CC0004 // Catch block cannot be empty
			catch (COMException)
			{
				// Sometimes getting and iterating the selected items collection blows up for no good reason.
			}
#pragma warning restore CC0004 // Catch block cannot be empty

			return result;
		}

		public static void ListAllProjectProperties(DTE dte)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			StringBuilder output = new StringBuilder();

			List<Project> selectedProjects = new List<Project>();
			if (!GetSelectedProjects(dte, selectedProjects, false))
			{
				OutputString(output, "One or more projects must be selected in Solution Explorer.");
			}
			else
			{
				bool first = true;
				foreach (Project project in selectedProjects)
				{
					if (!first)
					{
						OutputString(output, Environment.NewLine);
					}

					if (project != null)
					{
						OutputString(output, project.Name);
						OutputString(output, Environment.NewLine);

						const string Indent = "\t";

						// Show project-level properties
						OutputProperties(output, project.Properties, Indent);

						// Show configuration-level properties
						if (project.ConfigurationManager != null)
						{
							foreach (Configuration configuration in project.ConfigurationManager.Cast<Configuration>().OrderBy(c =>
								{
									ThreadHelper.ThrowIfNotOnUIThread();
									return c.ConfigurationName;
								}))
							{
								OutputString(output, Environment.NewLine);
								OutputString(output, Indent);
								OutputString(output, configuration.ConfigurationName);
								OutputString(output, Environment.NewLine);

								OutputProperties(output, configuration.Properties, Indent + Indent);
							}
						}
					}

					first = false;
				}
			}

			// Example from http://msdn.microsoft.com/en-us/library/envdte.dte.aspx
			dte.ItemOperations.NewFile(@"General\Text File", "Project Properties");
			TextDocumentHandler handler = new TextDocumentHandler(dte);
			handler.SetSelectedText(output.ToString(), "List All Project Properties");
		}

		public static void ViewProjectDependencies(MainPackage package, DTE dte)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			List<Project> selectedProjects = new List<Project>();
			if (!GetSelectedProjects(dte, selectedProjects, true))
			{
				package.ShowMessageBox("One or more projects (or the solution) must be selected in Solution Explorer.");
			}
			else
			{
				Graph graph = new Graph(selectedProjects);
				XDocument graphXml = graph.CreateDgmlDocument(dte.Edition);

				string tempFileName = Path.Combine(Path.GetTempPath(), "Project Dependencies.dgml");
				graphXml.Save(tempFileName);

				// Technically, the "Graph Document Editor" has a ViewKind of "{295A0962-5A59-4F4F-9E12-6BC670C15C3B}".
				// However, the default works fine. It should open the document in the XML editor if the DGML editor isn't installed.
				dte.ItemOperations.OpenFile(tempFileName);
			}
		}

		#endregion

		#region Private Methods

		private static void OutputString(StringBuilder output, string message)
		{
			output.Append(message);
		}

		[SuppressMessage(
			"Microsoft.Design",
			"CA1031:DoNotCatchGeneralExceptionTypes",
			Justification = "This is necessary because the project system can throw any exception type.")]
		private static void OutputProperties(StringBuilder output, EnvDTE.Properties properties, string indent)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			foreach (Property property in properties.Cast<Property>().OrderBy(p =>
				{
					ThreadHelper.ThrowIfNotOnUIThread();
					return p.Name;
				}))
			{
				OutputString(output, indent);
				OutputString(output, property.Name);
				OutputString(output, " = ");
				try
				{
					object value = property.Value;
					OutputString(output, Convert.ToString(value));
				}
				catch (Exception ex)
				{
					// Pulling the value of any of the web properties (e.g., "WebServer")
					// on a C# project will throw an exception if it isn't a web project.
					OutputString(output, "*** " + ex.Message);
				}

				OutputString(output, Environment.NewLine);
			}
		}

		#endregion
	}
}
