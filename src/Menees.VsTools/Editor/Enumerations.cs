namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	#region internal VsFormatCategory

	internal enum VsFormatCategory
	{
		TextEditor,
		OutputWindow,
		FindResults,
	}

	#endregion

	#region internal OutputHighlightType

	internal enum OutputHighlightType
	{
		None,
		Error,
		Warning,
		Information,
		Detail,
		Header,
		Custom1,
		Custom2,
	}

	#endregion
}
