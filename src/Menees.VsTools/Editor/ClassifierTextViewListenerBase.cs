namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel.Composition;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Editor;

	#endregion

	// Note: Derived classes must still specify the attributes.
	internal abstract class ClassifierTextViewListenerBase : IWpfTextViewConnectionListener
	{
		#region Private Data Members

		private readonly string classifierName;

		#endregion

		#region Constructors

		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Created by MEF.")]
		protected ClassifierTextViewListenerBase(string classifierName)
		{
			this.classifierName = classifierName;
		}

		#endregion

		#region Public Methods

		public void SubjectBuffersConnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
		{
		}

		public void SubjectBuffersDisconnected(IWpfTextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
		{
			// When the TextView closes, we need to detach all buffer classifiers from the various OptionsChanged
			// events they've advised on in order for everything to clean up properly.  Otherwise, global objects like
			// MainPackage.Instance.Options would end up with event handler references that would keep every
			// ClassifierBase alive for the life of the process.
			if (reason == ConnectionReason.TextViewLifetime)
			{
				foreach (ITextBuffer buffer in subjectBuffers)
				{
					ClassifierBase classifier = ClassifierProviderBase.TryGetClassifier(this.classifierName, buffer);
					if (classifier != null)
					{
						classifier.Dispose();
					}
				}
			}
		}

		#endregion
	}
}
