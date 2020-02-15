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
using keepass2android.services.AutofillBase;
using keepass2android.services.AutofillBase.model;
using Keepass2android.Pluginsdk;
using KeePassLib;
using KeePassLib.Utility;

namespace keepass2android.services.Kp2aAutofill
{
    [Activity(Label = "@string/app_name",
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
        Theme = "@style/MyTheme_ActionBar",
        Permission = "keepass2android." + AppNames.PackagePart + ".permission.Kp2aChooseAutofill")]
    public class ChooseForAutofillActivity : ChooseForAutofillActivityBase
    {
        protected override Intent GetQueryIntent(string requestedUrl, bool autoReturnFromQuery)
        {
            //launch FileSelectActivity (which is root of the stack (exception: we're even below!)) with the appropriate task.
            //will return the results later
            Intent i = new Intent(this, typeof(SelectCurrentDbActivity));
            //don't show user notifications when an entry is opened.
            var task = new SearchUrlTask() { UrlToSearchFor = requestedUrl, ShowUserNotifications = ShowUserNotificationsMode.WhenTotp, AutoReturnFromQuery = autoReturnFromQuery, ActivateKeyboard = false };
            task.ToIntent(i);
            return i;
        }

        protected override Result ExpectedActivityResult => KeePass.ExitCloseAfterTaskComplete;

        protected override FilledAutofillFieldCollection GetDataset(Intent data)
        {
            if (App.Kp2a.CurrentDb==null || (App.Kp2a.QuickLocked))
                return null;
            var entryOutput = App.Kp2a.LastOpenedEntry;

            return GetFilledAutofillFieldCollectionFromEntry(entryOutput, this);
        }

        public static FilledAutofillFieldCollection GetFilledAutofillFieldCollectionFromEntry(PwEntryOutput pwEntryOutput, Context context)
        {
            if (pwEntryOutput == null)
                return null;
            FilledAutofillFieldCollection fieldCollection = new FilledAutofillFieldCollection();
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
                        AutofillHints = new[] {View.AutofillHintCreditCardExpirationDate},
                        DateValue = (long) (1000 * TimeUtil.SerializeUnix(expTime))
                    };
                fieldCollection.Add(field);

                field =
                    new FilledAutofillField
                    {
                        AutofillHints = new[] {View.AutofillHintCreditCardExpirationDay},
                        TextValue = expTime.Day.ToString()
                    };
                fieldCollection.Add(field);

                field =
                    new FilledAutofillField
                    {
                        AutofillHints = new[] {View.AutofillHintCreditCardExpirationMonth},
                        TextValue = expTime.Month.ToString()
                    };
                fieldCollection.Add(field);

                field =
                    new FilledAutofillField
                    {
                        AutofillHints = new[] {View.AutofillHintCreditCardExpirationYear},
                        TextValue = expTime.Year.ToString()
                    };
                fieldCollection.Add(field);
            }


            fieldCollection.DatasetName = pwEntry.Strings.ReadSafe(PwDefs.TitleField);

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
                    Application.Context.GetString(Resource.String.TemplateField_CreditCard_CVV),
                    new List<string>{View.AutofillHintCreditCardSecurityCode}
                },
                {
                    Application.Context.GetString(Resource.String.TemplateField_CreditCard_Owner),
                    new List<string>{W3cHints.CC_NAME}
                },
                {Application.Context.GetString(Resource.String.TemplateField_Number), new List<string>{View.AutofillHintCreditCardNumber}},
                {Application.Context.GetString(Resource.String.TemplateField_IdCard_Name), new List<string>{View.AutofillHintName}},
            };
            return result;
        }

        private static List<string> GetCanonicalHintsFromKp2aField(string key)
        {
            List<string> result = new List<string>() {key};
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