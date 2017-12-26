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

        protected override FilledAutofillFieldCollection GetDataset(Intent data)
        {
            if (!App.Kp2a.GetDb().Loaded || (App.Kp2a.QuickLocked))
                return null;

            string username = App.Kp2a.GetDb().LastOpenedEntry.Entry.Strings.ReadSafe(PwDefs.UserNameField);
            string password = App.Kp2a.GetDb().LastOpenedEntry.Entry.Strings.ReadSafe(PwDefs.PasswordField);

            FilledAutofillField pwdField =
                new FilledAutofillField
                {
                    AutofillHints = new[] {W3cHints.NAME, W3cHints.EMAIL},
                    TextValue = password
                };

            FilledAutofillField userField = new FilledAutofillField
            {
                AutofillHints = new[] {W3cHints.NEW_PASSWORD, W3cHints.CURRENT_PASSWORD},
                TextValue = username
            };

            FilledAutofillFieldCollection fieldCollection = new FilledAutofillFieldCollection();
            fieldCollection.HintMap = new Dictionary<string, FilledAutofillField>();
            fieldCollection.Add(userField);
            fieldCollection.Add(pwdField);

            fieldCollection.DatasetName = App.Kp2a.GetDb().LastOpenedEntry.Entry.Strings.ReadSafe(PwDefs.TitleField);

            return fieldCollection;
        }

        public override IAutofillIntentBuilder IntentBuilder => new Kp2aAutofillIntentBuilder();
     
    }
}