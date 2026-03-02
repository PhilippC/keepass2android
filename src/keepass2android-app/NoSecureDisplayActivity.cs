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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using keepass2android;

namespace keepass2android
{
  [Activity(Label = AppNames.AppName, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden, Theme = "@style/Kp2aTheme_BlueActionBar",
      LaunchMode = LaunchMode.SingleInstance)]
  public class NoSecureDisplayActivity : AndroidX.AppCompat.App.AppCompatActivity
  {
    private readonly ActivityDesign _design;

    public NoSecureDisplayActivity()
    {
      _design = new ActivityDesign(this);
    }

    protected override void OnCreate(Bundle savedInstanceState)
    {
      _design.ApplyTheme();
      base.OnCreate(savedInstanceState);
      SetContentView(Resource.Layout.no_secure_display_layout);
      new Util.InsetListener(FindViewById(Resource.Id.no_secure_display_text)).Apply();
      FindViewById<Button>(Resource.Id.btn_goto_settings).Click += (sender, args) =>
      {
        AppSettingsActivity.Launch(this);
      };
      FindViewById<Button>(Resource.Id.disable_secure_screen_check).Click += (sender, args) =>
      {
        var prefs = PreferenceManager.GetDefaultSharedPreferences(LocaleManager.LocalizedAppContext);
        prefs.Edit()
                  .PutBoolean("no_secure_display_check", true)
                  .Commit();
        Finish();
      };
      FindViewById<Button>(Resource.Id.btn_close).Click += (sender, args) =>
      {
        Finish();
      };

    }

    protected override void OnResume()
    {
      base.OnResume();
      _design.ReapplyTheme();
      //close if displays changed
      if (!Util.SecureDisplayConfigured(this) || !Util.HasUnsecureDisplay(this))
        Finish();
    }
  }
}