using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Preference;
using KeePass.Util.Spr;
using keepass2android.services.AutofillBase;
using keepass2android.services.AutofillBase.model;
using Keepass2android.Pluginsdk;
using keepass2android;
using KeePassLib;
using KeePassLib.Utility;
using Kp2aAutofillParser;

namespace keepass2android.services.Kp2aAutofill
{
    [Activity(Label = "@string/app_name",
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
        Theme = "@style/Kp2aTheme_ActionBar",
        WindowSoftInputMode = SoftInput.AdjustResize,
        Permission = "keepass2android." + AppNames.PackagePart + ".permission.Kp2aChooseAutofill")]
    public class ChooseForAutofillActivity : ChooseForAutofillActivityBase
    {
        public bool ActivateKeyboardWhenTotpPreference
        {
            get
            {
                return PreferenceManager.GetDefaultSharedPreferences(this)
                    .GetBoolean("AutoFillTotp_prefs_ActivateKeyboard_key", false);
            }
        }
        public bool CopyTotpToClipboardPreference
        {
            get
            {
                return PreferenceManager.GetDefaultSharedPreferences(this)
                    .GetBoolean("AutoFillTotp_prefs_CopyTotpToClipboard_key", true);
            }
        }

        public bool ShowNotificationPreference
        {
            get
            {
                return PreferenceManager.GetDefaultSharedPreferences(this)
                    .GetBoolean("AutoFillTotp_prefs_ShowNotification_key", true);
            }
        }

        protected override Intent GetQueryIntent(string requestedUrl, bool autoReturnFromQuery, bool useLastOpenedEntry)
        {
            if (useLastOpenedEntry && (App.Kp2a.LastOpenedEntry?.SearchUrl == requestedUrl))
            {
                return null;
            }
            //launch SelectCurrentDbActivity (which is root of the stack (exception: we're even below!)) with the appropriate task.
            //will return the results later
            Intent i = new Intent(this, typeof(SelectCurrentDbActivity));
            //don't show user notifications when an entry is opened.
            var task = new SearchUrlTask()
            {
                UrlToSearchFor = requestedUrl,
                AutoReturnFromQuery = autoReturnFromQuery
            };
            SetTotpDependantActionsOnTask(task);

            task.ToIntent(i);
            return i;
        }

        private void SetTotpDependantActionsOnTask(SelectEntryTask task)
        {
            task.ShowUserNotifications =
                ShowNotificationPreference ? ActivationCondition.WhenTotp : ActivationCondition.Never;
            task.CopyTotpToClipboard = CopyTotpToClipboardPreference;
            task.ActivateKeyboard = ActivateKeyboardWhenTotpPreference
                ? ActivationCondition.WhenTotp
                : ActivationCondition.Never;
        }

        protected override Intent GetOpenEntryIntent(string entryUuid)
        {
            Intent i = new Intent(this, typeof(SelectCurrentDbActivity));
            //don't show user notifications when an entry is opened.
            var task = new OpenSpecificEntryTask() { EntryUuid = entryUuid };
            SetTotpDependantActionsOnTask(task);
            task.ToIntent(i);
            return i;
        }

        protected override Result ExpectedActivityResult => KeePass.ExitCloseAfterTaskComplete;

        protected override FilledAutofillFieldCollection<ViewNodeInputField> GetDataset()
        {
            if (App.Kp2a.CurrentDb == null || (App.Kp2a.QuickLocked))
                return null;
            var entryOutput = App.Kp2a.LastOpenedEntry;

            return GetFilledAutofillFieldCollectionFromEntry(entryOutput, this);
        }

