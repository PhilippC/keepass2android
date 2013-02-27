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

namespace keepass2android
{
#if NoNet
	public static class AppNames
	{
		public const string AppName = "@string/app_name_nonet";
		public const string AppNameShort = "@string/short_app_name_nonet";
		public const string AppLauncherTitle = "@string/short_app_name_nonet";
	}
#else
	public static class AppNames
	{
		public const string AppName = "@string/app_name";
		public const string AppNameShort = "@string/short_app_name";
		public const string AppLauncherTitle = "@string/app_name";
	}
#endif

	///Application class for Keepass2Android: Contains static Database variable to be used by all components.
#if NoNet
	[Application(Debuggable=false, Label=AppNames.AppName)]
#else
	#if RELEASE 
	[Application(Debuggable=false, Label=AppNames.AppName)] 
	#else
	[Application(Debuggable=true, Label=AppNames.AppName)]
	#endif
#endif
	public class App : Application {

		public App (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}

		private static Database db;
		private static bool shutdown = false;

		
		public static FileDbHelper fileDbHelper;
		
		public static Database getDB() {
			if ( db == null ) {
				db = new Database();
			}
			
			return db;
		}
		
		public static void setDB(Database d) {
			db = d;
		}
		
		public static bool isShutdown() {
			return shutdown;
		}
		
		public static void setShutdown() {
			shutdown = true;
		}
		
		public static void clearShutdown() {
			shutdown = false;
		}

		public override void OnCreate() {
			base.OnCreate();

			Android.Util.Log.Debug("DEBUG","Creating application");

			fileDbHelper = new FileDbHelper(this);
			fileDbHelper.open();
			
		}

		public override void OnTerminate() {
			base.OnTerminate();
			Android.Util.Log.Debug("DEBUG","Terminating application");
			if ( db != null ) {
				db.Clear();
			}
			
			if ( fileDbHelper != null && fileDbHelper.isOpen() ) {
				fileDbHelper.close();
			}
		}

		
	}
}

