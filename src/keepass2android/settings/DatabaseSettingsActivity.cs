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
using System.Globalization;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Preferences;
using Java.IO;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;
using keepass2android.Utils;

namespace keepass2android
{
	/// <summary>
	/// Activity to configure the application and database settings. The database must be unlocked, and this activity will close if it becomes locked.
	/// </summary>
	[Activity (Label = "@string/app_name", Theme="@style/NoTitleBar")]			
	public class DatabaseSettingsActivity : LockingClosePreferenceActivity 
	{
		private ActivityDesign _design;
		private AppSettingsActivity.KeyboardSwitchPrefManager _switchPrefManager;

		public DatabaseSettingsActivity()
		{
			_design = new ActivityDesign(this);
		}

		public static void Launch(Activity ctx)
		{
			ctx.StartActivity(new Intent(ctx, typeof(DatabaseSettingsActivity)));
		}

		protected override void OnCreate(Bundle savedInstanceState) 
		{
			_design.ApplyTheme();

			base.OnCreate(savedInstanceState);
			
			AddPreferencesFromResource(Resource.Xml.preferences);

			// Re-use the change handlers for the application settings
			FindPreference(GetString(Resource.String.keyfile_key)).PreferenceChange += AppSettingsActivity.OnRememberKeyFileHistoryChanged;
			FindPreference(GetString(Resource.String.ShowUnlockedNotification_key)).PreferenceChange += AppSettingsActivity.OnShowUnlockedNotificationChanged;
			Util.PrepareNoDonatePreference(this, FindPreference(GetString(Resource.String.NoDonateOption_key)));
			Preference designPref = FindPreference(GetString(Resource.String.design_key));
			if (!_design.HasThemes())
			{
				try
				{
					((PreferenceScreen)FindPreference(GetString(Resource.String.display_prefs_key))).RemovePreference(designPref);
				}
				catch (Exception ex)
				{
					Kp2aLog.Log(ex.ToString());
					throw;
				}
			}
			else
			{
				designPref.PreferenceChange += (sender, args) => Recreate();
			}
			

			Database db = App.Kp2a.GetDb();
			
			Preference rounds = FindPreference(GetString(Resource.String.rounds_key));
			rounds.PreferenceChange += (sender, e) => SetRounds(db, e.Preference);
			rounds.Enabled = db.CanWrite;

			Preference defaultUser = FindPreference(GetString(Resource.String.default_username_key));
			defaultUser.Enabled = db.CanWrite;
			((EditTextPreference)defaultUser).EditText.Text = db.KpDatabase.DefaultUserName;
			((EditTextPreference)defaultUser).Text = db.KpDatabase.DefaultUserName;
			defaultUser.PreferenceChange += (sender, e) => 
			{
				DateTime previousUsernameChanged = db.KpDatabase.DefaultUserNameChanged;
				String previousUsername = db.KpDatabase.DefaultUserName;
				db.KpDatabase.DefaultUserName = e.NewValue.ToString();
				
				SaveDb save = new SaveDb(this, App.Kp2a, new ActionOnFinish( (success, message) => 
				{
					if (!success)
					{
						db.KpDatabase.DefaultUserName = previousUsername;
						db.KpDatabase.DefaultUserNameChanged = previousUsernameChanged;
						Toast.MakeText(this, message, ToastLength.Long).Show();
					}
				}));
				ProgressTask pt = new ProgressTask(App.Kp2a, this, save);
				pt.Run();
			};

			Preference databaseName = FindPreference(GetString(Resource.String.database_name_key));
			databaseName.Enabled = db.CanWrite;
			((EditTextPreference)databaseName).EditText.Text = db.KpDatabase.Name;
			((EditTextPreference)databaseName).Text = db.KpDatabase.Name;
			databaseName.PreferenceChange += (sender, e) => 
			{
				DateTime previousNameChanged = db.KpDatabase.NameChanged;
				String previousName = db.KpDatabase.Name;
				db.KpDatabase.Name = e.NewValue.ToString();
					
				SaveDb save = new SaveDb(this, App.Kp2a, new ActionOnFinish( (success, message) => 
				{
					if (!success)
					{
						db.KpDatabase.Name = previousName;
						db.KpDatabase.NameChanged = previousNameChanged;
						Toast.MakeText(this, message, ToastLength.Long).Show();
					}
					else
					{
						// Name is reflected in notification, so update it
						App.Kp2a.UpdateOngoingNotification();
					}
				}));
                ProgressTask pt = new ProgressTask(App.Kp2a, this, save);
				pt.Run();
			};

			Preference changeMaster = FindPreference(GetString(Resource.String.master_pwd_key));
			if (App.Kp2a.GetDb().CanWrite)
			{
				changeMaster.Enabled = true;
				changeMaster.PreferenceClick += delegate {
						new SetPasswordDialog(this).Show();
					};
			}

			try
			{
				//depending on Android version, we offer to use a transparent icon for QuickUnlock or use the notification priority (since API level 16)
				Preference hideQuickUnlockTranspIconPref = FindPreference(GetString(Resource.String.QuickUnlockIconHidden_key));
				Preference hideQuickUnlockIconPref = FindPreference(GetString(Resource.String.QuickUnlockIconHidden16_key));
				var quickUnlockScreen = ((PreferenceScreen)FindPreference(GetString(Resource.String.QuickUnlock_prefs_key)));
				if ((int)Android.OS.Build.VERSION.SdkInt >= 16)
				{
					quickUnlockScreen.RemovePreference(hideQuickUnlockTranspIconPref);
					FindPreference(GetString(Resource.String.ShowUnlockedNotification_key)).PreferenceChange += (sender, args) => App.Kp2a.UpdateOngoingNotification();
					hideQuickUnlockIconPref.PreferenceChange += delegate { App.Kp2a.UpdateOngoingNotification(); };
				}
				else
				{
					//old version: only show transparent quickUnlock and no option to hide unlocked icon:
					quickUnlockScreen.RemovePreference(hideQuickUnlockIconPref);
					FindPreference(GetString(Resource.String.QuickUnlockIconHidden_key)).PreferenceChange +=
						delegate { App.Kp2a.UpdateOngoingNotification(); };
				
					((PreferenceScreen)FindPreference(GetString(Resource.String.display_prefs_key))).RemovePreference(
						FindPreference(GetString(Resource.String.ShowUnlockedNotification_key)));
				}
			}
			catch (Exception ex)
			{
				Kp2aLog.Log(ex.ToString());
			}

			SetRounds(db, rounds);
				
			Preference algorithm = FindPreference(GetString(Resource.String.algorithm_key));
			SetAlgorithm(db, algorithm);

			UpdateImportDbPref();
			UpdateImportKeyfilePref();
			//AppSettingsActivity.PrepareKeyboardSwitchingPreferences(this);
			_switchPrefManager = new AppSettingsActivity.KeyboardSwitchPrefManager(this);
			AppSettingsActivity.PrepareSeparateNotificationsPreference(this);
		}

