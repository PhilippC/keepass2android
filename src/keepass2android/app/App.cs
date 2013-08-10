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
using System.IO;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Android.Preferences;
using keepass2android.Io;

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
		public const int LauncherIcon = Resource.Drawable.ic_launcher_offline;

		public const string Searchable = "@xml/searchable_offline";
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
		public const int LauncherIcon = Resource.Drawable.ic_launcher;
		public const string Searchable = "@xml/searchable";
	}
#endif
	/// <summary>
	/// Main implementation of the IKp2aApp interface for usage in the real app.
	/// </summary>
    public class Kp2aApp: IKp2aApp, ICacheSupervisor
	{
		public void LockDatabase(bool allowQuickUnlock = true)
		{
			if (GetDb().Loaded)
			{
				if (QuickUnlockEnabled && allowQuickUnlock &&
					_db.KpDatabase.MasterKey.ContainsType(typeof(KcpPassword)) &&
					!((KcpPassword)App.Kp2a.GetDb().KpDatabase.MasterKey.GetUserKey(typeof(KcpPassword))).Password.IsEmpty)
				{
					if (!QuickLocked)
					{
						Kp2aLog.Log("QuickLocking database");

						QuickLocked = true;
					}
					else
					{
						Kp2aLog.Log("Database already QuickLocked");
					}
				}
				else
				{
					Kp2aLog.Log("Locking database");

					// Couldn't quick-lock, so unload database instead
					_db.Clear();
					QuickLocked = false;
				}
			}
			else
			{
				Kp2aLog.Log("Database not loaded, couldn't lock");
			}

			UpdateOngoingNotification();
			Application.Context.SendBroadcast(new Intent(Intents.DatabaseLocked));
        }

		public void LoadDatabase(IOConnectionInfo ioConnectionInfo, MemoryStream memoryStream, string password, string keyFile, ProgressDialogStatusLogger statusLogger)
		{
			_db.LoadData(this, ioConnectionInfo, memoryStream, password, keyFile, statusLogger);

			UpdateOngoingNotification();
		}

		internal void UnlockDatabase()
		{
			QuickLocked = false;

			UpdateOngoingNotification();
		}

		private void UpdateOngoingNotification()
		{
			// Start or update the notification icon service to reflect the current state
			var ctx = Application.Context;
			ctx.StartService(new Intent(ctx, typeof(OngoingNotificationsService)));
		}

		public bool DatabaseIsUnlocked
		{
			get { return _db.Loaded && !QuickLocked; }
		}

		#region QuickUnlock
		public void SetQuickUnlockEnabled(bool enabled)
		{
			if (enabled)
			{
				//Set KeyLength of QuickUnlock at time of enabling.
				//This is important to not allow an attacker to set the length to 1 when QuickUnlock is started already.

				var ctx = Application.Context;
				var prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);
				QuickUnlockKeyLength = Math.Max(1, int.Parse(prefs.GetString(ctx.GetString(Resource.String.QuickUnlockLength_key), ctx.GetString(Resource.String.QuickUnlockLength_default))));
			}
			QuickUnlockEnabled = enabled;
		}

		public bool QuickUnlockEnabled { get; private set; }

		public int QuickUnlockKeyLength { get; private set; }
    
		/// <summary>
		/// If true, the database must be regarded as locked and not exposed to the user.
		/// </summary>
		public bool QuickLocked { get; private set; }
		
		#endregion

		private Database _db;
        
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
				case PreferenceKey.CheckForFileChangesOnSave:
					return prefs.GetBoolean(ctx.Resources.GetString(Resource.String.CheckForFileChangesOnSave_key), true);
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
	                LockDatabase(false);
                    activity.SetResult(KeePass.ExitReloadDb);
                    activity.Finish();
					//todo: return?
                }
	            AskForReload(activity);
            }
        }

		private void AskForReload(Activity activity)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			builder.SetTitle(activity.GetString(Resource.String.AskReloadFile_title));

			builder.SetMessage(activity.GetString(Resource.String.AskReloadFile));

			builder.SetPositiveButton(activity.GetString(Android.Resource.String.Yes),
				(dlgSender, dlgEvt) =>
				{
					_db.ReloadRequested = true;
					LockDatabase(false);
					activity.SetResult(KeePass.ExitReloadDb);
					activity.Finish();

				});

			builder.SetNegativeButton(activity.GetString(Android.Resource.String.No), (dlgSender, dlgEvt) =>
			{

			});


			Dialog dialog = builder.Create();
			dialog.Show();
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
			AskYesNoCancel(titleKey, messageKey, UiStringKey.yes, UiStringKey.no,
				yesHandler, noHandler, cancelHandler, ctx);
		}

        public void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey,
			UiStringKey yesString, UiStringKey noString,
			EventHandler<DialogClickEventArgs> yesHandler,
            EventHandler<DialogClickEventArgs> noHandler,
            EventHandler<DialogClickEventArgs> cancelHandler,
            Context ctx)
        {
	        Handler handler = new Handler(Looper.MainLooper);
			handler.Post(() =>
				{
					AlertDialog.Builder builder = new AlertDialog.Builder(ctx);
					builder.SetTitle(GetResourceString(titleKey));

					builder.SetMessage(GetResourceString(messageKey));

					builder.SetPositiveButton(GetResourceString(yesString), yesHandler);

					builder.SetNegativeButton(GetResourceString(noString), noHandler);

					builder.SetNeutralButton(ctx.GetString(Android.Resource.String.Cancel),
											 cancelHandler);

					Dialog dialog = builder.Create();
					dialog.Show();
				}
			);
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
				_pd = new ProgressDialog(ctx);
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

		public IFileStorage GetFileStorage(IOConnectionInfo iocInfo)
		{
			if (iocInfo.IsLocalFile())
				return new BuiltInFileStorage();
			else
			{
				//todo: check if desired
				return new CachingFileStorage(new BuiltInFileStorage(), Application.Context.CacheDir.Path, this);
			}
		}

		public void TriggerReload(Context ctx)
		{
			AskForReload((Activity)ctx);
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
#endif
        }

        
        public Database CreateNewDatabase()
        {
            _db = new Database(new DrawableFactory(), this);
            return _db;
        }

		void ShowToast(string message)
		{
			var handler = new Handler(Looper.MainLooper);
			handler.Post(() => { Toast.MakeText(Application.Context, message, ToastLength.Long).Show(); });
		}

		public void CouldntSaveToRemote(IOConnectionInfo ioc, Exception e)
		{
			//TODO use resource strings
			ShowToast("Couldn't save to remote: "+e.Message+". Save again or use Sync menu when remote connection is available again.");
		}

		//todo: test changes in SaveDb with Cache: Save without conflict, save with conflict
		//add test?

		public void CouldntOpenFromRemote(IOConnectionInfo ioc, Exception ex)
		{
			ShowToast("Couldn't open from remote: " + ex.Message+". Loaded file from local cache. You can still make changes in the database and sync them later.");
		}

		public void NotifyOpenFromLocalDueToConflict(IOConnectionInfo ioc)
		{
			ShowToast("Opened local file due to conflict with changes in remote file. Use Synchronize menu to merge.");
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

			Kp2aLog.Log("Creating application "+PackageName+". Version=" + PackageManager.GetPackageInfo(PackageName, 0).VersionCode);

            Kp2a.OnCreate(this);
			AndroidEnvironment.UnhandledExceptionRaiser += MyApp_UnhandledExceptionHandler;
		}


		void MyApp_UnhandledExceptionHandler(object sender, RaiseThrowableEventArgs e)
		{
			Kp2aLog.Log(e.Exception.ToString());
			// Do your error handling here.
			throw e.Exception;
		}

		protected override void Dispose(bool disposing)
		{
			AndroidEnvironment.UnhandledExceptionRaiser -= MyApp_UnhandledExceptionHandler;
			base.Dispose(disposing);
		}

		public override void OnTerminate() {
			base.OnTerminate();
			Kp2aLog.Log("Terminating application");
            Kp2a.OnTerminate();
		}

        
    }
}

