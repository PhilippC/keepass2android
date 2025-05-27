
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
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Preference;

using keepass2android.Io;
using keepass2android.settings;
using keepass2android;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Utility;
using KeePassLib;
using static Android.Icu.Text.CaseMap;
using Object = Java.Lang.Object;
using Preference = AndroidX.Preference.Preference;
using PreferenceFragment = AndroidX.Preference.PreferenceFragment;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using Android.Views.Autofill;
using Google.Android.Material.Dialog;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;
using String = System.String;
using KeePassLib.Cryptography.Cipher;
using keepass2android.Utils;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using FragmentManager = AndroidX.Fragment.App.FragmentManager;

namespace keepass2android
{
    namespace settings
    {
        #region preference fragments
        public class PreferenceFragmentWithResource : PreferenceFragmentCompat
        {
            private readonly int _resXml;

            public PreferenceFragmentWithResource(int resXml)
            {
                _resXml = resXml;
            }
            public override void OnCreatePreferences(Bundle savedInstanceState, string rootKey)
            {
                SetPreferencesFromResource(_resXml, rootKey);
            }

            public override void OnResume()
            {
                base.OnResume();
                if (Activity != null && !string.IsNullOrEmpty(PreferenceScreen?.Title))
                {
                    Activity.Title = PreferenceScreen?.Title;
                }
             
            }
        }
        public class MainPreferenceFragment : PreferenceFragmentWithResource
        {
            public MainPreferenceFragment() : base(Resource.Xml.preferences)
            {
            }

            public override void OnCreate(Bundle? savedInstanceState)
            {
                
                base.OnCreate(savedInstanceState);
                FindPreference(GetString(Resource.String.db_key)).Enabled = (App.Kp2a.CurrentDb != null);

            }
        }
        public class SettingsFragmentDatabase : PreferenceFragmentWithResource
        {
            public SettingsFragmentDatabase() : base(Resource.Xml.pref_database)
            {
            }
            private void PrepareDefaultUsername(Database db)
            {
                Preference defaultUser = FindPreference(GetString(Resource.String.default_username_key));
                if (!db.DatabaseFormat.HasDefaultUsername)
                {
                    ((PreferenceScreen)FindPreference(GetString(Resource.String.db_key))).RemovePreference(defaultUser);
                }
                else
                {
                    defaultUser.Enabled = db.CanWrite;
                    //TODO test this
                    ((EditTextPreference)defaultUser).Text = db.KpDatabase.DefaultUserName;
                    defaultUser.PreferenceChange += (sender, e) =>
                    {
                        DateTime previousUsernameChanged = db.KpDatabase.DefaultUserNameChanged;
                        var previousUsername = db.KpDatabase.DefaultUserName;
                        db.KpDatabase.DefaultUserName = e.NewValue.ToString();

                        SaveDb save = new SaveDb(App.Kp2a, App.Kp2a.CurrentDb, new ActionOnOperationFinished(App.Kp2a, (success, message, context) =>
                        {
                            if (!success)
                            {
                                db.KpDatabase.DefaultUserName = previousUsername;
                                db.KpDatabase.DefaultUserNameChanged = previousUsernameChanged;
                                App.Kp2a.ShowMessage(context, message,  MessageSeverity.Error);
                            }
                        }));
                        BlockingOperationRunner pt = new BlockingOperationRunner(App.Kp2a, save);
                        pt.Run();
                    };
                }
            }


            private void PrepareTemplates(Database db)
            {
                Preference pref = FindPreference("AddTemplates_pref_key");
                if ((!db.DatabaseFormat.SupportsTemplates) || (AddTemplateEntries.ContainsAllTemplates(App.Kp2a.CurrentDb)))
                {
                    pref.Enabled = false;
                }
                else
                {
                    pref.PreferenceClick += (sender, args) =>
                    {
                        BlockingOperationRunner pt = new BlockingOperationRunner(App.Kp2a, 
                                        new AddTemplateEntries(App.Kp2a, new ActionOnOperationFinished(App.Kp2a,
                                            delegate
                                            {
                                                pref.Enabled = false;
                                            })));
                        pt.Run();
                    };
                }

            }

            private void PrepareMasterPassword()
            {
                Preference changeMaster = FindPreference(GetString(Resource.String.master_pwd_key));
                if (App.Kp2a.CurrentDb.CanWrite)
                {
                    changeMaster.Enabled = true;
                    changeMaster.PreferenceClick += delegate { new SetPasswordDialog(Activity).Show(); };
                }
            }

            private void PrepareDatabaseName(Database db)
            {
                Preference databaseName = FindPreference(GetString(Resource.String.database_name_key));
                if (!db.DatabaseFormat.HasDatabaseName)
                {
                    ((PreferenceScreen)FindPreference(GetString(Resource.String.db_key))).RemovePreference(databaseName);
                }
                else
                {
                    databaseName.Enabled = db.CanWrite;
                    ((EditTextPreference)databaseName).Text = db.KpDatabase.Name;
                    databaseName.PreferenceChange += (sender, e) =>
                    {
                        DateTime previousNameChanged = db.KpDatabase.NameChanged;
                        String previousName = db.KpDatabase.Name;
                        db.KpDatabase.Name = e.NewValue.ToString();

                        SaveDb save = new SaveDb(App.Kp2a, App.Kp2a.CurrentDb, new ActionOnOperationFinished(App.Kp2a, (success, message, context) =>
                        {
                            if (!success)
                            {
                                db.KpDatabase.Name = previousName;
                                db.KpDatabase.NameChanged = previousNameChanged;
                                App.Kp2a.ShowMessage(context, message,  MessageSeverity.Error);
                            }
                            else
                            {
                                // Name is reflected in notification, so update it
                                App.Kp2a.UpdateOngoingNotification();
                            }
                        }));
                        BlockingOperationRunner pt = new BlockingOperationRunner(App.Kp2a, save);
                        pt.Run();
                    };
                }
            }

