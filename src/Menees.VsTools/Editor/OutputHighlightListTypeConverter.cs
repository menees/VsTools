namespace Menees.VsTools.Editor
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Web.Script.Serialization;

	#endregion

	// I tried to make a generic JsonTypeConverter<T>, but the PropertyGrid just silently ignored it.
	[SuppressMessage("Internal class never created.", "CA1812", Justification = "Created via reflection by PropertyGrid when editing Options instance.")]
	internal sealed class OutputHighlightListTypeConverter : TypeConverter
	{
		#region Public Methods

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			bool result = sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
			return result;
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			bool result = destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
			return result;
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			object result;
			if (value is string text)
			{
				JavaScriptSerializer serializer = new JavaScriptSerializer();
				result = serializer.Deserialize<List<OutputHighlight>>(text);
			}
			else
			{
				result = base.ConvertFrom(context, culture, value);
			}

			return result;
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			object result;
			if (destinationType == typeof(string))
			{
				JavaScriptSerializer serializer = new JavaScriptSerializer();
				result = serializer.Serialize(value);
			}
			else
			{
				result = base.ConvertTo(context, culture, value, destinationType);
			}

			return result;
		}

		#endregion
	}
}
