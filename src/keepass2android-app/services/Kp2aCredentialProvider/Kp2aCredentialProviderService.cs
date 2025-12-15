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
using AndroidX.Credentials.Exceptions;
using AndroidX.Credentials.Provider;
using Java.Interop;
using Java.Util.Concurrent.Atomic;

namespace keepass2android.services.Kp2aCredentialProvider
{
  [Service(Enabled = true, Exported = true, Permission = "android.permission.BIND_CREDENTIAL_PROVIDER_SERVICE")]
  [IntentFilter(actions: ["android.service.credentials.CredentialProviderService"])]
  [MetaData(name: "android.credentials.provider", Resource = "@xml/credentials_provider")]
  [SupportedOSPlatform("android31.0")]
  public class Kp2aCredentialProviderService : CredentialProviderService
  {
    private readonly AtomicInteger requestCode = new();
    public override void OnBeginCreateCredentialRequest(BeginCreateCredentialRequest request, CancellationSignal cancellationSignal, IOutcomeReceiver callback)
    {
      Kp2aLog.Log("Kp2aCredentialProviderService:onBeginCreateCredentialRequest called");
      if (request is BeginCreatePasswordCredentialRequest)
      {
        var currentDb = App.Kp2a.CurrentDb;
        var accountName = currentDb?.KpDatabase?.Name ?? CreateDb.DefaultDbName;
        var blendMode = BlendMode.Dst;
        var icon = blendMode == null
        ? null
        : Icon.CreateWithResource(this, AppNames.LauncherIcon)?.SetTintBlendMode(blendMode);

        callback.OnResult(
              new BeginCreateCredentialResponse.Builder()
            .AddCreateEntry(
           new CreateEntry
            .Builder(
               accountName,
                PendingIntent.GetActivity(
                  ApplicationContext,
                  requestCode.IncrementAndGet(),
                  new Intent(this, typeof(Kp2aCredentialLauncherActivity))
                   .PutExtra(
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeKey,
                    Kp2aCredentialLauncherActivity.CredentialRequestTypeCreatePassword
                  ),
                  PendingIntentFlags.Mutable | PendingIntentFlags.UpdateCurrent
                )!
            )
            .SetIcon(icon)
            .SetDescription(
                GetString(Resource.String.credential_provider_password_creation_description)
            )
            // Set the last used time to "now"
            // so the active account is the default option in the system prompt.
            .SetLastUsedTime(currentDb == null ? null : Java.Time.Instant.Now())
            .Build()
            ).Build()
        );
      }
      else
      {
        callback.OnError(new CreateCredentialUnsupportedException()
           .JavaCast<Java.Lang.Object>());
      }
    }

    public override void OnBeginGetCredentialRequest(BeginGetCredentialRequest request, CancellationSignal cancellationSignal, IOutcomeReceiver callback)
    {
      //TODO implement Kp2aCredentialProviderService:OnBeginGetCredentialRequest
    }

    public override void OnClearCredentialStateRequest(ProviderClearCredentialStateRequest request, CancellationSignal cancellationSignal, IOutcomeReceiver callback)
    {
      //TODO implement Kp2aCredentialProviderService:OnClearCredentialStateRequest
    }
  }
}