            private void UpdateImportDbPref()
            {
                //Import db/key file preferences:
                Preference importDb = FindPreference("import_db_prefs");
                bool isLocalOrContent =
                    App.Kp2a.CurrentDb.Ioc.IsLocalFile() || App.Kp2a.CurrentDb.Ioc.Path.StartsWith("content://");
                if (!isLocalOrContent)
                {
                    importDb.Summary = GetString(Resource.String.OnlyAvailableForLocalFiles);
                    importDb.Enabled = false;
                }
                else
                {
                    if (IoUtil.IsInInternalDirectory(App.Kp2a.CurrentDb.Ioc.Path, Activity))
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
                        var sourceIoc = App.Kp2a.CurrentDb.Ioc;
                        var newIoc = IoUtil.ImportFileToInternalDirectory(sourceIoc, Activity, App.Kp2a);
                        return () =>
                        {
                            var builder = new MaterialAlertDialogBuilder(Activity);
                            builder
                                .SetMessage(Resource.String.DatabaseFileMoved);
                            builder.SetPositiveButton(Android.Resource.String.Ok, (sender, args) =>
                            {
                                var key = App.Kp2a.CurrentDb.KpDatabase.MasterKey;
                                App.Kp2a.CloseDatabase(App.Kp2a.CurrentDb);
                                PasswordActivity.Launch(Activity, newIoc, key, new ActivityLaunchModeSimple(), false);

                            });
                            builder.Show();

                        };




                    }
                    catch (System.Exception e)
                    {
                        return () =>
                        {
                            App.Kp2a.ShowMessage(Activity, App.Kp2a.GetResourceString(UiStringKey.ErrorOcurred) + " " + Util.GetErrorMessage(e),  MessageSeverity.Error);
                        };
                    }



                };

                new SimpleLoadingDialog(Activity, GetString(Resource.String.CopyingFile), false,
                                      copyAndReturnPostExecute
                    ).Execute();

            }

            private void UpdateImportKeyfilePref()
            {
                var prefs = PreferenceManager.GetDefaultSharedPreferences(Activity);
                var rememberKeyfile = prefs.GetBoolean(GetString(Resource.String.keyfile_key), Resources.GetBoolean(Resource.Boolean.keyfile_default));

                Preference importKeyfile = FindPreference("import_keyfile_prefs");
                Preference exportKeyfile = FindPreference("export_keyfile_prefs");
                importKeyfile.Summary = "";

                if (!rememberKeyfile)
                {
                    importKeyfile.Summary = GetString(Resource.String.KeyfileMoveRequiresRememberKeyfile);
                    importKeyfile.Enabled = false;
                    exportKeyfile.Enabled = false;
                    return;
                }
                CompositeKey masterKey = App.Kp2a.CurrentDb.KpDatabase.MasterKey;
                if (masterKey.ContainsType(typeof(KcpKeyFile)))
                {
                    IOConnectionInfo iocKeyfile = ((KcpKeyFile)masterKey.GetUserKey(typeof(KcpKeyFile))).Ioc;
                    if (iocKeyfile.IsLocalFile() && IoUtil.IsInInternalDirectory(iocKeyfile.Path, Activity))
                    {
                        importKeyfile.Enabled = false;
                        exportKeyfile.Enabled = true;
                        exportKeyfile.PreferenceClick += (sender, args) => { ExportKeyfileFromInternalFolder(); };
                        importKeyfile.Summary = GetString(Resource.String.FileIsInInternalDirectory);
                    }
                    else
                    {
                        exportKeyfile.Enabled = false;
                        importKeyfile.Enabled = true;
                        importKeyfile.PreferenceClick += (sender, args) => { MoveKeyfileToInternalFolder(); };
                    }


                }
                else
                {
                    exportKeyfile.Enabled = false;
                    importKeyfile.Enabled = false;
                }
            }



            private void ExportKeyfileFromInternalFolder()
            {
                StartActivity(new Intent(Activity.ApplicationContext, typeof(ExportKeyfileActivity)));

            }

