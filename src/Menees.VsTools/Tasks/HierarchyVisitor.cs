namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel.Composition;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.ComponentModelHost;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;

	#endregion

	internal sealed class HierarchyVisitor
	{
		#region Private Data Members

		private readonly Stack<HierarchyItem> ancestors = new Stack<HierarchyItem>();
		private readonly List<HierarchyItem> items = new List<HierarchyItem>();
		private readonly HashSet<string> visitedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		#endregion

		#region Constructors

		public HierarchyVisitor(IVsHierarchy hierarchy)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// TODO: Switch to old DTE.Solution.Projects model instead of IVsHierarchy. [Bill, 4/19/2020]
			// TODO: Get all tasks from VS provider. Then only manually search other open docs. [Bill, 4/19/2020]
			// Note: This uses IVsHierarchy instead of the old DTE.Solution.Projects API because several "unmodeled" projects
			// don't support the old Automation API (e.g., Setup/Deployment projects and some Database projects).
			if (hierarchy != null)
			{
				// We have to get all nodes even if they're not visible in order to visit any "open" documents.
				// VS delay loads documents, so when opening a solution, it can show tabs for documents
				// that our DocumentMonitor hasn't received any notification for yet.
				HierarchyNode root = new HierarchyNode(hierarchy);
				this.VisitHierarchyNodes(root, 0, visibleNodesOnly: false);
			}
		}

		#endregion

		#region Public Properties

		public IReadOnlyList<HierarchyItem> Items => this.items;

		#endregion

		#region Private Methods

		/// <summary>
		/// Enumerates over the hierarchy items for the given hierarchy traversing into nested hierarchies.
		/// </summary>
		/// <param name="node">hierarchy to enumerate over.</param>
		/// <param name="recursionLevel">Depth of recursion. e.g. if recursion started with the Solution
		/// node, then : Level 0 -- Solution node, Level 1 -- children of Solution, etc.</param>
		/// <param name="visibleNodesOnly">true if only nodes visible in the Solution Explorer should
		/// be traversed. false if all project items should be traversed.</param>
		private void VisitHierarchyNodes(HierarchyNode node, int recursionLevel, bool visibleNodesOnly)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Note: This code originated from "Solution Hierarchy Traversal Sample (C#)"
			// https://msdn.microsoft.com/en-us/library/bb165347(v=vs.80).aspx
			// In C:\Setups\Microsoft\Visual Studio\VS 2005\Visual Studio 2005 SDK\ ...
			// VisualStudioIntegration\Samples\Project\CSharp\Example.SolutionHierarchyTraversal
			//
			// Check first if this node has a nested hierarchy. If so, then there really are two
			// identities for this node: 1. hierarchy/itemid and 2. nestedHierarchy/nestedItemId.
			// We will recurse and call VisitHierarchyNodes which will traverse this node using
			// the inner nestedHierarchy/nestedItemId identity.  Basically, if this returns a
			// nested hierarchy, then the current node is just a shortcut to the nested hierarchy.
			if (node.TryGetNestedHierarchy(out HierarchyNode nestedNode))
			{
				this.VisitHierarchyNodes(nestedNode, recursionLevel, visibleNodesOnly);
			}
			else
			{
				// In VS 2015 and earlier, the Solution node enumerates ALL projects (including nested projects)
				// when asking for FirstChild/NextSibling, but it doesn't do that for FirstVisibleChild/NextVisibleSibling.
				// VS treats the actual hierarchy differently than the visible hierarchy, and the solution contains
				// shortcuts to all the projects in the actual hierarchy.  So if we're not restricted to visible nodes,
				// then we can encounter nested projects twice as we traverse the hierarchy.  We'll make sure
				// we don't traverse through the same project twice.  However, we can't restrict to visible items
				// because we need to ensure that we get the "Miscellaneous Files" (e.g., external files opened
				// in the solution), which may not be visible in the Solution Explorer.
				//
				// Note: currentItem can be null if we're visiting a physical folder (e.g., a Properties folder), a
				// virtual folder (e.g., Solution Items or Miscellaneous Files), or some other node type that is
				// not a solution, project or file type.
				HierarchyItem currentItem = this.VisitHierarchyNode(node);
				bool visitChildren = true;
				if (currentItem != null)
				{
					visitChildren = currentItem.ItemType != HierarchyItemType.Project || !this.visitedProjects.Contains(currentItem.FileName);
					if (visitChildren)
					{
						this.items.Add(currentItem);
					}
				}

				if (visitChildren)
				{
					// Note: We'll push a null parent for miscellaneous files since they're not directly referenced by the solution.
					bool pushCurrentItemAsAncestor = (currentItem != null && currentItem.IsContainer)
						|| (currentItem == null && node.ItemType == VSConstants.GUID_ItemType_VirtualFolder && node.CanonicalName == "Miscellaneous Files");
					if (pushCurrentItemAsAncestor)
					{
						this.ancestors.Push(currentItem);
					}

					try
					{
						recursionLevel++;
						List<HierarchyNode> children = node.GetChildren(visibleNodesOnly).ToList();
						foreach (HierarchyNode child in children)
						{
							this.VisitHierarchyNodes(child, recursionLevel, visibleNodesOnly);
						}
					}
					finally
					{
						if (pushCurrentItemAsAncestor)
						{
							this.ancestors.Pop();
						}
					}

					if (currentItem != null && currentItem.ItemType == HierarchyItemType.Project)
					{
						this.visitedProjects.Add(currentItem.FileName);
					}
				}
			}
		}

		private HierarchyItem VisitHierarchyNode(HierarchyNode node)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			HierarchyItem result = null;
			IVsHierarchy hierarchy = node.Hierarchy;

			Guid itemType = node.ItemType;

			if (itemType == VSConstants.GUID_ItemType_PhysicalFile)
			{
				string canonicalName = node.CanonicalName;
				if (!string.IsNullOrEmpty(canonicalName))
				{
					result = this.CreateItem(canonicalName, HierarchyItemType.File, node);
				}
			}
			else
			{
				if (hierarchy is IVsSolution solution)
				{
					int hr = solution.GetSolutionInfo(out string directory, out string file, out _);
					if (ErrorHandler.Succeeded(hr) && !string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(file))
					{
						result = this.CreateItem(Path.Combine(directory, file), HierarchyItemType.Solution, node);
					}
				}
				else
				{
					if (hierarchy is IVsProject project)
					{
						// For some reason, physical folders (like Properties) also implement IVsProject.
						if (itemType != VSConstants.GUID_ItemType_PhysicalFolder)
						{
							int hr = project.GetMkDocument(node.ItemId, out string projectFile);
							if (ErrorHandler.Succeeded(hr))
							{
								result = this.CreateItem(projectFile, HierarchyItemType.Project, node);
							}
						}
					}
				}
			}

			return result;
		}

		private HierarchyItem CreateItem(string fileName, HierarchyItemType itemType, HierarchyNode node)
		{
			HierarchyItem parent = this.ancestors.Count > 0 ? this.ancestors.Peek() : null;
			var result = new HierarchyItem(fileName, itemType, node.Caption, parent);
			return result;
		}

		#endregion

		#region Private Types

		[DebuggerDisplay("{CanonicalName}")]
		private sealed class HierarchyNode
		{
			#region Constructors

			public HierarchyNode(IVsHierarchy hierarchy)
				: this(hierarchy, VSConstants.VSITEMID_ROOT)
			{
			}

			public HierarchyNode(IVsHierarchy hierarchy, uint itemId)
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				this.Hierarchy = hierarchy;
				this.ItemId = itemId;

				// All of these properties aren't always needed for each node, but they're needed for files,
				// which is the majority of nodes.  And we can't pull them dynamically in the debugger,
				// so I'll just pull them all up front.
				this.Caption = (string)this.GetProperty(__VSHPROPID.VSHPROPID_Caption);
				this.ItemType = this.GetProperty(__VSHPROPID.VSHPROPID_TypeGuid, Guid.Empty);

				int hr = this.Hierarchy.GetCanonicalName(this.ItemId, out string canonicalName);
				if (ErrorHandler.Succeeded(hr))
				{
					this.CanonicalName = canonicalName;
				}
			}

			#endregion

			#region Public Properties

			public IVsHierarchy Hierarchy { get; }

			public uint ItemId { get; }

			public string Caption { get; }

			public Guid ItemType { get; }

			public string CanonicalName { get; }

			#endregion

			#region Public Methods

			public bool TryGetNestedHierarchy(out HierarchyNode nestedNode)
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				bool result = false;
				nestedNode = null;
				Guid hierGuid = typeof(IVsHierarchy).GUID;
				int hr = this.Hierarchy.GetNestedHierarchy(this.ItemId, ref hierGuid, out IntPtr nestedHierarchyObj, out uint nestedItemId);
				if (hr == VSConstants.S_OK && nestedHierarchyObj != IntPtr.Zero)
				{
#pragma warning disable IDE0019 // Use pattern matching. We must get the IVsHierarchy before we release the IUnknown.
					IVsHierarchy nestedHierarchy = Marshal.GetObjectForIUnknown(nestedHierarchyObj) as IVsHierarchy;
					Marshal.Release(nestedHierarchyObj); // We are responsible to release the refcount on the out IntPtr parameter.
					if (nestedHierarchy != null)
#pragma warning restore IDE0019
					{
						nestedNode = new HierarchyNode(nestedHierarchy, nestedItemId);
						result = true;
					}
				}

				return result;
			}

			public IEnumerable<HierarchyNode> GetChildren(bool visibleNodesOnly)
			{
				__VSHPROPID propertyId = visibleNodesOnly ? __VSHPROPID.VSHPROPID_FirstVisibleChild : __VSHPROPID.VSHPROPID_FirstChild;
				object propertyValue = this.GetProperty(propertyId);
				uint childId = GetItemId(propertyValue);

				while (childId != VSConstants.VSITEMID_NIL)
				{
					yield return new HierarchyNode(this.Hierarchy, childId);

					propertyId = visibleNodesOnly ? __VSHPROPID.VSHPROPID_NextVisibleSibling : __VSHPROPID.VSHPROPID_NextSibling;
					propertyValue = GetProperty<__VSHPROPID, object>(this.Hierarchy, childId, propertyId);
					childId = GetItemId(propertyValue);
				}
			}

			public object GetProperty<TPropId>(TPropId propertyId)
				where TPropId : struct
			{
				object result = this.GetProperty<TPropId, object>(propertyId, null);
				return result;
			}

			public TResult GetProperty<TPropId, TResult>(TPropId propertyId, TResult defaultValue = default)
				where TPropId : struct
			{
				TResult result = GetProperty(this.Hierarchy, this.ItemId, propertyId, defaultValue);
				return result;
			}

			#endregion

			#region Private Methods

			private static TResult GetProperty<TPropId, TResult>(
				IVsHierarchy hierarchy,
				uint itemId,
				TPropId propertyId,
				TResult defaultValue = default)
				where TPropId : struct
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				int propId = Convert.ToInt32(propertyId);

				TResult result;
				int hr;
				if (typeof(TResult) == typeof(Guid) || Nullable.GetUnderlyingType(typeof(TResult)) == typeof(Guid))
				{
					hr = hierarchy.GetGuidProperty(itemId, propId, out Guid guid);
					result = hr == VSConstants.S_OK ? (TResult)(object)guid : defaultValue;
				}
				else
				{
					hr = hierarchy.GetProperty(itemId, propId, out object value);
					result = hr == VSConstants.S_OK ? (TResult)value : defaultValue;
				}

				return result;
			}

			/// <summary>
			/// Gets the item id.
			/// </summary>
			/// <param name="pvar">VARIANT holding an itemid.</param>
			/// <returns>Item Id of the concerned node</returns>
			private static uint GetItemId(object pvar)
			{
				uint result;

				if (pvar == null)
				{
					result = VSConstants.VSITEMID_NIL;
				}
				else if (pvar is int)
				{
					result = (uint)(int)pvar;
				}
				else if (pvar is uint)
				{
					result = (uint)pvar;
				}
				else if (pvar is short)
				{
					result = (uint)(short)pvar;
				}
				else if (pvar is ushort)
				{
					result = (uint)(ushort)pvar;
				}
				else if (pvar is long)
				{
					result = (uint)(long)pvar;
				}
				else
				{
					result = VSConstants.VSITEMID_NIL;
				}

				return result;
			}

			#endregion
		}

		#endregion
	}
}
