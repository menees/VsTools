namespace Menees.VsTools.Sort
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

	internal partial class SortLinesDialog : DialogWindow
	{
		#region Constructors

		public SortLinesDialog()
		{
			this.InitializeComponent();
		}

		#endregion

		#region Public Methods

		public bool Execute(Options options)
		{
			LineOptions lineOptions = options.LineOptions;
			this.caseSensitive.IsChecked = lineOptions.HasFlag(LineOptions.CaseSensitive);
			this.compareByOrdinal.IsChecked = lineOptions.HasFlag(LineOptions.ByOrdinal);
			this.descending.IsChecked = lineOptions.HasFlag(LineOptions.Descending);
			this.ignoreWhitespace.IsChecked = lineOptions.HasFlag(LineOptions.IgnoreWhitespace);
			this.ignorePunctuation.IsChecked = lineOptions.HasFlag(LineOptions.IgnorePunctuation);
			this.eliminateDuplicates.IsChecked = lineOptions.HasFlag(LineOptions.EliminateDuplicates);
			this.compareByLength.IsChecked = lineOptions.HasFlag(LineOptions.ByLength);
			this.onlyShowWhenShiftIsPressed.IsChecked = options.OnlyShowSortLinesDialogWhenShiftIsPressed;

			bool result = false;
			if (this.ShowModal().GetValueOrDefault())
			{
				lineOptions = LineOptions.None;
				void AddOption(CheckBox checkBox, LineOptions option)
				{
					if (checkBox.IsChecked.GetValueOrDefault())
					{
						lineOptions |= option;
					}
				}

				AddOption(this.caseSensitive, LineOptions.CaseSensitive);
				AddOption(this.compareByOrdinal, LineOptions.ByOrdinal);
				AddOption(this.descending, LineOptions.Descending);
				AddOption(this.ignoreWhitespace, LineOptions.IgnoreWhitespace);
				AddOption(this.ignorePunctuation, LineOptions.IgnorePunctuation);
				AddOption(this.eliminateDuplicates, LineOptions.EliminateDuplicates);
				AddOption(this.compareByLength, LineOptions.ByLength);
				options.LineOptions = lineOptions;

				options.OnlyShowSortLinesDialogWhenShiftIsPressed = this.onlyShowWhenShiftIsPressed.IsChecked.GetValueOrDefault();

				options.SaveSettingsToStorage();
				result = true;
			}

			return result;
		}

		#endregion

		#region Private Methods

		private void OkayButton_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		#endregion
	}
}
