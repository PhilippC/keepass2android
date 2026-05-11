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

using Android.Content;
using Android.OS;
using AndroidX.Biometric;
using AndroidX.Fragment.App;
using Java.Util.Concurrent;
using Keepass2android;

namespace keepass2android.services.Kp2aCredentialProvider
{
  /// <summary>
  /// Shows a device credential / biometric prompt for WebAuthn User Verification.
  /// No database unlock fallback — only system biometric or device PIN/pattern/password.
  /// </summary>
  public static class UserVerificationHelper
  {
    /// <summary>
    /// Returns true if the device can show biometric or device credential authentication.
    /// </summary>
    public static bool CanAuthenticate(Context context)
    {
      var biometricManager = BiometricManager.From(context);
      var result = biometricManager.CanAuthenticate(BiometricManager.Authenticators.BiometricWeak | BiometricManager.Authenticators.DeviceCredential);
      return result == BiometricManager.BiometricSuccess;
    }

    /// <summary>
    /// Shows the user verification prompt (biometric or device credential).
    /// On success, <paramref name="onSuccess"/> is invoked on the main thread and then the activity may continue.
    /// On cancel or error, <paramref name="onCancelOrError"/> is invoked and the caller should finish with failure/cancel.
    /// </summary>
    /// <param name="activity">Must be a FragmentActivity (e.g. AppCompatActivity).</param>
    /// <param name="title">Dialog title (e.g. "User verification required").</param>
    /// <param name="subtitle">Optional subtitle (e.g. origin or app name).</param>
    /// <param name="onSuccess">Called when authentication succeeds.</param>
    /// <param name="onCancelOrError">Called when user cancels or authentication fails.</param>
    public static void ShowUserVerification(
      FragmentActivity activity,
      string title,
      string? subtitle,
      Action onSuccess,
      Action? onCancelOrError)
    {
      var executor = Executors.NewSingleThreadExecutor();
      var callback = new BiometricCallback(onSuccess, onCancelOrError);

      var promptInfo = new BiometricPrompt.PromptInfo.Builder()
        .SetTitle(title)
        .SetSubtitle(subtitle ?? "")
        .SetConfirmationRequired(false)
        .SetDeviceCredentialAllowed(true)
        .Build();

      var biometricPrompt = new BiometricPrompt(activity, executor, callback);
      biometricPrompt.Authenticate(promptInfo);
    }

    private sealed class BiometricCallback : BiometricPrompt.AuthenticationCallback
    {
      private readonly Action _onSuccess;
      private readonly Action? _onCancelOrError;

      public BiometricCallback(Action onSuccess, Action? onCancelOrError)
      {
        _onSuccess = onSuccess;
        _onCancelOrError = onCancelOrError;
      }

      public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
      {
        base.OnAuthenticationSucceeded(result);
        RunOnMain(() => _onSuccess());
      }

      public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence? errString)
      {
        base.OnAuthenticationError(errorCode, errString);
        if (errorCode != BiometricPrompt.ErrorCanceled &&
            errorCode != BiometricPrompt.ErrorNegativeButton &&
            errorCode != BiometricPrompt.ErrorUserCanceled)
        {
          Kp2aLog.Log("UserVerificationHelper: authentication error " + errorCode + " " + errString);
        }
        RunOnMain(() => _onCancelOrError?.Invoke());
      }

      public override void OnAuthenticationFailed()
      {
        // Single attempt failed (e.g. wrong fingerprint) — the prompt stays open for retry.
        // Do nothing here; OnAuthenticationError handles final cancellation/lockout.
        base.OnAuthenticationFailed();
      }

      private static void RunOnMain(Action action)
      {
        var handler = new Handler(Looper.MainLooper!);
        handler.Post(action);
      }
    }
  }
}
