using Android.Content;
using Android.Widget;
using KeePassLib.Security;

namespace keepass2android
{
	internal class CopyToClipboardService
	{
		public static void CopyValueToClipboardWithTimeout(Context ctx, string text)
		{
			Toast.MakeText(ctx, text, ToastLength.Short).Show();
		}
	}
}