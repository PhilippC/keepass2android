// This file is part of Keepass2Android, Copyright 2026 Philipp Crocoll.
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

using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Android.Views.Autofill;

namespace keepass2android.services.Kp2aCredentialProvider
{
  /// <summary>
  /// Checks whether Keepass2Android is set up as credential provider (passkeys) and whether Chrome forwards
  /// autofill requests to third-party services. Also opens the matching settings screens.
  /// </summary>
  public static class CredentialProviderStatus
  {
    // Settings.ACTION_CREDENTIAL_PROVIDER (API 34)
    private const string ActionCredentialProvider = "android.settings.CREDENTIAL_PROVIDER";

    // Chrome channels which may provide the AutofillThirdPartyModeContentProvider (Chrome 135+).
    // They must be listed in <queries> in the manifests.
    private static readonly string[] ChromePackages =
      ["com.android.chrome", "com.chrome.beta", "com.chrome.dev", "com.chrome.canary"];

    private static readonly TimeSpan ChromeStateCacheDuration = TimeSpan.FromHours(1);
    private static List<string>? _cachedChromePackagesWithoutThirdPartyMode;
    private static DateTime _chromeStateCheckTime;

    public static bool IsCredentialProviderSupported => OperatingSystem.IsAndroidVersionAtLeast(34);

    /// <summary>
    /// Returns whether our CredentialProviderService is enabled in the system settings.
    /// Returns null if this can't be determined (Android before 14 or no Credential Manager on this device).
    /// </summary>
    public static bool? IsCredentialProviderEnabled(Context context)
    {
      if (!OperatingSystem.IsAndroidVersionAtLeast(34))
        return null;
      try
      {
        if (!context.PackageManager!.HasSystemFeature(PackageManager.FeatureCredentials))
          return null;
        var credentialManager = (Android.Credentials.CredentialManager?)context.GetSystemService(
          Java.Lang.Class.FromType(typeof(Android.Credentials.CredentialManager)));
        if (credentialManager == null)
          return null;
        return credentialManager.IsEnabledCredentialProviderService(
          new ComponentName(context, Java.Lang.Class.FromType(typeof(Kp2aCredentialProviderService))));
      }
      catch (Exception e)
      {
        Kp2aLog.Log("Failed to check whether credential provider is enabled: " + e.Message);
        return null;
      }
    }

    /// <summary>
    /// Returns whether Keepass2Android is the selected autofill service.
    /// </summary>
    public static bool IsAutofillServiceEnabled(Context context)
    {
      if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        return false;
      try
      {
        var autofillManager = (AutofillManager?)context.GetSystemService(Java.Lang.Class.FromType(typeof(AutofillManager)));
        return autofillManager != null && autofillManager.IsAutofillSupported && autofillManager.HasEnabledAutofillServices;
      }
      catch (Exception e)
      {
        Kp2aLog.Log("Failed to check whether autofill service is enabled: " + e.Message);
        return false;
      }
    }

    /// <summary>
    /// True if Keepass2Android is the autofill service but not enabled as credential provider.
    /// This happens e.g. when the autofill service was selected before the credential provider was added
    /// to the app: selecting the preferred service in system settings sets both, but an update doesn't.
    /// </summary>
    public static bool IsAutofillEnabledButCredentialProviderDisabled(Context context)
    {
      return IsCredentialProviderEnabled(context) == false && IsAutofillServiceEnabled(context);
    }

    /// <summary>
    /// Opens the system settings for "Passwords, passkeys &amp; accounts". Selecting Keepass2Android as preferred
    /// service there enables both the autofill service and the credential provider.
    /// Returns false if the settings screen is not available.
    /// </summary>
    public static bool TryOpenCredentialProviderSettings(Context context)
    {
      if (!OperatingSystem.IsAndroidVersionAtLeast(34))
        return false;
      try
      {
        var intent = new Intent(ActionCredentialProvider, Android.Net.Uri.Parse("package:" + context.PackageName));
        if (context is not Android.App.Activity)
          intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
        return true;
      }
      catch (ActivityNotFoundException e)
      {
        Kp2aLog.Log("Credential provider settings not available: " + e.Message);
        return false;
      }
    }

    /// <summary>
    /// Returns the installed Chrome channels which fill passwords with Google Password Manager instead of
    /// forwarding autofill requests to third-party services such as Keepass2Android.
    /// Chrome versions which don't report this setting (before 135) are not included.
    /// The result is cached because querying starts Chrome's process.
    /// </summary>
    public static List<string> GetChromePackagesWithoutThirdPartyAutofill(Context context)
    {
      if (_cachedChromePackagesWithoutThirdPartyMode != null &&
          DateTime.UtcNow - _chromeStateCheckTime < ChromeStateCacheDuration)
        return _cachedChromePackagesWithoutThirdPartyMode;

      var result = new List<string>();
      foreach (var chromePackage in ChromePackages)
      {
        if (!IsPackageInstalled(context, chromePackage))
          continue;
        if (GetChromeThirdPartyAutofillState(context, chromePackage) == false)
          result.Add(chromePackage);
      }

      _cachedChromePackagesWithoutThirdPartyMode = result;
      _chromeStateCheckTime = DateTime.UtcNow;
      return result;
    }

    /// <summary>
    /// Opens Chrome's autofill settings, where "Autofill using another service" can be selected.
    /// </summary>
    public static bool TryOpenChromeAutofillSettings(Context context, string chromePackage)
    {
      // the user is likely to change the setting, so check again next time
      _cachedChromePackagesWithoutThirdPartyMode = null;
      try
      {
        var intent = new Intent(Intent.ActionApplicationPreferences);
        intent.AddCategory(Intent.CategoryDefault);
        intent.AddCategory(Intent.CategoryAppBrowser);
        intent.AddCategory(Intent.CategoryPreference);
        intent.SetPackage(chromePackage);
        if (context is not Android.App.Activity)
          intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
        return true;
      }
      catch (ActivityNotFoundException e)
      {
        Kp2aLog.Log("Chrome autofill settings not available: " + e.Message);
        return false;
      }
    }

    /// <summary>
    /// Returns true if Chrome forwards autofill requests to the Android autofill service, false if it uses
    /// its built-in password manager, null if Chrome doesn't report this setting.
    /// See https://android-developers.googleblog.com/2025/02/chrome-3p-autofill-services-update.html
    /// </summary>
    private static bool? GetChromeThirdPartyAutofillState(Context context, string chromePackage)
    {
      try
      {
        var uri = new Android.Net.Uri.Builder()
          .Scheme(ContentResolver.SchemeContent)!
          .Authority(chromePackage + ".AutofillThirdPartyModeContentProvider")!
          .Path("autofill_third_party_mode")!
          .Build()!;
        using var cursor = context.ContentResolver!.Query(uri, ["autofill_third_party_state"], null, null, null);
        if (cursor == null || !cursor.MoveToFirst())
          return null;
        int index = cursor.GetColumnIndex("autofill_third_party_state");
        if (index < 0)
          return null;
        return cursor.GetInt(index) != 0;
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Failed to query autofill mode of {chromePackage}: {e.Message}");
        return null;
      }
    }

    private static bool IsPackageInstalled(Context context, string packageName)
    {
      try
      {
        var appInfo = context.PackageManager!.GetApplicationInfo(packageName, 0);
        return appInfo.Enabled;
      }
      catch (PackageManager.NameNotFoundException)
      {
        return false;
      }
    }
  }
}