        public static FilledAutofillFieldCollection<ViewNodeInputField> GetFilledAutofillFieldCollectionFromEntry(PwEntryOutput pwEntryOutput, Context context)
        {
            if (pwEntryOutput == null)
                return null;
            FilledAutofillFieldCollection<ViewNodeInputField> fieldCollection = new FilledAutofillFieldCollection<ViewNodeInputField>();
            var pwEntry = pwEntryOutput.Entry;

            foreach (string key in pwEntryOutput.OutputStrings.GetKeys())
            {

                FilledAutofillField field =
                    new FilledAutofillField
                    {
                        AutofillHints = GetCanonicalHintsFromKp2aField(key).ToArray(),
                        TextValue = pwEntryOutput.OutputStrings.ReadSafe(key)
                    };
                fieldCollection.Add(field);

            }
            if (IsCreditCard(pwEntry, context) && pwEntry.Expires)
            {
                DateTime expTime = pwEntry.ExpiryTime;
                FilledAutofillField field =
                    new FilledAutofillField
                    {
                        AutofillHints = new[] { View.AutofillHintCreditCardExpirationDate },
                        DateValue = (long)(1000 * TimeUtil.SerializeUnix(expTime))
                    };
                fieldCollection.Add(field);

                field =
                    new FilledAutofillField
                    {
                        AutofillHints = new[] { View.AutofillHintCreditCardExpirationDay },
                        TextValue = expTime.Day.ToString()
                    };
                fieldCollection.Add(field);

                field =
                    new FilledAutofillField
                    {
                        AutofillHints = new[] { View.AutofillHintCreditCardExpirationMonth },
                        TextValue = expTime.Month.ToString()
                    };
                fieldCollection.Add(field);

                field =
                    new FilledAutofillField
                    {
                        AutofillHints = new[] { View.AutofillHintCreditCardExpirationYear },
                        TextValue = expTime.Year.ToString()
                    };
                fieldCollection.Add(field);
            }


            fieldCollection.DatasetName = pwEntry.Strings.ReadSafe(PwDefs.TitleField);
            fieldCollection.DatasetName = SprEngine.Compile(fieldCollection.DatasetName, new SprContext(pwEntry, App.Kp2a.CurrentDb.KpDatabase, SprCompileFlags.All));

            return fieldCollection;
        }

        private static bool IsCreditCard(PwEntry pwEntry, Context context)
        {
            return pwEntry.Strings.Exists("cc-number")
                || pwEntry.Strings.Exists("cc-csc")
                || pwEntry.Strings.Exists(context.GetString(Resource.String.TemplateField_CreditCard_CVV));
        }

        private static readonly Dictionary<string, List<string>> keyToHint = BuildKeyToHints();

        public static string GetKp2aKeyFromHint(string canonicalHint)
        {
            var key = keyToHint.FirstOrDefault(p => p.Value.Contains(canonicalHint)).Key;
            if (string.IsNullOrWhiteSpace(key))
                return canonicalHint;
            return key;
        }

        private static Dictionary<string, List<string>> BuildKeyToHints()
        {
            var result = new Dictionary<string, List<string>>
            {
                {PwDefs.UserNameField, new List<string>{View.AutofillHintUsername, View.AutofillHintEmailAddress}},
                {PwDefs.PasswordField, new List<string>{View.AutofillHintPassword}},
                {PwDefs.UrlField, new List<string>{W3cHints.URL}},
                {
                    LocaleManager.LocalizedAppContext.GetString(Resource.String.TemplateField_CreditCard_CVV),
                    new List<string>{View.AutofillHintCreditCardSecurityCode}
                },
                {
                    LocaleManager.LocalizedAppContext.GetString(Resource.String.TemplateField_CreditCard_Owner),
                    new List<string>{W3cHints.CC_NAME}
                },
                {LocaleManager.LocalizedAppContext.GetString(Resource.String.TemplateField_Number), new List<string>{View.AutofillHintCreditCardNumber}},
                {LocaleManager.LocalizedAppContext.GetString(Resource.String.TemplateField_IdCard_Name), new List<string>{View.AutofillHintName}},
            };
            return result;
        }

        private static List<string> GetCanonicalHintsFromKp2aField(string key)
        {
            List<string> result = new List<string>() { key };
            List<string> hints;
            if (keyToHint.TryGetValue(key, out hints))
                result = hints;
            for (int i = 0; i < result.Count; i++)
            {
                result[i] = result[i].ToLower();
            }
            return result;
        }

        public override IAutofillIntentBuilder IntentBuilder => new Kp2aAutofillIntentBuilder();

    }
}