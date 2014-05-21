using Android.Graphics.Drawables;

namespace keepass2android
{
	/// <summary>
	/// Represents the popup menu item in EntryActivity to store the binary attachment on SD card
	/// </summary>
	internal class WriteBinaryToFilePopupItem : IPopupMenuItem
	{
		private readonly string _key;
		private readonly EntryActivity _activity;

		public WriteBinaryToFilePopupItem(string key, EntryActivity activity)
		{
			_key = key;
			_activity = activity;
		}

		public Drawable Icon
		{
			get { return _activity.Resources.GetDrawable(Android.Resource.Drawable.IcMenuSave); }
		}

		public string Text
		{
			get { return _activity.Resources.GetString(Resource.String.SaveAttachmentDialog_save); }
		}

		public void HandleClick()
		{
			_activity.WriteBinaryToFile(_key, false);
		}
	}
}