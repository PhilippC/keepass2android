using Android.Graphics.Drawables;
using PluginHostTest;

namespace keepass2android
{
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