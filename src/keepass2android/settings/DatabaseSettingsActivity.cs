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
using System.IO;
using System.Linq;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Preferences;
using Android.Provider;
using Android.Views.Autofill;
using Java.IO;
using KeePass.DataExchange;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android.Io;
using keepass2android.Utils;
using KeePassLib;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Interfaces;
using System.Collections.Generic;

namespace keepass2android
{
    //http://stackoverflow.com/a/27422401/292233
        public class SettingsFragment : PreferenceFragment
    {


		

        public class KeyboardSwitchPrefManager
        {
            private readonly Activity _act;
            private CheckBoxPreference _switchPref;
            private CheckBoxPreference _openKp2aAutoPref;
            private CheckBoxPreference _openOnlyOnSearchPref;
            private CheckBoxPreference _switchBackPref;
            private PreferenceScreen _screen;
            private PreferenceFragment _fragment;

            public KeyboardSwitchPrefManager(PreferenceFragment fragment)
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

        private KeyboardSwitchPrefManager _switchPrefManager;
		private Preference aesRounds, argon2parallelism, argon2rounds, argon2memory;
        

        void OnRememberKeyFileHistoryChanged(object sender, Preference.PreferenceChangeEventArgs eventArgs)
        {
            if (!(bool)eventArgs.NewValue)
            {
                App.Kp2a.FileDbHelper.DeleteAllKeys();
            }
        }

        void OnShowUnlockedNotificationChanged(object sender, Preference.PreferenceChangeEventArgs eventArgs)
        {
            App.Kp2a.UpdateOngoingNotification();
        }

        public override void OnResume()
        {
            base.OnResume();

            UpdateAutofillPref();
        }


        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AddPreferencesFromResource(Resource.Xml.preferences);

            // Re-use the change handlers for the application settings
            FindPreference(GetString(Resource.String.keyfile_key)).PreferenceChange += OnRememberKeyFileHistoryChanged;
            var unlockedNotificationPref = FindPreference(GetString(Resource.String.ShowUnlockedNotification_key));
            unlockedNotificationPref.PreferenceChange += OnShowUnlockedNotificationChanged;
            if ((int)Build.VERSION.SdkInt >= 26)
            {
                //use system notification channels to control notification visibility
                unlockedNotificationPref.Parent.RemovePreference(unlockedNotificationPref);
            }


            FindPreference(GetString(Resource.String.DebugLog_key)).PreferenceChange += OnDebugLogChanged;
            FindPreference(GetString(Resource.String.DebugLog_send_key)).PreferenceClick += OnSendDebug;
            FindPreference(GetString(Resource.String.JSchDebug_key)).PreferenceChange += OnJSchDebugChanged;

            HashSet<string> supportedLocales = new HashSet<string>() { "en", "af", "ar", "az", "be", "bg", "ca", "cs", "da", "de", "el", "es", "eu", "fa", "fi", "fr", "gl", "he", "hr", "hu", "id", "in", "it", "iw", "ja", "ko", "ml", "nb", "nl", "nn", "no", "pl", "pt", "ro", "ru", "si", "sk", "sl", "sr", "sv", "tr", "uk", "vi", "zh" };

            ListPreference appLanguagePref = (ListPreference)FindPreference(GetString(Resource.String.app_language_pref_key));
            
            var localesByCode = new System.Collections.Generic.Dictionary<string, List<Java.Util.Locale>>();
            foreach (var loc in Java.Util.Locale.GetAvailableLocales())
            {
                if (!supportedLocales.Contains(loc.Language))
                    continue;
                if (!localesByCode.ContainsKey(loc.Language))
                {
                    localesByCode[loc.Language] = new List<Java.Util.Locale>();
                }
                localesByCode[loc.Language].Add(loc);

            }
            var localesByCodeUnique = localesByCode.Select(l => new KeyValuePair<string, Java.Util.Locale>(l.Key, l.Value.First())).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            List<KeyValuePair<string, List<Java.Util.Locale>>> codesWithMultiple = localesByCode.Where(l => l.Value.Count > 1).ToList();
            List<KeyValuePair<string, Java.Util.Locale>> localesByLanguageList = localesByCodeUnique
                .OrderBy(kvp => kvp.Value.DisplayLanguage).ToList();
            appLanguagePref.SetEntries(localesByLanguageList.Select(kvp => kvp.Value.DisplayLanguage).ToArray());
            appLanguagePref.SetEntryValues(localesByLanguageList.Select(kvp => kvp.Value.Language).ToArray());
            string languageCode = appLanguagePref.Value;
            string summary = GetDisplayLanguage(localesByCodeUnique, languageCode);
            ((ListPreference)FindPreference(GetString(Resource.String.app_language_pref_key))).Summary = summary;
            appLanguagePref.PreferenceChange += (sender, args) =>
            {
                ((ListPreference)FindPreference(GetString(Resource.String.app_language_pref_key))).Summary = GetDisplayLanguage(localesByCodeUnique, (string)args.NewValue);
                LocaleManager.Language = (string)args.NewValue;
            };


