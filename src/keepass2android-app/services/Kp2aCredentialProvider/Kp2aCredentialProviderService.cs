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

using System.Runtime.Versioning;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Credentials.Exceptions;
using AndroidX.Credentials.Provider;
using Java.Interop;
using Java.Time;
using Keepass2android.Pluginsdk;
using KeePassLib;

namespace keepass2android.services.Kp2aCredentialProvider
{
  [Service(
    Enabled = true,
    Exported = true,
    Permission = "android.permission.BIND_CREDENTIAL_PROVIDER_SERVICE"
  )]
  [IntentFilter(actions: ["android.service.credentials.CredentialProviderService"])]
  [MetaData(name: "android.credentials.provider", Resource = "@xml/credentials_provider")]
  [SupportedOSPlatform("android31.0")]
  public class Kp2aCredentialProviderService : CredentialProviderService
  {
    public override void OnBeginCreateCredentialRequest(
      BeginCreateCredentialRequest request,
      CancellationSignal cancellationSignal,
      IOutcomeReceiver callback
    )
    {
      Kp2aLog.Log("Kp2aCredentialProviderService:onBeginCreateCredentialRequest called");
      if (request is BeginCreatePasswordCredentialRequest)
      {
        var currentDb = App.Kp2a.CurrentDb;
        var accountName = currentDb?.KpDatabase?.Name ?? CreateDb.DefaultDbName;
        var blendMode = BlendMode.Dst;
        var icon =
          blendMode == null
            ? null
            : Icon.CreateWithResource(this, AppNames.LauncherIcon)?.SetTintBlendMode(blendMode);

        callback.OnResult(
          new BeginCreateCredentialResponse.Builder()
            .AddCreateEntry(
              new CreateEntry.Builder(
                accountName,
                PendingIntentCompat.GetActivity(
                  this,
                  App.Kp2a.RequestCodeForCredentialProvider,
                  new Intent(this, typeof(Kp2aCredentialLauncherActivity)).PutExtra(
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeCreatePassword
                  ),
                  (int)PendingIntentFlags.UpdateCurrent,
                  true
                )!
              )
                .SetIcon(icon)
                .SetDescription(
                  GetString(Resource.String.credential_provider_password_creation_description)
                )
                // Set the last used time to "now"
                // so the active account is the default option in the system prompt.
                .SetLastUsedTime(currentDb == null ? null : Instant.Now())
                .Build()
            )
            .Build()
        );
      }
      else
      {
        callback.OnError(new CreateCredentialUnsupportedException().JavaCast<Java.Lang.Object>());
      }
    }

    public override void OnBeginGetCredentialRequest(
      BeginGetCredentialRequest request,
      CancellationSignal cancellationSignal,
      IOutcomeReceiver callback
    )
    {
      Kp2aLog.Log("Kp2aCredentialProviderService:OnBeginGetCredentialRequest called");

      var callingPackage = request.CallingAppInfo?.PackageName;
      if (callingPackage == null)
      {
        callback.OnError(new NoCredentialException().JavaCast<Java.Lang.Object>());
      }

      var appLocked = !App.Kp2a.DatabaseIsUnlocked;
      var responseBuilder = new BeginGetCredentialResponse.Builder();

      // Note that if your credentials are locked, you can immediately set an AuthenticationAction on the response and invoke the callback.
      if (appLocked)
      {
        callback.OnResult(
          responseBuilder
            .AddAuthenticationAction(
              new AuthenticationAction(
                // Providers that require unlocking the credentials before returning any credentialEntries,
                // must set up a pending intent that navigates the user to the app's unlock flow.
                GetString(AppNames.AppNameResource),
                PendingIntentCompat.GetActivity(
                  this,
                  App.Kp2a.RequestCodeForCredentialProvider,
                  new Intent(this, typeof(Kp2aCredentialLauncherActivity)).PutExtra(
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeUnlock
                  ),
                  (int)PendingIntentFlags.UpdateCurrent,
                  true
                )!
              )
            )
            .Build()
        );
        return;
      }

      foreach (var option in request.BeginGetCredentialOptions)
      {
        if (option is BeginGetPasswordOption)
        {
          var query = $"{KeePass.AndroidAppScheme}{callingPackage}";
          var foundEntries = ShareUrlResults.GetSearchResultsForUrl(query)?.Entries ?? [];

          var lastOpenedEntry = App.Kp2a.LastOpenedEntry;
          if (lastOpenedEntry != null && lastOpenedEntry?.SearchUrl == query)
          {
            foundEntries.Clear();
            foundEntries.Add(lastOpenedEntry.Entry);
          }

          foreach (var entry in foundEntries)
          {
            var username = entry.Strings.ReadSafe(PwDefs.UserNameField);
            if (string.IsNullOrEmpty(username))
            {
              continue;
            }

            responseBuilder.AddCredentialEntry(
              new PasswordCredentialEntry.Builder(
                this,
                username,
                PendingIntentCompat.GetActivity(
                  this,
                  App.Kp2a.RequestCodeForCredentialProvider,
                  new Intent(this, typeof(Kp2aCredentialLauncherActivity))
                    .PutExtra(
                      Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
                      Kp2aCredentialLauncherActivity.CredentialRequestTypeGetPasswordForEntry
                    )
                    .PutExtra(Strings.ExtraEntryId, entry.Uuid.ToHexString()),
                  (int)PendingIntentFlags.UpdateCurrent,
                  true
                )!,
                option.JavaCast<BeginGetPasswordOption>()
              )
                .SetDisplayName(entry.Strings.ReadSafe(PwDefs.TitleField))
                .SetAffiliatedDomain(entry.ParentGroup?.Name)
                .SetLastUsedTime(
                  Instant.OfEpochMilli(
                    new DateTimeOffset(entry.LastAccessTime).ToUnixTimeMilliseconds()
                  )
                )
                //TODO SetIcon() of the entry
                .Build()
            );
          }
        }
      }

      responseBuilder.AddAction(
        new AndroidX.Credentials.Provider.Action(
          GetString(Resource.String.open_app_name, GetString(AppNames.AppNameResource)!),
          PendingIntentCompat.GetActivity(
            this,
            App.Kp2a.RequestCodeForCredentialProvider,
            new Intent(this, typeof(KeePass)),
            (int)PendingIntentFlags.UpdateCurrent,
            true
          )!,
          GetString(Resource.String.manage_credentials)
        )
      );

      callback.OnResult(responseBuilder.Build());
    }

    public override void OnClearCredentialStateRequest(
      ProviderClearCredentialStateRequest request,
      CancellationSignal cancellationSignal,
      IOutcomeReceiver callback
    )
    {
      Kp2aLog.Log("Kp2aCredentialProviderService:OnClearCredentialStateRequest called");
      //no-op
    }
  }
}
