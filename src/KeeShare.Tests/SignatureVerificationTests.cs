using System.Security.Cryptography;
using System.Text;
using keepass2android;

namespace KeeShare.Tests
{
    public class SignatureVerificationTests
    {
        /// <summary>
        /// Helper to convert bytes to hex string (KeeShare format)
        /// </summary>
        private static string BytesToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Helper to format signature in KeeShare format: "rsa|<hex>"
        /// </summary>
        private static byte[] FormatKeeShareSignature(byte[] signature)
        {
            string hex = BytesToHex(signature);
            return Encoding.UTF8.GetBytes($"rsa|{hex}");
        }

        [Fact]
        public void VerifySignature_WithValidSignature_ReturnsTrue()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test KDBX data content");
            byte[] hash = SHA256.HashData(testData);
            byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            byte[] signatureData = FormatKeeShareSignature(signature);
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, testData, signatureData);
            Assert.True(result, "Signature verification should succeed with valid signature");
        }

        [Fact]
        public void VerifySignature_WithValidSignatureWithoutPrefix_ReturnsTrue()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test KDBX data content");
            byte[] hash = SHA256.HashData(testData);
            byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            // Hex without "rsa|" prefix should also work
            string signatureHex = BytesToHex(signature);
            byte[] signatureData = Encoding.UTF8.GetBytes(signatureHex);
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, testData, signatureData);
            Assert.True(result, "Signature verification should succeed with hex signature without prefix");
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
            byte[] signatureData = FormatKeeShareSignature(invalidSignature);
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, testData, signatureData);
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
            byte[] signatureData = FormatKeeShareSignature(signature);
            byte[] tamperedData = Encoding.UTF8.GetBytes("Tampered KDBX data");
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, tamperedData, signatureData);
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
            byte[] signatureData = FormatKeeShareSignature(signature);
            bool result = KeeShare.VerifySignatureCore(publicKeyCertPem, testData, signatureData);
            Assert.True(result, "Signature verification should work with PEM formatted certificate");
        }

        [Fact]
        public void VerifySignature_WithEmptyCertificate_ReturnsFalse()
        {
            byte[] testData = Encoding.UTF8.GetBytes("Test data");
            byte[] signatureData = Encoding.UTF8.GetBytes("rsa|abcd1234");
            bool result = KeeShare.VerifySignatureCore("", testData, signatureData);
            Assert.False(result, "Signature verification should fail with empty certificate");
        }

        [Fact]
        public void VerifySignature_WithNullData_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] signatureData = Encoding.UTF8.GetBytes("rsa|abcd1234");
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, null, signatureData);
            Assert.False(result, "Signature verification should fail with null data");
        }

        [Fact]
        public void VerifySignature_WithEmptyData_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] signatureData = Encoding.UTF8.GetBytes("rsa|abcd1234");
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, new byte[0], signatureData);
            Assert.False(result, "Signature verification should fail with empty data");
        }

        [Fact]
        public void VerifySignature_WithMalformedHexSignature_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test data");
            // Invalid hex characters
            byte[] signatureData = Encoding.UTF8.GetBytes("rsa|not-valid-hex!@#$GHIJ");
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, testData, signatureData);
            Assert.False(result, "Signature verification should fail with malformed hex");
        }

        [Fact]
        public void VerifySignature_WithOddLengthHex_ReturnsFalse()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test data");
            // Odd-length hex string (invalid)
            byte[] signatureData = Encoding.UTF8.GetBytes("rsa|abc");
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, testData, signatureData);
            Assert.False(result, "Signature verification should fail with odd-length hex");
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
            string signatureHex = BytesToHex(signature);
            // Add whitespace around the signature
            string signatureWithWhitespace = $"\r\n  rsa|{signatureHex}  \r\n";
            byte[] signatureData = Encoding.UTF8.GetBytes(signatureWithWhitespace);
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, testData, signatureData);
            Assert.True(result, "Signature verification should handle whitespace in signature");
        }

        [Fact]
        public void VerifySignature_WithUppercaseHex_ReturnsTrue()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test KDBX data content");
            byte[] hash = SHA256.HashData(testData);
            byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            // Use uppercase hex
            string signatureHex = BitConverter.ToString(signature).Replace("-", "").ToUpperInvariant();
            byte[] signatureData = Encoding.UTF8.GetBytes($"rsa|{signatureHex}");
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, testData, signatureData);
            Assert.True(result, "Signature verification should handle uppercase hex");
        }

        [Fact]
        public void VerifySignature_WithUppercaseRsaPrefix_ReturnsTrue()
        {
            using var rsa = RSA.Create(2048);
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var publicKeyCert = Convert.ToBase64String(publicKeyBytes);
            byte[] testData = Encoding.UTF8.GetBytes("Test KDBX data content");
            byte[] hash = SHA256.HashData(testData);
            byte[] signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string signatureHex = BytesToHex(signature);
            // Use uppercase "RSA|" prefix
            byte[] signatureData = Encoding.UTF8.GetBytes($"RSA|{signatureHex}");
            bool result = KeeShare.VerifySignatureCore(publicKeyCert, testData, signatureData);
            Assert.True(result, "Signature verification should handle uppercase RSA prefix");
        }
    }
}
