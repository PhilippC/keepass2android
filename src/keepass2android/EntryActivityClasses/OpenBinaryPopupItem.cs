using Android.Graphics.Drawables;

namespace keepass2android
{
	/// <summary>
	/// Represents the popup menu item in EntryActivity to open the associated attachment
	/// </summary>
	internal class OpenBinaryPopupItem : IPopupMenuItem
	{
		private readonly string _key;
		private readonly EntryActivity _entryActivity;

		public OpenBinaryPopupItem(string key, EntryActivity entryActivity)
		{
			_key = key;
			_entryActivity = entryActivity;
		}

		public Drawable Icon
		{
			get { return _entryActivity.Resources.GetDrawable(Android.Resource.Drawable.IcMenuShare); }
		}

		public string Text
		{
			get { return _entryActivity.Resources.GetString(Resource.String.SaveAttachmentDialog_open); }
		}

		public void HandleClick()
		{
			Android.Net.Uri newUri = _entryActivity.WriteBinaryToFile(_key, true);
			if (newUri != null)
			{
				_entryActivity.OpenBinaryFile(newUri);
			}
			
		}
	}
}