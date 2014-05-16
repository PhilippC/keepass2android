using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Keepass2android.Pluginsdk;
using PluginHostTest;

namespace keepass2android
{
	//TODO theme?
	[Activity (Label = "Plugins (TODO)", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden )]		
	public class PluginListActivity : ListActivity
	{
		private PluginArrayAdapter _pluginArrayAdapter;
		private List<PluginItem> _items;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			
			//TODO _design.ApplyTheme();

			SetContentView(Resource.Layout.plugin_list);
			
			ListView listView = FindViewById<ListView>(Android.Resource.Id.List);
			listView.ItemClick +=
				(sender, args) =>
					{
						Intent i = new Intent(this, typeof(PluginDetailsActivity));
						i.PutExtra(Strings.ExtraPluginPackage, _items[args.Position].Package);
						StartActivity(i);
					};
			
			// Create your application here
		}
		protected override void OnResume()
		{
			base.OnResume();
			PluginDatabase pluginDb = new PluginDatabase(this);

			_items = (from pluginPackage in pluginDb.GetAllPluginPackages()
			          let version = PackageManager.GetPackageInfo(pluginPackage, 0).VersionName
			          let enabledStatus = pluginDb.IsEnabled(pluginPackage) ? GetString(Resource.String.plugin_enabled) : GetString(Resource.String.plugin_disabled)
			          select new PluginItem(pluginPackage, enabledStatus, this)).ToList();
			/*
				{
					new PluginItem("PluginA", Resource.Drawable.Icon, "keepass2android.plugina", "connected"),
					new PluginItem("KeepassNFC", Resource.Drawable.Icon, "com.bla.blubb.plugina", "disconnected")
				};
			 * */
			_pluginArrayAdapter = new PluginArrayAdapter(this, Resource.Layout.ListViewPluginRow, _items);
			ListAdapter = _pluginArrayAdapter;
		}
	}
}