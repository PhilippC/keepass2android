using Android.Graphics.Drawables;
using PluginHostTest;

namespace keepass2android
{
	/// <summary>
	/// Reperesents the popup menu item in EntryActivity to go to the URL in the field
	/// </summary>
	class GotoUrlMenuItem : IPopupMenuItem
	{
		private readonly EntryActivity _ctx;

		public GotoUrlMenuItem(EntryActivity ctx)
		{
			_ctx = ctx;
		}

		public Drawable Icon
		{
			get { return _ctx.Resources.GetDrawable(Android.Resource.Drawable.IcMenuUpload); }
		}

		public string Text
		{
			get { return _ctx.Resources.GetString(Resource.String.menu_url); }
		}

		public void HandleClick()
		{
			//TODO
			_ctx.GotoUrl();
		}
	}
}