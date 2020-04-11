namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel.Composition;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	[Export(typeof(IWpfTextViewConnectionListener))]
	[ContentType(FindResultsClassifierProvider.ContentType)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created by MEF.")]
	internal sealed class FindResultsTextViewListener : ClassifierTextViewListenerBase
	{
		#region Constructors

		public FindResultsTextViewListener()
			: base(FindResultsClassifierProvider.ClassifierName)
		{
		}

		#endregion
	}
}
