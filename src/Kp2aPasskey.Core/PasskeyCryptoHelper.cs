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

using System.Security.Cryptography;
using Android.Util;
using Java.Lang;
using Java.Security;
using Java.Security.Spec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using PeterO.Cbor;
using AndroidKeyPair = Java.Security.KeyPair;
using Exception = System.Exception;

namespace keepass2android.services.Kp2aCredentialProvider.Passkey
{
  /// <summary>
  /// Wrapper for BouncyCastle Ed25519 private key to implement Java IPrivateKey interface
  /// </summary>
  internal class Ed25519PrivateKeyWrapper(byte[] pkcs8EncodedKey) : Java.Lang.Object, IPrivateKey
  {
    public string Algorithm => "Ed25519";
    public string Format => "PKCS#8";
    public byte[] GetEncoded() => pkcs8EncodedKey;
  }

  /// <summary>
  /// Wrapper for BouncyCastle Ed25519 public key to implement Java IPublicKey interface
  /// </summary>
  public class Ed25519PublicKeyWrapper(byte[] x509EncodedKey) : Java.Lang.Object, IPublicKey
  {
    public string Algorithm => "Ed25519";
    public string Format => "X.509";
    public byte[] GetEncoded() => x509EncodedKey;
  }

  /// <summary>
  /// Cryptography helper for FIDO2/WebAuthn passkey operations
  /// </summary>
  public static class PasskeyCryptoHelper
  {

    // COSE Algorithm Identifiers (https://www.iana.org/assignments/cose/cose.xhtml)
    public const long CoseAlgEs256 = -7;   // ECDSA with SHA-256 (ES256_ALGORITHM)
    public const long CoseAlgRs256 = -257; // RSASSA-PKCS1-v1_5 with SHA-256 (RS256_ALGORITHM)
    public const long CoseAlgEd25519 = -8; // EdDSA with Ed25519 (ED_DSA_ALGORITHM)

    // COSE Key Common Parameter Labels (https://www.iana.org/assignments/cose/cose.xhtml#key-common-parameters)
    private const int CoseKeyKeytype = 1;   // Key Type
    private const int CoseKeyAlgorithm = 3;   // Key Algorithm
    private const int CoseKeyCurve = -1;  // Curve (EC2/OKP) or Modulus (RSA)
    private const int CoseKeyX = -2;    // X-coordinate (EC2/OKP) or Public Exponent (RSA)
    private const int CoseKeyY = -3;    // Y-coordinate (EC2)

    // COSE Key Type Values (https://www.iana.org/assignments/cose/cose.xhtml#key-type)
    private const int CoseKtyOkp = 1;   // Octet Key Pair (Ed25519)
    private const int CoseKtyEc2 = 2;   // Elliptic Curve Keys with x- and y-coordinate pair (P-256)
    private const int CoseKtyRsa = 3;   // RSA

    // COSE Elliptic Curves (https://www.iana.org/assignments/cose/cose.xhtml#elliptic-curves)
    private const int CoseCurveP256 = 1;     // NIST P-256 (secp256r1)
    private const int CoseCurveEd25519 = 6;  // Ed25519 for use with EdDSA

    private const int Rs256KeySizeInBits = 2048;

    /// <summary>
    /// Generate a key pair for passkey based on the supported algorithms
    /// </summary>
    /// <param name="supportedAlgorithms">List of COSE algorithm identifiers</param>
    /// <returns>Tuple of (KeyPair, algorithm ID) or null if no supported algorithm found</returns>
    public static (AndroidKeyPair keyPair, long algorithmId)? GenerateKeyPair(IEnumerable<long> supportedAlgorithms)
    {
      // IMPORTANT: Iterate through algorithms in the order provided by the relying party
      // This respects the RP's preference and matches the WebAuthn spec
      var algorithmIds = supportedAlgorithms.ToList();
      foreach (var algorithmId in algorithmIds)
      {
        switch (algorithmId)
        {
          case CoseAlgEd25519:
            {
              var keyPair = GenerateEd25519KeyPair();
              if (keyPair != null)
                return (keyPair, CoseAlgEd25519);
              break;
            }
          case CoseAlgEs256:
            {
              var keyPair = GenerateEs256KeyPair();
              if (keyPair != null)
                return (keyPair, CoseAlgEs256);
              break;
            }
          case CoseAlgRs256:
            {
              var keyPair = GenerateRs256KeyPair();
              if (keyPair != null)
                return (keyPair, CoseAlgRs256);
              break;
            }
          default:
            break; // Unsupported algorithm, try next one
        }
      }

      var errorMsg = $"No supported algorithm found. Requested: [{string.Join(", ", algorithmIds)}]. " +
        $"Supported: EdDSA(-8, may vary by device), ES256(-7), RS256(-257)";
      Log.Error("PasskeyCryptoHelper", errorMsg);
      return null;
    }

