namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio.Editor;

	#endregion

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	internal sealed class ClassificationFormatCategoryAttribute : Attribute
	{
		#region Constructors

		public ClassificationFormatCategoryAttribute(VsFormatCategory category)
		{
			this.Category = category;

			switch (category)
			{
				case VsFormatCategory.TextEditor:
					this.Guid = DefGuidList.guidTextEditorFontCategory;
					break;

				case VsFormatCategory.OutputWindow:
					this.Guid = DefGuidList.guidOutputWindowFontCategory;
					break;

				case VsFormatCategory.FindResults:
					this.Guid = DefGuidList.guidFindResultsFontCategory;
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(category));
			}
		}

		#endregion

		#region Public Properties

		public VsFormatCategory Category { get; }

		public Guid Guid { get; }

		#endregion
	}
}
