using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib.Serialization;

namespace keepass2android
{
    public interface IKp2aApp
    {
        void SetShutdown();
        Database GetDb();

        void StoreOpenedFileAsRecent(IOConnectionInfo ioc, string keyfile);

        Database CreateNewDatabase();

        string GetResourceString(UiStringKey stringKey);

        bool GetBooleanPreference(PreferenceKey key);

        void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey, 
            EventHandler<DialogClickEventArgs> yesHandler, 
            EventHandler<DialogClickEventArgs> noHandler, 
            EventHandler<DialogClickEventArgs> cancelHandler, 
            Context ctx);
    }
}