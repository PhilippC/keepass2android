using System.Security.Cryptography;
using System.Text;

namespace KeeShare.Tests
{
    public class SignatureVerificationTests
    {
        [Fact]
        public void VerifySignature_WithValidSignature_ReturnsTrue()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test KDBX data content");
            byte[] hash = SHA256.HashData(testData);
            byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string signatureBase64 = Convert.ToBase64String(signature);
            byte[] signatureData = Encoding.UTF8.GetBytes(signatureBase64);
            bool result = SignatureVerifier.VerifySignature(publicKeyCert, testData, signatureData);
            Assert.True(result, "Signature verification should succeed with valid signature");
        }

        [Fact]
        public void VerifySignature_WithInvalidSignature_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test KDBX data content");
            byte[] invalidSignature = new byte[256];
            new Random().NextBytes(invalidSignature);
            string signatureBase64 = Convert.ToBase64String(invalidSignature);
            byte[] signatureData = Encoding.UTF8.GetBytes(signatureBase64);
            bool result = SignatureVerifier.VerifySignature(publicKeyCert, testData, signatureData);
            Assert.False(result, "Signature verification should fail with invalid signature");
        }

        [Fact]
        public void VerifySignature_WithTamperedData_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] originalData = Encoding.UTF8.GetBytes("Original KDBX data");
            byte[] hash = SHA256.HashData(originalData);
            byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string signatureBase64 = Convert.ToBase64String(signature);
            byte[] signatureData = Encoding.UTF8.GetBytes(signatureBase64);
            byte[] tamperedData = Encoding.UTF8.GetBytes("Tampered KDBX data");
            bool result = SignatureVerifier.VerifySignature(publicKeyCert, tamperedData, signatureData);
            Assert.False(result, "Signature verification should fail when data is tampered");
        }

        [Fact]
        public void VerifySignature_WithPemFormattedCertificate_ReturnsTrue()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCertBase64 = Convert.ToBase64String(publicKeyBytes);
            string publicKeyCertPem = $"-----BEGIN PUBLIC KEY-----\n{publicKeyCertBase64}\n-----END PUBLIC KEY-----";
            byte[] testData = Encoding.UTF8.GetBytes("Test KDBX data content");
            byte[] hash = SHA256.HashData(testData);
            byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string signatureBase64 = Convert.ToBase64String(signature);
            byte[] signatureData = Encoding.UTF8.GetBytes(signatureBase64);
            bool result = SignatureVerifier.VerifySignature(publicKeyCertPem, testData, signatureData);
            Assert.True(result, "Signature verification should work with PEM formatted certificate");
        }

        [Fact]
        public void VerifySignature_WithEmptyCertificate_ReturnsFalse()
        {
            byte[] testData = Encoding.UTF8.GetBytes("Test data");
            byte[] signatureData = Encoding.UTF8.GetBytes("fake signature");
            bool result = SignatureVerifier.VerifySignature("", testData, signatureData);
            Assert.False(result, "Signature verification should fail with empty certificate");
        }

        [Fact]
        public void VerifySignature_WithNullData_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] signatureData = Encoding.UTF8.GetBytes("signature");
            bool result = SignatureVerifier.VerifySignature(publicKeyCert, null, signatureData);
            Assert.False(result, "Signature verification should fail with null data");
        }

        [Fact]
        public void VerifySignature_WithEmptyData_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] signatureData = Encoding.UTF8.GetBytes("signature");
            bool result = SignatureVerifier.VerifySignature(publicKeyCert, new byte[0], signatureData);
            Assert.False(result, "Signature verification should fail with empty data");
        }

        [Fact]
        public void VerifySignature_WithMalformedBase64Signature_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test data");
            byte[] signatureData = Encoding.UTF8.GetBytes("not-valid-base64!@#$");
            bool result = SignatureVerifier.VerifySignature(publicKeyCert, testData, signatureData);
            Assert.False(result, "Signature verification should fail with malformed base64");
        }

        [Fact]
        public void VerifySignature_WithSignatureContainingWhitespace_ReturnsTrue()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test KDBX data content");
            byte[] hash = SHA256.HashData(testData);
            byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string signatureBase64 = Convert.ToBase64String(signature);
            string signatureWithWhitespace = $"\r\n  {signatureBase64}  \r\n";
            byte[] signatureData = Encoding.UTF8.GetBytes(signatureWithWhitespace);
            bool result = SignatureVerifier.VerifySignature(publicKeyCert, testData, signatureData);
            Assert.True(result, "Signature verification should handle whitespace in signature");
        }
    }

    public static class SignatureVerifier
    {
        public static bool VerifySignature(string trustedCertificate, byte[]? kdbxData, byte[]? signatureData)
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

                byte[] signatureBytes;
                try
                {
                    signatureBytes = Convert.FromBase64String(signatureText);
                }
                catch (Exception)
                {
                    return false;
                }

                byte[] publicKeyBytes;
                try
                {
                    if (trustedCertificate.Contains("-----BEGIN"))
                    {
                        var lines = trustedCertificate.Split('\n');
                        var base64Lines = lines.Where(l => !l.Contains("BEGIN") && !l.Contains("END") && !string.IsNullOrWhiteSpace(l));
                        string base64Content = string.Join("", base64Lines).Trim();
                        publicKeyBytes = Convert.FromBase64String(base64Content);
                    }
                    else
                    {
                        publicKeyBytes = Convert.FromBase64String(trustedCertificate);
                    }
                }
                catch (Exception)
                {
                    return false;
                }

                RSA rsa = RSA.Create();
                try
                {
                    try
                    {
                        rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out int bytesRead);
                        if (bytesRead == 0)
                        {
                            throw new CryptographicException("No bytes read from public key");
                        }
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
                        }
                        catch (Exception)
                        {
                            rsa.Dispose();
                            return false;
                        }
                    }

                    byte[] hash;
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        hash = sha256.ComputeHash(kdbxData);
                    }

                    bool isValid = false;
                    try
                    {
                        isValid = rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    }
                    finally
                    {
                        rsa.Dispose();
                    }

                    return isValid;
                }
                catch
                {
                    rsa.Dispose();
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
