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
using System.IO;
using System.Net.Security;
using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Android.Preferences;
using Android.Support.V4.App;
#if !EXCLUDE_TWOFISH
using TwofishCipher;
#endif
using Keepass2android.Pluginsdk;
using keepass2android.Io;
using keepass2android.addons.OtpKeyProv;

namespace keepass2android
{
#if NoNet
	/// <summary>
	/// Static strings containing App names for the Offline ("nonet") release
	/// </summary>
	public static class AppNames
	{
		public const string AppName = "@string/app_name_nonet";
		public const int AppNameResource = Resource.String.app_name_nonet;
		public const string AppNameShort = "@string/short_app_name_nonet";
		public const string AppLauncherTitle = "@string/short_app_name_nonet";
		public const string PackagePart = "keepass2android_nonet";
		public const int LauncherIcon = Resource.Drawable.ic_launcher_offline;
		public const int NotificationLockedIcon = Resource.Drawable.ic_notify_offline;
		public const int NotificationUnlockedIcon = Resource.Drawable.ic_notify_locked;

		public const string Searchable = "@xml/searchable_offline";
	}
#else
	/// <summary>
	/// Static strings containing App names for the Online release
	/// </summary>
	public static class AppNames
	{
#if DEBUG
		public const string AppName = "@string/app_name_debug";
#else
		public const string AppName = "@string/app_name";
#endif

		public const int AppNameResource = Resource.String.app_name;
		public const string AppNameShort = "@string/short_app_name" + "DBG";
		public const string AppLauncherTitle = "@string/app_name" + " Debug";
#if DEBUG
		public const string PackagePart = "keepass2android_debug";
		public const string Searchable = "@xml/searchable_debug";
#else
		public const string PackagePart = "keepass2android";
		public const string Searchable = "@xml/searchable";
#endif
		public const int LauncherIcon = Resource.Drawable.ic_launcher_online;
		public const int NotificationLockedIcon = Resource.Drawable.ic_notify_loaded;
		public const int NotificationUnlockedIcon = Resource.Drawable.ic_notify_locked;

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
						BroadcastDatabaseAction(Application.Context, Strings.ActionLockDatabase);

