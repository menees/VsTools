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

	internal partial class SortDialog : DialogWindow
	{
		#region Constructors

		public SortDialog()
		{
			this.InitializeComponent();
		}

		#endregion

		#region Public Methods

		public bool Execute(Options options)
		{
			this.caseSensitive.IsChecked = options.SortCaseSensitive;
			this.compareByOrdinal.IsChecked = options.SortCompareByOrdinal;
			this.ascending.IsChecked = options.SortAscending;
			this.ignoreWhitespace.IsChecked = options.SortIgnoreWhitespace;
			this.ignorePunctuation.IsChecked = options.SortIgnorePunctuation;
			this.eliminateDuplicates.IsChecked = options.SortEliminateDuplicates;

			bool result = false;
			if (this.ShowModal().GetValueOrDefault())
			{
				options.SortCaseSensitive = this.caseSensitive.IsChecked.GetValueOrDefault();
				options.SortCompareByOrdinal = this.compareByOrdinal.IsChecked.GetValueOrDefault();
				options.SortAscending = this.ascending.IsChecked.GetValueOrDefault();
				options.SortIgnoreWhitespace = this.ignoreWhitespace.IsChecked.GetValueOrDefault();
				options.SortIgnorePunctuation = this.ignorePunctuation.IsChecked.GetValueOrDefault();
				options.SortEliminateDuplicates = this.eliminateDuplicates.IsChecked.GetValueOrDefault();
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
