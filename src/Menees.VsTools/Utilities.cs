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
	using System.Windows.Input;
	using Menees.Shell;
	using Microsoft.VisualStudio.Shell;
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
					result = Language.HTML;
					if (!string.IsNullOrEmpty(fileName))
					{
						// Note: This code should never be reached as of VS 2012.  Now these languages have their own names.
						switch (Path.GetExtension(fileName).ToLower())
						{
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
				case "XAML":
				case "XOML":
					result = Language.XML;
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

		#endregion
	}
}
