// The entry fields for app identities ("AndroidApp", "AndroidApp Signature") follow KeePassDX
// (https://github.com/Kunzisoft/KeePassDX), see AppOriginEntryField.kt, for compatibility.
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

using System.Net;
using System.Text.Json;
using KeePassLib;
using Org.Json;

namespace keepass2android.services.Kp2aCredentialProvider
{
  public enum AppLinkResult
  {
    /// <summary>The website's assetlinks.json lists the app (package name and signing certificate).</summary>
    Linked,
    /// <summary>The website was reachable, but does not list the app.</summary>
    NotLinked,
    /// <summary>The website's assetlinks.json could not be retrieved (offline, timeout, server error).</summary>
    Unreachable
  }

  /// <summary>
  /// Checks via Digital Asset Links (https://developers.google.com/digital-asset-links) whether a website
  /// allows an Android app to use its credentials.
  /// </summary>
  public static class AppLinkVerifier
  {
    private static readonly string[] AcceptedRelations =
    {
      "delegate_permission/common.get_login_creds",
      "delegate_permission/common.handle_all_urls"
    };

    private const int MaxFiles = 5; // assetlinks.json plus included statement lists
    private const int MaxFileSize = 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static async Task<AppLinkResult> VerifyAsync(string domain, PrivilegedApp app)
    {
      if (Uri.CheckHostName(domain) != UriHostNameType.Dns)
      {
        Kp2aLog.Log($"AppLinkVerifier: invalid domain {domain}");
        return AppLinkResult.NotLinked;
      }
#if NoNet
      // no internet permission in this flavor
      return AppLinkResult.Unreachable;
#else
      // Like Android's own verification, redirects are not followed
      using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
      client.Timeout = RequestTimeout;

      var mainUrl = $"https://{domain}/.well-known/assetlinks.json";
      var pending = new Queue<string>();
      pending.Enqueue(mainUrl);
      var visited = new HashSet<string>();
      var includeUnreachable = false;

      while (pending.Count > 0 && visited.Count < MaxFiles)
      {
        var url = pending.Dequeue();
        if (!visited.Add(url))
          continue;

        var (json, unreachable) = await FetchAsync(client, url);
        if (json == null)
        {
          if (url == mainUrl)
            return unreachable ? AppLinkResult.Unreachable : AppLinkResult.NotLinked;
          includeUnreachable |= unreachable;
          continue;
        }

        if (ContainsApp(json, app, pending))
        {
          Kp2aLog.Log($"AppLinkVerifier: {app.PackageName} is linked to {domain}");
          return AppLinkResult.Linked;
        }
      }

      Kp2aLog.Log($"AppLinkVerifier: {app.PackageName} is not linked to {domain}");
      return includeUnreachable ? AppLinkResult.Unreachable : AppLinkResult.NotLinked;
#endif
    }

    /// <summary>
    /// Returns the content of the file, or null. unreachable is true for network errors and server errors,
    /// false if the server answered but has no (valid) file.
    /// </summary>
    private static async Task<(string? Json, bool Unreachable)> FetchAsync(HttpClient client, string url)
    {
      try
      {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
        {
          Kp2aLog.Log($"AppLinkVerifier: {url} returned {(int)response.StatusCode}");
          return (null, true);
        }
        if (response.StatusCode != HttpStatusCode.OK)
        {
          Kp2aLog.Log($"AppLinkVerifier: {url} returned {(int)response.StatusCode}");
          return (null, false);
        }
        if (response.Content.Headers.ContentLength > MaxFileSize)
          return (null, false);
        var content = await response.Content.ReadAsStringAsync();
        return content.Length > MaxFileSize ? (null, false) : (content, false);
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"AppLinkVerifier: failed to fetch {url}: {e.Message}");
        return (null, true);
      }
    }

    /// <summary>
    /// Returns true if the statement list contains a statement for the app with an accepted relation.
    /// "include" statements are added to <paramref name="includes"/>.
    /// </summary>
    private static bool ContainsApp(string json, PrivilegedApp app, Queue<string> includes)
    {
      try
      {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
          return false;

        foreach (var statement in document.RootElement.EnumerateArray())
        {
          if (statement.ValueKind != JsonValueKind.Object)
            continue;

          if (statement.TryGetProperty("include", out var include) && include.ValueKind == JsonValueKind.String)
          {
            var includeUrl = include.GetString();
            if (includeUrl != null && includeUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
              includes.Enqueue(includeUrl);
            continue;
          }

          if (!statement.TryGetProperty("relation", out var relations) || relations.ValueKind != JsonValueKind.Array
              || !relations.EnumerateArray().Any(r => r.ValueKind == JsonValueKind.String && AcceptedRelations.Contains(r.GetString())))
            continue;

          if (!statement.TryGetProperty("target", out var target) || target.ValueKind != JsonValueKind.Object
              || !target.TryGetProperty("namespace", out var ns) || ns.ValueKind != JsonValueKind.String || ns.GetString() != "android_app"
              || !target.TryGetProperty("package_name", out var package) || package.ValueKind != JsonValueKind.String || package.GetString() != app.PackageName
              || !target.TryGetProperty("sha256_cert_fingerprints", out var fingerprints) || fingerprints.ValueKind != JsonValueKind.Array)
            continue;

          if (fingerprints.EnumerateArray().Any(fp => fp.ValueKind == JsonValueKind.String
                && app.Fingerprints.Contains(fp.GetString()!.Trim(), StringComparer.OrdinalIgnoreCase)))
            return true;
        }
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"AppLinkVerifier: invalid assetlinks.json: {e.Message}");
      }
      return false;
    }
  }

  /// <summary>
  /// Apps which are allowed to use a passkey entry, stored in the entry ("pinned") in the same fields as KeePassDX:
  /// "AndroidApp" (package name) and "AndroidApp Signature" (SHA-256 certificate fingerprints, separated by "##SIG##"),
  /// with suffixes "_1", "_2", ... for further apps.
  /// </summary>
  public static class PasskeyAppPins
  {
    public const string AppIdField = "AndroidApp";
    public const string AppSignatureField = "AndroidApp Signature";
    private const string SignatureDelimiter = "##SIG##";

    private static string Suffix(int position) => position > 0 ? "_" + position : "";

    public static List<PrivilegedApp> GetPinnedApps(PwEntry entry)
    {
      var result = new List<PrivilegedApp>();
      for (int position = 0; entry.Strings.Exists(AppIdField + Suffix(position)); position++)
      {
        var packageName = entry.Strings.ReadSafe(AppIdField + Suffix(position));
        var fingerprints = entry.Strings.ReadSafe(AppSignatureField + Suffix(position))
          .Split(SignatureDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        result.Add(new PrivilegedApp(packageName, fingerprints));
      }
      return result;
    }

    /// <summary>
    /// True if the entry contains the app with at least one matching signing certificate.
    /// Entries pinned by other KeePass apps without a signature don't match.
    /// </summary>
    public static bool IsPinned(PwEntry entry, PrivilegedApp app)
    {
      return GetPinnedApps(entry).Any(pinned => pinned.Matches(app));
    }

    /// <summary>
    /// Adds the app fields to the field JSON of a new entry.
    /// </summary>
    public static void AddPinFields(JSONObject fieldsJson, PrivilegedApp app)
    {
      fieldsJson.Put(AppIdField, app.PackageName);
      fieldsJson.Put(AppSignatureField, string.Join(SignatureDelimiter, app.Fingerprints));
    }
  }
}
