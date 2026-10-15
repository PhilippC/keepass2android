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
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using AndroidX.Core.App;
using AndroidX.Credentials.Exceptions;
using AndroidX.Credentials.Provider;
using Java.Interop;
using Java.Time;
using Keepass2android.Pluginsdk;
using KeePassLib;
using Kp2aPasskey.Core;

namespace keepass2android.services.Kp2aCredentialProvider
{
  [Service(
    Enabled = true,
    Exported = true,
    Permission = "android.permission.BIND_CREDENTIAL_PROVIDER_SERVICE"
  )]
  [IntentFilter(actions: ["android.service.credentials.CredentialProviderService"])]
  [MetaData(name: "android.credentials.provider", Resource = "@xml/credentials_provider")]
  [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
  public class Kp2aCredentialProviderService : CredentialProviderService
  {
    private const string TAG = "Kp2aCredentialProviderService";
    private Icon? _defaultIcon;
    private bool _isAutoSelectAllowed = false;

    private Icon DefaultIcon
    {
      get
      {
        if (_defaultIcon == null)
        {
          _defaultIcon = Icon.CreateWithResource(this, AppNames.LauncherIcon);
          _defaultIcon?.SetTintBlendMode(BlendMode.Dst);
        }
        return _defaultIcon!;
      }
    }

    public override void OnBeginCreateCredentialRequest(
      BeginCreateCredentialRequest request,
      CancellationSignal cancellationSignal,
      IOutcomeReceiver callback
    )
    {
      try
      {
        if (request is BeginCreatePasswordCredentialRequest)
        {
          HandleCreatePasswordRequest(callback);

        }
        else if (request is BeginCreatePublicKeyCredentialRequest passkeyRequest)
        {
          HandleCreatePasskeyRequest(passkeyRequest, callback);
        }
        else
        {
          callback.OnError(new CreateCredentialUnsupportedException().JavaCast<Java.Lang.Object>());
        }
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialProviderService: onBeginCreateCredentialRequest error. {e.Message}");
        callback.OnError(new CreateCredentialUnknownException(e.Message).JavaCast<Java.Lang.Object>());
      }
    }

    private void HandleCreatePasswordRequest(IOutcomeReceiver callback)
    {
      var currentDb = App.Kp2a.CurrentDb;
      var accountName = currentDb?.KpDatabase?.Name ?? CreateDb.DefaultDbName;

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
              .SetIcon(DefaultIcon)
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

    private void HandleCreatePasskeyRequest(
      BeginCreatePublicKeyCredentialRequest request,
      IOutcomeReceiver callback
    )
    {
      var currentDb = App.Kp2a.CurrentDb;
      var databaseName = currentDb?.KpDatabase?.Name;
      var accountName = string.IsNullOrWhiteSpace(databaseName)
        ? GetString(AppNames.AppNameResource)
        : databaseName;

      var createEntries = new List<CreateEntry>();

      try
      {
        var creationOptions = new PublicKeyCredentialCreationOptions(
          request.RequestJson!,
          null // ClientDataHash is not available in BeginCreatePublicKeyCredentialRequest
        );
        var relyingPartyId = creationOptions.RelyingPartyEntity.Id;


        // Search for existing passkeys with this relying party

        if (!App.Kp2a.DatabaseIsUnlocked)
        {
          // Database is locked, show unlock prompt
          createEntries.Add(
            new CreateEntry.Builder(
              accountName,
              PendingIntentCompat.GetActivity(
                this,
                App.Kp2a.RequestCodeForCredentialProvider,
                new Intent(this, typeof(Kp2aCredentialLauncherActivity))
                  .PutExtra(
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeCreatePasskey
                  )
                  .PutExtra(Kp2aCredentialLauncherActivity.ExtraRelyingPartyId, relyingPartyId)
                  .PutExtra(Kp2aCredentialLauncherActivity.ExtraRequestJson, request.RequestJson),
                (int)PendingIntentFlags.UpdateCurrent,
                true
              )!
            )
              .SetIcon(DefaultIcon)
              .SetDescription(GetString(Resource.String.credential_provider_locked_database_description))
              .Build()
          );
        }
        else if (currentDb?.CanWrite == false)
        {
          // Database is read-only, cannot create passkeys
          throw new Exception("Cannot register passkey in read-only database");
        }
        else
        {
          // Database is open and writable, show create entry
          createEntries.Add(
            new CreateEntry.Builder(
              accountName,
              PendingIntentCompat.GetActivity(
                this,
                App.Kp2a.RequestCodeForCredentialProvider,
                new Intent(this, typeof(Kp2aCredentialLauncherActivity))
                  .PutExtra(
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeCreatePasskey
                  )
                  .PutExtra(Kp2aCredentialLauncherActivity.ExtraRelyingPartyId, relyingPartyId)
                  .PutExtra(Kp2aCredentialLauncherActivity.ExtraRequestJson, request.RequestJson),
                (int)PendingIntentFlags.UpdateCurrent,
                true
              )!
            )
              .SetIcon(DefaultIcon)
              .SetDescription(GetString(Resource.String.credential_provider_passkey_creation_description))
              .Build()
          );
        }

        var responseBuilder = new BeginCreateCredentialResponse.Builder();
        foreach (var entry in createEntries)
        {
          responseBuilder.AddCreateEntry(entry);
        }
        callback.OnResult(responseBuilder.Build());
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialProviderService: Error handling passkey creation request. {e.Message}");
        callback.OnError(new CreateCredentialUnknownException(e.Message).JavaCast<Java.Lang.Object>());
      }
    }

    public override void OnBeginGetCredentialRequest(
      BeginGetCredentialRequest request,
      CancellationSignal cancellationSignal,
      IOutcomeReceiver callback
    )
    {
      try
      {
        var appLocked = !App.Kp2a.DatabaseIsUnlocked;
        var responseBuilder = new BeginGetCredentialResponse.Builder();

        if (appLocked)
        {
          callback.OnResult(
            responseBuilder
              .AddAuthenticationAction(
                GetCredentialHelper.CreateAuthenticationAction(this)
              )
              .Build()
          );
          return;
        }

        var hasKnownOption = false;
        foreach (var option in request.BeginGetCredentialOptions)
        {
          if (option is BeginGetPasswordOption passwordOption)
          {
            hasKnownOption = true;
            var callingPackage = request.CallingAppInfo?.PackageName;
            if (callingPackage == null)
            {
              callback.OnError(new NoCredentialException().JavaCast<Java.Lang.Object>());
              return;
            }
            GetCredentialHelper.AddMatchingPasswordEntries(this, callingPackage, passwordOption, responseBuilder);
          }
          else if (option is BeginGetPublicKeyCredentialOption passkeyOption)
          {
            hasKnownOption = true;
            GetCredentialHelper.AddMatchingPasskeyEntries(this, passkeyOption, responseBuilder, _isAutoSelectAllowed);
          }
        }

        if (!hasKnownOption)
        {
          throw new Exception("Unknown type of beginGetCredentialOption");
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
      catch (Exception e)
      {
        Kp2aLog.Log($"Kp2aCredentialProviderService: onBeginGetCredentialRequest error. {e.Message}");
        callback.OnError(new GetCredentialUnknownException().JavaCast<Java.Lang.Object>());
      }
    }


    public override void OnClearCredentialStateRequest(
      ProviderClearCredentialStateRequest request,
      CancellationSignal cancellationSignal,
      IOutcomeReceiver callback
    )
    {
      // ignored
    }
  }
}
