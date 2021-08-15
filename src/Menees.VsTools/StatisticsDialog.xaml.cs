namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Data;
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

	internal partial class StatisticsDialog : DialogWindow
	{
		#region Constructors

		public StatisticsDialog()
		{
			this.InitializeComponent();
		}

		#endregion

		#region Public Methods

		public void Execute(string text)
		{
			int lineCount = 0;
			int characterCount = 0;
			Dictionary<char, int> counts = new();

			// Process the text.
			if (!string.IsNullOrEmpty(text))
			{
				// The line counting scheme is very simplistic:
				// 1. Add 1 for every linefeed character we see.
				// 2. Add 1 more unless the last char was a linefeed.
				char previous = '\n';
				foreach (char ch in text)
				{
					characterCount++;
					counts.TryGetValue(ch, out int currentCharCount);
					counts[ch] = ++currentCharCount;

					if (ch == '\n')
					{
						lineCount++;
					}

					previous = ch;
				}

				if (previous != '\n')
				{
					lineCount++;
				}
			}

			// Display the results
			this.lineCountLabel.Content = lineCount.ToString();
			this.characterCountLabel.Content = characterCount.ToString();
			using (DataTable table = new())
			{
				DataColumn numberColumn = table.Columns.Add("Number", typeof(int));
				DataColumn characterColumn = table.Columns.Add("Character", typeof(string));
				DataColumn countColumn = table.Columns.Add("Count", typeof(int));

				foreach (var pair in counts.OrderBy(p => p.Key))
				{
					char ch = pair.Key;
					DataRow row = table.NewRow();
					row[numberColumn] = (int)ch;
					row[characterColumn] = GetDisplayValue(ch);
					row[countColumn] = pair.Value;
					table.Rows.Add(row);
				}

				this.countGrid.ItemsSource = table.DefaultView;
				this.ShowModal();
			}
		}

		#endregion

		#region Private Methods

		private static string GetDisplayValue(char ch)
		{
			string result;

			// Give back the C# escaped notation and "name" for these special characters.
			// We don't want the DataGrid displaying weird boxes or multi-line cells.
			switch (ch)
			{
				case '\0':
					result = @"\0 - Null";
					break;

				case '\a':
					result = @"\a - Alert";
					break;

				case '\b':
					result = @"\b - Bksp";
					break;

				case '\f':
					result = @"\f - FF";
					break;

				case '\n':
					result = @"\n - LF";
					break;

				case '\r':
					result = @"\r - CR";
					break;

				case '\t':
					result = @"\t - Tab";
					break;

				case '\v':
					result = @"\v - VT";
					break;

				case ' ':
					result = "  - Space";
					break;

				default:
					result = ch.ToString();
					break;
			}

			return result;
		}

		#endregion
	}
}