            UpdateAutofillPref();

            var autofillPref = FindPreference(GetString(Resource.String.AutoFill_prefs_key));
            if (autofillPref != null)
            {
                autofillPref.PreferenceClick += (sender, args) =>
                {

                    var intent = new Intent(Settings.ActionRequestSetAutofillService);
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
                        new AlertDialog.Builder(Context)
                            .SetTitle(Resource.String.autofill_enable)
                            .SetMessage(Resource.String.autofill_enable_failed)
                            .SetPositiveButton(Android.Resource.String.Ok, (o, eventArgs) => { })
                            .Show();

                    }
                    catch (Exception e)
                    {
                        Kp2aLog.LogUnexpectedError(e);
                    }
                };
            }


            PrepareNoDonatePreference(Activity, FindPreference(GetString(Resource.String.NoDonateOption_key)));
            PrepareNoDonationReminderPreference(Activity, ((PreferenceScreen)FindPreference(GetString(Resource.String.display_prefs_key))), FindPreference(GetString(Resource.String.NoDonationReminder_key)));

            FindPreference(GetString(Resource.String.design_key)).PreferenceChange += (sender, args) => Activity.Recreate();

            Database db = App.Kp2a.CurrentDb;
            if (db != null)
            {
                ListPreference kdfPref = (ListPreference)FindPreference(GetString(Resource.String.kdf_key));
                kdfPref.SetEntries(KdfPool.Engines.Select(eng => eng.Name).ToArray());
                string[] kdfValues = KdfPool.Engines.Select(eng => eng.Uuid.ToHexString()).ToArray();
                kdfPref.SetEntryValues(kdfValues);
                kdfPref.SetValueIndex(kdfValues.Select((v, i) => new { kdf = v, index = i }).First(el => el.kdf == db.KpDatabase.KdfParameters.KdfUuid.ToHexString()).index);
                kdfPref.PreferenceChange += OnKdfChange;

                aesRounds = FindPreference(GetString(Resource.String.rounds_key));
                argon2rounds = FindPreference("argon2rounds");
                argon2memory = FindPreference("argon2memory");
                argon2parallelism = FindPreference("argon2parallelism");

                aesRounds.PreferenceChange += (sender, e) => UpdateKdfSummary(e.Preference);
                argon2rounds.PreferenceChange += (sender, e) => UpdateKdfSummary(e.Preference);
                argon2memory.PreferenceChange += (sender, e) => UpdateKdfSummary(e.Preference);
                argon2parallelism.PreferenceChange += (sender, e) => UpdateKdfSummary(e.Preference);

                UpdateKdfScreen();

                PrepareDefaultUsername(db);
                PrepareDatabaseName(db);
                PrepareMasterPassword();
                PrepareTemplates(db);

                ListPreference algorithmPref = (ListPreference)FindPreference(GetString(Resource.String.algorithm_key));
                algorithmPref.SetEntries(CipherPool.GlobalPool.Engines.Select(eng => eng.DisplayName).ToArray());
                string[] algoValues = CipherPool.GlobalPool.Engines.Select(eng => eng.CipherUuid.ToHexString()).ToArray();
                algorithmPref.SetEntryValues(algoValues);
                algorithmPref.SetValueIndex(algoValues.Select((v, i) => new { kdf = v, index = i }).First(el => el.kdf == db.KpDatabase.DataCipherUuid.ToHexString()).index);
                algorithmPref.PreferenceChange += AlgorithmPrefChange;
                algorithmPref.Summary =
                    CipherPool.GlobalPool.GetCipher(App.Kp2a.CurrentDb.KpDatabase.DataCipherUuid).DisplayName;
                UpdateImportDbPref();
                UpdateImportKeyfilePref();
            }

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
                    unlockedNotificationPref.PreferenceChange += (sender, args) => App.Kp2a.UpdateOngoingNotification();
                    hideQuickUnlockIconPref.PreferenceChange += delegate { App.Kp2a.UpdateOngoingNotification(); };
                }
                else
                {
                    //old version: only show transparent quickUnlock and no option to hide unlocked icon:
                    quickUnlockScreen.RemovePreference(hideQuickUnlockIconPref);
                    FindPreference(GetString(Resource.String.QuickUnlockIconHidden_key)).PreferenceChange +=
                        delegate { App.Kp2a.UpdateOngoingNotification(); };

                    ((PreferenceScreen)FindPreference(GetString(Resource.String.display_prefs_key))).RemovePreference(
                        unlockedNotificationPref);
                }
            }
            catch (Exception ex)
            {
                Kp2aLog.LogUnexpectedError(ex);
            }



            //AppSettingsActivity.PrepareKeyboardSwitchingPreferences(this);
            _switchPrefManager = new KeyboardSwitchPrefManager(this);
            PrepareSeparateNotificationsPreference();

            FindPreference("IconSetKey").PreferenceChange += (sender, args) =>
            {
                if (App.Kp2a.CurrentDb != null)
                    App.Kp2a.CurrentDb.DrawableFactory.Clear();

            };

            Preference cachingPreference = FindPreference(GetString(Resource.String.UseOfflineCache_key));
            cachingPreference.PreferenceChange += OnUseOfflineCacheChanged;


        }

        private string GetDisplayLanguage(Dictionary<string, Java.Util.Locale> localesByCode, string languageCode)
        {
            return languageCode != null && localesByCode.ContainsKey(languageCode) ? localesByCode[languageCode]?.DisplayLanguage : GetString(Resource.String.SystemLanguage);
        }

        private void AppLanguagePref_PreferenceChange(object sender, Preference.PreferenceChangeEventArgs e)
        {
            throw new NotImplementedException();
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
                !((AutofillManager) Activity.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
                    .IsAutofillSupported)
            {
                var passwordAccessScreen =
                    (PreferenceScreen) FindPreference(Activity.GetString(Resource.String.password_access_prefs_key));
                passwordAccessScreen.RemovePreference(autofillScreen);
            }
            else
            {
                if (((AutofillManager) Activity.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager))))
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

            bool jschLogEnable = PreferenceManager.GetDefaultSharedPreferences(Application.Context)
                .GetBoolean(Application.Context.GetString(Resource.String.JSchDebug_key), false);
            SetJSchLogging(jschLogEnable);
        }

        private void OnJSchDebugChanged(object sender, Preference.PreferenceChangeEventArgs e)
        {
            SetJSchLogging((bool)e.NewValue);
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

	    private void AlgorithmPrefChange(object sender, Preference.PreferenceChangeEventArgs preferenceChangeEventArgs)
	    {
			var db = App.Kp2a.CurrentDb;
			var previousCipher = db.KpDatabase.DataCipherUuid;
			db.KpDatabase.DataCipherUuid = new PwUuid(MemUtil.HexStringToByteArray((string)preferenceChangeEventArgs.NewValue));

			SaveDb save = new SaveDb(Activity, App.Kp2a, App.Kp2a.CurrentDb, new ActionOnFinish(Activity, (success, message, activity) =>
			{
				if (!success)
				{
					db.KpDatabase.DataCipherUuid = previousCipher;
					Toast.MakeText(activity, message, ToastLength.Long).Show();
					return;
				}
				preferenceChangeEventArgs.Preference.Summary =
					CipherPool.GlobalPool.GetCipher(db.KpDatabase.DataCipherUuid).DisplayName;
			}));
			ProgressTask pt = new ProgressTask(App.Kp2a, Activity, save);
			pt.Run();
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
			Kp2aLog.Log("previous kdf: " + KdfPool.Get(db.KpDatabase.KdfParameters.KdfUuid) + " " + db.KpDatabase.KdfParameters.KdfUuid.ToHexString() );
		    db.KpDatabase.KdfParameters =
			    KdfPool.Get(
				    new PwUuid(MemUtil.HexStringToByteArray((string)preferenceChangeEventArgs.NewValue)))
				    .GetDefaultParameters();

			Kp2aLog.Log("--new    kdf: " + KdfPool.Get(db.KpDatabase.KdfParameters.KdfUuid) + " " + db.KpDatabase.KdfParameters.KdfUuid.ToHexString());
		    
			SaveDb save = new SaveDb(Activity, App.Kp2a, App.Kp2a.CurrentDb, new ActionOnFinish(Activity, (success, message, activity) =>
			{
				if (!success)
				{
					db.KpDatabase.KdfParameters = previousKdfParams;
					Toast.MakeText(activity, message, ToastLength.Long).Show();
					return;
				}
				UpdateKdfScreen();
				
			}));
			ProgressTask pt = new ProgressTask(App.Kp2a, Activity, save);
			pt.Run();

	    }

	    private void UpdateKdfSummary(Preference preference)
		{
			preference.Summary = ((keepass2android.settings.KdfNumberParamPreference)preference).ParamValue.ToString();
		}

	    private void PrepareNoDonationReminderPreference(Activity ctx, PreferenceScreen screen, Preference preference)
	    {
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);

			if (!prefs.GetBoolean("DismissedDonateReminder", false))
			{
				screen.RemovePreference(preference);
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
					ProgressTask pt = new ProgressTask(App.Kp2a, Activity,
									new AddTemplateEntries(Activity, App.Kp2a, new ActionOnFinish(Activity,
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
                ((PreferenceScreen) FindPreference(GetString(Resource.String.db_key))).RemovePreference(databaseName);
            }
            else
            {
                databaseName.Enabled = db.CanWrite;
                ((EditTextPreference) databaseName).EditText.Text = db.KpDatabase.Name;
                ((EditTextPreference) databaseName).Text = db.KpDatabase.Name;
                databaseName.PreferenceChange += (sender, e) =>
                {
                    DateTime previousNameChanged = db.KpDatabase.NameChanged;
                    String previousName = db.KpDatabase.Name;
                    db.KpDatabase.Name = e.NewValue.ToString();

                    SaveDb save = new SaveDb(Activity, App.Kp2a, App.Kp2a.CurrentDb, new ActionOnFinish(Activity, (success, message, activity) =>
                    {
                        if (!success)
                        {
                            db.KpDatabase.Name = previousName;
                            db.KpDatabase.NameChanged = previousNameChanged;
                            Toast.MakeText(activity, message, ToastLength.Long).Show();
                        }
                        else
                        {
                            // Name is reflected in notification, so update it
                            App.Kp2a.UpdateOngoingNotification();
                        }
                    }));
                    ProgressTask pt = new ProgressTask(App.Kp2a, Activity, save);
                    pt.Run();
                };
            }
        }

        private void PrepareDefaultUsername(Database db)
        {
            Preference defaultUser = FindPreference(GetString(Resource.String.default_username_key));
            if (!db.DatabaseFormat.HasDefaultUsername)
            {
                ((PreferenceScreen) FindPreference(GetString(Resource.String.db_key))).RemovePreference(defaultUser);
            }
            else
            {
                defaultUser.Enabled = db.CanWrite;
                ((EditTextPreference) defaultUser).EditText.Text = db.KpDatabase.DefaultUserName;
                ((EditTextPreference) defaultUser).Text = db.KpDatabase.DefaultUserName;
                defaultUser.PreferenceChange += (sender, e) =>
                {
                    DateTime previousUsernameChanged = db.KpDatabase.DefaultUserNameChanged;
                    String previousUsername = db.KpDatabase.DefaultUserName;
                    db.KpDatabase.DefaultUserName = e.NewValue.ToString();

                    SaveDb save = new SaveDb(Activity, App.Kp2a, App.Kp2a.CurrentDb, new ActionOnFinish(Activity, (success, message, activity) =>
                    {
                        if (!success)
                        {
                            db.KpDatabase.DefaultUserName = previousUsername;
                            db.KpDatabase.DefaultUserNameChanged = previousUsernameChanged;
                            Toast.MakeText(activity, message, ToastLength.Long).Show();
                        }
                    }));
                    ProgressTask pt = new ProgressTask(App.Kp2a, Activity, save);
                    pt.Run();
                };
            }
        }

        public void PrepareSeparateNotificationsPreference()
        {
            try
            {
                //depending on Android version, we offer to show a combined notification (with action buttons) (since API level 16)
                Preference separateNotificationsPref = FindPreference(Activity.GetString(Resource.String.ShowSeparateNotifications_key));
                var passwordAccessScreen = ((PreferenceScreen)FindPreference(Activity.GetString(Resource.String.password_access_prefs_key)));
                if ((int)Build.VERSION.SdkInt < 16)
                {
                    passwordAccessScreen.RemovePreference(separateNotificationsPref);
                }
            }
            catch (Exception ex)
            {
				Kp2aLog.LogUnexpectedError(ex);
            }
        }

        private void OnUseOfflineCacheChanged(object sender, Preference.PreferenceChangeEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                AlertDialog.Builder builder = new AlertDialog.Builder(Activity);
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
                        Toast.MakeText(LocaleManager.LocalizedAppContext, ex.Message, ToastLength.Long).Show();
                    }
                }
                    );

                builder.SetNegativeButton(App.Kp2a.GetResourceString(UiStringKey.no), (o, args) =>
                {
	                ((CheckBoxPreference) e.Preference).Checked = true;
                }
                );
                builder.SetCancelable(false);
                Dialog dialog = builder.Create();
                dialog.Show();


            }
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
                        var builder = new AlertDialog.Builder(Activity);
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
                        Toast.MakeText(Activity, App.Kp2a.GetResourceString(UiStringKey.ErrorOcurred) + " " + e.Message, ToastLength.Long).Show();
                    };
                }



            };

            new SimpleLoadingDialog(Activity, GetString(Resource.String.CopyingFile), false,
                                  copyAndReturnPostExecute
                ).Execute();

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
                        var builder = new AlertDialog.Builder(Activity);
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
                catch (Exception e)
                {
                    return () =>
                    {
                        Toast.MakeText(Activity, App.Kp2a.GetResourceString(UiStringKey.ErrorOcurred) + " " + e.Message, ToastLength.Long).Show();
                    };
                }



            };

            new SimpleLoadingDialog(Activity, GetString(Resource.String.CopyingFile), false,
                                  copyAndReturnPostExecute
                ).Execute();

        }

		
        
        private void SetAlgorithm(Database db, Preference algorithm)
        {
            algorithm.Summary = CipherPool.GlobalPool.GetCipher(db.KpDatabase.DataCipherUuid).DisplayName;
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
            preference.PreferenceChange += delegate(object sender, Preference.PreferenceChangeEventArgs args)
            {
                if ((bool)args.NewValue)
                {
                    new AlertDialog.Builder(ctx)
                        .SetTitle(ctx.GetString(AppNames.AppNameResource))
                        .SetCancelable(false)
                        .SetPositiveButton(Android.Resource.String.Ok, delegate(object o, DialogClickEventArgs eventArgs)
                        {
                            Util.GotoDonateUrl(ctx);
                            ((Dialog)o).Dismiss();
                        })
                        .SetMessage(Resource.String.NoDonateOption_question)
                        .Create().Show();

                }
            };

        }

        
    }

    
    /// <summary>
	/// Activity to configure the application and database settings. The database must be unlocked, and this activity will close if it becomes locked.
	/// </summary>
    [Activity(Label = "@string/app_name", Theme = "@style/MyTheme", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]			
	public class DatabaseSettingsActivity : LockCloseActivity 
	{

		public DatabaseSettingsActivity()
		{
			
		}

		public static void Launch(Activity ctx)
		{
			ctx.StartActivity(new Intent(ctx, typeof(DatabaseSettingsActivity)));
		}

	    protected override void OnCreate(Bundle savedInstanceState)
	    {
	        base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.preference);

            SetSupportActionBar(FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.mytoolbar));

	    }

	}
}

