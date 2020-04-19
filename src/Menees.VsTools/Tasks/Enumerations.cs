namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	#region Internal HierarchyItemType

	internal enum HierarchyItemType
	{
		Solution,
		Project,
		File,
	}

	#endregion

	#region internal RefreshAction

	internal enum RefreshAction
	{
		IfNeeded,
		Always,
		Remove,
	}

	#endregion

	#region Internal TaskColumns

	internal enum TaskColumns
	{
		Priority,
		Comment,
		File,
		Line,
		Project,
	}

	#endregion
}
