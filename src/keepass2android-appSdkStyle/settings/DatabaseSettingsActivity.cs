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
using AndroidX.Preference;
using keepass2android_appSdkStyle;


namespace keepass2android
{

    /// <summary>
    /// <para>
    /// A helper class that manages language preference display and selection.
    /// </para>
    /// <para>
    /// The idea is to provide a ListPreference with a "System language" item at the top, followed by
    /// the localized list of supported language names. The items are backed by their corresponding "code".
    /// For a langauge that's the 2-char lowercase language code, which is exactly the same code that
    /// LocaleManager.Language expects.
    /// </para>
    /// <para>
    /// "System language" is a special case. LocaleManager.Language expects null, but ListPreference
    /// does not support null as a valid code. To work around this, LanguageEntry.SYS_LANG_CODE
    /// is used as the preference code. LanguageEntry.PrefCodeToLanguage(string) is used to convert the
    /// preference codes to language codes as needed.
    /// </para>
    /// </summary>
    internal class AppLanguageManager
    {
        private readonly PreferenceFragmentCompat _fragment;
        private readonly AndroidX.Preference.ListPreference _langPref;
        private readonly Dictionary<string, LanguageEntry> _langEntriesByCodeUnique;
        
        public AppLanguageManager(PreferenceFragmentCompat fragment, AndroidX.Preference.ListPreference langPref, HashSet<string> supportedLocales)
        {
            this._fragment = fragment;
            this._langPref = langPref;
            this._langEntriesByCodeUnique = CreateCodeToEntryMapping(fragment, supportedLocales);

            ConfigureLanguageList();
        }

        private static Dictionary<string, LanguageEntry> CreateCodeToEntryMapping(PreferenceFragmentCompat fragment, HashSet<string> supportedLocales)
        {
            var localesByCode = new Dictionary<string, List<Java.Util.Locale>>();
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

            var langEntriesByCodeUnique = localesByCode
                .Select(l => new KeyValuePair<string, LanguageEntry>(l.Key, LanguageEntry.OfLocale(l.Value.First())))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var sysLangEntry = LanguageEntry.SystemDefault(fragment.GetString(Resource.String.SystemLanguage));
            langEntriesByCodeUnique.Add(sysLangEntry.Code, sysLangEntry);

            return langEntriesByCodeUnique;
        }

        private void ConfigureLanguageList()
        {
            List<KeyValuePair<string, LanguageEntry>> langEntriesList = _langEntriesByCodeUnique
                .OrderByDescending(kvp => kvp.Value.IsSystem)
                .ThenBy(kvp => kvp.Value.Name)
                .ToList();

            _langPref.SetEntries(langEntriesList
                .Select(kvp => kvp.Value.Name)
                .ToArray());
            _langPref.SetEntryValues(langEntriesList
                .Select(kvp => kvp.Value.Code)
                .ToArray());

            _langPref.Summary = GetDisplayLanguage(LanguageEntry.PrefCodeToLanguage(_langPref.Value));
            _langPref.PreferenceChange += AppLanguagePrefChange;
        }

        private string GetDisplayLanguage(string languageCode)
        {
            if (languageCode != null && this._langEntriesByCodeUnique.ContainsKey(languageCode))
                return this._langEntriesByCodeUnique[languageCode]?.Name;
            else
                return _fragment.GetString(Resource.String.SystemLanguage);
        }

        private void AppLanguagePrefChange(object sender, AndroidX.Preference.Preference.PreferenceChangeEventArgs args)
        {
            string langCode = LanguageEntry.PrefCodeToLanguage((string)args.NewValue);
            LocaleManager.Language = langCode;
            _langPref.Summary = GetDisplayLanguage(langCode);
        }
    }

#pragma warning restore CS0618 // Type or member is obsolete


    /// <summary>
    /// Activity to configure the application and database settings. The database must be unlocked, and this activity will close if it becomes locked.
    /// </summary>
    [Activity(Label = "@string/app_name", Theme = "@style/Kp2aTheme_ActionBar", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
    public class DatabaseSettingsActivity : LockCloseActivity, PreferenceFragmentCompat.IOnPreferenceStartFragmentCallback
    {

		public static void Launch(Activity ctx)
		{
			ctx.StartActivity(new Intent(ctx, typeof(DatabaseSettingsActivity)));
		}

        private ActivityDesign _design;

        public DatabaseSettingsActivity()
        {
            _design = new ActivityDesign(this);
            settingsFragmentManager = new SettingsFragmentManager(this);
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

    }
}

