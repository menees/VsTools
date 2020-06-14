namespace Menees.VsTools.Projects
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Drawing;
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
		private readonly Options options;

		#endregion

		#region Constructors

		public Graph(IReadOnlyList<Project> projects, Options options)
		{
			HashSet<string> rootProjects = new HashSet<string>(projects.Count);
			this.options = options;

			ThreadHelper.ThrowIfNotOnUIThread();
			foreach (Project project in projects)
			{
				Node projectNode = this.GetNode(project.Name, NodeType.Project, project.FullName, true);
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
							Node refNode = this.GetNode(reference.Name, NodeType.Project, reference.SourceProject.FullName);
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
						Node source = this.GetNode(sourceProject.Name, NodeType.Project, sourceProject.FullName, true);
						foreach (Project targetProject in ((object[])dependency.RequiredProjects).Cast<Project>())
						{
							Node target = this.GetNode(targetProject.Name, NodeType.Project, targetProject.FullName);
							if (!source.References.Any(tuple => ReferenceEquals(tuple.Item1, target)))
							{
								source.References.Add((target, LinkType.SolutionDependency));
							}
						}
					}
				}
			}
		}

		#endregion

		#region Public Methods

		public XDocument CreateDgmlDocument(string edition)
		{
			// DGML = Directed Graph Markup Language
			// https://docs.microsoft.com/en-us/visualstudio/modeling/directed-graph-markup-language-dgml-reference
			// https://en.wikipedia.org/wiki/DGML
			// http://schemas.microsoft.com/vs/2009/dgml/dgml.xsd
			XNamespace ns = XNamespace.Get("http://schemas.microsoft.com/vs/2009/dgml");
			XElement graphXml = new XElement(ns.GetName("DirectedGraph"));
			graphXml.SetAttributeValue("ZoomLevel", "-1");
			switch (this.options.DefaultLayout)
			{
				case GraphLayout.QuickClusters:
					graphXml.SetAttributeValue("GraphDirection", GraphLayout.BottomToTop);
					graphXml.SetAttributeValue("Layout", "ForceDirected");
					break;

				default:
					graphXml.SetAttributeValue("GraphDirection", this.options.DefaultLayout);
					graphXml.SetAttributeValue("Layout", "Sugiyama");
					break;
			}

			AddProperties(graphXml);
			this.AddNodesAndLinks(graphXml);
			this.AddCategories(graphXml);
			this.AddStyles(graphXml);

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

		private static void AddProperties(XElement graphXml)
		{
			XNamespace ns = graphXml.Name.Namespace;
			XElement propertiesXml = new XElement(ns.GetName(nameof(Properties)));
			graphXml.Add(propertiesXml);
			AddProperty("Label", typeof(string).FullName);
			AddProperty("GraphDirection", "Microsoft.VisualStudio.Diagrams.Layout.LayoutOrientation");
			AddProperty("Layout", typeof(string).FullName);

			// We can't name this "IsSelected" because that's an actualy property of the Diagrams graph node,
			// and we if try setting a virtual "IsSelected" property, it selects the node on the graph.
			AddProperty("IsRoot", typeof(bool).FullName, "Selected");
			AddProperty("ZoomLevel", typeof(string).FullName);
			AddProperty("Project", typeof(string).FullName, isReference: true);

			void AddProperty(string id, string dataType, string label = null, bool isReference = false)
			{
				XElement property = new XElement(ns.GetName("Property"));
				property.SetAttributeValue("Id", id);
				property.SetAttributeValue("Label", label ?? id);
				property.SetAttributeValue("DataType", dataType);
				if (isReference)
				{
					property.SetAttributeValue("IsReference", true);
				}

				propertiesXml.Add(property);
			}
		}

		private void AddNodesAndLinks(XElement graphXml)
		{
			XNamespace ns = graphXml.Name.Namespace;
			XElement nodesXml = new XElement(ns.GetName("Nodes"));
			graphXml.Add(nodesXml);

			XElement linksXml = new XElement(ns.GetName("Links"));
			graphXml.Add(linksXml);

			string commonPrefix = this.options.RemoveCommonPrefix ? FindCommonPrefix(this.idToNodeMap.Values.Select(n => n.Label).ToList()) : null;

			foreach (Node node in this.idToNodeMap.Values)
			{
				XElement nodeXml = new XElement(ns.GetName(nameof(Node)));
				nodeXml.SetAttributeValue("Id", node.Id);
				string label = !string.IsNullOrEmpty(commonPrefix) && node.Label.StartsWith(commonPrefix)
					? node.Label.Substring(commonPrefix.Length)
					: node.Label;
				nodeXml.SetAttributeValue("Label", label);
				nodeXml.SetAttributeValue("Category", node.Category);
				nodeXml.SetAttributeValue("IsRoot", node.IsRoot);
				if (this.options.AddHyperLinks)
				{
					nodeXml.SetAttributeValue("Project", node.Reference);
				}

				nodesXml.Add(nodeXml);

				AddLinks(linksXml, node, node.References);
			}
		}

		private void AddCategories(XElement graphXml)
		{
			XNamespace ns = graphXml.Name.Namespace;
			XElement categoriesXml = new XElement(ns.GetName("Categories"));
			graphXml.Add(categoriesXml);

			// WPF color chart: https://wpfknowledge.blogspot.com/2012/05/note-this-is-not-original-work.html
			AddCategory(NodeType.Project.ToString());
			AddCategory(NodeType.Package.ToString());
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
				category.SetAttributeValue("Style", this.options.UseGlassStyle ? "Glass" : "Plain");
				categoriesXml.Add(category);
			}
		}

		private void AddStyles(XElement graphXml)
		{
			XNamespace ns = graphXml.Name.Namespace;
			XElement stylesXml = new XElement(ns.GetName("Styles"));
			graphXml.Add(stylesXml);

			AddStyle(
				nameof(Node),
				nameof(Project),
				"Selected",
				"HasCategory('Project') and IsRoot",
				this.options.SelectedProjectColor,
				this.options.SelectedProjectIcon);
			AddStyle(
				nameof(Node),
				nameof(Project),
				"Referenced",
				"HasCategory('Project') and !IsRoot",
				this.options.ReferencedProjectColor,
				this.options.ReferencedProjectIcon);

			void AddStyle(
				string targetType,
				string groupLabel,
				string valueLabel,
				string conditionExpression,
				Color background,
				string icon)
			{
				XElement style = new XElement(ns.GetName("Style"));
				style.SetAttributeValue("TargetType", targetType);
				style.SetAttributeValue("GroupLabel", groupLabel);
				style.SetAttributeValue("ValueLabel", valueLabel);
				stylesXml.Add(style);

				XElement condition = new XElement(ns.GetName("Condition"));
				condition.SetAttributeValue("Expression", conditionExpression);
				style.Add(condition);

				// System.Drawing's colors aren't the same as WPF's colors, so we'll use ARGB format.
				AddSetter(style, "Background", $"#{background.ToArgb():X8}");
				if (this.options.UseIcons && !string.IsNullOrEmpty(icon))
				{
					AddSetter(style, "Icon", icon);
				}
			}

			void AddSetter(XElement style, string property, object value)
			{
				XElement setter = new XElement(ns.GetName("Setter"));
				setter.SetAttributeValue("Property", property);
				setter.SetAttributeValue("Value", value);
				style.Add(setter);
			}
		}

		private Node GetNode(string name, NodeType type, string reference, bool root = false)
		{
			string id = Path.GetFileName(name);

			if (!this.idToNodeMap.TryGetValue(id, out Node result))
			{
				result = new Node(id, type, reference);
				this.idToNodeMap.Add(id, result);
			}

			// We may see a node first as a referenced target but later discover it was a root node.
			if (root)
			{
				result.IsRoot = true;
			}

			return result;
		}

		#endregion
	}
}
