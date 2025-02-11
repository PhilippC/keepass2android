using Android.Content;
using Android.Graphics.Drawables;

namespace keepass2android
{
	class PluginMenuOption
	{
		public string DisplayText { get; set; }

		public Drawable Icon { get; set; }

		public Intent Intent { get; set; }
	}
}