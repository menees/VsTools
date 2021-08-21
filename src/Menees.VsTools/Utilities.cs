namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Input;
	using System.Windows.Media;
	using Menees.Shell;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.ComponentModelHost;
	using Microsoft.VisualStudio.Editor;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.TextManager.Interop;
	using Microsoft.VisualStudio.Utilities;
	using VS = EnvDTE;

	#endregion

	internal static class Utilities
	{
		#region Public Properties

		public static bool IsShiftPressed
		{
			get
			{
				bool result = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
				return result;
			}
		}

		#endregion

		#region Public Methods

		public static Language GetLanguage(VS.Document doc)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			Language result = Language.Unknown;

			if (doc != null)
			{
				result = GetLanguage(doc.Language, doc.FullName);

				// I'd like to get the "current" (based on the caret position) ITextBuffer's ContentType.
				// That would allow for better Language decisions in multi-language files like .razor,
				// which allows C#, HTML, and CSS. But I can't figure out a way to get the current
				// caret position's ITextBuffer. This started from https://stackoverflow.com/a/7373385/1882616.
				if ((result == Language.Unknown || result == Language.PlainText)
					&& doc.DTE is Microsoft.VisualStudio.OLE.Interop.IServiceProvider oleServiceProvider
					&& !string.IsNullOrEmpty(doc.FullName))
				{
					using (var serviceProvider = new ServiceProvider(oleServiceProvider))
					{
						if (VsShellUtilities.IsDocumentOpen(
							serviceProvider,
							doc.FullName,
							Guid.Empty,
							out _,
							out _,
							out IVsWindowFrame windowFrame))
						{
							IVsTextView view = VsShellUtilities.GetTextView(windowFrame);
							if (view.GetBuffer(out IVsTextLines lines) == VSConstants.S_OK && lines is IVsTextBuffer vsBuffer)
							{
								IComponentModel componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
								IVsEditorAdaptersFactoryService adapterFactory = componentModel.GetService<IVsEditorAdaptersFactoryService>();
								ITextBuffer buffer = adapterFactory.GetDataBuffer(vsBuffer);
								string contentType = buffer?.ContentType?.TypeName;
								Language contentLanguage = GetLanguage(contentType, doc.FullName);
								if (contentLanguage != Language.Unknown)
								{
									result = contentLanguage;
								}
							}
						}
					}
				}
			}

			return result;
		}

		public static Language GetLanguage(string languageOrContentType, string fileName)
		{
			Language result = Language.Unknown;

			switch (languageOrContentType)
			{
				case "C/C++":
					result = Language.CPlusPlus;
					if (!string.IsNullOrEmpty(fileName))
					{
						switch (Path.GetExtension(fileName).ToLower())
						{
							case ".idl":
							case ".odl":
								result = Language.IDL;
								break;
						}
					}

					break;

				case "HTML":
				case "HTMLX": // Added in VS 2013 for non-Web Forms HTML editing
				case "htmlx":
				case "HTMLXProjection": // Seen in VS 2019 for .razor files.
					result = Language.HTML;
					if (!string.IsNullOrEmpty(fileName))
					{
						switch (Path.GetExtension(fileName).ToLower())
						{
							case ".razor":
								result = Language.Razor;
								break;

							case ".vbs":
								// VS dropped support for VBScript in VS 2008, but added it back in VS 2008 SP1.
								// http://blogs.msdn.com/b/webdev/archive/2008/05/12/visual-studio-2008-sp1-beta.aspx
								result = Language.VBScript;
								break;

							case ".js":
								// Older versions of VS treated JScript as HTML, but by VS 2012 JavaScript had its own language service.
								result = Language.JavaScript;
								break;
						}
					}

					break;

				case "XML":
					result = Language.XML;
					break;

				case "XAML":
				case "XOML":
					result = Language.XAML;
					break;

				case "Basic":
					result = Language.VB;
					break;

				case "CSharp":
					result = Language.CSharp;
					break;

				case "CSS":
				case "css":
					result = Language.CSS;
					break;

				case "Plain Text":
				case "plaintext":
					result = Language.PlainText;
					break;

				case "PowerShell":
					result = Language.PowerShell;
					break;

				case "Python":
					result = Language.Python;
					break;

				case "F#":
				case "FSharpInteractive":
					result = Language.FSharp;
					break;

				case "JavaScript":
				case "javascript":
				case "node.js":
					result = Language.JavaScript;
					break;

				case "TypeScript":
					result = Language.TypeScript;
					break;

				case "VBScript":
				case "vbscript":
					result = Language.VBScript;
					break;

				default: // PL/SQL, SQL, T-SQL, T-SQL7, T-SQL80, T-SQL90, SQL Server Tools, etc.
					if (!string.IsNullOrEmpty(languageOrContentType) && languageOrContentType.IndexOf("SQL") >= 0)
					{
						result = Language.SQL;
					}

					break;
			}

			return result;
		}

		public static bool ShellExecute(string fileName)
		{
			bool result = false;
			try
			{
				using (Process process = ShellUtility.ShellExecute(null, fileName))
				{
					result = true;
				}
			}
			catch (Win32Exception)
			{
				result = false;
			}
			catch (FileNotFoundException)
			{
				result = false;
			}

			return result;
		}

		public static bool IsContentOfType(IContentType actualType, string typeNameToCheck)
		{
			bool result = actualType.IsOfType(typeNameToCheck)
				|| string.Equals(actualType.TypeName, typeNameToCheck, StringComparison.OrdinalIgnoreCase);
			return result;
		}

		public static TItem GetItemTarget<TItem>(Visual visual, Point point)
			where TItem : class
		{
			TItem result = null;

			// http://stackoverflow.com/questions/3788337/how-to-get-item-under-cursor-in-wpf-listview
			HitTestResult hit = VisualTreeHelper.HitTest(visual, point);
			if (hit != null)
			{
				DependencyObject dependencyObject = hit.VisualHit;
				if (dependencyObject != null)
				{
					do
					{
						result = dependencyObject as TItem;
						dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
					}
					while (result == null && dependencyObject != null);
				}
			}

			return result;
		}

		#endregion
	}
}