            private void MoveKeyfileToInternalFolder()
            {
                Func<Action> copyAndReturnPostExecute = () =>
                {
                    try
                    {
                        CompositeKey masterKey = App.Kp2a.CurrentDb.KpDatabase.MasterKey;
                        var sourceIoc = ((KcpKeyFile)masterKey.GetUserKey(typeof(KcpKeyFile))).Ioc;
                        var newIoc = IoUtil.ImportFileToInternalDirectory(sourceIoc, Activity, App.Kp2a);
                        ((KcpKeyFile)masterKey.GetUserKey(typeof(KcpKeyFile))).ResetIoc(newIoc);
                        var keyfileString = IOConnectionInfo.SerializeToString(newIoc);
                        App.Kp2a.StoreOpenedFileAsRecent(App.Kp2a.CurrentDb.Ioc, keyfileString, false);
                        return () =>
                        {
                            UpdateImportKeyfilePref();
                            var builder = new MaterialAlertDialogBuilder(Activity);
                            builder
                                .SetMessage(Resource.String.KeyfileMoved);
                            builder.SetPositiveButton(Android.Resource.String.Ok, (sender, args) => { });
                            builder.Show();

                        };




                    }
                    catch (System.Exception e)
                    {
                        return () =>
                        {
                            App.Kp2a.ShowMessage(Activity, App.Kp2a.GetResourceString(UiStringKey.ErrorOcurred) + " " + Util.GetErrorMessage(e),  MessageSeverity.Error);
                        };
                    }



                };

                new SimpleLoadingDialog(Activity, GetString(Resource.String.CopyingFile), false,
                                      copyAndReturnPostExecute
                    ).Execute();

            }

            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);
                var db = App.Kp2a.CurrentDb;
                if (db != null)
                {
                    PrepareDefaultUsername(App.Kp2a.CurrentDb);

                    PrepareDatabaseName(db);
                    PrepareMasterPassword();
                    PrepareTemplates(db);

                    PrepareEncryptionAlgorithm(db);

                    UpdateImportDbPref();
                    UpdateImportKeyfilePref();
                }
                

            }

            private void PrepareEncryptionAlgorithm(Database db)
            {
                ListPreference algorithmPref = (ListPreference)FindPreference(GetString(Resource.String.algorithm_key));
                algorithmPref.SetEntries(CipherPool.GlobalPool.Engines.Select(eng => eng.DisplayName).ToArray());
                string[] algoValues = CipherPool.GlobalPool.Engines.Select(eng => eng.CipherUuid.ToHexString()).ToArray();
                algorithmPref.SetEntryValues(algoValues);
                algorithmPref.SetValueIndex(algoValues.Select((v, i) => new { kdf = v, index = i }).First(el => el.kdf == db.KpDatabase.DataCipherUuid.ToHexString()).index);
                algorithmPref.PreferenceChange += AlgorithmPrefChange;
                algorithmPref.Summary =
                    CipherPool.GlobalPool.GetCipher(App.Kp2a.CurrentDb.KpDatabase.DataCipherUuid).DisplayName;
            }

            private void AlgorithmPrefChange(object sender, Preference.PreferenceChangeEventArgs preferenceChangeEventArgs)
            {
                var db = App.Kp2a.CurrentDb;
                var previousCipher = db.KpDatabase.DataCipherUuid;
                db.KpDatabase.DataCipherUuid = new PwUuid(MemUtil.HexStringToByteArray((string)preferenceChangeEventArgs.NewValue));

                SaveDb save = new SaveDb(App.Kp2a, App.Kp2a.CurrentDb, new ActionOnOperationFinished(App.Kp2a, (success, message, context) =>
                {
                    if (!success)
                    {
                        db.KpDatabase.DataCipherUuid = previousCipher;
                        App.Kp2a.ShowMessage(context, message,  MessageSeverity.Error);
                        return;
                    }
                    preferenceChangeEventArgs.Preference.Summary =
                        CipherPool.GlobalPool.GetCipher(db.KpDatabase.DataCipherUuid).DisplayName;
                }));
                BlockingOperationRunner pt = new BlockingOperationRunner(App.Kp2a, save);
                pt.Run();
            }


        }
        public class SettingsFragmentApp : PreferenceFragmentWithResource
        {
            public SettingsFragmentApp() : base(Resource.Xml.pref_app)
            {
            }
        }
        public class SecurityPreferenceFragment : PreferenceFragmentWithResource
        {
            public SecurityPreferenceFragment() : base(Resource.Xml.pref_app_security)
            {
            }

            void OnRememberKeyFileHistoryChanged(object sender, Preference.PreferenceChangeEventArgs eventArgs)
            {
                if (!(bool)eventArgs.NewValue)
                {
                    App.Kp2a.FileDbHelper.DeleteAllKeys();
                }
            }

            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);
                FindPreference(GetString(Resource.String.keyfile_key)).PreferenceChange += OnRememberKeyFileHistoryChanged;
                

            }
        }
        public class DisplayPreferenceFragment : PreferenceFragmentWithResource
        {
            public DisplayPreferenceFragment() : base(Resource.Xml.pref_app_display)
            {
            }



            void OnShowUnlockedNotificationChanged(object sender, Preference.PreferenceChangeEventArgs eventArgs)
            {
                App.Kp2a.UpdateOngoingNotification();
            }

            private void PrepareNoDonationReminderPreference(Activity ctx, PreferenceScreen screen, Preference preference)
            {
                ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);

                if (!prefs.GetBoolean("DismissedDonateReminder", false))
                {
                    screen.RemovePreference(preference);
                }


            }

            public void PrepareNoDonatePreference(Context ctx, Preference preference)
            {
                ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);

                long usageCount = prefs.GetLong(ctx.GetString(Resource.String.UsageCount_key), 0);

#if DEBUG
                preference.Enabled = (usageCount > 1);
#else
			preference.Enabled = (usageCount > 50);
