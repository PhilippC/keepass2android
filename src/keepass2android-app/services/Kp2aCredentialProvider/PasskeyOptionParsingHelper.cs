using Android.Content.PM;
using Android.Runtime;
using Android.Util;
using AndroidX.Credentials;
using AndroidX.Credentials.Provider;

namespace keepass2android.services.Kp2aCredentialProvider
{
  public class PasskeyOptionParsingHelper
  {
    /// <summary>
    /// Extract clientDataHash from GetPublicKeyCredentialOption
    /// Returns null if not available (non-privileged app)
    /// Tries multiple methods: JNI direct call, reflection, property access
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
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(certDer);
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