namespace Menees.VsTools.BaseConverter
{
	#region Using Directives

	using System;
	using System.Collections;
	using System.ComponentModel;
	using System.Data;
	using System.Diagnostics.CodeAnalysis;
	using System.Drawing;
	using System.Runtime.InteropServices;
	using System.Windows;
	using Microsoft.VisualStudio.Shell;
	using Microsoft.VisualStudio.Shell.Interop;

	#endregion

	/// <summary>
	/// This class implements the tool window exposed by this package and hosts a user control.
	///
	/// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
	/// usually implemented by the package implementer.
	///
	/// This class derives from the ToolWindowPane class provided from the MPF in order to use its
	/// implementation of the IVsUIElementPane interface.
	/// </summary>
	[Guid(Guids.BaseConverterWindowPersistanceString)]
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by VS.")]
	internal sealed class Window : ToolWindowPane
	{
		#region Internal Constants

		internal const string DefaultCaption = "Base Converter";

		#endregion

		#region Constructors

		/// <summary>
		/// Standard constructor for the tool window.
		/// </summary>
		public Window()
			: base(null)
		{
			this.Caption = DefaultCaption;

			// Set the image that will appear on the tab of the window frame
			// when docked with an other window
			// The resource ID correspond to the one defined in the resx file
			// while the Index is the offset in the bitmap strip. Each image in
			// the strip being 16x16.
			const int ResourceId = 301;
			this.BitmapResourceID = ResourceId; // In VSPackage.resx, the Images.png file is an image resource named "301".
			const int ImageIndex = 11;
			this.BitmapIndex = ImageIndex; // This is a 1-based image index.  From <GuidSymbol name="Images"> in Menees.VsTools.vsct.

			// This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
			// we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
			// the object returned by the Content property.
			this.Content = new ConverterControl();
		}

		public Window(string unusedAsyncConstructorParam)
			: this()
		{
			// See explanation for the unused string parameter for AsyncPackages creating async tool windows.
			// https://github.com/Microsoft/VSSDK-Analyzers/blob/master/doc/VSSDK003.md
			unusedAsyncConstructorParam.ToString();
		}

		#endregion
	}
}
