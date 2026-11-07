// The privileged allowlist approach (bundled community list, user-editable custom list) is derived from
// KeePassDX (https://github.com/Kunzisoft/KeePassDX), see PrivilegedAllowLists.kt and AndroidPrivilegedApp.kt.
// Original work Copyright 2025 Jeremy Jamet / Kunzisoft.
// Licensed under the GNU General Public License v3 or later.
//
// Modifications Copyright 2026 Philipp Crocoll.
// This file is part of Keepass2Android.
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
using AndroidX.Credentials.Provider;
using Org.Json;

namespace keepass2android.services.Kp2aCredentialProvider
{
  /// <summary>
  /// An app in a privileged allowlist: package name and the SHA-256 fingerprints of its signing certificates
  /// (upper case hex, separated by colons, as in the allowlist JSON).
  /// </summary>
  public record PrivilegedApp(string PackageName, IReadOnlyCollection<string> Fingerprints)
  {
    public bool Matches(PrivilegedApp other)
    {
      return PackageName == other.PackageName
        && Fingerprints.Any(fp => other.Fingerprints.Contains(fp, StringComparer.OrdinalIgnoreCase));
    }
  }

  /// <summary>
  /// Manages the allowlists of privileged apps (browsers) which may make passkey requests on behalf of websites.
  /// All lists use the format of https://www.gstatic.com/gpm-passkeys-privileged-apps/apps.json:
  /// - bundled lists in the Assets folder (Google's list and the community list)
  /// - a custom list which the user can edit, stored in the app's private files directory.
  /// </summary>
  public static class PrivilegedAppsAllowlist
  {
    /// <summary>
    /// Bundled allowlists in the Assets folder:
    /// - passkeys_privileged_apps_google.json: copy of https://www.gstatic.com/gpm-passkeys-privileged-apps/apps.json
    /// - passkeys_privileged_apps_community.json: copy of Bitwarden's community list
    ///   (https://github.com/bitwarden/android/blob/main/app/src/main/assets/fido2_privileged_community.json)
    /// </summary>
    private static readonly string[] BundledAllowlistAssetFiles =
    {
      "passkeys_privileged_apps_google.json",
      "passkeys_privileged_apps_community.json"
    };

    private const string CustomAllowlistFileName = "passkeys_privileged_apps_custom.json";

    private static List<string>? _bundledAllowlists;
    private static readonly object Lock = new object();

    private static List<string> GetBundledAllowlists(Context context)
    {
      lock (Lock)
      {
        if (_bundledAllowlists != null)
          return _bundledAllowlists;

        var allowlists = new List<string>();
        foreach (var fileName in BundledAllowlistAssetFiles)
        {
          try
          {
            using var reader = new StreamReader(context.Assets!.Open(fileName));
            allowlists.Add(reader.ReadToEnd());
          }
          catch (Exception e)
          {
            Kp2aLog.Log($"PrivilegedAppsAllowlist: failed to load bundled allowlist {fileName}: {e.Message}");
          }
        }
        _bundledAllowlists = allowlists;
        return allowlists;
      }
    }

    private static string GetCustomAllowlistPath(Context context)
    {
      return Path.Combine(context.FilesDir!.AbsolutePath, CustomAllowlistFileName);
    }

    private static string? GetCustomAllowlist(Context context)
    {
      lock (Lock)
      {
        try
        {
          var path = GetCustomAllowlistPath(context);
          return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e)
        {
          Kp2aLog.Log($"PrivilegedAppsAllowlist: failed to load custom allowlist: {e.Message}");
          return null;
        }
      }
    }

    /// <summary>
    /// Returns all allowlists (custom first, then bundled) as JSON strings, for use with CallingAppInfo.GetOrigin.
    /// </summary>
    public static List<string> GetAllAllowlists(Context context)
    {
      var result = new List<string>();
      var custom = GetCustomAllowlist(context);
      if (!string.IsNullOrEmpty(custom))
        result.Add(custom);
      result.AddRange(GetBundledAllowlists(context));
      return result;
    }

