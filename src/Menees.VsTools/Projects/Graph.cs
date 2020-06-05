namespace Menees.VsTools.Projects
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
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

		private const string SelectedPrefix = "Selected";

		private readonly Dictionary<string, Node> idToNodeMap = new Dictionary<string, Node>(StringComparer.CurrentCultureIgnoreCase);

		#endregion

		#region Constructors

		public Graph(IReadOnlyList<Project> projects)
		{
			HashSet<string> rootProjects = new HashSet<string>(projects.Count);

			ThreadHelper.ThrowIfNotOnUIThread();
			foreach (Project project in projects)
			{
				Node projectNode = this.GetNode(project.Name, NodeType.Project, true);
				rootProjects.Add(project.Name);

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
			}

			BuildDependencies dependencies = projects[0]?.DTE?.Solution?.SolutionBuild?.BuildDependencies;
			if (dependencies?.Count > 0)
			{
				foreach (BuildDependency dependency in dependencies)
				{
					Project sourceProject = dependency.Project;
					if (rootProjects.Contains(sourceProject.Name))
					{
						Node source = this.GetNode(sourceProject.Name, NodeType.Project, true);
						foreach (Project targetProject in ((object[])dependency.RequiredProjects).Cast<Project>())
						{
							Node target = this.GetNode(targetProject.Name, NodeType.Project);
							if (!source.References.Any(tuple => ReferenceEquals(tuple.Item1, target)))
							{
								source.References.Add((target, LinkType.SolutionDependency));
							}
						}
					}
				}
			}

			// TODO: Add Package references. [Bill, 5/29/2020]
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

			string commonPrefix = FindCommonPrefix(this.idToNodeMap.Values.Select(n => n.Label).ToList());

			foreach (Node node in this.idToNodeMap.Values)
			{
				XElement nodeXml = new XElement(ns.GetName(nameof(Node)));
				nodeXml.SetAttributeValue("Id", node.Id);
				string label = !string.IsNullOrEmpty(commonPrefix) && node.Label.StartsWith(commonPrefix)
					? node.Label.Substring(commonPrefix.Length)
					: node.Label;
				nodeXml.SetAttributeValue("Label", label);
				nodeXml.SetAttributeValue("Category", node.Category);
				nodesXml.Add(nodeXml);

				AddLinks(linksXml, node, node.References);
			}

			XElement categoriesXml = new XElement(ns.GetName("Categories"));
			graphXml.Add(categoriesXml);

			// WPF color chart: https://wpfknowledge.blogspot.com/2012/05/note-this-is-not-original-work.html
			AddCategory(SelectedPrefix + NodeType.Project, "AliceBlue");
			AddCategory(NodeType.Project.ToString(), "LavenderBlush");
			AddCategory(NodeType.Package.ToString(), "Lavender");
			AddCategory(LinkType.ProjectReference.ToString(), stroke: "Silver");
			AddCategory(LinkType.PackageReference.ToString(), stroke: "MediumSlateBlue");
			AddCategory(LinkType.SolutionDependency.ToString(), stroke: "DarkGray", strokeDashArray: "5,5");

			void AddCategory(string id, string background = null, string stroke = null, string strokeDashArray = null)
			{
				XElement category = new XElement(ns.GetName("Category"));
				category.SetAttributeValue("Id", id);
				category.SetAttributeValue("Background", background);
				category.SetAttributeValue("Stroke", stroke);
				category.SetAttributeValue("StrokeDashArray", strokeDashArray);
				category.SetAttributeValue("Style", "Plain"); // Glass or Plain
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

		private static string FindCommonPrefix(IReadOnlyList<string> values)
		{
			string result = null;

			if (values.Count >= 2)
			{
				var ordered = values.OrderBy(v => v.Length).ThenBy(v => v).ToArray();
				string prefix = ordered[0];
				for (int itemIndex = 1; itemIndex < ordered.Length; itemIndex++)
				{
					static int FindCommonPrefixLength(string a, string b)
					{
						Debug.Assert(a.Length <= b.Length, "len(a) <= len(b) because of OrderBy above.");

						int commonLength = 0;
						while (commonLength < a.Length && a[commonLength] == b[commonLength])
						{
							commonLength++;
						}

						return commonLength;
					}

					int commonPrefixLength = FindCommonPrefixLength(prefix, ordered[itemIndex]);
					if (commonPrefixLength == 0)
					{
						prefix = null;
						break;
					}
					else if (commonPrefixLength < prefix.Length)
					{
						prefix = prefix.Substring(0, commonPrefixLength);
					}
				}

				// Make sure the prefix ends with a non-digit and non-letter. Otherwise, we end up with weird cases:
				// E.g., Library1 and Library2 have a Library prefix, and we'd end up with just 1 and 2.
				// E.g., Menees.Common and Menees.Core have a Menees.Co prefix, so we'd have mmon and re.
				// We really only want to remove prefixes like "Menees." or "Menees_".
				int prefixLength = prefix?.Length ?? 0;
				while (prefixLength > 0 && char.IsLetterOrDigit(prefix[prefixLength - 1]))
				{
					prefixLength--;
				}

				if (prefixLength > 0)
				{
					result = prefix.Substring(0, prefixLength);
				}
			}

			return result;
		}

		private Node GetNode(string name, NodeType type, bool? selected = null)
		{
			string id = Path.GetFileName(name);

			if (!this.idToNodeMap.TryGetValue(id, out Node result))
			{
				result = new Node(id, type);
				this.idToNodeMap.Add(id, result);
			}

			// We may see a node first as an unselected target but later discover it was a selected node.
			if (selected ?? false)
			{
				result.Category = SelectedPrefix + type;
			}

			return result;
		}

		#endregion
	}
}
