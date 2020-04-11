namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Shapes;
	using Microsoft.VisualStudio.PlatformUI;

	#endregion

	// VS extension modal dialog boxes: http://msdn.microsoft.com/en-us/library/ff770546.aspx
	internal partial class TrimDialog : DialogWindow
	{
		#region Constructors

		public TrimDialog()
		{
			this.InitializeComponent();
		}

		#endregion

		#region Public Methods

		public bool Execute(Options options)
		{
			this.trimStart.IsChecked = options.TrimStart;
			this.trimEnd.IsChecked = options.TrimEnd;
			this.onlyShowWhenShiftIsPressed.IsChecked = options.OnlyShowTrimDialogWhenShiftIsPressed;
			this.UpdateControlStates();

			bool result = false;
			if (this.ShowModal().GetValueOrDefault())
			{
				options.TrimStart = this.trimStart.IsChecked.GetValueOrDefault();
				options.TrimEnd = this.trimEnd.IsChecked.GetValueOrDefault();
				options.OnlyShowTrimDialogWhenShiftIsPressed = this.onlyShowWhenShiftIsPressed.IsChecked.GetValueOrDefault();
				options.SaveSettingsToStorage();
				result = true;
			}

			return result;
		}

		#endregion

		#region Private Methods

		private void TrimCheckBoxChanged(object sender, RoutedEventArgs e)
		{
			this.UpdateControlStates();
		}

		private void OkayButton_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void UpdateControlStates()
		{
			this.okayButton.IsEnabled = this.trimStart.IsChecked.GetValueOrDefault() || this.trimEnd.IsChecked.GetValueOrDefault();
		}

		#endregion
	}
}
