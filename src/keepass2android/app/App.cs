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
using System.Linq;
using System.Net.Security;
using Android.App;
using Android.Content;
using Android.Graphics;
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
using Android.Support.V4.Content;
#if !EXCLUDE_TWOFISH
using TwofishCipher;
#endif
using Keepass2android.Pluginsdk;
using keepass2android.Io;
using keepass2android.addons.OtpKeyProv;
using keepass2android.database.edit;
using KeePassLib.Interfaces;
using KeePassLib.Utility;
#if !NoNet
using Keepass2android.Javafilestorage;
using GoogleDriveFileStorage = keepass2android.Io.GoogleDriveFileStorage;
using PCloudFileStorage = keepass2android.Io.PCloudFileStorage;
#endif
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
	    public const int AppNameResource = Resource.String.app_name_debug;
#else
		public const string AppName = "@string/app_name";
        public const int AppNameResource = Resource.String.app_name;
#endif



#if DEBUG
        public const string PackagePart = "keepass2android_debug";
		public const string Searchable = "@xml/searchable_debug";
#else
		public const string PackagePart = "keepass2android";
		public const string Searchable = "@xml/searchable";
#endif
		public const int LauncherIcon = Resource.Mipmap.ic_launcher_online;
		public const int NotificationLockedIcon = Resource.Drawable.ic_notify_loaded;
		public const int NotificationUnlockedIcon = Resource.Drawable.ic_notify_locked;

	}