		private void UpdateImportKeyfilePref()
		{
			var prefs = PreferenceManager.GetDefaultSharedPreferences(this);
			var rememberKeyfile = prefs.GetBoolean(GetString(Resource.String.keyfile_key), Resources.GetBoolean(Resource.Boolean.keyfile_default));

			Preference importDb = FindPreference("import_keyfile_prefs");
			importDb.Summary = "";

			if (!rememberKeyfile)
			{
				importDb.Summary = GetString(Resource.String.KeyfileMoveRequiresRememberKeyfile);
				importDb.Enabled = false;
				return;
			}
			CompositeKey masterKey = App.Kp2a.GetDb().KpDatabase.MasterKey;
			if (masterKey.ContainsType(typeof (KcpKeyFile)))
			{
				IOConnectionInfo iocKeyfile = ((KcpKeyFile)masterKey.GetUserKey(typeof (KcpKeyFile))).Ioc;
				if (iocKeyfile.IsLocalFile() && IoUtil.IsInInternalDirectory(iocKeyfile.Path, this))
				{
					importDb.Enabled = false;
					importDb.Summary = GetString(Resource.String.FileIsInInternalDirectory);
				}
				else
				{
					importDb.Enabled = true;
					importDb.PreferenceClick += (sender, args) => { MoveKeyfileToInternalFolder();};
				}
				

			}
			else
			{
				importDb.Enabled = false;
			}
		}

