using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Preferences;

namespace keepass2android.services.AutofillBase
{

    internal class Kp2aDigitalAssetLinksDataSource
    {

        private const string Autofilltrustedapps = "AutoFillTrustedApps";
        private readonly Context _ctx;

        public Kp2aDigitalAssetLinksDataSource(Context ctx)
        {
            _ctx = ctx;
        }

        public bool IsTrustedApp(string packageName)
        {
            if (_trustedBrowsers.Contains(packageName))
                return true;
            var prefs = PreferenceManager.GetDefaultSharedPreferences(_ctx);
            var trustedApps = prefs.GetStringSet(Autofilltrustedapps, new List<string>()).ToHashSet();
            return trustedApps.Contains(packageName);
        }

        public bool IsTrustedLink(string domain, string targetPackage)
        {
            //we can fill everything into trusted apps (aka browsers)
            if (IsTrustedApp(targetPackage))
                return true;
            //see if the user explicitly allows to fill credentials for domain into targetPackage:
            var prefs = PreferenceManager.GetDefaultSharedPreferences(_ctx);
            var trustedLinks = prefs.GetStringSet("AutoFillTrustedLinks", new List<string>()).ToHashSet();
            return trustedLinks.Contains(BuildLink(domain, targetPackage));
        }

        public void RememberAsTrustedApp(string packageName)
        {
            var prefs = PreferenceManager.GetDefaultSharedPreferences(_ctx);
            var trustedApps = prefs.GetStringSet(Autofilltrustedapps, new List<string>()).ToHashSet();
            trustedApps.Add(packageName);
            prefs.Edit().PutStringSet(Autofilltrustedapps, trustedApps).Commit();

        }

        public void RememberTrustedLink(string domain, string package)
        {
            var prefs = PreferenceManager.GetDefaultSharedPreferences(_ctx);
            var trustedLinks = prefs.GetStringSet("AutoFillTrustedLinks", new List<string>()).ToHashSet();
            trustedLinks.Add(BuildLink(domain, package));
            prefs.Edit().PutStringSet("AutoFillTrustedLinks", trustedLinks).Commit();
        }

        private static string BuildLink(string domain, string package)
        {
            return domain + " + " + package;
        }


        static readonly HashSet<string> _trustedBrowsers = new HashSet<string>
        {
            "org.mozilla.firefox","org.mozilla.firefox_beta","org.mozilla.klar","org.mozilla.focus",
            "org.mozilla.fenix","org.mozilla.fenix.nightly","org.mozilla.reference.browser",
            "com.android.browser","com.android.chrome","com.chrome.beta","com.chrome.dev","com.chrome.canary",
            "com.google.android.apps.chrome","com.google.android.apps.chrome_dev",
            "com.opera.browser","com.opera.browser.beta","com.opera.mini.native","com.opera.mini.native.beta","com.opera.touch",
            "com.brave.browser","com.yandex.browser","com.microsoft.emmx","com.amazon.cloud9",
            "com.sec.android.app.sbrowser","com.sec.android.app.sbrowser.beta","org.codeaurora.swe.browser",
            "mark.via.gp","org.bromite.bromite", "org.mozilla.fennec_fdroid", "com.vivaldi.browser","com.kiwibrowser.browser",
            "acr.browser.lightning", "acr.browser.barebones", "jp.hazuki.yuzubrowser"
        };

    }
}
