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

namespace keepass2android.services.Kp2aAutofill
{
    [Activity(Label = "@string/app_name",
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden,
        Theme = "@style/MyTheme_ActionBar",
        Permission = "keepass2android." + AppNames.PackagePart + ".permission.Kp2aChooseAutofill")]
    public class ChooseForAutofillActivity : ChooseForAutofillActivityBase
    {
        protected override Intent GetQueryIntent(string requestedUrl)
        {
            //launch FileSelectActivity (which is root of the stack (exception: we're even below!)) with the appropriate task.
            //will return the results later
            Intent i = new Intent(this, typeof(FileSelectActivity));
            //don't show user notifications when an entry is opened.
            var task = new SearchUrlTask() { UrlToSearchFor = requestedUrl, ShowUserNotifications = false };
            task.ToIntent(i);
            return i;
        }

        protected override Result ExpectedActivityResult => KeePass.ExitCloseAfterTaskComplete;

        protected override FilledAutofillFieldCollection GetDataset(Intent data)
        {
            if (!App.Kp2a.GetDb().Loaded || (App.Kp2a.QuickLocked))
                return null;

            FilledAutofillFieldCollection fieldCollection = new FilledAutofillFieldCollection();

            var pwEntry = App.Kp2a.GetDb().LastOpenedEntry.Entry;
            foreach (string key in pwEntry.Strings.GetKeys())
            {
                FilledAutofillField field =
                    new FilledAutofillField
                    {
                        AutofillHints = new[] { GetCanonicalHintFromKp2aField(pwEntry, key) },
                        TextValue = pwEntry.Strings.ReadSafe(key),
                        Protected = pwEntry.Strings.Get(key).IsProtected
                    };
                fieldCollection.Add(field);
            }
            //TODO add support for Keepass templates
            //TODO add values like expiration?
            //TODO if cc-exp is there, also set cc-exp-month etc.

            fieldCollection.DatasetName = pwEntry.Strings.ReadSafe(PwDefs.TitleField);

            return fieldCollection;
        }

        private static readonly Dictionary<string, string> keyToHint = new Dictionary<string, string>()
        {
            {PwDefs.UserNameField, View.AutofillHintUsername },
            {PwDefs.PasswordField, View.AutofillHintPassword },
            {PwDefs.UrlField, W3cHints.URL },
        };

        private string GetCanonicalHintFromKp2aField(PwEntry pwEntry, string key)
        {
            if (!keyToHint.TryGetValue(key, out string result))
                result = key;
            result = result.ToLower();
            return result;
        }

        public override IAutofillIntentBuilder IntentBuilder => new Kp2aAutofillIntentBuilder();
     
    }
}