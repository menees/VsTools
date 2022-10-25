namespace Menees.VsTools.Regions
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel.Composition;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Menees.VsTools.Tasks;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Tagging;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	// html is listed for the old web forms HTML editor (not the new htmlx content type from VS 2013 update 4).
	// VS 2019's built-in XML and XAML region support is weak. It's space-sensitive and doesn't show the region name or tooltip.
	[Export(typeof(ITaggerProvider))]
	[TagType(typeof(IOutliningRegionTag))]
	[ContentType("html")]
	[ContentType("Python")]
	[ContentType("SQL Server Tools")]
	[ContentType("T-SQL90")]
	[ContentType("XAML")]
	[ContentType("XML")]
	[ContentType("XOML")]
	[ContentType("CSS")]
	[ContentType("LESS")]
	[ContentType("SCSS")]
	internal sealed class OutliningTaggerProvider : ITaggerProvider
	{
		#region Public Methods

		public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
			where T : ITag
		{
			ITagger<IOutliningRegionTag> CreateBufferTagger()
			{
				Language language = DocumentItem.GetLanguage(buffer);

				ThreadHelper.ThrowIfNotOnUIThread();
				ITagger<IOutliningRegionTag> tagger;
				if (MainPackage.RegionOptions.IsSupported(language) && ScanInfo.TryGet(language, out ScanInfo scanInfo))
				{
					tagger = new OutliningTagger(buffer, scanInfo);
				}
				else
				{
					tagger = NonOutliningTagger.Instance;
				}

				return tagger;
			}

			ITagger<IOutliningRegionTag> result = buffer.Properties.GetOrCreateSingletonProperty(CreateBufferTagger);
			return result as ITagger<T>;
		}

		#endregion
	}
}
