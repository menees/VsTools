namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;
	using EnvDTE;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;

	#endregion

	internal static class ProjectHandler
	{
		#region Public Methods

		public static bool GetSelectedProjects(DTE dte, List<Project> selectedProjects)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			bool result = false;

			try
			{
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
							if (selectedProjects != null)
							{
								selectedProjects.Add(project);
							}
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
			if (!GetSelectedProjects(dte, selectedProjects))
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
