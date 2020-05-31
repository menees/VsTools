namespace Menees.VsTools
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

		#region Private Types

		private sealed class Graph
		{
			#region Private Data Members

			private readonly Dictionary<string, Node> idToNodeMap = new Dictionary<string, Node>(StringComparer.CurrentCultureIgnoreCase);

			#endregion

			#region Constructors

			public Graph(IReadOnlyList<Project> projects)
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				foreach (Project project in projects)
				{
					Node projectNode = this.GetNode(project.Name);

					// Note: SDK-style projects only implement the old VSLangProj interfaces. They don't implement the
					// newer VSProject3 or VSProject4 interfaces to get AnalyzerReferences or PackageReferences.
					// They also don't implement new interfaces like Reference6.
					if (project.Object is VSProject vsProj)
					{
						foreach (Reference reference in vsProj.References)
						{
							// Only report project references not framework and external DLL assemblies.
							if (reference.SourceProject != null)
							{
								Node refNode = this.GetNode(reference.Name);
								projectNode.ProjectReferences.Add(refNode);
							}
						}
					}

					// Note: See the comments for the VSLangProj150 PackageReference in the .csproj
					// for the voodoo magic I had to do in order to reference VSProject4.
					// Sometimes project.Object is not a VSProject4 (e.g., for "Miscellaneous Files" if the solution was selected).
					// TODO: Add explicit Solution dependencies. [Bill, 5/29/2020]
					// TODO: Add Package references. [Bill, 5/29/2020]
				}
			}

			#endregion

			#region Public Methods

			public XDocument CreateDgmlDocument(string edition)
			{
				// DGML = Directed Graph Markup Language
				// https://docs.microsoft.com/en-us/visualstudio/modeling/directed-graph-markup-language-dgml-reference?view=vs-2019
				// https://en.wikipedia.org/wiki/DGML
				// http://schemas.microsoft.com/vs/2009/dgml/dgml.xsd
				XNamespace ns = XNamespace.Get("http://schemas.microsoft.com/vs/2009/dgml");
				XElement graphXml = new XElement(ns.GetName("DirectedGraph"));
				graphXml.SetAttributeValue("GraphDirection", "BottomToTop");
				graphXml.SetAttributeValue("Layout", "Sugiyama");
				graphXml.SetAttributeValue("ZoomLevel", "-1");

				XElement nodesXml = new XElement(ns.GetName("Nodes"));
				graphXml.Add(nodesXml);

				XElement linksXml = new XElement(ns.GetName("Links"));
				graphXml.Add(linksXml);

				XElement propertiesXml = new XElement(ns.GetName(nameof(Properties)));
				graphXml.Add(propertiesXml);
				AddProperty("Label", typeof(string).FullName);
				AddProperty("GraphDirection", "Microsoft.VisualStudio.Diagrams.Layout.LayoutOrientation");
				AddProperty("Layout", typeof(string).FullName);
				AddProperty("ZoomLevel", typeof(string).FullName);

				void AddProperty(string id, string dataType)
				{
					XElement property = new XElement(ns.GetName("Property"));
					property.SetAttributeValue("Id", id);
					property.SetAttributeValue("Label", id);
					property.SetAttributeValue("DataType", dataType);
					propertiesXml.Add(property);
				}

				foreach (Node node in this.idToNodeMap.Values)
				{
					XElement nodeXml = new XElement(ns.GetName(nameof(Node)), new XAttribute("Id", node.Id));
					nodeXml.SetAttributeValue("Label", node.Label);
					nodesXml.Add(nodeXml);

					AddLinks(linksXml, node, node.ProjectReferences);
					AddLinks(linksXml, node, node.AnalyzerReferences);
				}

				string fullVsName = $"Visual Studio {edition} {MainPackage.VersionYear}";
				XDocument result = new XDocument();
				result.Add(new XComment("If this opens in the XML editor, then you need to install Visual Studio's DGML editor."));
				result.Add(new XComment($"Run the Visual Studio Installer, click Modify on {fullVsName}, and go to the Individual Components tab."));
				result.Add(new XComment("Search for 'DGML editor' (under 'Code tools'), check its checkbox, and click Modify."));
				result.Add(graphXml);
				return result;
			}

			#endregion

			#region Private Methods

			private static void AddLinks(XElement linksXml, Node sourceNode, IEnumerable<Node> targetNodes)
			{
				XNamespace ns = linksXml.Name.Namespace;
				foreach (Node targetNode in targetNodes)
				{
					XElement linkXml = new XElement(ns.GetName("Link"));
					linkXml.SetAttributeValue("Source", sourceNode.Id);
					linkXml.SetAttributeValue("Target", targetNode.Id);
					linksXml.Add(linkXml);
				}
			}

			private Node GetNode(string name)
			{
				string id = Path.GetFileName(name);

				if (!this.idToNodeMap.TryGetValue(id, out Node result))
				{
					result = new Node(id);
					this.idToNodeMap.Add(id, result);
				}

				return result;
			}

			#endregion
		}

		private sealed class Node
		{
			#region Constructors

			public Node(string id)
			{
				this.Id = id;
			}

			#endregion

			#region Public Properties

			public string Id { get; }

			public string Label => this.Id;

			public List<Node> ProjectReferences { get; } = new List<Node>();

			public List<Node> AnalyzerReferences { get; } = new List<Node>();

			#endregion
		}

		#endregion
	}
}
