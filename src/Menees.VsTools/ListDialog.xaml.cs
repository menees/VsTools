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

	internal partial class ListDialog : DialogWindow
	{
		#region Constructors

		public ListDialog()
		{
			this.InitializeComponent();
		}

		#endregion

		#region Public Methods

		public string Execute(string title, string prompt, string itemName, string[] items, string initialSelection)
		{
			this.Title = title;
			this.prompt.Content = prompt;
			this.itemColumn.Header = itemName;
			this.editBox.Text = initialSelection;

			int numItems = items.Length;
			for (int i = 0; i < numItems; i++)
			{
				this.list.Items.Add(new Tuple<int, string>(i + 1, items[i]));
			}

			this.UpdateControlStates();

			string result = null;
			if (this.ShowModal().GetValueOrDefault())
			{
				result = TranslateName(this.editBox.Text, items);
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static string TranslateName(string name, string[] items)
		{
			string result;

			// See if they entered a 1-based item number.
			if (int.TryParse(name, out int number) && number >= 1 && number <= items.Length)
			{
				result = items[number - 1];
			}
			else
			{
				result = name.Trim();
			}

			return result;
		}

		private void UpdateControlStates()
		{
			this.okayButton.IsEnabled = this.editBox.Text.Trim().Length > 0;
		}

		#endregion

		#region Private Event Handlers

		private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (this.list.SelectedItems.Count > 0)
			{
				var item = (Tuple<int, string>)this.list.SelectedItems[0];
				this.editBox.Text = item.Item2;
			}
			else
			{
				this.editBox.Clear();
			}
		}

		private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			this.UpdateControlStates();

			if (this.list.SelectedItems.Count > 0 && this.okayButton.IsEnabled)
			{
				this.DialogResult = true;
			}
		}

		private void OkayButton_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void EditBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			this.UpdateControlStates();
		}

		#endregion
	}
}