    /// <summary>
    /// Generate an EC (secp256r1 / P-256) key pair
    /// </summary>
    private static AndroidKeyPair? GenerateEs256KeyPair()
    {
      try
      {
        const string es256CurveName = "secp256r1";
        var spec = new ECGenParameterSpec(es256CurveName);
        var keyPairGenerator = KeyPairGenerator.GetInstance("EC");
        keyPairGenerator?.Initialize(spec);
        return keyPairGenerator?.GenerateKeyPair();
      }
      catch (Exception ex)
      {
        Log.Error("PasskeyCryptoHelper", "Failed to generate EC key pair", ex);
        return null;
      }
    }

    /// <summary>
    /// Generate an RSA key pair (2048 bits)
    /// </summary>
    private static AndroidKeyPair? GenerateRs256KeyPair()
    {
      try
      {
        var keyPairGenerator = KeyPairGenerator.GetInstance("RSA");
        keyPairGenerator?.Initialize(Rs256KeySizeInBits);
        return keyPairGenerator?.GenerateKeyPair();
      }
      catch (Exception ex)
      {
        Log.Error("PasskeyCryptoHelper", "Failed to generate RSA key pair", ex);
        return null;
      }
    }

    /// <summary>
    /// Generate an Ed25519 key pair
    /// Uses BouncyCastle C# API since Android's native Ed25519 requires Keystore
    /// </summary>
    private static AndroidKeyPair? GenerateEd25519KeyPair()
    {
      try
      {
        // Android's Ed25519 KeyPairGenerator requires Keystore, so we use BouncyCastle C# API
        var keyPairGenerator = new Ed25519KeyPairGenerator();
        keyPairGenerator.Init(new Ed25519KeyGenerationParameters(new Org.BouncyCastle.Security.SecureRandom()));

        var bcKeyPair = keyPairGenerator.GenerateKeyPair();

        // Convert BouncyCastle key pair to encoded bytes
        var privateKeyParams = (Ed25519PrivateKeyParameters)bcKeyPair.Private;
        var publicKeyParams = (Ed25519PublicKeyParameters)bcKeyPair.Public;

        // Get PKCS#8 encoded private key
        var privateKeyInfo = Org.BouncyCastle.Pkcs.PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKeyParams);
        var privateKeyBytes = privateKeyInfo.GetEncoded();

        // Get X.509 encoded public key
        var publicKeyInfo = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKeyParams);
        var publicKeyBytes = publicKeyInfo.GetEncoded();

        // Create wrapper keys that implement Java interfaces
        var privateKey = new Ed25519PrivateKeyWrapper(privateKeyBytes);
        var publicKey = new Ed25519PublicKeyWrapper(publicKeyBytes);