#endif
                preference.PreferenceChange += delegate (object sender, Preference.PreferenceChangeEventArgs args)
                {
                    if ((bool)args.NewValue)
                    {
                        new MaterialAlertDialogBuilder(ctx)
                            .SetTitle(ctx.GetString(AppNames.AppNameResource))
                            .SetCancelable(false)
                            .SetPositiveButton(Android.Resource.String.Ok, delegate (object o, DialogClickEventArgs eventArgs)
                            {
                                Util.GotoDonateUrl(ctx);
                                ((Dialog)o).Dismiss();
                            })
                            .SetMessage(Resource.String.NoDonateOption_question)
                            .Create().Show();

                    }
                };

            }

            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);

                var unlockedNotificationPref = FindPreference(GetString(Resource.String.ShowUnlockedNotification_key));
                unlockedNotificationPref.PreferenceChange += OnShowUnlockedNotificationChanged;
                if ((int)Build.VERSION.SdkInt >= 26)
                {
                    //use system notification channels to control notification visibility
                    unlockedNotificationPref.Parent.RemovePreference(unlockedNotificationPref);
                }
                unlockedNotificationPref.PreferenceChange += (sender, args) => App.Kp2a.UpdateOngoingNotification();



                HashSet<string> supportedLocales = new HashSet<string>() { "en", "af", "ar", "az", "be", "bg", "ca", "cs", "da", "de", "el", "es", "eu", "fa", "fi", "fr", "gl", "he", "hr", "hu", "id", "in", "it", "iw", "ja", "ko", "ml", "nb", "nl", "nn", "no", "pl", "pt", "ro", "ru", "si", "sk", "sl", "sr", "sv", "tr", "uk", "vi", "zh" };
                var languagePref = (ListPreference)FindPreference(GetString(Resource.String.app_language_pref_key));
                new AppLanguageManager(this, languagePref, supportedLocales);


                PrepareNoDonatePreference(Activity, FindPreference(GetString(Resource.String.NoDonateOption_key)));
                var displayPrefScreen = ((PreferenceScreen)FindPreference(GetString(Resource.String.display_prefs_key)));
                PrepareNoDonationReminderPreference(Activity, displayPrefScreen, FindPreference(GetString(Resource.String.NoDonationReminder_key)));

                FindPreference(GetString(Resource.String.design_key)).PreferenceChange += (sender, args) =>
                {
                    //it would be nicer to recreate, but that - for some reason - causes GroupActivity to be twice on the backstack afterwards :-( 
                    //So better finish here.
                    Activity.Finish();
                };


                displayPrefScreen.RemovePreference(unlockedNotificationPref);



                FindPreference("IconSetKey").PreferenceChange += (sender, args) =>
                {
                    if (App.Kp2a.CurrentDb != null)
                        App.Kp2a.CurrentDb.DrawableFactory.Clear();

                };


                

            }
        }
        public class QuickUnlockPreferenceFragment : PreferenceFragmentWithResource
        {
            public QuickUnlockPreferenceFragment() : base(Resource.Xml.pref_app_quick_unlock)
            {
            }

            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);
                try
                {
                    //depending on Android version, we offer to use a transparent icon for QuickUnlock or use the notification priority (since API level 16)
                    Preference hideQuickUnlockTranspIconPref = FindPreference(GetString(Resource.String.QuickUnlockIconHidden_key));
                    Preference hideQuickUnlockIconPref = FindPreference(GetString(Resource.String.QuickUnlockIconHidden16_key));
                    var quickUnlockScreen = ((PreferenceScreen)FindPreference(GetString(Resource.String.QuickUnlock_prefs_key)));
                    if ((int)Android.OS.Build.VERSION.SdkInt >= 26)
                    {
                        //use notification channels
                        quickUnlockScreen.RemovePreference(hideQuickUnlockTranspIconPref);
                        quickUnlockScreen.RemovePreference(hideQuickUnlockIconPref);
                    }
                    else if ((int)Android.OS.Build.VERSION.SdkInt >= 16)
                    {
                        quickUnlockScreen.RemovePreference(hideQuickUnlockTranspIconPref);

                        hideQuickUnlockIconPref.PreferenceChange += delegate { App.Kp2a.UpdateOngoingNotification(); };
                    }
                    else
                    {
                        //old version: only show transparent quickUnlock and no option to hide unlocked icon:
                        quickUnlockScreen.RemovePreference(hideQuickUnlockIconPref);
                        FindPreference(GetString(Resource.String.QuickUnlockIconHidden_key)).PreferenceChange +=
                            delegate { App.Kp2a.UpdateOngoingNotification(); };

                    }
                }
                catch (Exception ex)
                {
                    Kp2aLog.LogUnexpectedError(ex);
                }

                

            }
        }

        public class FileHandlingPreferenceFragment : PreferenceFragmentWithResource
        {
            public FileHandlingPreferenceFragment() : base(Resource.Xml.pref_app_file_handling)
            {
            }
            private void OnUseOfflineCacheChanged(object sender, Preference.PreferenceChangeEventArgs e)
            {
                if (!(bool)e.NewValue)
                {
                    MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(Activity);
                    builder.SetTitle(GetString(Resource.String.ClearOfflineCache_title));

                    builder.SetMessage(GetString(Resource.String.ClearOfflineCache_question));

                    builder.SetPositiveButton(App.Kp2a.GetResourceString(UiStringKey.yes), (o, args) =>
                        {
                            try
                            {
                                App.Kp2a.ClearOfflineCache();
                            }
                            catch (Exception ex)
                            {
                                Kp2aLog.LogUnexpectedError(ex);
                                App.Kp2a.ShowMessage(LocaleManager.LocalizedAppContext, Util.GetErrorMessage(ex),  MessageSeverity.Error);
                            }
                        }
                    );

                    builder.SetNegativeButton(App.Kp2a.GetResourceString(UiStringKey.no), (o, args) =>
                        {
                            ((CheckBoxPreference)e.Preference).Checked = true;
                        }
                    );
                    builder.SetCancelable(false);
                    Dialog dialog = builder.Create();
                    dialog.Show();


                }
            }


            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);

                Preference cachingPreference = FindPreference(GetString(Resource.String.UseOfflineCache_key));
                cachingPreference.PreferenceChange += OnUseOfflineCacheChanged;

                

            }
        }

        public class TotpPreferenceFragment : PreferenceFragmentWithResource
        {
            public TotpPreferenceFragment() : base(Resource.Xml.pref_app_traytotp)
            {
            }
        }

        public class DebugLogPreferenceFragment : PreferenceFragmentWithResource
        {

            public DebugLogPreferenceFragment() : base(Resource.Xml.pref_app_debug)
            {
            }

            private void UpdateDependingPreferences(bool debugLogEnabled)
            {

                FindPreference(GetString(Resource.String.DebugLog_send_key)).Visible = debugLogEnabled;

#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
                FindPreference(GetString(Resource.String.FtpDebug_key)).Visible = debugLogEnabled;
#endif
            }


            private void OnSendDebug(object sender, Preference.PreferenceClickEventArgs e)
            {
                Kp2aLog.SendLog(this.Activity);
            }

            private void OnDebugLogChanged(object sender, Preference.PreferenceChangeEventArgs e)
            {
                if ((bool)e.NewValue)
                    Kp2aLog.CreateLogFile();
                else
                    Kp2aLog.FinishLogFile();

#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
                SetJSchLogging(PreferenceManager.GetDefaultSharedPreferences(Application.Context)
                    .GetBoolean(Application.Context.GetString(Resource.String.FtpDebug_key), false));
#endif
                UpdateDependingPreferences((bool)e.NewValue);
            }

#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
            private void OnJSchDebugChanged(object sender, Preference.PreferenceChangeEventArgs e)
            {
                bool debugEnabled = (bool)e.NewValue;
                SetJSchLogging(debugEnabled);

                string prefKey = Application.Context.GetString(Resource.String.FtpDebug_key);
                PreferenceManager.SharedPreferences.Edit().PutBoolean(prefKey, debugEnabled).Apply();
            }

            private void SetJSchLogging(bool enabled)
            {
                var sftpStorage = new Keepass2android.Javafilestorage.SftpStorage(Context);
                string? logFilename = null;
                if (Kp2aLog.LogToFile)
                {
                    logFilename = Kp2aLog.LogFilename;
                }
                sftpStorage.SetJschLogging(enabled, logFilename);
            }

#endif

            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);

                FindPreference(GetString(Resource.String.DebugLog_key)).PreferenceChange += OnDebugLogChanged;
                FindPreference(GetString(Resource.String.DebugLog_send_key)).PreferenceClick += OnSendDebug;

