/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. 

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
using Android.App;
using Android.OS;
using Android.Runtime;

namespace keepass2android
{
				
	public abstract class LifecycleDebugActivity : Activity
	{
		protected LifecycleDebugActivity (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		protected LifecycleDebugActivity()
		{
		}


		string _className;
		string ClassName
		{
			get {
				if (_className == null)
					_className = GetType().Name;
				return _className;
			}
		}

		protected override void OnResume()
		{
			base.OnResume();
			Kp2aLog.Log(ClassName+".OnResume");
			if (App.Kp2a.GetDb() == null)
			{
				Kp2aLog.Log(" DB null");
			}
			else
			{
				Kp2aLog.Log(" Loaded=" + App.Kp2a.GetDb().Loaded + ", Locked=" + App.Kp2a.GetDb().Locked 
					+ ", shutdown=" + App.Kp2a.IsShutdown());
			}
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
			Kp2aLog.Log(ClassName+":apptask="+Intent.GetStringExtra("KP2A_APP_TASK_TYPE"));
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			Kp2aLog.Log(ClassName+".OnDestroy"+IsFinishing.ToString());
		}

		protected override void OnPause()
		{
			base.OnPause();
			Kp2aLog.Log(ClassName+".OnPause");
		}

		protected override void OnStop()
		{
			base.OnStop();
			Kp2aLog.Log(ClassName+".OnStop");
		}
	}
}

