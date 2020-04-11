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
	[ContentType(OutputClassifierProvider.ContentType)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created by MEF.")]
	internal sealed class OutputTextViewListener : ClassifierTextViewListenerBase
	{
		#region Constructors

		public OutputTextViewListener()
			: base(OutputClassifierProvider.ClassifierName)
		{
		}

		#endregion
	}
}
