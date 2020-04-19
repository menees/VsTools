namespace Menees.VsTools.Tasks
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
	using System.Windows;
	using System.Windows.Data;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using Microsoft.VisualStudio.Shell;

	#endregion

	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created via XAML.")]
	internal sealed class PriorityImageConverter : IValueConverter
	{
		#region Public Methods

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			ImageSource result = null;

			TaskPriority? priority = value as TaskPriority?;
			if (priority != null)
			{
				string imageName = null;
				switch (priority)
				{
					case TaskPriority.High:
						imageName = "PriorityHigh.png";
						break;

					case TaskPriority.Normal:
						imageName = "PriorityNormal.png";
						break;

					case TaskPriority.Low:
						imageName = "PriorityLow.png";
						break;
				}

				if (!string.IsNullOrEmpty(imageName))
				{
					Uri uri = ImageNameToSourceConverter.CreateResourceUri(imageName);
					result = BitmapFrame.Create(uri);
				}
			}

			return result;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;

		#endregion
	}
}
