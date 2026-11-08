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
using Android.Preferences;

namespace keepass2android
{
  /// <summary>
  /// Helper class for reading passkey-related preferences
  /// Based on KeePassDX PreferencesUtil pattern
  /// </summary>
  public static class PasskeyPreferences
  {
    /// <summary>
    /// Backup Eligibility (BE) flag used for passkey entries that have no stored KPEX_PASSKEY_FLAG_BE field
    /// (e.g. created by older versions or other apps). WebAuthn requires BE to stay constant over a credential's
    /// lifetime, so this must NOT depend on a user-changeable preference. Matches KeePassXC's DEFAULT_BE_FLAG.
    /// </summary>
    public const bool BackupEligibilityForEntriesWithoutFlags = true;

    /// <summary>
    /// Backup State (BS) flag used for passkey entries that have no stored KPEX_PASSKEY_FLAG_BS field.
    /// Matches KeePassXC's DEFAULT_BS_FLAG. Callers must still force BS to false if BE is false.
    /// </summary>
    public const bool BackupStateForEntriesWithoutFlags = true;

    /// <summary>
    /// Get the backup eligibility preference value.
    /// Determines whether new passkeys should be marked as eligible for backup.
    /// Only use this when creating a passkey; the value is stored in the entry at creation time.
    /// </summary>
    public static bool GetBackupEligibility(Context context)
    {
      var prefs = PreferenceManager.GetDefaultSharedPreferences(context);
      return prefs.GetBoolean(
        context.GetString(Resource.String.passkeys_backup_eligibility_key),
        context.Resources.GetBoolean(Resource.Boolean.passkeys_backup_eligibility_default)
      );
    }

    /// <summary>
    /// Get the backup state preference value.
    /// Determines whether new passkeys should be marked as currently backed up.
    /// Only use this when creating a passkey; the value is stored in the entry at creation time.
    /// Note: This only returns true if backup eligibility is also enabled.
    /// </summary>
    public static bool GetBackupState(Context context)
    {
      // If backup eligibility is disabled, backup state must be false
      if (!GetBackupEligibility(context))
        return false;

      var prefs = PreferenceManager.GetDefaultSharedPreferences(context);
      return prefs.GetBoolean(
        context.GetString(Resource.String.passkeys_backup_state_key),
        context.Resources.GetBoolean(Resource.Boolean.passkeys_backup_state_default)
      );
    }

    /// <summary>
    /// When true, treat WebAuthn "preferred" user verification as "required"
    /// (show device credential/biometric prompt before using or creating a passkey).
    /// </summary>
    public static bool GetForceUserVerificationWhenPreferred(Context context)
    {
      var prefs = PreferenceManager.GetDefaultSharedPreferences(context);
      return prefs.GetBoolean(
        context.GetString(Resource.String.passkeys_force_user_verification_when_preferred_key),
        context.Resources.GetBoolean(Resource.Boolean.passkeys_force_user_verification_when_preferred_default)
      );
    }
  }
}