#if !EXCLUDE_JAVAFILESTORAGE && !NoNet
                FindPreference(GetString(Resource.String.FtpDebug_key)).PreferenceChange += OnJSchDebugChanged;
#else
            FindPreference(GetString(Resource.String.FtpDebug_key)).Enabled = false;
#endif
                bool debugLogEnabled = (PreferenceManager.GetDefaultSharedPreferences(Application.Context)
                    .GetBoolean(Application.Context.GetString(Resource.String.DebugLog_key), false));
                UpdateDependingPreferences(debugLogEnabled);


            }
        }


        public class KeyboardSwitchPreferenceFragment : PreferenceFragmentWithResource
        {
            public class KeyboardSwitchPrefManager
            {
                private readonly Activity _act;
                private readonly CheckBoxPreference _switchPref;
                private readonly CheckBoxPreference _openKp2aAutoPref;
                private readonly CheckBoxPreference _openOnlyOnSearchPref;
                private CheckBoxPreference _switchBackPref;
                private readonly PreferenceScreen _screen;
                private readonly PreferenceFragmentCompat _fragment;

                public KeyboardSwitchPrefManager(PreferenceFragmentCompat fragment)
                {
                    var act = fragment.Activity;
                    this._act = act;
                    this._fragment = fragment;
                    this._screen = (PreferenceScreen)_fragment.FindPreference(act.GetString(Resource.String.keyboardswitch_prefs_key));

                    var keyboardSwapPref = _fragment.FindPreference("get_keyboardswap");
                    var pm = act.PackageManager;
                    var intnt = Keepass2android.Kbbridge.ImeSwitcher.GetLaunchIntentForKeyboardSwap(act);
                    if ((intnt != null) && pm.QueryIntentActivities(intnt, 0).Any())
                    {
                        _screen.RemovePreference(keyboardSwapPref);
                    }
                    else
                    {
                        keyboardSwapPref.PreferenceClick += (sender, args) =>
                        {
                            Util.GotoUrl(act, act.GetString(Resource.String.MarketURL) + "keepass2android.plugin.keyboardswap2");
                        };
                    }

                    _switchPref = (CheckBoxPreference)_fragment.FindPreference("kp2a_switch_rooted");
                    _openKp2aAutoPref =
                        (CheckBoxPreference)_fragment.FindPreference(act.GetString(Resource.String.OpenKp2aKeyboardAutomatically_key));
                    _openOnlyOnSearchPref =
                        (CheckBoxPreference)
                        _fragment.FindPreference(act.GetString(Resource.String.OpenKp2aKeyboardAutomaticallyOnlyAfterSearch_key));
                    _switchBackPref =
                        (CheckBoxPreference)_fragment.FindPreference(act.GetString(Resource.String.AutoSwitchBackKeyboard_key));

                    EnableSwitchPreferences(_switchPref.Checked);

                    _switchPref.PreferenceChange += (sender, args) =>
                    {
                        bool switchOnRooted = (bool)args.NewValue;
                        EnableSwitchPreferences(switchOnRooted);
                    };
                }


                private void EnableSwitchPreferences(bool switchOnRooted)
                {
                    if (!switchOnRooted)
                    {
                        if (_fragment.FindPreference(_act.GetString(Resource.String.OpenKp2aKeyboardAutomatically_key)) == null)
                        {
                            _screen.AddPreference(_openKp2aAutoPref);
                        }
                        if (_fragment.FindPreference(_act.GetString(Resource.String.OpenKp2aKeyboardAutomaticallyOnlyAfterSearch_key)) != null)
                        {
                            _screen.RemovePreference(_openOnlyOnSearchPref);
                        }
                    }
                    else
                    {
                        {
                            _screen.RemovePreference(_openKp2aAutoPref);
                        }
                        if (_fragment.FindPreference(_act.GetString(Resource.String.OpenKp2aKeyboardAutomaticallyOnlyAfterSearch_key)) == null)
                        {
                            _screen.AddPreference(_openOnlyOnSearchPref);
                        }
                    }
                    /*_openKp2aAutoPref.Enabled = !switchOnRooted;

                    _openOnlyOnSearchPref.Enabled = switchOnRooted;

                    _switchBackPref.Enabled = switchOnRooted;*/
                }
            }
            KeyboardSwitchPrefManager _manager;
            public KeyboardSwitchPreferenceFragment() : base(Resource.Xml.pref_app_password_access_keyboard_switch)
            {
                
            }

            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);
                _manager = new KeyboardSwitchPrefManager(this);
            }
        }
        public class AutofillPreferenceFragment : PreferenceFragmentWithResource
        {
            public AutofillPreferenceFragment() : base(Resource.Xml.pref_app_password_access_autofill)
            {
            }

            private void UpdateAutofillPref()
            {
                var autofillScreen = FindPreference(GetString(Resource.String.AutoFill_prefs_screen_key));
                var autofillPref = FindPreference(GetString(Resource.String.AutoFill_prefs_key));
                var autofillDisabledPref = FindPreference(GetString(Resource.String.AutofillDisabledQueriesPreference_key));
                var autofillSavePref = FindPreference(GetString(Resource.String.OfferSaveCredentials_key));
                var autofillInlineSuggestions = FindPreference(GetString(Resource.String.InlineSuggestions_key));
                var noAutofillDisablingPref = FindPreference(GetString(Resource.String.NoAutofillDisabling_key));
                var autofillNoDalVerification = FindPreference(GetString(Resource.String.NoDalVerification_key));
                if (autofillPref == null)
                    return;
                if ((Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.O) ||
                    !((AutofillManager)Activity.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
                        .IsAutofillSupported)
                {
                    var passwordAccessScreen =
                        (PreferenceScreen)FindPreference(Activity.GetString(Resource.String.password_access_prefs_key));
                    passwordAccessScreen.RemovePreference(autofillScreen);
                }
                else
                {
                    if (((AutofillManager)Activity.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
                        .HasEnabledAutofillServices)
                    {
                        autofillDisabledPref.Enabled = true;
                        autofillSavePref.Enabled = true;
                        autofillNoDalVerification.Enabled = true;
                        autofillInlineSuggestions.Enabled = true;
                        noAutofillDisablingPref.Enabled = true;
                        autofillPref.Summary = Activity.GetString(Resource.String.plugin_enabled);
                        autofillPref.Intent = new Intent(Intent.ActionView);
                        autofillPref.Intent.SetData(Android.Net.Uri.Parse("https://philippc.github.io/keepass2android/OreoAutoFill.html"));
                    }
                    else
                    {
                        autofillNoDalVerification.Enabled = false;
                        autofillDisabledPref.Enabled = false;
                        autofillSavePref.Enabled = false;
                        noAutofillDisablingPref.Enabled = false;
                        autofillInlineSuggestions.Enabled = false;
                        autofillPref.Summary = Activity.GetString(Resource.String.not_enabled);
                    }
                    if ((int)Android.OS.Build.VERSION.SdkInt < 30)
                    {
                        autofillInlineSuggestions.Summary = Activity.GetString(Resource.String.requires_android11);
                        CheckBoxPreference cbp = autofillInlineSuggestions as CheckBoxPreference;
                        if (cbp != null)
                            cbp.Checked = false;
                        autofillInlineSuggestions.Enabled = false;
                    }
                }
            }


            public override void OnResume()
            {
                base.OnResume();

                UpdateAutofillPref();
            }

            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);

                UpdateAutofillPref();

                var autofillPref = FindPreference(GetString(Resource.String.AutoFill_prefs_key));
                if (autofillPref != null)
                {
                    autofillPref.PreferenceClick += (sender, args) =>
                    {

                        var intent = new Intent(Android.Provider.Settings.ActionRequestSetAutofillService);
                        if (((AutofillManager)Activity.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
                            .HasEnabledAutofillServices)
                        {
                            intent.SetData(Android.Net.Uri.Parse("package:" + Context.PackageName + "notexisting")); //if we use our package name, the activity won't launch
                        }
                        else
                        {
                            intent.SetData(Android.Net.Uri.Parse("package:" + Context.PackageName));
                        }

                        try
                        {
                            Context.StartActivity(intent);
                        }
                        catch (ActivityNotFoundException e)
                        {
                            //this exception was reported by many Huawei users
                            Kp2aLog.LogUnexpectedError(e);
                            new MaterialAlertDialogBuilder(Context)
                                .SetTitle(Resource.String.autofill_enable)
                                .SetMessage(Resource.String.autofill_enable_failed)
                                .SetPositiveButton(Android.Resource.String.Ok, (o, eventArgs) => { })
                                .Show();

                        }
                        catch (System.Exception e)
                        {
                            Kp2aLog.LogUnexpectedError(e);
                        }
                    };
                }


                

            }
        }

        public class AutofillTotpPreferenceFragment : PreferenceFragmentWithResource
        {
            public AutofillTotpPreferenceFragment() : base(Resource.Xml.pref_app_password_access_autofill_totp)
            {
            }
        }

        public class PasswordAccessPreferenceFragment : PreferenceFragmentWithResource
        {
            public PasswordAccessPreferenceFragment() : base(Resource.Xml.pref_app_password_access)
            {
            }
        }


        public class KeyDerivFuncPreferenceFragment : PreferenceFragmentWithResource
        {
            private Preference aesRounds, argon2parallelism, argon2rounds, argon2memory;
            public KeyDerivFuncPreferenceFragment() : base(Resource.Xml.pref_database_key_deriv_func)
            {
            }

            public override void OnCreate(Bundle? savedInstanceState)
            {
                base.OnCreate(savedInstanceState);
                aesRounds = FindPreference(GetString(Resource.String.rounds_key));
                argon2rounds = FindPreference("argon2rounds");
                argon2memory = FindPreference("argon2memory");
                argon2parallelism = FindPreference("argon2parallelism");

                aesRounds.PreferenceChange += (sender, e) => UpdateKdfSummary(e.Preference);
                argon2rounds.PreferenceChange += (sender, e) => UpdateKdfSummary(e.Preference);
                argon2memory.PreferenceChange += (sender, e) => UpdateKdfSummary(e.Preference);
                argon2parallelism.PreferenceChange += (sender, e) => UpdateKdfSummary(e.Preference);

                var db = App.Kp2a.CurrentDb;
                if (db != null)
                {
                    ListPreference kdfPref = (ListPreference)FindPreference(GetString(Resource.String.kdf_key));
                    kdfPref.SetEntries(KdfPool.Engines.Select(eng => eng.Name).ToArray());
                    string[] kdfValues = KdfPool.Engines.Select(eng => eng.Uuid.ToHexString()).ToArray();
                    kdfPref.SetEntryValues(kdfValues);
                    kdfPref.SetValueIndex(kdfValues.Select((v, i) => new { kdf = v, index = i }).First(el => el.kdf == db.KpDatabase.KdfParameters.KdfUuid.ToHexString()).index);
                    kdfPref.PreferenceChange += OnKdfChange;



                    UpdateKdfScreen();
                }
            }

            public override void OnDisplayPreferenceDialog(Preference preference)
            {
                if (preference is KdfNumberDialogPreference dialogPreference)
                {
                    dialogPreference.ShowDialog(this);
                }
                else
                    base.OnDisplayPreferenceDialog(preference);
            }

            private void UpdateKdfScreen()
            {
                var db = App.Kp2a.CurrentDb;
                var kdf = KdfPool.Get(db.KpDatabase.KdfParameters.KdfUuid);

                var kdfpref = FindPreference(GetString(Resource.String.kdf_key));


                kdfpref.Summary = kdf.Name;

                var kdfscreen = ((PreferenceScreen)FindPreference(GetString(Resource.String.kdf_screen_key)));
                if (kdf is AesKdf)
                {
                    if (kdfscreen.FindPreference(GetString(Resource.String.rounds_key)) == null)
                        kdfscreen.AddPreference(aesRounds);
                    kdfscreen.RemovePreference(argon2rounds);
                    kdfscreen.RemovePreference(argon2memory);
                    kdfscreen.RemovePreference(argon2parallelism);

                    aesRounds.Enabled = db.CanWrite;
                    UpdateKdfSummary(aesRounds);
                }
                else
                {
                    kdfscreen.RemovePreference(aesRounds);
                    if (kdfscreen.FindPreference("argon2rounds") == null)
                    {
                        kdfscreen.AddPreference(argon2rounds);
                        kdfscreen.AddPreference(argon2memory);
                        kdfscreen.AddPreference(argon2parallelism);
                    }
                    UpdateKdfSummary(argon2rounds);
                    UpdateKdfSummary(argon2memory);
                    UpdateKdfSummary(argon2parallelism);
                }

            }

            private void OnKdfChange(object sender, Preference.PreferenceChangeEventArgs preferenceChangeEventArgs)
            {
                var db = App.Kp2a.CurrentDb;
                var previousKdfParams = db.KpDatabase.KdfParameters;
                Kp2aLog.Log("previous kdf: " + KdfPool.Get(db.KpDatabase.KdfParameters.KdfUuid) + " " + db.KpDatabase.KdfParameters.KdfUuid.ToHexString());
                db.KpDatabase.KdfParameters =
                    KdfPool.Get(
                        new PwUuid(MemUtil.HexStringToByteArray((string)preferenceChangeEventArgs.NewValue)))
                        .GetDefaultParameters();

                Kp2aLog.Log("--new    kdf: " + KdfPool.Get(db.KpDatabase.KdfParameters.KdfUuid) + " " + db.KpDatabase.KdfParameters.KdfUuid.ToHexString());

                SaveDb save = new SaveDb(App.Kp2a, App.Kp2a.CurrentDb, new ActionOnOperationFinished(App.Kp2a, (success, message, context) =>
                {
                    if (!success)
                    {
                        db.KpDatabase.KdfParameters = previousKdfParams;
                        App.Kp2a.ShowMessage(context, message,  MessageSeverity.Error);
                        return;
                    }
                    UpdateKdfScreen();

                }));
                BlockingOperationRunner pt = new BlockingOperationRunner(App.Kp2a, save);
                pt.Run();

            }

            private void UpdateKdfSummary(Preference preference)
            {
                preference.Summary = ((KdfNumberDialogPreference)preference).ParamValue.ToString();
            }

        }

        #endregion
    }

    public class SettingsFragmentManager
    {
        private readonly LifecycleAwareActivity _parentActivity;
        private const string TitleTag = "settingsActivityTitle";

        public SettingsFragmentManager(LifecycleAwareActivity parentActivity)
        {
            _parentActivity = parentActivity;
            _parentActivity.OnCreateListener = OnCreate;
            _parentActivity.OnSaveInstanceStateListener = OnSaveInstanceState;
            _parentActivity.OnSupportNavigateUpListener = OnSupportNavigateUp;


        }

        protected void OnCreate(Bundle savedInstanceState)
        {

            _parentActivity.SetContentView(Resource.Layout.preference);

            if (savedInstanceState == null)
            {
                _parentActivity.SupportFragmentManager
                    .BeginTransaction()
                    .Replace(Resource.Id.settings, new MainPreferenceFragment())
                    .Commit();
            }
            else
            {
                _parentActivity.Title = savedInstanceState.GetCharSequence(TitleTag);
            }

            _parentActivity.SupportActionBar?.SetDisplayHomeAsUpEnabled(true);
        }

        protected void OnSaveInstanceState(Bundle outState)
        {
            // Save the current activity title to restore after configuration changes
            outState.PutCharSequence(TitleTag, _parentActivity.Title);
        }

        public bool? OnSupportNavigateUp()
        {
            if (_parentActivity.SupportFragmentManager.PopBackStackImmediate())
            {
                if (_parentActivity.SupportFragmentManager.BackStackEntryCount == 0)
                {
                    _parentActivity.SetTitle(Resource.String.app_name);
                }

                return true;
            }
            else
            {
                _parentActivity.Finish();
                return true;
            }
        }



        public bool OnPreferenceStartFragment(PreferenceFragmentCompat caller, AndroidX.Preference.Preference pref)
        {
            var t = Type.GetType(pref.Fragment);
            var javaName = Java.Lang.Class.FromType(t).Name;
            // Instantiate the new Fragment
            var args = pref.Extras;
            var fragment = _parentActivity.SupportFragmentManager.FragmentFactory.Instantiate(
                _parentActivity.ClassLoader,
                javaName);

            fragment.Arguments = args;
            fragment.SetTargetFragment(caller, 0);

            // Replace the existing Fragment with the new Fragment
            _parentActivity.SupportFragmentManager.BeginTransaction()
                .Replace(Resource.Id.settings, fragment)
                .AddToBackStack(null)
                .Commit();

            _parentActivity.Title = pref.Title;
            return true;
        }

        public void OnBackStackChanged()
        {
            
        }
    }



    /// <summary>
    /// Activity to configure the application, without database settings. Does not require an unlocked database, or close when the database is locked
    /// </summary>
    [Activity(Label = "@string/app_name", Theme = "@style/Kp2aTheme_BlueActionBar", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
    public class AppSettingsActivity : LockingActivity, PreferenceFragmentCompat.IOnPreferenceStartFragmentCallback, FragmentManager.IOnBackStackChangedListener
    {
        private ActivityDesign _design;

        public static bool BeingRecreated = false;

        public AppSettingsActivity()
        {
            _design = new ActivityDesign(this);
            settingsFragmentManager = new SettingsFragmentManager(this);
            //TODO adding this makes the app crash on back (https://github.com/dotnet/android-libraries/issues/1055)
            //We need this in order to update the activity title to the re-activated preference
            //SupportFragmentManager.AddOnBackStackChangedListener(this);
        }

        public static void Launch(Context ctx)
        {
            ctx.StartActivity(new Intent(ctx, typeof(AppSettingsActivity)));
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _design.ApplyTheme();
            base.OnCreate(savedInstanceState);


        }

        public SettingsFragmentManager settingsFragmentManager;
        

        public bool OnPreferenceStartFragment(PreferenceFragmentCompat caller, Preference pref)
        {
            return settingsFragmentManager.OnPreferenceStartFragment(caller, pref);
        }

        public void OnBackStackChanged()
        {
            settingsFragmentManager.OnBackStackChanged();
        }
    }

}

