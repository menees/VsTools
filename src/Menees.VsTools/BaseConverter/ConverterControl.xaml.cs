namespace Menees.VsTools.BaseConverter
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Navigation;
	using System.Windows.Shapes;
	using Microsoft.VisualStudio.Shell;

	#endregion

	/// <summary>
	/// Interaction logic for ConverterControl.xaml
	/// </summary>
	internal sealed partial class ConverterControl : UserControl
	{
		#region Private Data Members

		private readonly bool initialized;
		private TextBox currentTextBox;
		private bool updatingDisplay;

		#endregion

		#region Constructors

		internal ConverterControl()
		{
			this.InitializeComponent();

			this.currentTextBox = this.hexEdit;
			this.hexEdit.Tag = NumberBase.Hex;
			this.decimalEdit.Tag = NumberBase.Decimal;
			this.binaryEdit.Tag = NumberBase.Binary;
			this.initialized = true;
		}

		#endregion

		#region Private Properties

		private Options BaseConverterOptions
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				Options result = this.initialized ? MainPackage.BaseConverterOptions : null;
				return result;
			}
		}

		private NumberByteOrder CurrentByteOrder
		{
			get
			{
				NumberByteOrder result = (NumberByteOrder)this.byteOrder.SelectedIndex;
				return result;
			}

			set
			{
				this.byteOrder.SelectedIndex = (int)value;
			}
		}

		private NumberType CurrentNumberType
		{
			get
			{
				NumberType result = (NumberType)this.numberType.SelectedIndex;
				return result;
			}

			set
			{
				this.numberType.SelectedIndex = (int)value;
			}
		}

		#endregion

		#region Private Helper Methods

		private void UpdateFromCurrentTextBox()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			Options options = this.BaseConverterOptions;
			if (options != null)
			{
				NumberBase numBase = (NumberBase)this.currentTextBox.Tag;
				Converter num = new Converter(this.currentTextBox.Text, options, numBase, this.CurrentByteOrder, this.CurrentNumberType);

				// Update the display.
				bool previouslyUpdatingDisplay = this.updatingDisplay;
				this.updatingDisplay = true;
				try
				{
					if (this.hexEdit != this.currentTextBox)
					{
						this.hexEdit.Text = num.HexValue;
					}

					if (this.decimalEdit != this.currentTextBox)
					{
						this.decimalEdit.Text = num.DecimalValue;
					}

					if (this.binaryEdit != this.currentTextBox)
					{
						this.binaryEdit.Text = num.BinaryValue;
					}
				}
				finally
				{
					this.updatingDisplay = previouslyUpdatingDisplay;
				}
			}
		}

		#endregion

		#region Private Event Handlers

		private void BaseConverterControl_Loaded(object sender, RoutedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// The Loaded event will be raised again whenever the tool window tab is changed,
			// so we must make sure this event handler isn't called again.
			// http://www.hardcodet.net/2008/01/wpf-loaded-event-fired-repeatedly
			this.Loaded -= this.BaseConverterControl_Loaded;

			Options options = this.BaseConverterOptions;
			if (options != null)
			{
				this.CurrentByteOrder = options.BaseConverterByteOrder;
				this.CurrentNumberType = options.BaseConverterNumberType;

				// Any time the options are applied (even if they didn't change), update the text boxes.
				// This is cheaper/easier than doing a bunch of work to figure out which options changed.
				options.Applied += (s, a) =>
					{
						ThreadHelper.ThrowIfNotOnUIThread();
						this.UpdateFromCurrentTextBox();
					};
			}
			else
			{
				// The associated window's package should have been set by now.
				throw new InvalidOperationException("The base converter control can't be loaded without its associated options.");
			}
		}

		private void ByteOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			Options options = this.BaseConverterOptions;
			if (options != null)
			{
				if (this.CurrentByteOrder != options.BaseConverterByteOrder)
				{
					options.BaseConverterByteOrder = this.CurrentByteOrder;
					options.SaveSettingsToStorage();
					this.UpdateFromCurrentTextBox();
				}
			}
		}

		private void DataType_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			Options options = this.BaseConverterOptions;
			if (options != null)
			{
				if (this.CurrentNumberType != options.BaseConverterNumberType)
				{
					options.BaseConverterNumberType = this.CurrentNumberType;
					options.SaveSettingsToStorage();
					this.UpdateFromCurrentTextBox();
				}
			}
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (!this.updatingDisplay)
			{
				this.currentTextBox = sender as TextBox;
				this.UpdateFromCurrentTextBox();
			}
		}

		#endregion
	}
}