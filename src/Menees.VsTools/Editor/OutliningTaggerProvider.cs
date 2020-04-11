namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel.Composition;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Menees.VsTools.Tasks;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Tagging;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	[Export(typeof(ITaggerProvider))]
	[TagType(typeof(IOutliningRegionTag))]
	[ContentType("XML")]
	internal sealed class OutliningTaggerProvider : ITaggerProvider
	{
		#region Public Methods

		public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
			where T : ITag
		{
			OutliningTagger CreateBufferTagger()
			{
				Language language = DocumentItem.GetLanguage(buffer);
				ScanInfo.TryGet(language, out ScanInfo scanInfo);
				return new OutliningTagger(buffer, scanInfo);
			}

			OutliningTagger result = buffer.Properties.GetOrCreateSingletonProperty(CreateBufferTagger);
			return result as ITagger<T>;
		}

		#endregion
	}
}
