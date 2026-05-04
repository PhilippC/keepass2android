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

using Android.Content;
using Android.Util;
using AndroidX.Core.App;
using AndroidX.Credentials.Provider;
using Java.Time;
using Keepass2android.Pluginsdk;
using KeePassLib;
using Kp2aPasskey.Core;

namespace keepass2android.services.Kp2aCredentialProvider
{
  /// <summary>
  /// Shared helpers for building <see cref="BeginGetCredentialResponse"/> entries.
  /// Used by both <see cref="Kp2aCredentialProviderService"/> (DB already unlocked) and
  /// <see cref="Kp2aCredentialLauncherActivity"/> (after-unlock path via
  /// <c>PendingOperation.UnlockForGetCredentials</c>).
  /// </summary>
  [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
  internal static class GetCredentialHelper
  {
    /// <summary>
    /// Searches the open databases for password entries matching <paramref name="callingPackage"/>
    /// and adds a <see cref="PasswordCredentialEntry"/> for each one to <paramref name="responseBuilder"/>.
    /// </summary>
    public static void AddMatchingPasswordEntries(
      Context context,
      string callingPackage,
      BeginGetPasswordOption option,
      BeginGetCredentialResponse.Builder responseBuilder
    )
    {
      var query = $"{KeePass.AndroidAppScheme}{callingPackage}";
      var searchResults = ShareUrlResults.GetSearchResultsForUrl(query);
      var foundEntries = searchResults?.Entries.ToList() ?? new List<PwEntry>();

      var lastOpenedEntry = App.Kp2a.LastOpenedEntry;
      if (lastOpenedEntry != null && lastOpenedEntry.SearchUrl == query)
      {
        foundEntries.Clear();
        foundEntries.Add(lastOpenedEntry.Entry);
      }

      foreach (var entry in foundEntries)
      {
        var username = entry.Strings.ReadSafe(PwDefs.UserNameField);
        if (string.IsNullOrEmpty(username)) continue;

        responseBuilder.AddCredentialEntry(
          new PasswordCredentialEntry.Builder(
            context,
            username,
            PendingIntentCompat.GetActivity(
              context,
              // Use a random request code to ensure distinct PendingIntents for each entry
              System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue),
              new Intent(context, typeof(Kp2aCredentialLauncherActivity))
                .PutExtra(
                  Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
                  Kp2aCredentialLauncherActivity.CredentialRequestTypeGetPasswordForEntry
                )
                .PutExtra(Strings.ExtraEntryId, entry.Uuid.ToHexString()),
              (int)PendingIntentFlags.UpdateCurrent,
              true
            )!,
            option
          )
            .SetDisplayName(entry.Strings.ReadSafe(PwDefs.TitleField))
            .SetAffiliatedDomain(entry.ParentGroup?.Name)
            .SetLastUsedTime(
              Instant.OfEpochMilli(
                new DateTimeOffset(entry.LastAccessTime).ToUnixTimeMilliseconds()
              )
            )
            .Build()
        );
      }
    }

    /// <summary>
    /// Searches the open databases for passkey entries matching the relying party declared in
    /// <paramref name="option"/> and adds a <see cref="PublicKeyCredentialEntry"/> for each one
    /// to <paramref name="responseBuilder"/>. If <c>allowCredentials</c> is specified in the request,
    /// only passkeys with matching credential IDs are returned. When no entries are found, a generic
    /// "search all passkeys" entry is added instead.
    /// </summary>
    public static void AddMatchingPasskeyEntries(
      Context context,
      BeginGetPublicKeyCredentialOption option,
      BeginGetCredentialResponse.Builder responseBuilder,
      bool isAutoSelectAllowed = false
    )
    {
      try
      {
        var requestOptions = new PublicKeyCredentialRequestOptions(option.RequestJson);
        var relyingPartyId = requestOptions.RpId;
        var allowCredentials = requestOptions.AllowCredentials;

        var query = $"passkey:{relyingPartyId}";
        var searchResults = ShareUrlResults.GetSearchResultsForUrl(query);
        var foundEntries = searchResults?.Entries.ToList() ?? new List<PwEntry>();

        // Filter by allowCredentials if specified
        if (allowCredentials.Count > 0)
        {
          foundEntries = FilterEntriesByAllowCredentials(foundEntries, allowCredentials);
        }

        if (foundEntries.Count > 0)
        {
          foreach (var entry in foundEntries)
          {
            // NOTE: if multiple entries have the same username, they might not all be shown in the UI.
            // This is an Android design decision which we respect (no pseudo-different usernames or so)

            // The passkey username (account name at the RP) is shown as the primary label.
            var passkeyUsername = entry.Strings.ReadSafe(PasskeyStorage.FIELD_USERNAME);
            if (string.IsNullOrEmpty(passkeyUsername))
              passkeyUsername = entry.Strings.ReadSafe(PwDefs.UserNameField);
            if (string.IsNullOrEmpty(passkeyUsername)) passkeyUsername = "Unknown";

            // Use a random request code per PendingIntent. FLAG_UPDATE_CURRENT only matches a
            // prior PendingIntent when the request code is the same, so a random code guarantees
            // every entry gets a distinct PendingIntent even when entries share the same username.
            var entryRequestCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue);
            responseBuilder.AddCredentialEntry(
              new PublicKeyCredentialEntry.Builder(
                context,
                passkeyUsername,
                PendingIntentCompat.GetActivity(
                  context,
                  entryRequestCode,
                  new Intent(context, typeof(Kp2aCredentialLauncherActivity))
                    .PutExtra(
                      Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
                      Kp2aCredentialLauncherActivity.CredentialRequestTypeGetPasskeyForEntry
                    )
                    .PutExtra(Strings.ExtraEntryId, entry.Uuid.ToHexString())
                    .PutExtra(Kp2aCredentialLauncherActivity.ExtraRelyingPartyId, relyingPartyId)
                    .PutExtra(Kp2aCredentialLauncherActivity.ExtraRequestJson, option.RequestJson),
                  (int)PendingIntentFlags.UpdateCurrent,
                  true
                )!,
                option
              )
                .SetDisplayName(entry.Strings.ReadSafe(PwDefs.TitleField))
                .SetLastUsedTime(
                  Instant.OfEpochMilli(
                    new DateTimeOffset(entry.LastAccessTime).ToUnixTimeMilliseconds()
                  )
                )
                .SetAutoSelectAllowed(isAutoSelectAllowed)
                .Build()
            );
          }
        }
        else
        {
          // No passkeys found for this RP — show a generic "search all passkeys" entry.
          // This allows unlocking a closed database or manual entry selection.
          responseBuilder.AddAuthenticationAction(
            CreateAuthenticationAction(context)
          ).Build();
        }
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Error handling passkey get request {e.Message}");
      }
    }

