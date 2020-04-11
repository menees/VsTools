namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Windows.Data;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;

	#endregion

	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Called via WPF Binding (via Reflection).")]
	internal sealed class ImageNameToSourceConverter : IValueConverter
	{
		#region Private Data Members

		private static readonly string UriPrefix = "pack://application:,,,/" + Assembly.GetExecutingAssembly().GetName().Name + ";component/Resources/Items/";

		#endregion

		#region Public Methods

		public static Uri CreateResourceUri(string imageName)
		{
			Uri result = new Uri(UriPrefix + imageName);
			return result;
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			ImageSource result = null;

			string text = value as string;
			if (!string.IsNullOrEmpty(text))
			{
				// For type images (e.g., class, enum, struct, interface, module) the name will be the first token
				// in the CodeMember.TypeDescription property.  So we have to parse it out.  That means that
				// C# type keywords will be in lowercase.
				string[] tokens = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length > 0)
				{
					string imageName = tokens[0];
					switch (imageName)
					{
						case "class":
						case "Class":
						case "enum":
						case "Enum":
						case "EnumItem":
						case "Event":
						case "Field":
						case "interface":
						case "Interface":
						case "Method":
						case "Module":
						case "Operator":
						case "Property":
						case "Structure":
							imageName += ".png";
							break;

						case "struct":
							imageName = "Structure.png";
							break;

						default:
							imageName = "Unknown.png";
							break;
					}

					Uri uri = CreateResourceUri(imageName);
					result = BitmapFrame.Create(uri);
				}
			}

			return result;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		#endregion
	}
}