    public static List<PrivilegedApp> GetBundledApps(Context context)
    {
      return GetBundledAllowlists(context).SelectMany(ParseApps).ToList();
    }

    public static List<PrivilegedApp> GetCustomApps(Context context)
    {
      var custom = GetCustomAllowlist(context);
      return custom == null ? new List<PrivilegedApp>() : ParseApps(custom);
    }

    /// <summary>
    /// Replaces the custom allowlist. An empty list deletes the file.
    /// </summary>
    public static bool SaveCustomApps(Context context, IEnumerable<PrivilegedApp> apps)
    {
      lock (Lock)
      {
        try
        {
          var path = GetCustomAllowlistPath(context);
          var appList = apps.ToList();
          if (appList.Count == 0)
          {
            if (File.Exists(path))
              File.Delete(path);
            return true;
          }

          // write to a temporary file first so that a failure can't leave a corrupt list behind
          var tempPath = path + ".tmp";
          File.WriteAllText(tempPath, ToJson(appList));
          File.Move(tempPath, path, true);
          return true;
        }
        catch (Exception e)
        {
          Kp2aLog.Log($"PrivilegedAppsAllowlist: failed to save custom allowlist: {e.Message}");
          return false;
        }
      }
    }

    /// <summary>
    /// Adds an app to the custom allowlist, replacing any previous entry for the same package.
    /// </summary>
    public static bool AddCustomApp(Context context, PrivilegedApp app)
    {
      var apps = GetCustomApps(context).Where(a => a.PackageName != app.PackageName).ToList();
      apps.Add(app);
      return SaveCustomApps(context, apps);
    }

    /// <summary>
    /// Parses an allowlist JSON. Invalid entries are skipped.
    /// </summary>
    public static List<PrivilegedApp> ParseApps(string json)
    {
      var result = new List<PrivilegedApp>();
      try
      {
        var apps = new JSONObject(json).OptJSONArray("apps");
        if (apps == null)
          return result;
        for (int i = 0; i < apps.Length(); i++)
        {
          var app = apps.OptJSONObject(i);
          if (app == null || app.OptString("type") != "android")
            continue;
          var info = app.OptJSONObject("info");
          var packageName = info?.OptString("package_name");
          var signatures = info?.OptJSONArray("signatures");
          if (string.IsNullOrEmpty(packageName) || signatures == null)
            continue;
          var fingerprints = new List<string>();
          for (int j = 0; j < signatures.Length(); j++)
          {
            var fingerprint = signatures.OptJSONObject(j)?.OptString("cert_fingerprint_sha256");
            if (!string.IsNullOrEmpty(fingerprint))
              fingerprints.Add(fingerprint);
          }
          if (fingerprints.Count > 0)
            result.Add(new PrivilegedApp(packageName, fingerprints));
        }
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"PrivilegedAppsAllowlist: failed to parse allowlist: {e.Message}");
      }
      return result;
    }

    public static string ToJson(IEnumerable<PrivilegedApp> apps)
    {
      var appsArray = new JSONArray();
      foreach (var app in apps)
      {
        var signatures = new JSONArray();
        foreach (var fingerprint in app.Fingerprints)
        {
          var signature = new JSONObject();
          signature.Put("build", "release");
          signature.Put("cert_fingerprint_sha256", fingerprint);
          signatures.Put(signature);
        }
        var info = new JSONObject();
        info.Put("package_name", app.PackageName);
        info.Put("signatures", signatures);

        var entry = new JSONObject();
        entry.Put("type", "android");
        entry.Put("info", info);
        appsArray.Put(entry);
      }
      var root = new JSONObject();
      root.Put("apps", appsArray);
      return root.ToString(2);
    }

