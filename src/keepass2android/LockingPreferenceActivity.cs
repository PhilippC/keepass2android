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
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Preferences;
using Android.Support.V7.App;
using Android.Views;
using Java.Lang;
using keepass2android;

namespace keepass2android
{

    public class AppCompatPreferenceActivity: PreferenceActivity
    {
        public AppCompatPreferenceActivity(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

        public AppCompatPreferenceActivity()
        {
            
        }

        private AppCompatDelegate _appCompatDelegate;

        AppCompatDelegate Delegate
        {
            get
            {
                if (_appCompatDelegate == null)
                    _appCompatDelegate = AppCompatDelegate.Create(this, null);
                return _appCompatDelegate;
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            Delegate.InstallViewFactory();
            Delegate.OnCreate(savedInstanceState);
            base.OnCreate(savedInstanceState);
            
        }


        public override MenuInflater MenuInflater
        {
            get { return Delegate.MenuInflater; }
        }


        public override void SetContentView(int layoutResId)
        {
            Delegate.SetContentView(layoutResId);
            
        }

        
    public override void SetContentView(View view) {
        Delegate.SetContentView(view);
    }
    
    public override void SetContentView(View view, ViewGroup.LayoutParams @params) {
        Delegate.SetContentView(view, @params);
    }
    
    public override void AddContentView(View view, ViewGroup.LayoutParams @params) {
        Delegate.AddContentView(view, @params);
    }

    protected override void OnPostResume()
    {
        base.OnPostResume();
        Delegate.OnPostResume();
    }

        protected override void OnTitleChanged(ICharSequence title, Color color)
        {
            base.OnTitleChanged(title, color);
            Delegate.SetTitle(title);
        }


        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            Delegate.OnConfigurationChanged(newConfig);
        }


        protected override void OnStop()
        {
            base.OnStop();
            Delegate.OnStop();
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            Delegate.OnDestroy();
        }

        public override void InvalidateOptionsMenu()
        {
            Delegate.InvalidateOptionsMenu();
        }
    }

	
	public class LockingPreferenceActivity : AppCompatPreferenceActivity {
		
		public LockingPreferenceActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
		public LockingPreferenceActivity ()
		{
		}
		
		
		string _className = null;
		string ClassName
		{
			get {
				if (_className == null)
					_className = this.GetType().Name;
				return _className;
			}
		}
		
		protected override void OnResume()
		{
			base.OnResume();
			TimeoutHelper.Resume(this);
			Kp2aLog.Log(ClassName+".OnResume");
		}
		
		protected override void OnStart()
		{
			base.OnStart();
			Kp2aLog.Log(ClassName+".OnStart");
		}
		
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			Kp2aLog.Log(ClassName+".OnCreate");
		}
		
		protected override void OnDestroy()
		{
			base.OnDestroy();
			GC.Collect();
			Kp2aLog.Log(ClassName+".OnDestroy"+IsFinishing.ToString());
		}
		
		protected override void OnPause()
		{
			base.OnPause();
			TimeoutHelper.Pause(this);
			Kp2aLog.Log(ClassName+".OnPause");
		}
		
		protected override void OnStop()
		{
			base.OnStop();
			Kp2aLog.Log(ClassName+".OnStop");
		}
	}

}

