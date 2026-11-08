// Derived from KeePassDX (https://github.com/Kunzisoft/KeePassDX)
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

using System;
using System.Linq;
using KeePassLib;
using KeePassLib.Security;
using Org.Json;

namespace Kp2aPasskey.Core
{
  /// <summary>
  /// Helper class for storing and retrieving passkey data in KeePass entries.
  /// Passkeys are stored in ExtraFields (Strings) using the KPEX interop format,
  /// compatible with KeePassXC and other apps supporting KPEX passkey fields.
  /// </summary>
  public static class PasskeyStorage
  {
    // ExtraField keys for passkey storage — KPEX interop format
    // KPEX prefix (KeePass EXtension) is the established interop standard for passkey fields
    public const string FIELD_USERNAME = "KPEX_PASSKEY_USERNAME";
    public const string FIELD_PRIVATE_KEY = "KPEX_PASSKEY_PRIVATE_KEY_PEM";
    public const string FIELD_CREDENTIAL_ID = "KPEX_PASSKEY_CREDENTIAL_ID";
    public const string FIELD_USER_HANDLE = "KPEX_PASSKEY_USER_HANDLE";
    public const string FIELD_RELYING_PARTY = "KPEX_PASSKEY_RELYING_PARTY";
    public const string FIELD_FLAG_BE = "KPEX_PASSKEY_FLAG_BE";
    public const string FIELD_FLAG_BS = "KPEX_PASSKEY_FLAG_BS";
    public const string PASSKEY_TAG = "Passkey";

    // Fields that must be stored encrypted (ProtectInMemory) in the KeePass entry
    public static readonly IReadOnlyList<string> ProtectedFields =
      [FIELD_PRIVATE_KEY, FIELD_CREDENTIAL_ID, FIELD_USER_HANDLE];


