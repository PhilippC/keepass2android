using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using KeePassLib.Security;

namespace keepass2android
{
	[Service]
	public class CopyToClipboardService: Service
	{
		public CopyToClipboardService()
		{
			
		}

		public CopyToClipboardService(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}


		public static void CopyValueToClipboardWithTimeout(Context ctx, string text)
		{
			Toast.MakeText(ctx, text, ToastLength.Short).Show();
		}

		public override IBinder OnBind(Intent intent)
		{
			return null;
		}
	}
}