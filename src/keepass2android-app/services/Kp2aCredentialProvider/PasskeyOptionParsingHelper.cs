// This file is part of Keepass2Android, Copyright 2026 Philipp Crocoll.
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

using Android.Content.PM;
using Android.Runtime;
using Android.Util;
using AndroidX.Credentials;
using AndroidX.Credentials.Provider;

namespace keepass2android.services.Kp2aCredentialProvider
{
  public static class PasskeyOptionParsingHelper
  {
    /// <summary>
    /// Extracts the clientDataHash from a GetPublicKeyCredentialOption via JNI (there is no C# binding).
    /// Returns null if not available.
    /// </summary>
    public static byte[]? ExtractClientDataHashFromOption(GetPublicKeyCredentialOption passkeyOption)
    {
      // There is no C# binding available at present. Call Java method directly via JNI
      try
      {
        var handle = passkeyOption.Handle;
        if (handle != IntPtr.Zero)
        {
          var classHandle = passkeyOption.Class.Handle;
          var methodId = JNIEnv.GetMethodID(
            classHandle,
            "getClientDataHash",
            "()[B"
          );

          if (methodId != IntPtr.Zero)
          {
            IntPtr resultPtr = JNIEnv.CallObjectMethod(handle, methodId);
            if (resultPtr != IntPtr.Zero)
            {
              try
              {
                // Convert Java byte[] to C# byte[] using JNIEnv.GetArray
                var javaArray = JNIEnv.GetArray<byte>(resultPtr);
                if (javaArray is { Length: > 0 })
                {
                  var clientDataHash = new byte[javaArray.Length];
                  Array.Copy(javaArray, clientDataHash, javaArray.Length);
                  return clientDataHash;
                }
              }
              finally
              {
                JNIEnv.DeleteLocalRef(resultPtr);
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        Kp2aLog.Log($"JNI getClientDataHash() failed: {ex.Message}");
      }
      return null;
    }

    private const string AppKeyHashStringPrefix = "android:apk-key-hash:";

    /// <summary>
    /// Returns true if origin is an android:apk-key-hash origin with an actual hash
    /// (i.e. not the bare prefix returned by GetOriginForCallingApp on failure).
    /// </summary>
    public static bool IsValidAppOrigin(string? origin)
    {
      return origin != null
        && origin.StartsWith(AppKeyHashStringPrefix, StringComparison.Ordinal)
        && origin.Length > AppKeyHashStringPrefix.Length;
    }

    /// <summary>
    /// Get the origin for a calling app in the format: android:apk-key-hash:base64-sha256
    /// </summary>
    public static string GetOriginForCallingApp(CallingAppInfo? callingAppInfo)
    {

      if (callingAppInfo == null || string.IsNullOrEmpty(callingAppInfo.PackageName))
      {
        return AppKeyHashStringPrefix;
      }

      try
      {
        // Use the SigningInfo the framework already attached to CallingAppInfo
        var signers = callingAppInfo.SigningInfo?.GetApkContentsSigners();
        if (signers == null || signers.Length == 0)
        {
          Kp2aLog.Log($"GetOriginForCallingApp: no signers in CallingAppInfo.SigningInfo for {callingAppInfo.PackageName}");
          return AppKeyHashStringPrefix;
        }

        // signers[0].ToByteArray() returns the raw DER-encoded X.509 certificate bytes
        var certDer = signers[0].ToByteArray();
        var hash = KeePassLib.Cryptography.CryptoUtil.HashSha256(certDer);
        var base64Hash = Base64.EncodeToString(
          hash,
          Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap
        );

        var origin = $"{AppKeyHashStringPrefix}{base64Hash}";
        Kp2aLog.Log($"GetOriginForCallingApp: {callingAppInfo.PackageName} -> {origin}");
        return origin;
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"GetOriginForCallingApp: error for {callingAppInfo.PackageName}: {e}");
        return AppKeyHashStringPrefix;
      }
    }
  }

}