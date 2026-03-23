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
        var packageManager = Application.Context.PackageManager;

        var packageInfo = packageManager?.GetPackageInfo(
          callingAppInfo.PackageName,
          PackageInfoFlags.Signatures
        );

        if (packageInfo?.Signatures == null || packageInfo.Signatures.Count == 0)
        {
          return AppKeyHashStringPrefix;
        }

        // Get the first signature and extract the X.509 certificate
        var signature = packageInfo.Signatures[0];

        // Parse the X.509 certificate from the signature (same as KeePassDX)
        var certFactory = Java.Security.Cert.CertificateFactory.GetInstance("X.509");
        var signatureBytes = signature.ToByteArray();
        using var memStream = new MemoryStream(signatureBytes);
        var x509Cert = (Java.Security.Cert.X509Certificate)certFactory.GenerateCertificate(memStream);

        // Hash the DER-encoded certificate (not the signature!)
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var certEncoded = x509Cert?.GetEncoded();
        if (certEncoded == null)
        {
          return AppKeyHashStringPrefix;
        }
        var hash = sha256.ComputeHash(certEncoded);
        var base64Hash = Base64.EncodeToString(
          hash,
          Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap
        );

        return $"{AppKeyHashStringPrefix}{base64Hash}";
      }
      catch (Exception e)
      {
        Kp2aLog.Log($"Error getting origin for {callingAppInfo.PackageName}: {e.Message}");
        return AppKeyHashStringPrefix;
      }
    }
  }
}