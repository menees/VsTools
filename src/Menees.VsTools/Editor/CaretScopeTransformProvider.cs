namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System.ComponentModel.Composition;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.Text.Formatting;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	[Export(typeof(ILineTransformSourceProvider))]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	internal sealed class CaretScopeTransformProvider : ILineTransformSourceProvider
	{
		#region Public Methods

		public ILineTransformSource Create(IWpfTextView textView)
		{
			CaretScopeTransform result = textView.Properties.GetOrCreateSingletonProperty<CaretScopeTransform>(() => new CaretScopeTransform(textView));
			return result;
		}

		#endregion
	}
}
