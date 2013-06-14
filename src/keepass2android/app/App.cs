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
using KeePassLib.Serialization;
using Android.Preferences;

namespace keepass2android
{
#if NoNet
	public static class AppNames
	{
		public const string AppName = "@string/app_name_nonet";
		public const string AppNameShort = "@string/short_app_name_nonet";
		public const string AppLauncherTitle = "@string/short_app_name_nonet";
		public const string PackagePart = "keepass2android_nonet";
	}
#else
	public static class AppNames
	{
		public const string AppName = "@string/app_name";
		public const string AppNameShort = "@string/short_app_name";
		public const string AppLauncherTitle = "@string/app_name";
		public const string PackagePart = "keepass2android";
	}
#endif

    public class Kp2aApp: IKp2aApp
    {
        public bool isShutdown()
        {
            return shutdown;
        }

        public void SetShutdown()
        {
            shutdown = true;
        }

        public void clearShutdown()
        {
            shutdown = false;
        }

        private Database db;
        private bool shutdown = false;

        /// <summary>
        /// See comments to EntryEditActivityState.
        /// </summary>
        internal EntryEditActivityState entryEditActivityState = null;

        public FileDbHelper fileDbHelper;

        public Database GetDb()
        {
            if (db == null)
            {
                db = CreateNewDatabase();
            }

            return db;
        }



        public bool GetBooleanPreference(PreferenceKey key)
        {
            Context ctx = Application.Context;
            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);
            switch (key)
            {
                case PreferenceKey.remember_keyfile:
                    return prefs.GetBoolean(ctx.Resources.GetString(Resource.String.keyfile_key), ctx.Resources.GetBoolean(Resource.Boolean.keyfile_default));
                case PreferenceKey.UseFileTransactions:
                    return prefs.GetBoolean(ctx.Resources.GetString(Resource.String.UseFileTransactions_key), true);
                default:
                    throw new Exception("unexpected key!");
            }

        }


        public void CheckForOpenFileChanged(Activity activity)
        {
            if (db.DidOpenFileChange())
            {
                if (db.ReloadRequested)
                {
                    activity.SetResult(KeePass.EXIT_RELOAD_DB);
                    activity.Finish();
                }
                AlertDialog.Builder builder = new AlertDialog.Builder(activity);
                builder.SetTitle(activity.GetString(Resource.String.AskReloadFile_title));

                builder.SetMessage(activity.GetString(Resource.String.AskReloadFile));

                builder.SetPositiveButton(activity.GetString(Android.Resource.String.Yes), new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) =>
                {
                    db.ReloadRequested = true;
                    activity.SetResult(KeePass.EXIT_RELOAD_DB);
                    activity.Finish();

                }));

                builder.SetNegativeButton(activity.GetString(Android.Resource.String.No), new EventHandler<DialogClickEventArgs>((dlgSender, dlgEvt) =>
                {

                }));


                Dialog dialog = builder.Create();
                dialog.Show();
            }
        }

        public void StoreOpenedFileAsRecent(IOConnectionInfo ioc, string keyfile)
        {
            fileDbHelper.createFile(ioc, keyfile);
        }

        public string GetResourceString(UiStringKey key)
        {
            var field = typeof (Resource.String).GetField(key.ToString());
            if (field == null)
                throw new Exception("Invalid key " + key);
            return App.Context.GetString((int)field.GetValue(null));
        }

        public void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey,
            EventHandler<DialogClickEventArgs> yesHandler,
            EventHandler<DialogClickEventArgs> noHandler,
            EventHandler<DialogClickEventArgs> cancelHandler,
            Context ctx)
        {

            AlertDialog.Builder builder = new AlertDialog.Builder(ctx);
            builder.SetTitle(GetResourceString(titleKey));

            builder.SetMessage(GetResourceString(messageKey));

            builder.SetPositiveButton(Resource.String.yes, yesHandler);

            builder.SetNegativeButton(Resource.String.no, noHandler);

            builder.SetNeutralButton(ctx.GetString(Android.Resource.String.Cancel),
                                    cancelHandler);

            Dialog dialog = builder.Create();
            dialog.Show();

        }



        internal void OnTerminate()
        {
            if (db != null)
            {
                db.Clear();
            }

            if (fileDbHelper != null && fileDbHelper.isOpen())
            {
                fileDbHelper.close();
            }
        }

        internal void OnCreate(Application app)
        {
            fileDbHelper = new FileDbHelper(app);
            fileDbHelper.open();

#if DEBUG
            foreach (UiStringKey key in Enum.GetValues(typeof(UiStringKey)))
            {
                GetResourceString(key);
            }
            
#else
                this should case a compiler error when switching to release (and thus ensure DEBUG is defined in DEBUG)
#endif
        }

        
        public Database CreateNewDatabase()
        {
            db = new Database(new DrawableFactory(), this);
            return db;
        }
    }


    ///Application class for Keepass2Android: Contains static Database variable to be used by all components.
#if NoNet
	[Application(Debuggable=false, Label=AppNames.AppName)]
#else
#if RELEASE 
	[Application(Debuggable=false, Label=AppNames.AppName)] 
#else
    [Application(Debuggable = true, Label = AppNames.AppName)]
#endif
#endif
	public class App : Application {

		public App (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}

        private static readonly Kp2aApp instance = new Kp2aApp();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static App()
        {
        }

        private App()
        {
        }

        public static Kp2aApp Kp2a
        {
            get
            {
                return instance;
            }
        }

		public override void OnCreate() {
			base.OnCreate();

			Android.Util.Log.Debug("DEBUG","Creating application");

            instance.OnCreate(this);
			
		}

		public override void OnTerminate() {
			base.OnTerminate();
			Android.Util.Log.Debug("DEBUG","Terminating application");
            instance.OnTerminate();
		}

        
    }
}

