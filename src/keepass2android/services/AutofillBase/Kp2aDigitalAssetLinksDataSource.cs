using System.Collections.Generic;
using Android.Content;

namespace keepass2android.services.AutofillBase
{

    internal class Kp2aDigitalAssetLinksDataSource
    {
        private static Kp2aDigitalAssetLinksDataSource instance;

        private Kp2aDigitalAssetLinksDataSource() { }

        public static Kp2aDigitalAssetLinksDataSource Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Kp2aDigitalAssetLinksDataSource();
                }
                return instance;
            }
        }

        public bool IsValid(Context context, string webDomain, string packageName)
        {
            return (IsTrustedBrowser(packageName));

        }

        static readonly HashSet<string> _trustedBrowsers = new HashSet<string>
        {
            "org.mozilla.firefox","org.mozilla.firefox_beta","org.mozilla.klar","org.mozilla.focus",
            "org.mozilla.fenix","org.mozilla.fenix.nightly","org.mozilla.reference.browser",
            "com.android.browser","com.android.chrome","com.chrome.beta","com.chrome.dev","com.chrome.canary",
            "com.google.android.apps.chrome","com.google.android.apps.chrome_dev",
            "com.opera.browser","com.opera.browser.beta","com.opera.mini.native",
            "com.brave.browser","com.yandex.browser","com.microsoft.emmx","com.amazon.cloud9",
            "com.sec.android.app.sbrowser","com.sec.android.app.sbrowser.beta","org.codeaurora.swe.browser",
            "mark.via.gp","org.bromite.bromite", "org.mozilla.fennec_fdroid"
        };

        private bool IsTrustedBrowser(string packageName)
        {
            return _trustedBrowsers.Contains(packageName);
        }
    }
}