						QuickLocked = true;
						_db.LastOpenedEntry = null;
					}
					else
					{
						Kp2aLog.Log("Database already QuickLocked");
					}
				}
				else
				{
					Kp2aLog.Log("Locking database");

					BroadcastDatabaseAction(Application.Context, Strings.ActionCloseDatabase);

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


		public void BroadcastDatabaseAction(Context ctx, string action)
		{
			Intent i = new Intent(action);

			//seems like this can happen. This code is for debugging.
			if (App.Kp2a.GetDb().Ioc == null)
			{
				Kp2aLog.LogUnexpectedError(new Exception("App.Kp2a.GetDb().Ioc is null"));
				return;
			}

			i.PutExtra(Strings.ExtraDatabaseFileDisplayname, App.Kp2a.GetFileStorage(App.Kp2a.GetDb().Ioc).GetDisplayName(App.Kp2a.GetDb().Ioc));
			i.PutExtra(Strings.ExtraDatabaseFilepath, App.Kp2a.GetDb().Ioc.Path);
			foreach (var plugin in new PluginDatabase(ctx).GetPluginsWithAcceptedScope(Strings.ScopeDatabaseActions))
			{
				i.SetPackage(plugin);
				ctx.SendBroadcast(i);
			}
		}



		public void LoadDatabase(IOConnectionInfo ioConnectionInfo, MemoryStream memoryStream, CompositeKey compositeKey, ProgressDialogStatusLogger statusLogger, IDatabaseFormat databaseFormat)
		{
			_db.LoadData(this, ioConnectionInfo, memoryStream, compositeKey, statusLogger, databaseFormat);

			UpdateOngoingNotification();
		}

		internal void UnlockDatabase()
		{
			QuickLocked = false;

			UpdateOngoingNotification();

			BroadcastDatabaseAction(Application.Context, Strings.ActionUnlockDatabase);
		}

		public void UpdateOngoingNotification()
		{
			// Start or update the notification icon service to reflect the current state
			var ctx = Application.Context;
			StartOnGoingService(ctx);
		}

		public static void StartOnGoingService(Context ctx)
		{
			ctx.StartService(new Intent(ctx, typeof (OngoingNotificationsService)));
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
		private List<IFileStorage> _fileStorages;

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
	        return GetResourceString(key.ToString());
        }
		public string GetResourceString(string key)
		{
			var field = typeof(Resource.String).GetField(key);
			if (field == null)
				throw new Exception("Invalid key " + key);
			return Application.Context.GetString((int)field.GetValue(null));
		}
		public Drawable GetResourceDrawable(string key)
		{
			var field = typeof(Resource.Drawable).GetField(key);
			if (field == null)
				throw new Exception("Invalid key " + key);
			return Application.Context.Resources.GetDrawable((int)field.GetValue(null));
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
			AskYesNoCancel(titleKey, messageKey, yesString, noString, yesHandler, noHandler, cancelHandler, null, ctx);
		}

		public void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey,
			UiStringKey yesString, UiStringKey noString,
			EventHandler<DialogClickEventArgs> yesHandler,
            EventHandler<DialogClickEventArgs> noHandler,
            EventHandler<DialogClickEventArgs> cancelHandler,
			EventHandler dismissHandler,
            Context ctx)
        {
	        Handler handler = new Handler(Looper.MainLooper);
			handler.Post(() =>
				{
					AlertDialog.Builder builder = new AlertDialog.Builder(ctx);
					builder.SetTitle(GetResourceString(titleKey));

					builder.SetMessage(GetResourceString(messageKey));

					string yesText = GetResourceString(yesString);
					builder.SetPositiveButton(yesText, yesHandler);
					string noText = "";
					if (noHandler != null)
					{
						noText = GetResourceString(noString);
						builder.SetNegativeButton(noText, noHandler);
					}
					string cancelText = "";
					if (cancelHandler != null)
					{
						cancelText = ctx.GetString(Android.Resource.String.Cancel);
						builder.SetNeutralButton(cancelText,
												 cancelHandler);	
					}

					

					AlertDialog dialog = builder.Create();
					if (dismissHandler != null)
						dialog.SetOnDismissListener(new Util.DismissListener(() => dismissHandler(dialog, EventArgs.Empty)));
					dialog.Show();

					if (yesText.Length + noText.Length + cancelText.Length >= 20)
					{
						try
						{
							Button button = dialog.GetButton((int)DialogButtonType.Positive);
							LinearLayout linearLayout = (LinearLayout)button.Parent;
							linearLayout.Orientation = Orientation.Vertical;
						}
						catch (Exception e)
						{
							Kp2aLog.LogUnexpectedError(e);
						}
	
					}
					
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
				_pd.SetCancelable(false);
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
				try
				{
					_pd.Dismiss();
				}
				catch (Exception e)
				{
					Kp2aLog.LogUnexpectedError(e);
				}
				
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
			return GetFileStorage(iocInfo, true);
		}
		public IFileStorage GetFileStorage(IOConnectionInfo iocInfo, bool allowCache)
		{
			IFileStorage fileStorage;
			if (iocInfo.IsLocalFile())
				fileStorage = new LocalFileStorage(this);
			else
			{
				IFileStorage innerFileStorage = GetCloudFileStorage(iocInfo);

				if (DatabaseCacheEnabled && allowCache)
				{
					fileStorage = new CachingFileStorage(innerFileStorage, Application.Context.CacheDir.Path, this);
				}
				else
				{
					fileStorage = innerFileStorage;
				}
			}
			if (fileStorage is IOfflineSwitchable)
			{
				((IOfflineSwitchable)fileStorage).IsOffline = App.Kp2a.OfflineMode;
			}
			return fileStorage;
		}

		private IFileStorage GetCloudFileStorage(IOConnectionInfo iocInfo)
		{
			foreach (IFileStorage fs in FileStorages)
			{
				foreach (string protocolId in fs.SupportedProtocols)
				{
					if (iocInfo.Path.StartsWith(protocolId + "://"))
						return fs;
				}

			}
			//TODO: catch!
			throw new NoFileStorageFoundException("Unknown protocol " + iocInfo.Path);
		}

		public IEnumerable<IFileStorage> FileStorages
		{
			get
			{
				if (_fileStorages == null)
				{
					_fileStorages = new List<IFileStorage>
						{
							
							new AndroidContentStorage(Application.Context),
#if !EXCLUDE_JAVAFILESTORAGE
#if !NoNet
							new DropboxFileStorage(Application.Context, this),
							new DropboxAppFolderFileStorage(Application.Context, this),
							new GoogleDriveFileStorage(Application.Context, this),
							new SkyDriveFileStorage(Application.Context, this),
							new SftpFileStorage(this),
							new NetFtpFileStorage(Application.Context),
#endif
#endif
							new LocalFileStorage(this)
						};
				}
				return _fileStorages;
			}
		}

		public void TriggerReload(Context ctx)
		{
			Handler handler = new Handler(Looper.MainLooper);
			handler.Post(() =>
				{
					AskForReload((Activity) ctx);
				});
		}

		public RemoteCertificateValidationCallback CertificateValidationCallback
		{
			get
			{
				var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);

				ValidationMode validationMode = ValidationMode.Warn;

				string strValMode = prefs.GetString(Application.Context.Resources.GetString(Resource.String.AcceptAllServerCertificates_key),
													 Application.Context.Resources.GetString(Resource.String.AcceptAllServerCertificates_default));

				if (strValMode == "IGNORE")
					validationMode = ValidationMode.Ignore;
				else if (strValMode == "ERROR")
					validationMode = ValidationMode.Error;
				;

				switch (validationMode)
				{
					case ValidationMode.Ignore:
						return (sender, certificate, chain, errors) => true;
					case ValidationMode.Warn:
						return (sender, certificate, chain, errors) =>
						{
							if (errors != SslPolicyErrors.None)
								ShowToast(Application.Context.GetString(Resource.String.CertificateWarning,
															new Java.Lang.Object[]
					                                        {
						                                       errors.ToString()
					                                        }));
							return true;
						};
						
					case ValidationMode.Error:
						return (sender, certificate, chain, errors) =>
						{
							if (errors == SslPolicyErrors.None)
								return true;
						
							return false;
						};;
					default:
						throw new ArgumentOutOfRangeException();
				}

			}

		}

		public bool CheckForDuplicateUuids
		{
			get
			{
				var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
				return prefs.GetBoolean(Application.Context.GetString(Resource.String.CheckForDuplicateUuids_key), true);
			}
		}


		public enum ValidationMode
		{
			Ignore, Warn, Error
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
#if !EXCLUDE_TWOFISH
			CipherPool.GlobalPool.AddCipher(new TwofishCipherEngine());
#endif
        }

        
        public Database CreateNewDatabase()
        {
            _db = new Database(new DrawableFactory(), this);
            return _db;
        }

		internal void ShowToast(string message)
		{
			var handler = new Handler(Looper.MainLooper);
			handler.Post(() => { var toast = Toast.MakeText(Application.Context, message, ToastLength.Long);
				                   toast.SetGravity(GravityFlags.Center, 0, 0);
									toast.Show(); 
			});
		}

		public void CouldntSaveToRemote(IOConnectionInfo ioc, Exception e)
		{
			var errorMessage = GetErrorMessageForFileStorageException(e);
			ShowToast(Application.Context.GetString(Resource.String.CouldNotSaveToRemote, errorMessage));
		}

		private string GetErrorMessageForFileStorageException(Exception e)
		{
			string errorMessage = e.Message;
			if (e is OfflineModeException)
				errorMessage = GetResourceString(UiStringKey.InOfflineMode);
			return errorMessage;
		}


		public void CouldntOpenFromRemote(IOConnectionInfo ioc, Exception ex)
		{
			var errorMessage = GetErrorMessageForFileStorageException(ex);
			ShowToast(Application.Context.GetString(Resource.String.CouldNotLoadFromRemote, errorMessage));
		}

		public void UpdatedCachedFileOnLoad(IOConnectionInfo ioc)
		{
			ShowToast(Application.Context.GetString(Resource.String.UpdatedCachedFileOnLoad, 
				new Java.Lang.Object[] { Application.Context.GetString(Resource.String.database_file) }));
		}

		public void UpdatedRemoteFileOnLoad(IOConnectionInfo ioc)
		{
			ShowToast(Application.Context.GetString(Resource.String.UpdatedRemoteFileOnLoad));
		}

		public void NotifyOpenFromLocalDueToConflict(IOConnectionInfo ioc)
		{
			ShowToast(Application.Context.GetString(Resource.String.NotifyOpenFromLocalDueToConflict));
		}

		public void LoadedFromRemoteInSync(IOConnectionInfo ioc)
		{
			ShowToast(Application.Context.GetString(Resource.String.LoadedFromRemoteInSync));
		}

		public void ClearOfflineCache()
		{
			new CachingFileStorage(new BuiltInFileStorage(this), Application.Context.CacheDir.Path, this).ClearCache();
		}

		public IFileStorage GetFileStorage(string protocolId)
		{
			return GetFileStorage(new IOConnectionInfo() {Path = protocolId + "://"});
		}

		/// <summary>
		/// returns a file storage object to be used when accessing the auxiliary OTP file
		/// </summary>
		/// The reason why this requires a different file storage is the different caching behavior.
		public IFileStorage GetOtpAuxFileStorage(IOConnectionInfo iocInfo)
		{

			if (iocInfo.IsLocalFile())
				return new BuiltInFileStorage(this);
			else
			{
				IFileStorage innerFileStorage = GetCloudFileStorage(iocInfo);

				
				if (DatabaseCacheEnabled)
				{
					return new OtpAuxCachingFileStorage(innerFileStorage, Application.Context.CacheDir.Path, new OtpAuxCacheSupervisor(this));
				}
				else
				{
					return innerFileStorage;
				}
			}
		}

		private static bool DatabaseCacheEnabled
		{
			get
			{
				var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
				bool cacheEnabled = prefs.GetBoolean(Application.Context.Resources.GetString(Resource.String.UseOfflineCache_key),
#if NoNet
					false
#else
					true
#endif
				    );
				return cacheEnabled;
			}
		}

		public bool OfflineModePreference
		{
			get
			{
				var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context); 
				return prefs.GetBoolean(Application.Context.GetString(Resource.String.OfflineMode_key), false);
			}
			set
			{
				ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
				ISharedPreferencesEditor edit = prefs.Edit();
				edit.PutBoolean(Application.Context.GetString(Resource.String.OfflineMode_key), value);
				edit.Commit();

			}
		}

		/// <summary>
		/// true if the app is used in offline mode
		/// </summary>
		public bool OfflineMode
		{
			get; set;
		}

		public void OnScreenOff()
		{
			if (PreferenceManager.GetDefaultSharedPreferences(Application.Context)
											 .GetBoolean(
												Application.Context.GetString(Resource.String.LockWhenScreenOff_key),
												false))
			{
				App.Kp2a.LockDatabase();
			}
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
		public const string PrefErrorreportmode = "pref_ErrorReportMode";
		public const string PrefHaspendingerrorreport = "pref_hasPendingErrorReport";

		public App (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}

        public static readonly Kp2aApp Kp2a = new Kp2aApp();

		public enum ErrorReportMode
		{
			AskAgain=0,
			Disabled=1,
			Enabled=2
		}
        
		public override void OnCreate() {
			base.OnCreate();

			Kp2aLog.Log("Creating application "+PackageName+". Version=" + PackageManager.GetPackageInfo(PackageName, 0).VersionCode);

            Kp2a.OnCreate(this);
			AndroidEnvironment.UnhandledExceptionRaiser += MyApp_UnhandledExceptionHandler;
#if !NoNet
			Kp2aLog.OnUnexpectedError += (sender, exception) =>
			{
				var currentErrorReportMode = GetErrorReportMode(ApplicationContext);
				if (currentErrorReportMode != ErrorReportMode.Disabled)
				{
					Xamarin.Insights.Report(exception);
					if (Xamarin.Insights.DisableDataTransmission)
					{
						PreferenceManager.GetDefaultSharedPreferences(ApplicationContext)
							.Edit().PutBoolean(PrefHaspendingerrorreport, true).Commit();
					}
				}
			};
			Xamarin.Insights.Initialize("fed2b273ed2a964d0ba6acc3743e68f7a04da957", ApplicationContext);
			Xamarin.Insights.DisableExceptionCatching = true;
			var errorReportMode = GetErrorReportMode(ApplicationContext);
			SetErrorReportMode(ApplicationContext, errorReportMode);
#endif
		}

		public static ErrorReportMode GetErrorReportMode(Context ctx)
		{
			ErrorReportMode errorReportMode;
			Enum.TryParse(PreferenceManager.GetDefaultSharedPreferences(ctx)
				.GetString(PrefErrorreportmode, ErrorReportMode.AskAgain.ToString()), out errorReportMode);
			return errorReportMode;
		}


		
		
		public override void OnTerminate() {
			base.OnTerminate();
			Kp2aLog.Log("Terminating application");
            Kp2a.OnTerminate();
		}

#if !NoNet
		void MyApp_UnhandledExceptionHandler(object sender, RaiseThrowableEventArgs e)
		{
			Kp2aLog.LogUnexpectedError(e.Exception);
			Xamarin.Insights.Save();
			// Do your error handling here.
			//throw e.Exception;
		}
		protected override void Dispose(bool disposing)
		{
			AndroidEnvironment.UnhandledExceptionRaiser -= MyApp_UnhandledExceptionHandler;
			base.Dispose(disposing);
		}

		public static void SetErrorReportMode(Context ctx, ErrorReportMode mode)
		{
			Xamarin.Insights.DisableCollection = (mode == ErrorReportMode.Disabled);
			Xamarin.Insights.DisableDataTransmission = mode != ErrorReportMode.Enabled;

			var pref = PreferenceManager.GetDefaultSharedPreferences(ctx); 
			var edit = pref.Edit();
			if (mode != ErrorReportMode.AskAgain)
			{
				edit.PutBoolean(PrefHaspendingerrorreport, false);	
			}
			edit.PutString(PrefErrorreportmode, mode.ToString());
			edit.Commit();


		}
#else
		private void MyApp_UnhandledExceptionHandler(object sender, RaiseThrowableEventArgs e)
		{
			Kp2aLog.LogUnexpectedError(e.Exception);
		}
#endif
	}

}

