// The privileged allowlist approach is derived from KeePassDX (https://github.com/Kunzisoft/KeePassDX),
// see PrivilegedAllowLists.kt. Original work Copyright 2025 Jeremy Jamet / Kunzisoft.
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
using AndroidX.Credentials.Provider;
using Kp2aPasskey.Core;

namespace keepass2android.services.Kp2aCredentialProvider
{
  /// <summary>
  /// Origin and clientDataHash to use for a passkey response, after verifying the caller.
  /// </summary>
  /// <param name="Origin">The verified origin: either the web origin reported by a verified privileged browser,
  /// or android:apk-key-hash:... for native apps.</param>
  /// <param name="ClientDataHash">The caller-supplied clientDataHash. Only non-null for verified privileged callers.</param>
  public record PasskeyCallerOrigin(string Origin, byte[]? ClientDataHash);

  /// <summary>
  /// Determines the origin of a passkey request in a secure way.
  /// Browsers are only trusted to provide a web origin (and a pre-computed clientDataHash) if their package name
  /// AND signing certificate match one of the privileged apps allowlists (verified by CallingAppInfo.GetOrigin).
  /// All other callers are treated as native apps, identified by the hash of their signing certificate.
  /// The origin is never derived from the (caller-controlled) relying party id.
  /// </summary>
  public static class PasskeyOriginHelper
  {
    /// <summary>
    /// Returns the web origin reported by the caller if (and only if) the caller is a privileged app, i.e. a browser
    /// whose package name and signing certificate are contained in one of the allowlists (bundled or custom,
    /// see <see cref="PrivilegedAppsAllowlist"/>). Returns null otherwise.
    /// Note: Only the caller's identity is verified; the origin itself is trusted because a privileged browser reported it.
    /// </summary>
    public static string? GetOriginFromPrivilegedCaller(Context context, CallingAppInfo? callingAppInfo)
    {
      if (callingAppInfo == null)
        return null;

      foreach (var allowlist in PrivilegedAppsAllowlist.GetAllAllowlists(context))
      {
        try
        {
          // GetOrigin verifies package name + signing certificate fingerprint against the allowlist.
          // It returns null (or throws) if the caller is not privileged or did not provide an origin.
          var origin = callingAppInfo.GetOrigin(allowlist);
          if (!string.IsNullOrEmpty(origin))
          {
            // WebAuthn origins must not have a trailing slash
            return origin.TrimEnd('/');
          }
        }
        catch (Exception e)
        {
          Kp2aLog.Log($"PasskeyOriginHelper: GetOrigin failed for {callingAppInfo.PackageName}: {e.Message}");
        }
      }
      return null;
    }

    /// <summary>
    /// Determines origin and clientDataHash for a passkey request.
    /// For verified privileged callers (browsers in the allowlist), the browser-provided origin and clientDataHash are used.
    /// For all other callers, the origin is android:apk-key-hash:... and any caller-supplied clientDataHash is ignored,
    /// so that the clientDataJSON is built by us.
    /// Throws if the origin of a native app cannot be determined.
    /// </summary>
    public static PasskeyCallerOrigin ResolveCallerOrigin(Context context, CallingAppInfo? callingAppInfo, byte[]? callerClientDataHash)
    {
      var originFromPrivilegedCaller = GetOriginFromPrivilegedCaller(context, callingAppInfo);
      if (originFromPrivilegedCaller != null)
      {
        Kp2aLog.Log($"PasskeyOriginHelper: verified privileged caller {callingAppInfo?.PackageName} with origin {originFromPrivilegedCaller}");
        return new PasskeyCallerOrigin(originFromPrivilegedCaller, callerClientDataHash);
      }

      if (callerClientDataHash != null)
      {
        Kp2aLog.Log($"PasskeyOriginHelper: ignoring clientDataHash supplied by non-privileged caller {callingAppInfo?.PackageName}");
      }

      var appOrigin = PasskeyOptionParsingHelper.GetOriginForCallingApp(callingAppInfo);
      if (!PasskeyOptionParsingHelper.IsValidAppOrigin(appOrigin))
      {
        throw new InvalidOperationException($"Unable to determine origin of calling app {callingAppInfo?.PackageName}");
      }
      return new PasskeyCallerOrigin(appOrigin, null);
    }

    /// <summary>
    /// Creates the client data response for a resolved caller origin.
    /// </summary>
    public static IClientDataResponse CreateClientDataResponse(
      PasskeyCallerOrigin callerOrigin,
      ClientDataBuildResponse.RequestType requestType,
      byte[] challenge)
    {
      if (callerOrigin.ClientDataHash != null)
      {
        // Verified privileged caller: it has built the clientDataJSON itself
        return new ClientDataDefinedResponse(callerOrigin.ClientDataHash);
      }

      if (string.IsNullOrEmpty(callerOrigin.Origin))
      {
        throw new InvalidOperationException("No origin available for passkey response");
      }

      return new ClientDataBuildResponse(requestType, challenge, callerOrigin.Origin);
    }
  }
}
