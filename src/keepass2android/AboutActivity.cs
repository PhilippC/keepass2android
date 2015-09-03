using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;

namespace keepass2android
{
	[Activity(Label = "@string/app_name",
		ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
		Theme = "@style/NoTitleBar")]
	[IntentFilter(new[] { "kp2a.action.AboutActivity" }, Categories = new[] { Intent.CategoryDefault })]
	public class AboutActivity: Activity, IDialogInterfaceOnDismissListener
	{
		private AboutDialog _dialog;

		protected override void OnResume()
		{
			if ((_dialog == null) || (_dialog.IsShowing == false))
			{
				if (new ActivityDesign(this).UseDarkTheme)
					_dialog = new AboutDialog(this, Android.Resource.Style.ThemeHoloNoActionBarFullscreen);
				else
					_dialog = new AboutDialog(this, Android.Resource.Style.ThemeHoloLightNoActionBarFullscreen);
				_dialog.SetOnDismissListener(this);
				_dialog.Show();
			}
			base.OnResume();
		}

		public void OnDismiss(IDialogInterface dialog)
		{
			Finish();
		}
	}
}