    /// <summary>
    /// Filters entries to only those whose credential ID matches one in allowCredentials list.
    /// </summary>
    private static List<PwEntry> FilterEntriesByAllowCredentials(
      List<PwEntry> entries,
      List<PublicKeyCredentialDescriptor> allowCredentials
    )
    {
      var filtered = new List<PwEntry>();

      // Convert allowCredentials IDs to base64url strings for comparison
      var allowedCredentialIds = new HashSet<string>();
      foreach (var descriptor in allowCredentials)
      {
        // Convert the byte array to base64url string (URL-safe, no padding)
        var credentialIdBase64 = Base64.EncodeToString(
          descriptor.Id,
          Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap
        );
        if (!string.IsNullOrEmpty(credentialIdBase64))
        {
          allowedCredentialIds.Add(credentialIdBase64);
        }
      }

      // Filter entries
      foreach (var entry in entries)
      {
        var passkey = Kp2aPasskey.Core.PasskeyStorage.RetrievePasskey(entry);
        if (passkey != null && allowedCredentialIds.Contains(passkey.CredentialId))
        {
          filtered.Add(entry);
        }
      }

      return filtered;
    }


    public static AuthenticationAction CreateAuthenticationAction(Context context)
    {
      var action = new AuthenticationAction(
        // Providers that require unlocking the credentials before returning any credentialEntries,
        // must set up a pending intent that navigates the user to the app's unlock flow.
        context.GetString(AppNames.AppNameResource),
        PendingIntentCompat.GetActivity(
          context,
          App.Kp2a.RequestCodeForCredentialProvider,
          new Intent(context, typeof(Kp2aCredentialLauncherActivity)).PutExtra(
            Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
            Kp2aCredentialLauncherActivity.CredentialRequestTypeUnlockForGetCredentials
          ),
          (int)PendingIntentFlags.UpdateCurrent,
          true
        )!
      );

      return action;
    }
  }
}
