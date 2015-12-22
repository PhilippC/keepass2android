
/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using keepass2android.Io;
using Object = Java.Lang.Object;
using Toolbar = Android.Support.V7.Widget.Toolbar;

namespace keepass2android
{
    public class ToolbarPreference : Preference
    {
        #region constructors
        protected ToolbarPreference(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public ToolbarPreference(Context context) : base(context)
        {
        }

        public ToolbarPreference(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public ToolbarPreference(Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
        }

        public ToolbarPreference(Context context, IAttributeSet attrs, int defStyleAttr, int defStyleRes) : base(context, attrs, defStyleAttr, defStyleRes)
        {
        }
#endregion

        protected override View OnCreateView(ViewGroup parent)
        {
            parent.SetPadding(0, 0, 0, 0);

    LayoutInflater inflater = (LayoutInflater) Context.GetSystemService(Context.LayoutInflaterService);
    View layout = inflater.Inflate(Resource.Layout.toolbar, parent, false);

    Toolbar toolbar = (Toolbar) layout.FindViewById<Toolbar>(Resource.Id.mytoolbar);
    toolbar.SetNavigationIcon(Resource.Drawable.ic_arrow_back_white_24dp);
    toolbar.Title = Title;
            toolbar.NavigationClick += (sender, args) =>
            {
                PreferenceScreen prefScreen = (PreferenceScreen) PreferenceManager.FindPreference(Key);
                if (prefScreen == null)
                    throw new Exception("didn't find preference " + Key);
                prefScreen.Dialog.Dismiss();
            };

    return layout;
            
        }
    }



	/// <summary>
	/// Activity to configure the application, without database settings. Does not require an unlocked database, or close when the database is locked
	/// </summary>
    [Activity(Label = "@string/app_name", Theme = "@style/MyTheme")]			
	public class AppSettingsActivity : LockingActivity
	{
		private ActivityDesign _design;
		
		public AppSettingsActivity()
		{
			_design = new ActivityDesign(this);
		}

		public static void Launch(Context ctx)
		{
			ctx.StartActivity(new Intent(ctx, typeof(AppSettingsActivity)));
		}

		protected override void OnCreate(Bundle savedInstanceState) 
		{
			_design.ApplyTheme(); 
			base.OnCreate(savedInstanceState);
			
			
			SetContentView(Resource.Layout.preference);

		    SetSupportActionBar(FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.mytoolbar));

            FragmentManager.FindFragmentById<SettingsFragment>(Resource.Id.settings_fragment).FindPreference(GetString(Resource.String.db_key)).Enabled = false;
			
		}

	}

}