		private void MoveKeyfileToInternalFolder()
		{
			Func<Action> copyAndReturnPostExecute = () =>
			{
				try
				{	
					CompositeKey masterKey = App.Kp2a.GetDb().KpDatabase.MasterKey;
					var sourceIoc = ((KcpKeyFile)masterKey.GetUserKey(typeof(KcpKeyFile))).Ioc;
					var newIoc = ImportFileToInternalDirectory(sourceIoc);
					((KcpKeyFile)masterKey.GetUserKey(typeof(KcpKeyFile))).ResetIoc(newIoc);
					var keyfileString = IOConnectionInfo.SerializeToString(newIoc);
					App.Kp2a.StoreOpenedFileAsRecent(App.Kp2a.GetDb().Ioc, keyfileString);
					return () =>
					{
						UpdateImportKeyfilePref();
						var builder = new AlertDialog.Builder(this);
						builder
							.SetMessage(Resource.String.KeyfileMoved);
						builder.SetPositiveButton(Android.Resource.String.Ok, (sender, args) => { });
						builder.Show();

					};




				}
				catch (Exception e)
				{
					return () =>
					{
						Toast.MakeText(this, App.Kp2a.GetResourceString(UiStringKey.ErrorOcurred) + " " + e.Message, ToastLength.Long).Show();
					};
				}



			};

			new SimpleLoadingDialog(this, GetString(Resource.String.CopyingFile), false,
								  copyAndReturnPostExecute
				).Execute();

		}
		private void UpdateImportDbPref()
		{
//Import db/key file preferences:
			Preference importDb = FindPreference("import_db_prefs");
			if (!App.Kp2a.GetDb().Ioc.IsLocalFile())
			{
				importDb.Summary = GetString(Resource.String.OnlyAvailableForLocalFiles);
				importDb.Enabled = false;
			}
			else
			{
				if (IoUtil.IsInInternalDirectory(App.Kp2a.GetDb().Ioc.Path, this))
				{
					importDb.Summary = GetString(Resource.String.FileIsInInternalDirectory);
					importDb.Enabled = false;
				}
				else
				{
					importDb.Enabled = true;
					importDb.PreferenceClick += delegate { MoveDbToInternalFolder(); };
				}
			}
		}

		private void MoveDbToInternalFolder()
		{
			Func<Action> copyAndReturnPostExecute = () =>
				{
					try
					{
						var sourceIoc = App.Kp2a.GetDb().Ioc;
						var newIoc = ImportFileToInternalDirectory(sourceIoc);
						return () =>
							{
								var builder = new AlertDialog.Builder(this);
								builder
									.SetMessage(Resource.String.DatabaseFileMoved);
								builder.SetPositiveButton(Android.Resource.String.Ok, (sender, args) =>
								                                                      PasswordActivity.Launch(this, newIoc, new NullTask()));
								builder.Show();

							};




					}
					catch (Exception e)
					{
						return () =>
							{
								Toast.MakeText(this, App.Kp2a.GetResourceString(UiStringKey.ErrorOcurred) + " " + e.Message, ToastLength.Long).Show();
							};
					}


					
				};

			new SimpleLoadingDialog(this, GetString(Resource.String.CopyingFile), false,
								  copyAndReturnPostExecute
				).Execute();
			
		}

		private IOConnectionInfo ImportFileToInternalDirectory(IOConnectionInfo sourceIoc)
		{
			string targetPath = UrlUtil.GetFileName(sourceIoc.Path);
			targetPath = targetPath.Trim("|\\?*<\":>+[]/'".ToCharArray());
			if (targetPath == "")
				targetPath = "imported";
			if (new File(FilesDir, targetPath).Exists())
			{
				int c = 1;
				var ext = UrlUtil.GetExtension(targetPath);
				var filenameWithoutExt = UrlUtil.StripExtension(targetPath);
				do
				{
					c++;
					targetPath = filenameWithoutExt + c;
					if (!String.IsNullOrEmpty(ext))
						targetPath += "." + ext;
				} while (new File(FilesDir, targetPath).Exists());
			}
			var targetIoc = IOConnectionInfo.FromPath(new File(FilesDir, targetPath).CanonicalPath);

			IoUtil.Copy(targetIoc, sourceIoc, App.Kp2a);
			return targetIoc;
		}

		private void SetRounds(Database db, Preference rounds)
		{
			rounds.Summary = db.KpDatabase.KeyEncryptionRounds.ToString(CultureInfo.InvariantCulture);
		}
		
		private void SetAlgorithm(Database db, Preference algorithm) 
		{
			algorithm.Summary = CipherPool.GlobalPool.GetCipher(db.KpDatabase.DataCipherUuid).DisplayName;
		}
	}
}

