namespace Menees.VsTools
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Microsoft.VisualStudio.Shell;

	#endregion

	internal abstract class OptionsBase : DialogPage
	{
		#region Constructors

		protected OptionsBase()
		{
		}

		#endregion

		#region Public Events

		public event EventHandler Applied;

		#endregion

		#region Public Methods

		public static string[] SplitValues(string multiLineValue)
		{
			string lines = multiLineValue ?? string.Empty;
			string[] result = lines.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			return result;
		}

		// Raise an event so non-modal windows like BaseConverterControl
		// can get a notification that they may need to update.
		public void Apply() => this.Applied?.Invoke(this, EventArgs.Empty);

		#endregion

		#region Protected Methods

		protected override void OnApply(PageApplyEventArgs e)
		{
			base.OnApply(e);

			if (e.ApplyBehavior == ApplyKind.Apply)
			{
				this.Apply();
			}
		}

		#endregion
	}
}
