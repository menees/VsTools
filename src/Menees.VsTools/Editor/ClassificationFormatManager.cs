namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows.Media;
	using Microsoft;
	using Microsoft.VisualStudio;
	using Microsoft.VisualStudio.Editor;
	using Microsoft.VisualStudio.OLE.Interop;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;
	using Microsoft.VisualStudio.TextManager.Interop;
	using Microsoft.VisualStudio.Utilities;

	#endregion

	// Visual Studio puts all MEF EditorFormatDefinition-derived types in the "Text Editor" category.
	// However, the "Output Window" and "Find Results" have separate categories, and VS never
	// syncs the "Text Editor" colors to it.  Unfortunately, VS's MEF attributes don't provide a way
	// to change the category for our formats.  So this class jumps through a lot of hoops to try
	// to make the updated colors available for the non-TextEditor categories the next time VS is
	// restarted.  I can't find any way to force the other categories to notice the changes within
	// the current run of VS.  More info:
	// "Have you had fun with Fonts and Colors yet?" - http://blogs.msdn.com/b/dr._ex/archive/2005/06/03/425099.aspx
	// "VS 2010 MEF Extension: Fonts and Color settings cannot be changed for EditorFormatDefinition used in BuildOutput"
	// - http://connect.microsoft.com/VisualStudio/feedback/details/679678/
	// "Only default colors work" - http://vscoloroutput.codeplex.com/workitem/16
	internal sealed class ClassificationFormatManager : IVsTextManagerEvents
	{
		#region Private Data Members

		private readonly Dictionary<string, Guid> formatNameToCategoryMap = new();
		private readonly IVsFontAndColorStorage storage;
		private readonly IVsFontAndColorStorage2 storage2;

		#endregion

		#region Constructors

		public ClassificationFormatManager(System.IServiceProvider serviceProvider)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			this.storage = (IVsFontAndColorStorage)serviceProvider.GetService(typeof(SVsFontAndColorStorage));
			Assumes.Present(this.storage);
			this.storage2 = (IVsFontAndColorStorage2)this.storage;
			if (this.storage == null)
			{
				throw new InvalidOperationException("Unable to obtain an IVsFontAndColorStorage.");
			}

			// Note: textManager also implements IVsTextManager2 in case we need to call ResetColorableItems.
			IConnectionPointContainer textManager = (IConnectionPointContainer)serviceProvider.GetService(typeof(SVsTextManager));
			Assumes.Present(textManager);
			Guid interfaceGuid = typeof(IVsTextManagerEvents).GUID;
			textManager.FindConnectionPoint(ref interfaceGuid, out IConnectionPoint connectionPoint);
			if (connectionPoint == null)
			{
				throw new InvalidOperationException("The connection point was null.");
			}

			connectionPoint.Advise(this, out uint cookie);

			// Find all ClassificationFormatBase-derived types in this assembly, get their category GUIDs and names, and
			// save them for when we need to refresh a category's fonts and colors.
			IEnumerable<TypeInfo> formatTypes = typeof(ClassificationFormatManager).Assembly.DefinedTypes
				.Where(t => !t.IsNestedPrivate && t.IsSubclassOf(typeof(ClassificationFormatBase)));
			foreach (TypeInfo formatType in formatTypes)
			{
				// See if the format type or any of its containing types has an associated category.
				TypeInfo categoryType = formatType;
				while (categoryType != null)
				{
					var category = categoryType.GetCustomAttribute<ClassificationFormatCategoryAttribute>();
					if (category != null && category.Category != VsFormatCategory.TextEditor)
					{
						// We found a non-TextEditor category, so store it along with the format name.
						NameAttribute name = formatType.GetCustomAttribute<NameAttribute>();
						if (name != null)
						{
							string formatName = name.Name;
							this.formatNameToCategoryMap.Add(formatName, category.Guid);
						}

						break;
					}

					// Keep walking up the chain of declaring/containing types.
					categoryType = categoryType.DeclaringType?.GetTypeInfo();
				}
			}
		}

		#endregion

		#region Public Methods

		public void UpdateFormats()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Make sure we're not updating any categories from multiple threads at once.
			lock (this.formatNameToCategoryMap)
			{
				// Read all of the format info out of the TextEditor category.
				var formatInfo = this.ReadTextEditorCategoryFormats();
				if (formatInfo != null)
				{
					// For each other category, update all of its colors to match what was just read from the TextEditor category.
					foreach (var categoryGroup in this.formatNameToCategoryMap.GroupBy(pair => pair.Value))
					{
						this.UpdateOtherCategoryFormats(categoryGroup.Key, categoryGroup.Select(pair => pair.Key), formatInfo);
					}
				}
			}
		}

		#endregion

		#region Explicit IVsTextManagerEvents Methods

		void IVsTextManagerEvents.OnRegisterMarkerType(int markerType)
		{
		}

		void IVsTextManagerEvents.OnRegisterView(IVsTextView view)
		{
		}

		void IVsTextManagerEvents.OnUnregisterView(IVsTextView view)
		{
		}

		void IVsTextManagerEvents.OnUserPreferencesChanged(
			VIEWPREFERENCES[] viewPrefs,
			FRAMEPREFERENCES[] framePrefs,
			LANGPREFERENCES[] langPrefs,
			FONTCOLORPREFERENCES[] colorPrefs)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			if (colorPrefs != null && colorPrefs.Length > 0)
			{
				FONTCOLORPREFERENCES prefs = colorPrefs[0];
				Guid fontCategory = Marshal.PtrToStructure<Guid>(prefs.pguidFontCategory);
				if (fontCategory == DefGuidList.guidTextEditorFontCategory)
				{
					this.UpdateFormats();
				}
			}
		}

		#endregion

		#region Private Methods

		private static void CheckDefaultColors(ColorableItemInfo[] colorInfos)
		{
			ColorableItemInfo item = colorInfos[0];

			// If the colors are set to "Default" then that's for the TextEditor category NOT for the target category.
			// So crBackground might be 0x01000001, which is a White background (as a VS palette color index?).
			// Since Output and Find Result windows have different default colors (typically different background colors),
			// we can't just push the color information from the TextEditor category.
			//
			// Note: The ColorableItemInfo.crForeground and crBackground fields are documented as returning COLORREF,
			// which is a uint in the form 0x00BBGGRR.  However, VS packages can combine in the __VSCOLORTYPE flags
			// in the most significant byte, and VS applies those values in ways that don't seem to match the docs.
			// So we'll just ignore any "color" value that's not a standard Windows COLORREF.
			const uint ColorTypeMask = 0xFF000000;
			if (item.bBackgroundValid == 1 && (item.crBackground & ColorTypeMask) != 0)
			{
				item.bBackgroundValid = 0;
			}

			if (item.bForegroundValid == 1 && (item.crForeground & ColorTypeMask) != 0)
			{
				item.bForegroundValid = 0;
			}

			colorInfos[0] = item;
		}

		private IDictionary<string, ColorableItemInfo[]> ReadTextEditorCategoryFormats()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			Dictionary<string, ColorableItemInfo[]> result = null;

			// Leave out items where the colors are all defaults.
			using (IDisposable category = this.OpenCategory(DefGuidList.guidTextEditorFontCategory, __FCSTORAGEFLAGS.FCSF_READONLY))
			{
				// If no fonts and colors have been changed from their defaults, then VS won't open the
				// category for us since we specified FCSF_READONLY without FCSF_LOADDEFAULTS.
				if (category != null)
				{
					result = new Dictionary<string, ColorableItemInfo[]>();

					foreach (var pair in this.formatNameToCategoryMap)
					{
						string itemName = pair.Key;

						ColorableItemInfo[] colorInfos = new ColorableItemInfo[1];

						// As of VS 2015, GetItem will fail and return REGDB_E_KEYMISSING (0x80040152)
						// if the item hasn't been customized before and has no associated registry key.
						if (ErrorHandler.Succeeded(this.storage.GetItem(itemName, colorInfos)))
						{
							CheckDefaultColors(colorInfos);
							result.Add(itemName, colorInfos);
						}
					}
				}
			}

			return result;
		}

		private void UpdateOtherCategoryFormats(Guid categoryId, IEnumerable<string> itemNames, IDictionary<string, ColorableItemInfo[]> formatInfo)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			using (IDisposable category = this.OpenCategory(categoryId, __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES))
			{
				if (category != null)
				{
					foreach (string itemName in itemNames)
					{
						// Get rid of any earlier customizations since the caller selectively filtered the formatInfo
						// to contain only the non-default info that we'll need to apply after the default.
						if (ErrorHandler.Succeeded(this.storage2.RevertItemToDefault(itemName)))
						{
							if (formatInfo.TryGetValue(itemName, out ColorableItemInfo[] colorInfos) && colorInfos != null && colorInfos.Length == 1)
							{
								// Ignore the result if this fails.
								ErrorHandler.Succeeded(this.storage.SetItem(itemName, colorInfos));
							}
						}
					}
				}
			}

			// Note: I tried to force VS to refresh by using IVsFontAndColorCacheManager's ClearCache and RefreshCache methods,
			// but neither had a visible, positive effect.  Also, sometimes ClearCache would cause a FileNotFoundException during
			// startup (if it was called before the cache was initialized?).
		}

		private IDisposable OpenCategory(Guid category, __FCSTORAGEFLAGS flags)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			int hr = this.storage.OpenCategory(category, (uint)flags);

			// OpenCategory usually succeeds, but it can fail with REGDB_E_KEYMISSING (0x80040152) if flags equals FCSF_READONLY
			// and doesn't include FCSF_LOADDEFAULTS and the underlying registry key hasn't been created yet (e.g., if the user hasn't
			// customized any fonts or colors). In that case the IVsFontAndColorStorage provider has no registry key to open and will
			// return an error, and we can just return null.
			IDisposable result = null;
			if (ErrorHandler.Succeeded(hr))
			{
				// When calling CloseCategory we'll just ignore the returned HRESULT.
				result = new Disposer(() =>
				{
					ThreadHelper.ThrowIfNotOnUIThread();
					ErrorHandler.Succeeded(this.storage.CloseCategory());
				});
			}

			return result;
		}

		#endregion
	}
}
