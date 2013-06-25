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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using KeePassLib.Serialization;
using Android.Preferences;

namespace keepass2android
{
#if NoNet
	/// <summary>
	/// Static strings containing App names for the Offline ("nonet") release
	/// </summary>
	public static class AppNames
	{
		public const string AppName = "@string/app_name_nonet";
		public const string AppNameShort = "@string/short_app_name_nonet";
		public const string AppLauncherTitle = "@string/short_app_name_nonet";
		public const string PackagePart = "keepass2android_nonet";
	}
#else
	/// <summary>
	/// Static strings containing App names for the Online release
	/// </summary>
	public static class AppNames
	{
		public const string AppName = "@string/app_name";
		public const string AppNameShort = "@string/short_app_name";
		public const string AppLauncherTitle = "@string/app_name";
		public const string PackagePart = "keepass2android";
	}
#endif
	/// <summary>
	/// Main implementation of the IKp2aApp interface for usage in the real app.
	/// </summary>
    public class Kp2aApp: IKp2aApp
    {
        public bool IsShutdown()
        {
            return _shutdown;
        }

        public void SetShutdown()
        {
            _shutdown = true;
        }

        public void ClearShutdown()
        {
            _shutdown = false;
        }

        private Database _db;
        private bool _shutdown;

        /// <summary>
        /// See comments to EntryEditActivityState.
        /// </summary>
        internal EntryEditActivityState EntryEditActivityState = null;

        public FileDbHelper FileDbHelper;

        public Database GetDb()
        {
            if (_db == null)
            {
                _db = CreateNewDatabase();
            }

            return _db;
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
            if (_db.DidOpenFileChange())
            {
                if (_db.ReloadRequested)
                {
                    activity.SetResult(KeePass.ExitReloadDb);
                    activity.Finish();
                }
                AlertDialog.Builder builder = new AlertDialog.Builder(activity);
                builder.SetTitle(activity.GetString(Resource.String.AskReloadFile_title));

                builder.SetMessage(activity.GetString(Resource.String.AskReloadFile));

                builder.SetPositiveButton(activity.GetString(Android.Resource.String.Yes), 
                    (dlgSender, dlgEvt) =>
                        {
                            _db.ReloadRequested = true;
                            activity.SetResult(KeePass.ExitReloadDb);
                            activity.Finish();

                        });

                builder.SetNegativeButton(activity.GetString(Android.Resource.String.No), (dlgSender, dlgEvt) =>
                    {

                    });


                Dialog dialog = builder.Create();
                dialog.Show();
            }
        }

        public void StoreOpenedFileAsRecent(IOConnectionInfo ioc, string keyfile)
        {
            FileDbHelper.CreateFile(ioc, keyfile);
        }

        public string GetResourceString(UiStringKey key)
        {
            var field = typeof (Resource.String).GetField(key.ToString());
            if (field == null)
                throw new Exception("Invalid key " + key);
            return Application.Context.GetString((int)field.GetValue(null));
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

		public Handler UiThreadHandler 
		{
			get { return new Handler(); }
		}

		/// <summary>
		/// Simple wrapper around ProgressDialog implementing IProgressDialog
		/// </summary>
		private class RealProgressDialog : IProgressDialog
		{
			private readonly ProgressDialog _pd;

			public RealProgressDialog(Context ctx)
			{
				this._pd = new ProgressDialog(ctx);
			}

			public void SetTitle(string title)
			{
				_pd.SetTitle(title);
			}

			public void SetMessage(string message)
			{
				_pd.SetMessage(message);
			}

			public void Dismiss()
			{
				_pd.Dismiss();
			}

			public void Show()
			{
				_pd.Show();
			}
		}

		public IProgressDialog CreateProgressDialog(Context ctx)
		{
			return new RealProgressDialog(ctx);
		}


		internal void OnTerminate()
        {
            if (_db != null)
            {
                _db.Clear();
            }

            if (FileDbHelper != null && FileDbHelper.IsOpen())
            {
                FileDbHelper.Close();
            }
        }

        internal void OnCreate(Application app)
        {
            FileDbHelper = new FileDbHelper(app);
            FileDbHelper.Open();

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
            _db = new Database(new DrawableFactory(), this);
            return _db;
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

        public static readonly Kp2aApp Kp2a = new Kp2aApp();
        
		public override void OnCreate() {
			base.OnCreate();

			Android.Util.Log.Debug("DEBUG","Creating application");

            Kp2a.OnCreate(this);
			
		}

		public override void OnTerminate() {
			base.OnTerminate();
			Android.Util.Log.Debug("DEBUG","Terminating application");
            Kp2a.OnTerminate();
		}

        
    }
}

