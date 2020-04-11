namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Classification;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	internal sealed class OutputClassifier : ClassifierBase
	{
		#region Private Data Members

		private static readonly Dictionary<OutputHighlightType, IClassificationType> Classifications =
			new Dictionary<OutputHighlightType, IClassificationType>();

		private IContentType contentType;
		private List<Highlight> highlights;

		#endregion

		#region Constructors

		public OutputClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
			: base(buffer)
		{
			this.contentType = buffer.ContentType;

			lock (Classifications)
			{
				if (Classifications.Count == 0)
				{
					Classifications.Add(OutputHighlightType.None, null);
					Classifications.Add(OutputHighlightType.Error, registry.GetClassificationType(OutputFormats.ErrorFormat.ClassificationName));
					Classifications.Add(OutputHighlightType.Warning, registry.GetClassificationType(OutputFormats.WarningFormat.ClassificationName));
					Classifications.Add(OutputHighlightType.Information, registry.GetClassificationType(OutputFormats.InformationFormat.ClassificationName));
					Classifications.Add(OutputHighlightType.Detail, registry.GetClassificationType(OutputFormats.DetailFormat.ClassificationName));
					Classifications.Add(OutputHighlightType.Header, registry.GetClassificationType(OutputFormats.HeaderFormat.ClassificationName));
					Classifications.Add(OutputHighlightType.Custom1, registry.GetClassificationType(OutputFormats.Custom1Format.ClassificationName));
					Classifications.Add(OutputHighlightType.Custom2, registry.GetClassificationType(OutputFormats.Custom2Format.ClassificationName));
				}
			}
		}

		#endregion

		#region Protected Methods

		protected override void GetClassificationSpans(List<ClassificationSpan> result, SnapshotSpan span, Options options)
		{
			if (this.CacheHighlights(options))
			{
				foreach (ITextSnapshotLine line in GetSpanLines(span))
				{
					string text = line.GetText();
					if (!string.IsNullOrEmpty(text))
					{
						Highlight highlight = this.highlights.FirstOrDefault(h => h.Pattern.IsMatch(text));
						if (highlight != null)
						{
							// If the matched highlight type is None, then we'll get a null classificationType and not classify this line.
							if (Classifications.TryGetValue(highlight.HighlightType, out IClassificationType classificationType) && classificationType != null)
							{
								result.Add(new ClassificationSpan(line.Extent, classificationType));
							}
						}
					}
				}
			}
		}

		protected override bool ReadOptions(string changedOptionId)
		{
			// Clear the cached highlights so they'll be re-cached on the next classification.
			this.highlights = null;
			return base.ReadOptions(changedOptionId);
		}

		protected override void ContentTypeChanged(ITextBuffer buffer, ContentTypeChangedEventArgs e)
		{
			base.ContentTypeChanged(buffer, e);

			// Update the content type and force the options to be re-cached.
			this.contentType = e.AfterContentType;
			this.OptionsChanged(null);
		}

		#endregion

		#region Private Methods

		private bool CacheHighlights(Options options)
		{
			bool result = options != null && options.HighlightOutputText;

			if (result)
			{
				if (this.highlights == null)
				{
					this.highlights = options.OutputHighlights.Select(h => Highlight.TryCreate(h, this.contentType)).Where(h => h != null).ToList();
				}

				result = this.highlights.Count > 0;
			}

			return result;
		}

		#endregion

		#region Private Types

		private sealed class Highlight
		{
			#region Constructors

			private Highlight()
			{
				// Use TryCreate.
			}

			#endregion

			#region Public Properties

			public OutputHighlightType HighlightType { get; private set; }

			public Regex Pattern { get; private set; }

			#endregion

			#region Public Methods

			public static Highlight TryCreate(OutputHighlight highlight, IContentType contentType)
			{
				Highlight result = null;

				// We shouldn't encounter an empty pattern, but we'll check for it anyway
				// since we can't really highlight empty lines.
				if (!string.IsNullOrEmpty(highlight.Pattern))
				{
					// We don't need to validate against OutputClassifierProvider.GetOutputContentTypes() here.
					// The OutputHighlight class already tries to do that.  Here we're passed a known valid type,
					// and we just need to see if it's in the OutputHighlight's pre-verified list.
					if (highlight.ContentTypes.Any(ct => Utilities.IsContentOfType(contentType, ct)))
					{
						Regex pattern = new Regex(highlight.Pattern, highlight.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
						result = new Highlight
							{
								HighlightType = highlight.Highlight,
								Pattern = pattern,
							};
					}
				}

				return result;
			}

			#endregion
		}

		#endregion
	}
}
