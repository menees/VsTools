namespace Menees.VsTools.Projects
{
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
