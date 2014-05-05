using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Keepass2android.Pluginsdk;

namespace keepass2android
{
	class PluginPopupMenuItem : IPopupMenuItem
	{
		private readonly Context _ctx;
		private readonly string _pluginPackage;
		private readonly string _fieldId;
		private readonly string _displayText;
		private readonly int _iconId;
		private readonly Bundle _bundleExtra;

		public PluginPopupMenuItem(Context ctx, string pluginPackage, string fieldId, string displayText, int iconId, Bundle bundleExtra)
		{
			_ctx = ctx;
			_pluginPackage = pluginPackage;
			_fieldId = fieldId;
			_displayText = displayText;
			_iconId = iconId;
			_bundleExtra = bundleExtra;
		}

		public Drawable Icon 
		{ 
			get { return _ctx.PackageManager.GetResourcesForApplication(_pluginPackage).GetDrawable(_iconId); }
		}
		public string Text 
		{ 
			get { return _displayText; } 
		}
		public void HandleClick()
		{
			Intent i = new Intent(Strings.ActionEntryActionSelected);
			i.SetPackage(_pluginPackage);
			i.PutExtra(Strings.ExtraActionData, _bundleExtra);
			i.PutExtra(Strings.ExtraFieldId, _fieldId);
			i.PutExtra(Strings.ExtraSender, _ctx.PackageName);
			PluginHost.AddEntryToIntent(i, Entry);

			_ctx.SendBroadcast(i);
		}
	}
}