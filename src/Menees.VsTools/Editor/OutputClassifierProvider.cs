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
	[ContentType(OutputClassifierProvider.ContentType)]
	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created by MEF.")]
	internal sealed class OutputClassifierProvider : ClassifierProviderBase
	{
		#region Internal Constants

		internal const string ContentType = "Output";
		internal const string ClassifierName = "Output Highlight";

		#endregion

		#region Private Data Members

		private static readonly string[] KnownContentTypes = new[]
		{
			"Output",
			"BuildOrderOutput",
			"BuildOutput",
			"ConsoleOutput",
			"DatabaseOutput",
			"DebugOutput",
			"SourceControlOutput",
			"TestsOutput",
			"TFSourceControlOutput",
		};

		private static readonly object ResourceLock = new();
		private static IContentTypeRegistryService contentTypeRegistryService;

		#endregion

		#region Constructors

		public OutputClassifierProvider()
			: base(ClassifierName)
		{
		}

		#endregion

		#region Public Properties

		[Import]
		[SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "MEF requires an instance property.")]
		public IContentTypeRegistryService ContentTypeRegistryService
		{
			set
			{
				// We only need to set this once.
				lock (ResourceLock)
				{
					if (contentTypeRegistryService == null)
					{
						contentTypeRegistryService = value;
					}
				}
			}
		}

		#endregion

		#region Internal Methods

		internal static IEnumerable<string> GetOutputContentTypes()
		{
			// If no output windows have been displayed, then this may not be set yet.
			// We'll return a default set of content types if we have to.
			IContentTypeRegistryService registry;
			lock (ResourceLock)
			{
				registry = contentTypeRegistryService;
			}

			IEnumerable<string> result;
			if (registry == null)
			{
				result = KnownContentTypes;
			}
			else
			{
				result = registry.ContentTypes
					.Where(ct => Utilities.IsContentOfType(ct, ContentType))
					.Select(ct => ct.TypeName);
			}

			return result;
		}

		#endregion

		#region Protected Methods

		protected override ClassifierBase CreateClassifier(ITextBuffer buffer)
		{
			OutputClassifier result = new(buffer, this.ClassificationRegistry);
			return result;
		}

		#endregion
	}
}
