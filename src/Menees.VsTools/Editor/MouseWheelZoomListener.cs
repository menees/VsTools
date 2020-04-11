namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel.Composition;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType("Text")]
	[TextViewRole(PredefinedTextViewRoles.Zoomable)]
	internal sealed class MouseWheelZoomListener : IWpfTextViewCreationListener
	{
		#region Public Methods

		public void TextViewCreated(IWpfTextView textView)
		{
			bool enable = MainPackage.Instance?.Options.IsMouseWheelZoomEnabled ?? false;
			textView.Options.SetOptionValue(DefaultWpfViewOptions.EnableMouseWheelZoomId, enable);
		}

		#endregion
	}
}
