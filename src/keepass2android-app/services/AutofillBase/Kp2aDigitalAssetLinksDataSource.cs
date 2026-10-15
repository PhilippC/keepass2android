// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Preferences;
using keepass2android;
using Kp2aAutofillParser;

namespace keepass2android.services.AutofillBase
{


  internal class Kp2aDigitalAssetLinksDataSource : IKp2aDigitalAssetLinksDataSource
  {

    private const string Autofilltrustedapps = "AutoFillTrustedApps";
    private readonly Context _ctx;

    public Kp2aDigitalAssetLinksDataSource(Context ctx)
    {
      _ctx = ctx;
    }

    static public bool IsTrustedBrowser(string packageName)
    {
      return _trustedBrowsers.Contains(packageName);
    }

    public bool IsTrustedApp(string packageName)
    {
      if (IsTrustedBrowser(packageName))
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

    public bool IsEnabled()
    {
      return !PreferenceManager.GetDefaultSharedPreferences(_ctx).GetBoolean(_ctx.GetString(Resource.String.NoDalVerification_key), false);
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
      // Chrome variants
      "com.android.browser",
      "com.android.chrome",
      "com.chrome.beta",
      "com.chrome.canary",
      "com.chrome.dev",
      "com.google.android.apps.chrome",
      "com.google.android.apps.chrome_dev",
      "org.chromium.chrome",
        
      // Firefox variants
      "org.mozilla.fenix",
      "org.mozilla.fennec_fdroid",
      "org.mozilla.firefox",
      "org.mozilla.firefox_beta",
      "org.mozilla.focus",
      "org.mozilla.klar",
      "org.mozilla.reference.browser",
        
      // Microsoft Edge
      "com.microsoft.emmx",
        
      // Opera variants
      "com.opera.browser",
      "com.opera.browser.beta",
      "com.opera.mini.native",
      "com.opera.mini.native.beta",
      "com.opera.touch",
        
      // Samsung Internet
      "com.sec.android.app.sbrowser",
      "com.sec.android.app.sbrowser.beta",
        
      // Other established browsers
      "com.brave.browser",
      "com.kiwibrowser.browser",
      "com.vivaldi.browser",
      "com.yandex.browser",
        
      // Privacy-focused browsers
      "acr.browser.barebones",
      "acr.browser.lightning",
      "io.github.forkmaintainers.iceraven",
      "mark.via.gp",
      "org.bromite.bromite",
      "org.cromite.cromite",
      "org.ironfoxoss.ironfox",
        
      // Regional/specialized browsers
      "jp.hazuki.yuzubrowser",
      "org.codeaurora.swe.browser",
    };

  }
}
