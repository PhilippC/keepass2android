using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Keepass2android.Pluginsdk;

namespace keepass2android
{
	/// <summary>
	/// Represents a popup menu item in EntryActivity which was added by a plugin. The click will therefore broadcast to the plugin.
	/// </summary>
	class PluginPopupMenuItem : IPopupMenuItem
	{
		private readonly EntryActivity _activity;
		private readonly string _pluginPackage;
		private readonly string _fieldId;
		private readonly string _popupItemId;
		private readonly string _displayText;
		private readonly int _iconId;
		private readonly Bundle _bundleExtra;

		public PluginPopupMenuItem(EntryActivity activity, string pluginPackage, string fieldId, string popupItemId, string displayText, int iconId, Bundle bundleExtra)
		{
			_activity = activity;
			_pluginPackage = pluginPackage;
			_fieldId = fieldId;
			_popupItemId = popupItemId;
			_displayText = displayText;
			_iconId = iconId;
			_bundleExtra = bundleExtra;
		}

		public Drawable Icon 
		{ 
			get { return _activity.PackageManager.GetResourcesForApplication(_pluginPackage).GetDrawable(_iconId); }
		}
		public string Text 
		{ 
			get { return _displayText; } 
		}

		public string PopupItemId
		{
			get { return _popupItemId; }
		}

		public void HandleClick()
		{
			Intent i = new Intent(Strings.ActionEntryActionSelected);
			i.SetPackage(_pluginPackage);
			i.PutExtra(Strings.ExtraActionData, _bundleExtra);
			i.PutExtra(Strings.ExtraFieldId, _fieldId);
			i.PutExtra(Strings.ExtraSender, _activity.PackageName);

			_activity.AddEntryToIntent(i);

			_activity.SendBroadcast(i);
		}
	}
}