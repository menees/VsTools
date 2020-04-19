namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;

	#endregion

	[DebuggerDisplay("{Caption}")]
	internal sealed class HierarchyItem
	{
		#region Constructors

		public HierarchyItem(string fileName, HierarchyItemType itemType, string caption, HierarchyItem parent)
		{
			this.FileName = fileName;
			this.ItemType = itemType;

			// Note: Solution captions can contain the Unicode Left-To-Right Order Mark (0x200E) character.
			// It doesn't hurt anything usually, but it can show up as ? if it's incorrectly decoded.
			this.Caption = caption;
			this.Parent = parent;
			this.Level = parent != null ? parent.Level + 1 : 0;
		}

		#endregion

		#region Public Properties

		public string FileName { get; }

		public HierarchyItemType ItemType { get; }

		public string Caption { get; }

		public HierarchyItem Parent { get; }

		public int Level { get; }

		public bool IsContainer => this.ItemType != HierarchyItemType.File;

		public bool IsMiscellaneousFile => this.Parent == null && this.ItemType == HierarchyItemType.File;

		#endregion
	}
}
