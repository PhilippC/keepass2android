/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
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
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Preferences;

namespace keepass2android
{
	
	public class LockingPreferenceActivity : PreferenceActivity {
		
		public LockingPreferenceActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
		public LockingPreferenceActivity ()
		{
		}
		
		
		string className = null;
		string ClassName
		{
			get {
				if (className == null)
					className = this.GetType().Name;
				return className;
			}
		}
		
		protected override void OnResume()
		{
			base.OnResume();
			TimeoutHelper.resume(this);
			Android.Util.Log.Debug("DEBUG",ClassName+".OnResume");
		}
		
		protected override void OnStart()
		{
			base.OnStart();
			Android.Util.Log.Debug("DEBUG",ClassName+".OnStart");
		}
		
		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);
			Android.Util.Log.Debug("DEBUG",ClassName+".OnCreate");
		}
		
		protected override void OnDestroy()
		{
			base.OnDestroy();
			GC.Collect();
			Android.Util.Log.Debug("DEBUG",ClassName+".OnDestroy"+IsFinishing.ToString());
		}
		
		protected override void OnPause()
		{
			base.OnPause();
			TimeoutHelper.pause(this);
			Android.Util.Log.Debug("DEBUG",ClassName+".OnPause");
		}
		
		protected override void OnStop()
		{
			base.OnStop();
			Android.Util.Log.Debug("DEBUG",ClassName+".OnStop");
		}
	}

}