    /// <param name="title">Entry title (localized by the caller).</param>
    public static JSONObject CreatePasskeyFieldsJson(string title, string relyingParty, string username, PasskeyData passkey)
    {
      // Build AllFields JSON with passkey data in extra fields (compatible with KeePassDX/KeePassXC)
      // Start with standard entry fields
      var passkeyFieldsJson = new JSONObject();
      passkeyFieldsJson.Put(PwDefs.TitleField, title);
      passkeyFieldsJson.Put(PwDefs.UserNameField, username);
      passkeyFieldsJson.Put(PwDefs.UrlField, relyingParty);

      // Add passkey extra fields
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_USERNAME, passkey.Username);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_PRIVATE_KEY, passkey.PrivateKeyPem);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_CREDENTIAL_ID, passkey.CredentialId);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_USER_HANDLE, passkey.UserHandle);
      passkeyFieldsJson.Put(PasskeyStorage.FIELD_RELYING_PARTY, passkey.RelyingParty);
      if (passkey.BackupEligibility.HasValue)
        passkeyFieldsJson.Put(PasskeyStorage.FIELD_FLAG_BE, passkey.BackupEligibility.Value ? "1" : "0");
      if (passkey.BackupState.HasValue)
        passkeyFieldsJson.Put(PasskeyStorage.FIELD_FLAG_BS, passkey.BackupState.Value ? "1" : "0");
      return passkeyFieldsJson;
    }

    // Alternative field names written by StrongBox. KeePassXC reads them with priority over the
    // KPEX_* names (see KeePassXC src/browser/PasskeyUtils.cpp: getCredentialIdFromEntry /
    // getUsernameFromEntry, constants in src/core/EntryAttributes.cpp). Kp2a mirrors that
    // precedence for reading but never writes these fields.
    public const string FIELD_GENERATED_USER_ID = "KPEX_PASSKEY_GENERATED_USER_ID";
    public const string FIELD_KPXC_USERNAME = "KPXC_PASSKEY_USERNAME";

    // KeePassXC treats every attribute starting with this prefix as a passkey attribute
    // (EntryAttributes::isPasskeyAttribute / hasPasskey / removePasskeyAttributes).
    public const string PASSKEY_FIELD_PREFIX = "KPEX_PASSKEY";

    /// <summary>
    /// Returns the name of the first field in <paramref name="candidates"/> that exists in the
    /// entry with a non-empty value, or null if none does. Candidates are given in priority order.
    /// </summary>
    private static string? FindField(PwEntry entry, params string[] candidates)
    {
      foreach (var key in candidates)
      {
        if (!string.IsNullOrEmpty(entry.Strings.ReadSafe(key)))
          return key;
      }
      return null;
    }

    /// <summary>
    /// Name of the field holding the credential ID. Same precedence as KeePassXC:
    /// KPEX_PASSKEY_GENERATED_USER_ID (StrongBox) first, then KPEX_PASSKEY_CREDENTIAL_ID.
    /// </summary>
    public static string? GetCredentialIdField(PwEntry entry) =>
      FindField(entry, FIELD_GENERATED_USER_ID, FIELD_CREDENTIAL_ID);

    /// <summary>
    /// Name of the field holding the passkey username. Same precedence as KeePassXC:
    /// KPXC_PASSKEY_USERNAME (StrongBox) first, then KPEX_PASSKEY_USERNAME.
    /// </summary>
    public static string? GetUsernameField(PwEntry entry) =>
      FindField(entry, FIELD_KPXC_USERNAME, FIELD_USERNAME);

    /// <summary>Credential ID of the passkey in the entry (base64url), or "" if none.</summary>
    public static string GetCredentialId(PwEntry entry)
    {
      var key = GetCredentialIdField(entry);
      return key == null ? "" : entry.Strings.ReadSafe(key);
    }

    /// <summary>
    /// Passkey username (account name at the RP), or "" if none. Does not fall back to the
    /// standard UserName field; callers decide about that.
    /// </summary>
    public static string GetUsername(PwEntry entry)
    {
      var key = GetUsernameField(entry);
      return key == null ? "" : entry.Strings.ReadSafe(key);
    }

    /// <summary>
    /// Retrieve passkey data from a KeePass entry
    /// </summary>
    public static PasskeyData? RetrievePasskey(PwEntry entry)
    {
      if (entry == null)
        return null;

      var privateKeyPem = entry.Strings.ReadSafe(FIELD_PRIVATE_KEY);
      var credentialId = GetCredentialId(entry);
      var userHandle = entry.Strings.ReadSafe(FIELD_USER_HANDLE);
      var relyingParty = entry.Strings.ReadSafe(FIELD_RELYING_PARTY);

      // Required for signing an assertion. (KeePassXC only knows the KPEX names for these.)
      if (string.IsNullOrEmpty(privateKeyPem) ||
          string.IsNullOrEmpty(credentialId) ||
          string.IsNullOrEmpty(userHandle) ||
          string.IsNullOrEmpty(relyingParty))
      {
        return null;
      }

      // The username is informational only (KeePassXC does not require it either), so fall back
      // to the entry's standard UserName field instead of rejecting the passkey.
      var username = GetUsername(entry);
      if (string.IsNullOrEmpty(username))
        username = entry.Strings.ReadSafe(PwDefs.UserNameField);

      var passkey = new PasskeyData
      {
        Username = username,
        PrivateKeyPem = privateKeyPem,
        CredentialId = credentialId,
        UserHandle = userHandle,
        RelyingParty = relyingParty
      };

      // Parse optional boolean fields ("1" or "true", like KeePassXC)
      var backupEligibleStr = entry.Strings.ReadSafe(FIELD_FLAG_BE);
      if (!string.IsNullOrEmpty(backupEligibleStr))
        passkey.BackupEligibility = ParseBool(backupEligibleStr);

      var backupStateStr = entry.Strings.ReadSafe(FIELD_FLAG_BS);
      if (!string.IsNullOrEmpty(backupStateStr))
        passkey.BackupState = ParseBool(backupStateStr);

      return passkey;
    }

    private static bool IsPasskeyField(string key) =>
      key.StartsWith(PASSKEY_FIELD_PREFIX, StringComparison.Ordinal) ||
      key == FIELD_KPXC_USERNAME;

    private static bool ParseBool(string value) =>
      value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Check if an entry contains passkey data
    /// </summary>
    public static bool HasPasskey(PwEntry entry)
    {
      // Like KeePassXC's EntryAttributes::hasPasskey (any KPEX_PASSKEY* attribute), plus the
      // StrongBox-only KPXC_PASSKEY_USERNAME and Kp2a's Passkey tag.
      return entry != null &&
             (entry.Tags.Contains(PASSKEY_TAG) ||
              entry.Strings.GetKeys().Any(IsPasskeyField));
    }

    /// <summary>
    /// Remove passkey data from an entry
    /// </summary>
    public static void RemovePasskey(PwEntry entry)
    {
      if (entry == null)
        return;

      // Remove tag
      if (entry.Tags.Contains(PASSKEY_TAG))
      {
        var tags = new List<string>(entry.Tags);
        tags.Remove(PASSKEY_TAG);
        entry.Tags = tags;
      }

      // Remove all passkey fields (every KPEX_PASSKEY* field incl. StrongBox names, and
      // KPXC_PASSKEY_USERNAME), like KeePassXC's removePasskeyAttributes
      foreach (var key in entry.Strings.GetKeys().Where(IsPasskeyField).ToList())
        entry.Strings.Remove(key);
    }
  }
}
