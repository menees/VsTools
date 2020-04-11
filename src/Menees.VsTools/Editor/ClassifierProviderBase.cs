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
	using Microsoft.VisualStudio.Text.Editor;

	#endregion

	// Note: Derived classes must still specify the attributes.
	internal abstract class ClassifierProviderBase : IClassifierProvider
	{
		#region Private Data Members

		private readonly string classifierName;

		#endregion

		#region Constructors

		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Created by MEF.")]
		protected ClassifierProviderBase(string classifierName)
		{
			this.classifierName = classifierName;
		}

		#endregion

		#region Internal Properties

		/// <summary>
		/// Import the classification registry to be used for getting a reference
		/// to the custom classification type later.
		/// </summary>
		[Import]
		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
		internal IClassificationTypeRegistryService ClassificationRegistry { get; set; }

		/// <summary>
		/// Import the service so we can get the editor options for a text buffer.
		/// </summary>
		[Import]
		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Used by MEF.")]
		internal IEditorOptionsFactoryService EditorOptionsFactory { get; set; }

		#endregion

		#region Public Methods

		public IClassifier GetClassifier(ITextBuffer buffer)
		{
			IClassifier result = buffer.Properties.GetOrCreateSingletonProperty<ClassifierBase>(this.classifierName, () => this.CreateClassifier(buffer));
			return result;
		}

		#endregion

		#region Internal Methods

		internal static ClassifierBase TryGetClassifier(string classifierName, ITextBuffer buffer)
		{
			buffer.Properties.TryGetProperty(classifierName, out ClassifierBase result);
			return result;
		}

		#endregion

		#region Protected Methods

		protected abstract ClassifierBase CreateClassifier(ITextBuffer buffer);

		#endregion
	}
}
