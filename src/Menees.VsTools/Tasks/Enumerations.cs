namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	#region Public TodoSuffix

	public enum TodoSuffix
	{
		None,
		Date,
		User,
		UserDate,
	}

	#endregion

	#region Internal HierarchyItemType

	internal enum HierarchyItemType
	{
		Solution,
		Project,
		File,
	}

	#endregion

	#region Internal RefreshAction

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
