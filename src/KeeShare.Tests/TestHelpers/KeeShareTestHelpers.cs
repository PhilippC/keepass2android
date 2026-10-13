using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace KeeShare.Tests.TestHelpers
{
    /// <summary>
    /// Copy of KeeShare signature verification logic for testing.
    /// This is a pure function that doesn't depend on Android-specific code.
    /// Kept in sync with KeeShareCheckOperation.VerifySignatureCore in the app project.
    /// </summary>
    public static class KeeShareTestHelpers
    {
        /// <summary>
        /// Verifies a KeeShare signature. This is a copy of the production code
        /// for testing purposes (to avoid Android assembly dependencies).
        /// </summary>
        public static bool VerifySignatureCore(string trustedCertificate, byte[] kdbxData, byte[] signatureData)
        {
            try
            {
                if (string.IsNullOrEmpty(trustedCertificate))
                {
                    return false;
                }

                if (signatureData == null || signatureData.Length == 0)
                {
                    return false;
                }

                if (kdbxData == null || kdbxData.Length == 0)
                {
                    return false;
                }

                string signatureText = Encoding.UTF8.GetString(signatureData).Trim();
                
                signatureText = signatureText.Replace("\r", "").Replace("\n", "").Replace(" ", "");
                
                const string rsaPrefix = "rsa|";
                if (signatureText.StartsWith(rsaPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    signatureText = signatureText.Substring(rsaPrefix.Length);
                }
                
                byte[] signatureBytes;
                try
                {
                    signatureBytes = HexStringToByteArray(signatureText);
                }
                catch (Exception)
                {
                    return false;
                }
                
                if (signatureBytes == null || signatureBytes.Length == 0)
                {
                    return false;
                }

                byte[] publicKeyBytes;
                try
                {
                    string pemText = trustedCertificate.Trim();
                    
                    if (pemText.StartsWith("-----BEGIN CERTIFICATE-----"))
                    {
                        var cert = X509Certificate2.CreateFromPem(pemText);
                        publicKeyBytes = cert.GetPublicKey();
                    }
                    else if (pemText.StartsWith("-----BEGIN PUBLIC KEY-----"))
                    {
                        var lines = pemText.Split('\n');
                        var base64Lines = lines
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("-----"));
                        string base64Content = string.Join("", base64Lines);
                        publicKeyBytes = Convert.FromBase64String(base64Content);
                    }
                    else
                    {
                        publicKeyBytes = Convert.FromBase64String(pemText);
                    }
                }
                catch (Exception)
                {
                    return false;
                }

                bool isValid = false;
                bool importFailed = false;
                using (RSA rsa = RSA.Create())
                {
                    bool importSucceeded = false;
                    try
                    {
                        rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out int bytesRead);
                        if (bytesRead == 0)
                        {
                            throw new CryptographicException("No bytes read from public key");
                        }
                        importSucceeded = true;
                    }
                    catch (Exception)
                    {
                        try
                        {
                            rsa.ImportRSAPublicKey(publicKeyBytes, out int bytesRead);
                            if (bytesRead == 0)
                            {
                                throw new CryptographicException("No bytes read from public key");
                            }
                            importSucceeded = true;
                        }
                        catch (Exception)
                        {
                            importFailed = true;
                        }
                    }

                    if (!importFailed && importSucceeded)
                    {
                        byte[] hash;
                        using (SHA256 sha256 = SHA256.Create())
                        {
                            hash = sha256.ComputeHash(kdbxData);
                        }

                        isValid = rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    }
                }

                return !importFailed && isValid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Convert hex string to byte array (matching KeePassLib.Utility.MemUtil.HexStringToByteArray behavior)
        /// </summary>
        private static byte[] HexStringToByteArray(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Array.Empty<byte>();

            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have even length");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}