#endif
	/// <summary>
	/// Main implementation of the IKp2aApp interface for usage in the real app.
	/// </summary>
    public class Kp2aApp: IKp2aApp, ICacheSupervisor
	{
	    public void Lock(bool allowQuickUnlock = true)
	    {
			if (OpenDatabases.Any())
			{
				if (QuickUnlockEnabled && allowQuickUnlock &&
					GetDbForQuickUnlock().KpDatabase.MasterKey.ContainsType(typeof(KcpPassword)) &&
					!((KcpPassword)App.Kp2a.GetDbForQuickUnlock().KpDatabase.MasterKey.GetUserKey(typeof(KcpPassword))).Password.IsEmpty)
				{
					if (!QuickLocked)
					{
						Kp2aLog.Log("QuickLocking database");
					    QuickLocked = true;
					    LastOpenedEntry = null;
                        BroadcastDatabaseAction(Application.Context, Strings.ActionLockDatabase);
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

                    // Couldn't quick-lock, so unload database(s) instead
                    _openAttempts.Clear();
				    _openDatabases.Clear();
				    _currentDatabase = null;
				    LastOpenedEntry = null;
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
		    foreach (Database db in OpenDatabases)
		    {
		        Intent i = new Intent(action);

		        i.PutExtra(Strings.ExtraDatabaseFileDisplayname, GetFileStorage(db.Ioc).GetDisplayName(db.Ioc));
		        i.PutExtra(Strings.ExtraDatabaseFilepath, db.Ioc.Path);
		        foreach (var plugin in new PluginDatabase(ctx).GetPluginsWithAcceptedScope(Strings.ScopeDatabaseActions))
		        {
		            i.SetPackage(plugin);
		            ctx.SendBroadcast(i);
		        }
            }
			
		}



	    public Database LoadDatabase(IOConnectionInfo ioConnectionInfo, MemoryStream memoryStream, CompositeKey compositeKey, ProgressDialogStatusLogger statusLogger, IDatabaseFormat databaseFormat, bool makeCurrent)
	    {
	        var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
	        var createBackup = prefs.GetBoolean(Application.Context.GetString(Resource.String.CreateBackups_key), true)
                && !(new LocalFileStorage(this).IsLocalBackup(ioConnectionInfo));

	        MemoryStream backupCopy = new MemoryStream();
	        if (createBackup)
	        {

	            memoryStream.CopyTo(backupCopy);
	            backupCopy.Seek(0, SeekOrigin.Begin);
	            //reset stream if we need to reuse it later:
	            memoryStream.Seek(0, SeekOrigin.Begin);
	        }

            foreach (Database openDb in _openDatabases)
	        {
	            if (openDb.Ioc.IsSameFileAs(ioConnectionInfo))
	            {
                    //TODO check this earlier and simply open the database's root group
	                throw new Exception("Database already loaded!");
	            }
	            
	        }

	        _openAttempts.Add(ioConnectionInfo);
	        var newDb = new Database(new DrawableFactory(), this);
            newDb.LoadData(this, ioConnectionInfo, memoryStream, compositeKey, statusLogger, databaseFormat);



            if ((_currentDatabase == null) || makeCurrent)
                _currentDatabase = newDb;
	        _openDatabases.Add(newDb);



            if (createBackup)
            { 
		        statusLogger.UpdateMessage(Application.Context.GetString(Resource.String.UpdatingBackup));
		        Java.IO.File internalDirectory = IoUtil.GetInternalDirectory(Application.Context);
                string baseDisplayName = App.Kp2a.GetFileStorage(ioConnectionInfo).GetDisplayName(ioConnectionInfo);
                string targetPath = baseDisplayName;
		        var charsToRemove = "|\\?*<\":>+[]/'";
		        foreach (char c in charsToRemove)
		        {
		            targetPath = targetPath.Replace(c.ToString(), string.Empty);
		        }
                if (targetPath == "")
		            targetPath = "local_backup";
		       
		        var targetIoc = IOConnectionInfo.FromPath(new Java.IO.File(internalDirectory, targetPath).CanonicalPath);

                using (var transaction = new LocalFileStorage(App.Kp2a).OpenWriteTransaction(targetIoc, false))
		        {
		            using (var file = transaction.OpenFile())
		            {
		                backupCopy.CopyTo(file);
		                transaction.CommitWrite();
                    }

		        }
                Java.Lang.Object baseIocDisplayName = baseDisplayName;

                string keyfile = App.Kp2a.FileDbHelper.GetKeyFileForFile(ioConnectionInfo.Path);
		        App.Kp2a.StoreOpenedFileAsRecent(targetIoc, keyfile, false, Application.Context.
		            GetString(Resource.String.LocalBackupOf, new Java.Lang.Object[]{baseIocDisplayName}));

                prefs.Edit()
                    .PutBoolean(IoUtil.GetIocPrefKey(ioConnectionInfo, "has_local_backup"), true)
                    .PutBoolean(IoUtil.GetIocPrefKey(targetIoc, "is_local_backup"), true)
                    .Commit();


            }
		    else
		    {
		        prefs.Edit()
		            .PutBoolean(IoUtil.GetIocPrefKey(ioConnectionInfo, "has_local_backup"), false) //there might be an older local backup, but we won't "advertise" this anymore
		            .Commit();
            }

            UpdateOngoingNotification();

	        return newDb;
	    }

	    

	    public void CloseDatabase(Database db)
	    {
	        if (!_openDatabases.Contains(db))
	            throw new Exception("Cannot close database which is not open!");
	        if (_openDatabases.Count == 1)
	        {
	            Lock(false);
                return;
	        }
	        if (LastOpenedEntry != null && db.EntriesById.ContainsKey(LastOpenedEntry.Uuid))
	        {
	            LastOpenedEntry = null;
	        }
            
	        _openDatabases.Remove(db);
	        if (_currentDatabase == db)
	            _currentDatabase = _openDatabases.First();
            UpdateOngoingNotification();
            //TODO broadcast event so affected activities can close/update? 
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
		    if (DatabaseIsUnlocked || QuickLocked)
		    {
		        ContextCompat.StartForegroundService(ctx, new Intent(ctx, typeof(OngoingNotificationsService)));
            }
		    else
		    {
                //Anrdoid 8 requires that we call StartForeground() shortly after starting the service with StartForegroundService.
                //This is not possible when we're closing the service. In this case we don't use the StopSelf in the OngoingNotificationsService.OnStartCommand() anymore but directly stop the service.

                OngoingNotificationsService.CancelNotifications(ctx); //The docs are not 100% clear if OnDestroy() will be called immediately. So make sure the notifications are up to date.

		        ctx.StopService(new Intent(ctx, typeof(OngoingNotificationsService)));
		    }
		}
        
		public bool DatabaseIsUnlocked
		{
			get { return OpenDatabases.Any() && !QuickLocked; }
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

        /// <summary>
        /// See comments to EntryEditActivityState.
        /// </summary>
        internal EntryEditActivityState EntryEditActivityState = null;

        public FileDbHelper FileDbHelper;
		private List<IFileStorage> _fileStorages;

        private readonly List<IOConnectionInfo> _openAttempts = new List<IOConnectionInfo>(); //stores which files have been attempted to open. Used to avoid that we repeatedly try to load files which failed to load.
	    private readonly List<Database> _openDatabases = new List<Database>();
	    private readonly List<IOConnectionInfo> _childDatabases = new List<IOConnectionInfo>(); //list of databases which were opened as child databases
        private Database _currentDatabase;

	    public IEnumerable<Database> OpenDatabases
	    {
	        get { return _openDatabases; }
	    }

	    public readonly HashSet<PwGroup> dirty = new HashSet<PwGroup>(new PwGroupEqualityFromIdComparer());
	    
	    public HashSet<PwGroup> DirtyGroups {  get { return dirty; } }

	    public bool AttemptedToOpenBefore(IOConnectionInfo ioc)
	    {
	        foreach (var attemptedIoc in _openAttempts)
	        {
                if (attemptedIoc.IsSameFileAs(ioc))
                    return true;
	        }
	        return false;
	    }


        public void MarkAllGroupsAsDirty()
	    {
            foreach (var db in OpenDatabases)
	        foreach (PwGroup group in db.GroupsById.Values)
	        {
	            DirtyGroups.Add(group);
	        }


	    }


        /// <summary>
        /// Information about the last opened entry. Includes the entry but also transformed fields.
        /// </summary>
        public PwEntryOutput LastOpenedEntry { get; set; }

	    public Database CurrentDb
	    {
	      get { return _currentDatabase; }
	        set
	        {
	            if (!OpenDatabases.Contains(value))
	                throw new Exception("Cannot set database as current. Not in list of opened databases!");
	            _currentDatabase = value;
	        }
	    } 

        public Database GetDbForQuickUnlock()
	    {
	        return OpenDatabases.FirstOrDefault();
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
            if (CurrentDb?.DidOpenFileChange() == true)
            {
                if (CurrentDb.ReloadRequested)
                {
                    activity.SetResult(KeePass.ExitReloadDb);
                    activity.Finish();
                }
                else
                {
                    AskForReload(activity);
                }
                
            }
        }

	    public void LockSingleDatabase(Database databaseToLock, bool allowQuickUnlock)
	    {
            //TODO implement
	        throw new Exception("lock single is not implemented");
	    }

	    private void AskForReload(Activity activity)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			builder.SetTitle(activity.GetString(Resource.String.AskReloadFile_title));

			builder.SetMessage(activity.GetString(Resource.String.AskReloadFile));

			builder.SetPositiveButton(activity.GetString(Android.Resource.String.Yes),
				(dlgSender, dlgEvt) =>
				{
				    CurrentDb.ReloadRequested = true;
					activity.SetResult(KeePass.ExitReloadDb);
					activity.Finish();

				});

			builder.SetNegativeButton(activity.GetString(Android.Resource.String.No), (dlgSender, dlgEvt) =>
			{

			});


			Dialog dialog = builder.Create();
			dialog.Show();
		}

		public void StoreOpenedFileAsRecent(IOConnectionInfo ioc, string keyfile, bool updateTimestamp, string displayName = "")
        {
            FileDbHelper.CreateFile(ioc, keyfile, updateTimestamp, displayName);
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
			if (key == "ic_storage_skydrive")
				key = "ic_storage_onedrive"; //resource was renamed. do this to avoid crashes with legacy file entries.
			var field = typeof(Resource.Drawable).GetField(key);
			if (field == null)
				throw new Exception("Invalid key " + key);
			return Application.Context.Resources.GetDrawable((int)field.GetValue(null));
		}

		public void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey, EventHandler<DialogClickEventArgs> yesHandler, EventHandler<DialogClickEventArgs> noHandler, EventHandler<DialogClickEventArgs> cancelHandler, Context ctx, string messageSuffix)
		{
			AskYesNoCancel(titleKey, messageKey, UiStringKey.yes, UiStringKey.no,
				yesHandler, noHandler, cancelHandler, ctx, messageSuffix);
		}

		public void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey,
			UiStringKey yesString, UiStringKey noString,
			EventHandler<DialogClickEventArgs> yesHandler,
			EventHandler<DialogClickEventArgs> noHandler,
			EventHandler<DialogClickEventArgs> cancelHandler,
			Context ctx, string messageSuffix = "")
		{
			AskYesNoCancel(titleKey, messageKey, yesString, noString, yesHandler, noHandler, cancelHandler, null, ctx, messageSuffix);
		}

		public void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey,
			UiStringKey yesString, UiStringKey noString,
			EventHandler<DialogClickEventArgs> yesHandler,
            EventHandler<DialogClickEventArgs> noHandler,
            EventHandler<DialogClickEventArgs> cancelHandler,
			EventHandler dismissHandler,
            Context ctx, string messageSuffix = "")
        {
	        Handler handler = new Handler(Looper.MainLooper);
			handler.Post(() =>
				{
					AlertDialog.Builder builder = new AlertDialog.Builder(ctx);
					builder.SetTitle(GetResourceString(titleKey));

					builder.SetMessage(GetResourceString(messageKey)+(messageSuffix != "" ? " " + messageSuffix: ""));

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
					fileStorage = new CachingFileStorage(innerFileStorage, Application.Context, this);
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
							new OneDriveFileStorage(Application.Context, this),
							new SftpFileStorage(Application.Context, this),
							new NetFtpFileStorage(Application.Context, this),
							new WebDavFileStorage(this),
							new PCloudFileStorage(Application.Context, this),
							//new LegacyWebDavStorage(this),
							//new LegacyFtpStorage(this),
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

		public bool AlwaysFailOnValidationError()
		{
			return true;
		}

		public bool OnValidationError()
		{
			return false;
		}

		public RemoteCertificateValidationCallback CertificateValidationCallback
		{
			get
			{
				switch (GetValidationMode())
				{
					case ValidationMode.Ignore:
						return (sender, certificate, chain, errors) => true;
					case ValidationMode.Warn:
						return (sender, certificate, chain, errors) =>
						{
							if (errors != SslPolicyErrors.None)
								ShowValidationWarning(errors.ToString());
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

		private ValidationMode GetValidationMode()
		{
			var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);

			ValidationMode validationMode = ValidationMode.Warn;

			string strValMode = prefs.GetString(Application.Context.Resources.GetString(Resource.String.AcceptAllServerCertificates_key),
												 Application.Context.Resources.GetString(Resource.String.AcceptAllServerCertificates_default));

			if (strValMode == "IGNORE")
				validationMode = ValidationMode.Ignore;
			else if (strValMode == "ERROR")
				validationMode = ValidationMode.Error;
			return validationMode;
		}

		public bool CheckForDuplicateUuids
		{
			get
			{
				var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
				return prefs.GetBoolean(Application.Context.GetString(Resource.String.CheckForDuplicateUuids_key), true);
			}
		}
#if !NoNet
		public ICertificateErrorHandler CertificateErrorHandler
		{
			get { return new CertificateErrorHandlerImpl(this); }
		}
	    


	    public class CertificateErrorHandlerImpl : Java.Lang.Object, Keepass2android.Javafilestorage.ICertificateErrorHandler
		{
			private readonly Kp2aApp _app;

			public CertificateErrorHandlerImpl(Kp2aApp app)
			{
				_app = app;
			}

			public bool AlwaysFailOnValidationError()
			{
				return _app.GetValidationMode() == ValidationMode.Error;
			}


			public bool OnValidationError(string errorMessage)
			{
				switch (_app.GetValidationMode())
				{
					case ValidationMode.Ignore:
						return true;
					case ValidationMode.Warn:
						_app.ShowValidationWarning(errorMessage);
						return true;
					case ValidationMode.Error:
						return false;
					default:
						throw new Exception("Unexpected Validation mode!");
				}

			}
		}
#endif
		private void ShowValidationWarning(string error)
		{
			ShowToast(Application.Context.GetString(Resource.String.CertificateWarning, error));
		}


		public enum ValidationMode
		{
			Ignore, Warn, Error
		}


		internal void OnTerminate()
        {

            _openDatabases.Clear();
            _currentDatabase = null;
            
            if (FileDbHelper != null && FileDbHelper.IsOpen())
            {
                FileDbHelper.Close();
            }
            GC.Collect();
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

        
        public Database CreateNewDatabase(bool makeCurrent)
        {
            Database newDatabase = new Database(new DrawableFactory(), this);
            if ((_currentDatabase == null) || makeCurrent)
                _currentDatabase = newDatabase;
            _openDatabases.Add(newDatabase);
            return newDatabase;
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
		    if (e is DocumentAccessRevokedException)
		        errorMessage = GetResourceString(UiStringKey.DocumentAccessRevoked);

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
			new CachingFileStorage(new LocalFileStorage(this), Application.Context, this).ClearCache();
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
				return new LocalFileStorage(this);
			else
			{
				IFileStorage innerFileStorage = GetCloudFileStorage(iocInfo);

				
				if (DatabaseCacheEnabled)
				{
					return new OtpAuxCachingFileStorage(innerFileStorage, Application.Context, new OtpAuxCacheSupervisor(this));
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
				App.Kp2a.Lock();
			}
		}

	    public Database TryGetDatabase(IOConnectionInfo dbIoc)
	    {
	        foreach (Database db in OpenDatabases)
	        {
	            if (db.Ioc.IsSameFileAs(dbIoc))
	                return db;
	        }
	        return null;
	    }

	    public Database GetDatabase(IOConnectionInfo dbIoc)
	    {
	        Database result = TryGetDatabase(dbIoc);
            if (result == null)
	            throw new Exception("Database not found for dbIoc!");
	        return result;
	    }

	    public Database GetDatabase(string databaseId)
	    {
	        foreach (Database db in OpenDatabases)
	        {
	            if (IoUtil.IocAsHexString(db.Ioc) == databaseId)
	                return db;
	        }
	        throw new Exception("Database not found for databaseId!");
        }

        public PwGroup FindGroup(PwUuid uuid)
	    {
	        foreach (Database db in OpenDatabases)
	        {
	            PwGroup result;
	            if (db.GroupsById.TryGetValue(uuid, out result))
	                return result;
	        }
	        return null;
	    }
	    public IStructureItem FindStructureItem(PwUuid uuid)
	    {
	        
	        foreach (Database db in OpenDatabases)
	        {
	            PwGroup resultGroup;
                if (db.GroupsById.TryGetValue(uuid, out resultGroup))
	                return resultGroup;
	            PwEntry resultEntry;
	            if (db.EntriesById.TryGetValue(uuid, out resultEntry))
	                return resultEntry;
            }
	        return null;
	    }

	    public bool TrySelectCurrentDb(IOConnectionInfo ioConnection)
	    {
	        var matchingOpenDb = App.Kp2a.OpenDatabases.FirstOrDefault(db => db.Ioc.IsSameFileAs(ioConnection));
	        if (matchingOpenDb != null)
	        {
	            CurrentDb = matchingOpenDb;
	            return true;
            }
            return false;
	        
	    }

	    public Database FindDatabaseForElement(IStructureItem element)
	    {
	        var db = TryFindDatabaseForElement(element);
            if (db == null)
                throw new Exception("Database element not found!");
	        return db;
	    }

	    public Database TryFindDatabaseForElement(IStructureItem element)
	    {
            foreach (var db in OpenDatabases)
            {
                if (db.Elements.Contains(element))
                    return db;
            }
	        return null;
	    }

	    public void RegisterChildDatabase(IOConnectionInfo ioc)
	    {
	        _childDatabases.Add(ioc);
	    }

	    public bool IsChildDatabase(Database db)
	    {
	        return _childDatabases.Any(ioc => ioc.IsSameFileAs(db.Ioc));
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

	    public const string NotificationChannelIdUnlocked = "channel_db_unlocked_5";
	    public const string NotificationChannelIdQuicklocked = "channel_db_quicklocked_5";
	    public const string NotificationChannelIdEntry = "channel_db_entry_5";

        public App (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}

        public static readonly Kp2aApp Kp2a = new Kp2aApp();

	    private static void InitThaiCalendarCrashFix()
	    {
	        var localeIdentifier = Java.Util.Locale.Default.ToString();
	        if (localeIdentifier == "th_TH")
	        {
	            new System.Globalization.ThaiBuddhistCalendar();
	        }
	    }

        public override void OnCreate()
        {
            InitThaiCalendarCrashFix();

            base.OnCreate();

			Kp2aLog.Log("Creating application "+PackageName+". Version=" + PackageManager.GetPackageInfo(PackageName, 0).VersionCode);

		    CreateNotificationChannels();

            Kp2a.OnCreate(this);
			AndroidEnvironment.UnhandledExceptionRaiser += MyApp_UnhandledExceptionHandler;

		    IntentFilter intentFilter = new IntentFilter();
		    intentFilter.AddAction(Intents.LockDatabase);
		    intentFilter.AddAction(Intents.CloseDatabase);
            Context.RegisterReceiver(broadcastReceiver, intentFilter);
        }

	    private ApplicationBroadcastReceiver broadcastReceiver = new ApplicationBroadcastReceiver();


        private void CreateNotificationChannels()
	    {
	        if ((int)Build.VERSION.SdkInt < 26)
	            return;
	        NotificationManager mNotificationManager =
	            (NotificationManager)GetSystemService(Context.NotificationService);

	        {
	            string name = GetString(Resource.String.DbUnlockedChannel_name);
	            string desc = GetString(Resource.String.DbUnlockedChannel_desc);
	            NotificationChannel mChannel =
	                new NotificationChannel(NotificationChannelIdUnlocked, name, NotificationImportance.Min);
	            mChannel.Description = desc;
	            mChannel.EnableLights(false);
	            mChannel.EnableVibration(false);
	            mChannel.SetSound(null, null);
	            mChannel.SetShowBadge(false);
	            mNotificationManager.CreateNotificationChannel(mChannel);
	        }

	        {
	            string name = GetString(Resource.String.DbQuicklockedChannel_name);
	            string desc = GetString(Resource.String.DbQuicklockedChannel_desc);
	            NotificationChannel mChannel =
	                new NotificationChannel(NotificationChannelIdQuicklocked, name, NotificationImportance.Min);
	            mChannel.Description = desc;
	            mChannel.EnableLights(false);
	            mChannel.EnableVibration(false);
	            mChannel.SetSound(null, null);
	            mChannel.SetShowBadge(false);
                mNotificationManager.CreateNotificationChannel(mChannel);
	        }

	        {
	            string name = GetString(Resource.String.EntryChannel_name);
	            string desc = GetString(Resource.String.EntryChannel_desc);
	            NotificationChannel mChannel =
	                new NotificationChannel(NotificationChannelIdEntry, name, NotificationImportance.Default);
	            mChannel.Description = desc;
	            mChannel.EnableLights(false);
	            mChannel.EnableVibration(false);
	            mChannel.SetSound(null, null);
	            mChannel.SetShowBadge(false);
                mNotificationManager.CreateNotificationChannel(mChannel);
	        }
        }


        public override void OnTerminate() {
			base.OnTerminate();
			Kp2aLog.Log("Terminating application");
            Kp2a.OnTerminate();
            Context.UnregisterReceiver(broadcastReceiver);
		}

		private void MyApp_UnhandledExceptionHandler(object sender, RaiseThrowableEventArgs e)
		{
			Kp2aLog.LogUnexpectedError(e.Exception);
		}

	}

}

