using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	[Activity(Label = AppNames.AppName)]
	public class AppKilledInfo : Activity, IDialogInterfaceOnDismissListener
	{
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			//as we expect this to happen only rarely (having a foreground service running when unlocked),
			//we don't try to handle this better
			//But at least explain to the user what happened!
			((NotificationManager)GetSystemService(Context.NotificationService)).CancelAll();
			AlertDialog.Builder b = new AlertDialog.Builder(this);
			b.SetMessage(Resource.String.killed_by_os);
			b.SetPositiveButton(Android.Resource.String.Ok, delegate
			{
				Intent i = new Intent(this, typeof(SelectCurrentDbActivity));
				i.AddFlags(ActivityFlags.ClearTask | ActivityFlags.NewTask);
				StartActivity(i);

			});
			b.SetNegativeButton(Resource.String.cancel, delegate { });
			b.SetTitle(GetString(AppNames.AppNameResource));
			
			var dialog = b.Create();
			dialog.SetOnDismissListener(this);
			dialog.Show();
		}

		public void OnDismiss(IDialogInterface dialog)
		{
			Finish();
		}
	}
}