        var keyPair = new AndroidKeyPair(publicKey, privateKey);
        return keyPair;
      }
      catch (Exception ex)
      {
        Log.Error("PasskeyCryptoHelper",
          $"Ed25519 generation failed (Android {Android.OS.Build.VERSION.SdkInt}): {ex.GetType().Name}: {ex.Message}", ex);
        return null;
      }
    }

    /// <summary>
    /// Convert a private key to PEM format for storage (PKCS#8).
    /// Uses BouncyCastle PemWriter (PKCS#8 DER → base64 with PEM headers).
    /// </summary>
    public static string ConvertPrivateKeyToPem(IPrivateKey privateKey)
    {
      var encoded = privateKey?.GetEncoded()
        ?? throw new ArgumentException("Cannot encode private key");

      // Parse the PKCS#8 DER bytes into a BouncyCastle PrivateKeyInfo ASN.1 structure
      var privateKeyInfo = Org.BouncyCastle.Asn1.Pkcs.PrivateKeyInfo.GetInstance(encoded);

      // Let PemWriter handle headers, base64 and line-wrapping
      using var writer = new StringWriter();
      new PemWriter(writer).WriteObject(privateKeyInfo);
      return writer.ToString().Trim();

    }

    /// <summary>
    /// Sign data with a private key using BouncyCastle
    /// </summary>
    /// <param name="privateKeyPem">PEM-formatted private key</param>
    /// <param name="dataToSign">Data to sign</param>
    /// <returns>Signature bytes in DER format</returns>
    public static byte[] Sign(string privateKeyPem, byte[] dataToSign)
    {
      try
      {
        // Parse PEM once with BouncyCastle — covers PKCS#8, SEC1 (EC), PKCS#1 (RSA)
        AsymmetricKeyParameter privateKeyParams;
        using (var reader = new StringReader(privateKeyPem))
        {
          var pemObject = new PemReader(reader).ReadObject();
          privateKeyParams = pemObject switch
          {
            AsymmetricCipherKeyPair kp => kp.Private,
            AsymmetricKeyParameter kp => kp,
            _ => throw new ArgumentException($"Unsupported PEM object type: {pemObject?.GetType().Name}")
          };
        }

        // Determine signature algorithm directly from key type — no string matching on .Algorithm
        string algorithmSignature = privateKeyParams switch
        {
          ECPrivateKeyParameters => "SHA256withECDSA",
          RsaPrivateCrtKeyParameters => "SHA256withRSA",
          Ed25519PrivateKeyParameters => "Ed25519",
          _ => throw new SecurityException($"Unknown key type: {privateKeyParams.GetType().Name}")
        };

        // Sign using BouncyCastle C# API
        var signer = SignerUtilities.GetSigner(algorithmSignature);
        signer.Init(true, privateKeyParams);
        signer.BlockUpdate(dataToSign, 0, dataToSign.Length);
        var signatureBytes = signer.GenerateSignature();
        return signatureBytes;
      }
      catch (Exception ex)
      {
        Log.Error("PasskeyCryptoHelper", $"Failed to sign data: {ex.Message}");
        throw new Exception("Failed to sign data", ex);
      }
    }


    /// <summary>
    /// Convert a public key to CBOR format for COSE encoding
    /// </summary>
    public static CBORObject? ConvertPublicKeyToMap(IPublicKey publicKey, long keyTypeId)
    {
      if (publicKey == null)
        return null;

      var keyAlgorithm = publicKey.Algorithm;

      if (keyTypeId == CoseAlgEd25519 && (keyAlgorithm?.Equals("Ed25519", StringComparison.OrdinalIgnoreCase) == true ||
                                             keyAlgorithm?.Equals("EdDSA", StringComparison.OrdinalIgnoreCase) == true))
      {
        return ConvertEd25519PublicKeyToMap(publicKey);
      }
      else if (keyTypeId == CoseAlgEs256 && keyAlgorithm?.Equals("EC", StringComparison.OrdinalIgnoreCase) == true)
      {
        return ConvertEcPublicKeyToMap(publicKey);
      }
      else if (keyTypeId == CoseAlgRs256 && keyAlgorithm?.Equals("RSA", StringComparison.OrdinalIgnoreCase) == true)
      {
        return ConvertRsaPublicKeyToMap(publicKey);
      }

      return null;
    }

    private static CBORObject ConvertEcPublicKeyToMap(IPublicKey ecPublicKey)
    {
      // Use BouncyCastle to parse the X.509 SubjectPublicKeyInfo
      var encoded = ecPublicKey.GetEncoded();
      if (encoded == null) throw new ArgumentException("Cannot encode EC public key");

      var bcPublicKey = PublicKeyFactory.CreateKey(encoded);
      if (bcPublicKey is not ECPublicKeyParameters ecParams)
        throw new ArgumentException("Public key is not an EC key");

      // Extract coordinates from BouncyCastle EC parameters
      var x = ecParams.Q.AffineXCoord.GetEncoded();
      var y = ecParams.Q.AffineYCoord.GetEncoded();

      // Ensure coordinates are exactly 32 bytes (P-256)
      if (x.Length != 32 || y.Length != 32)
        throw new ArgumentException($"Invalid P-256 coordinate length: x={x.Length}, y={y.Length}");

      // Build COSE EC2 key structure
      return CBORObject.NewMap()
        .Add(CoseKeyKeytype, CoseKtyEc2)
        .Add(CoseKeyAlgorithm, (int)CoseAlgEs256)
        .Add(CoseKeyCurve, CoseCurveP256)
        .Add(CoseKeyX, x)
        .Add(CoseKeyY, y);
    }

    private static CBORObject ConvertEd25519PublicKeyToMap(IPublicKey ed25519PublicKey)
    {
      // Use BouncyCastle to parse the X.509 SubjectPublicKeyInfo
      var encoded = ed25519PublicKey.GetEncoded();
      if (encoded == null) throw new ArgumentException("Cannot encode Ed25519 public key");

      var bcPublicKey = PublicKeyFactory.CreateKey(encoded);
      if (bcPublicKey is not Ed25519PublicKeyParameters ed25519Params)
        throw new ArgumentException("Public key is not an Ed25519 key");

      // Extract public key bytes from BouncyCastle parameters
      var publicKeyBytes = ed25519Params.GetEncoded();

      // Ed25519 public keys must be exactly 32 bytes
      if (publicKeyBytes.Length != 32)
        throw new ArgumentException($"Invalid Ed25519 public key length: {publicKeyBytes.Length}");

      // Build COSE OKP key structure
      return CBORObject.NewMap()
        .Add(CoseKeyKeytype, CoseKtyOkp)
        .Add(CoseKeyAlgorithm, (int)CoseAlgEd25519)
        .Add(CoseKeyCurve, CoseCurveEd25519)
        .Add(CoseKeyX, publicKeyBytes);
    }

    private static CBORObject ConvertRsaPublicKeyToMap(IPublicKey rsaPublicKey)
    {
      // Extract RSA key information from encoded key data
      // Use KeyFactory to convert to RSAPublicKeySpec to access modulus and exponent
      try
      {
        var encoded = rsaPublicKey.GetEncoded();
        if (encoded == null)
          throw new ArgumentException("Cannot encode RSA public key");

        var keyFactory = KeyFactory.GetInstance("RSA");
        var rsaSpec = keyFactory?.GetKeySpec(rsaPublicKey, Class.FromType(typeof(RSAPublicKeySpec)));

        if (rsaSpec is RSAPublicKeySpec rsaPublicKeySpec)
        {
          var n = rsaPublicKeySpec.Modulus?.ToByteArray();
          var e = rsaPublicKeySpec.PublicExponent?.ToByteArray();

          n = RemoveLeadingZero(n);
          e = RemoveLeadingZero(e);

          // Build COSE RSA key structure
          return CBORObject.NewMap()
            .Add(CoseKeyKeytype, CoseKtyRsa)
            .Add(CoseKeyAlgorithm, (int)CoseAlgRs256)
            .Add(CoseKeyCurve, n!)  // n: modulus
            .Add(CoseKeyX, e!);   // e: exponent
        }

        throw new ArgumentException("Failed to extract RSA key specification");
      }
      catch (Exception ex)
      {
        throw new ArgumentException("Failed to convert RSA public key to map", ex);
      }
    }



    private static byte[]? RemoveLeadingZero(byte[]? bytes)
    {
      if (bytes == null || bytes.Length == 0)
        return bytes;

      if (bytes[0] == 0 && bytes.Length > 1)
      {
        var result = new byte[bytes.Length - 1];
        Array.Copy(bytes, 1, result, 0, result.Length);
        return result;
      }

      return bytes;
    }


    /// <summary>
    /// Compute SHA-256 hash of data
    /// </summary>
    public static byte[] HashSha256(byte[] data)
    {
      using var sha256 = SHA256.Create();
      return sha256.ComputeHash(data);
    }
  }
}
