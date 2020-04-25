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

		#region Protected Methods

		protected override void OnApply(PageApplyEventArgs e)
		{
			base.OnApply(e);

			// Raise an event so non-modal windows like BaseConverterControl
			// can get a notification that they may need to update.
			if (e.ApplyBehavior == ApplyKind.Apply && this.Applied != null)
			{
				EventHandler eh = this.Applied;
				eh(this, e);
			}
		}

		#endregion
	}
}