    /// <summary>
    /// Returns the SHA-256 fingerprints of all certificates in the signing info in allowlist format.
    /// Includes the current signers and the signing certificate history, so that the entry matches
    /// regardless of which of these the allowlist check in CallingAppInfo.GetOrigin uses.
    /// </summary>
    internal static List<string> GetFingerprints(SigningInfo? signingInfo)
    {
      var result = new List<string>();
      if (signingInfo == null || !OperatingSystem.IsAndroidVersionAtLeast(28))
        return result;
      var signatures = (signingInfo.GetApkContentsSigners() ?? Array.Empty<Signature>())
        .Concat(signingInfo.GetSigningCertificateHistory() ?? Array.Empty<Signature>());
      foreach (var signature in signatures)
      {
        var hash = System.Security.Cryptography.SHA256.HashData(signature.ToByteArray()!);
        var fingerprint = string.Join(":", hash.Select(b => b.ToString("X2")));
        if (!result.Contains(fingerprint))
          result.Add(fingerprint);
      }
      return result;
    }

    /// <summary>
    /// Returns the package names of all installed apps which can open https URLs, i.e. browsers.
    /// </summary>
    public static HashSet<string> GetInstalledBrowserPackages(Context context)
    {
      var result = new HashSet<string>();
      try
      {
        var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse("https://example.com"));
        intent.AddCategory(Intent.CategoryBrowsable);
        var flags = OperatingSystem.IsAndroidVersionAtLeast(23) ? PackageInfoFlags.MatchAll : 0;
        var activities = context.PackageManager!.QueryIntentActivities(intent, flags);
        foreach (var resolveInfo in activities)
        {
          var packageName = resolveInfo.ActivityInfo?.PackageName;
          if (!string.IsNullOrEmpty(packageName))
            result.Add(packageName);
        }
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"PrivilegedAppsAllowlist: failed to query browsers: {e.Message}");
      }
      return result;
    }

    /// <summary>
    /// Returns an installed app with its current signing certificates, or null if it can't be determined.
    /// </summary>
    public static PrivilegedApp? GetInstalledApp(Context context, string packageName)
    {
      if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        return null;
      try
      {
        var packageInfo = context.PackageManager!.GetPackageInfo(packageName, PackageInfoFlags.SigningCertificates);
        var fingerprints = GetFingerprints(packageInfo?.SigningInfo);
        return fingerprints.Count > 0 ? new PrivilegedApp(packageName, fingerprints) : null;
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"PrivilegedAppsAllowlist: failed to get signing info for {packageName}: {e.Message}");
        return null;
      }
    }

    public static string GetAppLabel(Context context, string packageName)
    {
      try
      {
        var pm = context.PackageManager!;
        return pm.GetApplicationLabel(pm.GetApplicationInfo(packageName, 0)) ?? packageName;
      }
      catch (Exception)
      {
        return packageName;
      }
    }

    /// <summary>
    /// Returns the caller as a candidate for the custom allowlist if it is a browser acting on behalf of a website
    /// (it provides an origin and can open https URLs) but is not contained in any allowlist.
    /// Returns null otherwise, in particular for privileged browsers and for regular apps.
    /// </summary>
    public static PrivilegedApp? GetUnknownBrowserCaller(Context context, CallingAppInfo? callingAppInfo)
    {
      try
      {
        if (callingAppInfo == null || string.IsNullOrEmpty(callingAppInfo.PackageName))
          return null;
        if (!callingAppInfo.IsOriginPopulated)
          return null; // not acting on behalf of a website
        if (PasskeyOriginHelper.GetOriginFromPrivilegedCaller(context, callingAppInfo) != null)
          return null; // already privileged
        if (!GetInstalledBrowserPackages(context).Contains(callingAppInfo.PackageName))
          return null;

        var fingerprints = GetFingerprints(callingAppInfo.SigningInfo);
        if (fingerprints.Count == 0)
          return null;
        return new PrivilegedApp(callingAppInfo.PackageName, fingerprints);
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"PrivilegedAppsAllowlist: failed to check for unknown browser: {e.Message}");
        return null;
      }
    }
  }
}
