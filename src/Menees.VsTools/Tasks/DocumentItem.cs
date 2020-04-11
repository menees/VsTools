namespace Menees.VsTools.Tasks
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Microsoft.VisualStudio.Editor;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.TextManager.Interop;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	internal static class DocumentItem
	{
		#region Public Methods

		public static Language GetLanguage(ITextBuffer buffer)
		{
			ITextDocument doc = GetTextDocument(buffer);
			string fileName = doc?.FilePath;
			Language result = GetDocumentLanguage(buffer.ContentType, fileName);
			return result;
		}

		public static ITextDocument GetTextDocument(ITextBuffer buffer)
		{
			// https://social.msdn.microsoft.com/Forums/vstudio/en-US/0f6ef03a-df6b-4670-856e-f4a539fbfbe1/how-get-document-name-of-an-iwpftextview
			ITextDocument result = null;

			if (buffer != null)
			{
				if (buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out ITextDocument document))
				{
					result = document;
				}
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static Language GetDocumentLanguage(IContentType contentType, string fileName)
		{
			Language result = Utilities.GetLanguage(contentType.TypeName, fileName);
			if (result == Language.Unknown)
			{
				foreach (IContentType baseType in contentType.BaseTypes)
				{
					result = GetDocumentLanguage(baseType, fileName);
					if (result != Language.Unknown)
					{
						break;
					}
				}
			}

			return result;
		}

		#endregion
	}
}
