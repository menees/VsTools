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
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Classification;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	[Export(typeof(IClassifierProvider))]
	[ContentType(FindResultsClassifierProvider.ContentType)]
	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created by MEF.")]
	internal sealed class FindResultsClassifierProvider : ClassifierProviderBase
	{
		#region Internal Constants

		internal const string ContentType = "FindResults";
		internal const string ClassifierName = "Find Results Highlight";

		#endregion

		#region Constructors

		public FindResultsClassifierProvider()
			: base(ClassifierName)
		{
		}

		#endregion

		#region Protected Methods

		protected override ClassifierBase CreateClassifier(ITextBuffer buffer)
		{
			FindResultsClassifier result = new FindResultsClassifier(buffer, this.ClassificationRegistry);
			return result;
		}

		#endregion
	}
}
