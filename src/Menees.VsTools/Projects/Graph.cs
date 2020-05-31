namespace Menees.VsTools.Projects
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Xml.Linq;
	using EnvDTE;
	using Microsoft.VisualStudio.Shell;
	using VSLangProj;

	#endregion

	internal sealed class Graph
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
				Node projectNode = this.GetNode(project.Name, NodeType.Project);

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
							Node refNode = this.GetNode(reference.Name, NodeType.Project);
							projectNode.References.Add((refNode, LinkType.ProjectReference));
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
			graphXml.SetAttributeValue("Layout", "Sugiyama"); // None, Sugiyama (tree layout), ForceDirected (quick clusters), or DependencyMatrix
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
				XElement nodeXml = new XElement(ns.GetName(nameof(Node)));
				nodeXml.SetAttributeValue("Id", node.Id);
				nodeXml.SetAttributeValue("Label", node.Label);
				nodeXml.SetAttributeValue("Category", node.Type);
				nodesXml.Add(nodeXml);

				AddLinks(linksXml, node, node.References);
			}

			XElement categoriesXml = new XElement(ns.GetName("Categories"));
			graphXml.Add(categoriesXml);
			AddCategory(NodeType.Project.ToString(), "LightBlue");
			AddCategory(NodeType.Package.ToString(), "LightGreen");
			AddCategory(LinkType.ProjectReference.ToString(), stroke: "Black");
			AddCategory(LinkType.PackageReference.ToString(), stroke: "DarkGreen");
			AddCategory(LinkType.SolutionDependency.ToString(), stroke: "Black", strokeDashArray: "2,2");

			void AddCategory(string id, string background = null, string stroke = null, string strokeDashArray = null)
			{
				XElement category = new XElement(ns.GetName("Category"));
				category.SetAttributeValue("Id", id);
				category.SetAttributeValue("Background", background);
				category.SetAttributeValue("Stroke", stroke);
				category.SetAttributeValue("StrokeDashArray", strokeDashArray);
				category.SetAttributeValue("Style", "Glass"); // Glass or Plain
				categoriesXml.Add(category);
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

		private static void AddLinks(XElement linksXml, Node sourceNode, IEnumerable<(Node, LinkType)> targetNodes)
		{
			XNamespace ns = linksXml.Name.Namespace;
			foreach ((Node targetNode, LinkType linkType) in targetNodes)
			{
				XElement linkXml = new XElement(ns.GetName("Link"));
				linkXml.SetAttributeValue("Source", sourceNode.Id);
				linkXml.SetAttributeValue("Target", targetNode.Id);
				linkXml.SetAttributeValue("Category", linkType);
				linksXml.Add(linkXml);
			}
		}

		private Node GetNode(string name, NodeType type)
		{
			string id = Path.GetFileName(name);

			if (!this.idToNodeMap.TryGetValue(id, out Node result))
			{
				result = new Node(id, type);
				this.idToNodeMap.Add(id, result);
			}

			return result;
		}

		#endregion
	}
}
