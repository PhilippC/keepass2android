// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Keepass2android.Pluginsdk;
using keepass2android;

namespace keepass2android
{
  [Activity(Label = "@string/plugins", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden, Theme = "@style/android:Theme.Material.Light", Exported = true)]
  [IntentFilter(new[] { "kp2a.action.PluginListActivity" }, Categories = new[] { Intent.CategoryDefault })]
  public class PluginListActivity : ListActivity
  {
    private PluginArrayAdapter _pluginArrayAdapter;
    private List<PluginItem> _items;

    protected override void OnCreate(Bundle bundle)
    {
      new ActivityDesign(this).ApplyTheme();
      base.OnCreate(bundle);



      SetContentView(Resource.Layout.plugin_list);

      PluginHost.TriggerRequests(this);

      ListView listView = FindViewById<ListView>(Android.Resource.Id.List);
      listView.ItemClick +=
          (sender, args) =>
          {
            Intent i = new Intent(this, typeof(PluginDetailsActivity));
            i.PutExtra(Strings.ExtraPluginPackage, _items[args.Position].Package);
            StartActivity(i);
          };

      FindViewById<Button>(Resource.Id.btnPluginsOnline).Click += delegate
          {
            Util.GotoUrl(this, "https://github.com/PhilippC/keepass2android/blob/master/docs/Available-Plug-ins.md");
          };

    }
    protected override void OnResume()
    {

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
      base.OnResume();
    }

    protected override void OnPause()
    {
      base.OnPause();
      ListAdapter = _pluginArrayAdapter = null;
    }
  }
}