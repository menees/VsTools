namespace Menees.VsTools.Projects
{
	#region public GraphLayout

	public enum GraphLayout
	{
		// These values come from the GraphDirectionEnum in http://schemas.microsoft.com/vs/2009/dgml/dgml.xsd.
		// They all go with the Sugiyama (tree layout) value from the LayoutEnum.
		TopToBottom,
		BottomToTop,
		LeftToRight,
		RightToLeft,

		// This value is for the ForceDirected value from the LayoutEnum.
		QuickClusters,
	}

	#endregion

	#region internal NodeType

	internal enum NodeType
	{
		Project,
		Package,
	}

	#endregion

	#region internal LinkType

	internal enum LinkType
	{
		ProjectReference,
		PackageReference,
		SolutionDependency,
	}

	#endregion
}
