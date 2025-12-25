using NUnit.Framework;
using keepass2android.KeeShare;
using System;
using System.Security.Cryptography;

namespace KeeShare.Tests
{
    [TestFixture]
    public class KeeShareTrustSettingsTests
    {
        [Test]
        public void TestCalculateKeyFingerprint()
        {
            // Create test key data
            var modulus = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var exponent = new byte[] { 0x01, 0x00, 0x01 };
            
            var fingerprint = KeeShareTrustSettings.CalculateKeyFingerprint(modulus, exponent);
            
            Assert.That(fingerprint, Is.Not.Null);
            Assert.That(fingerprint.Length, Is.EqualTo(64)); // SHA256 hex = 64 chars
            Assert.That(fingerprint, Does.Match("^[a-f0-9]+$"));
        }
        
        [Test]
        public void TestCalculateKeyFingerprintConsistency()
        {
            var modulus = new byte[] { 0xAB, 0xCD, 0xEF };
            var exponent = new byte[] { 0x01, 0x00, 0x01 };
            
            var fingerprint1 = KeeShareTrustSettings.CalculateKeyFingerprint(modulus, exponent);
            var fingerprint2 = KeeShareTrustSettings.CalculateKeyFingerprint(modulus, exponent);
            
            Assert.That(fingerprint1, Is.EqualTo(fingerprint2), "Same key data should produce same fingerprint");
        }
        
        [Test]
        public void TestCalculateKeyFingerprintDifferentKeys()
        {
            var modulus1 = new byte[] { 0x01, 0x02, 0x03 };
            var modulus2 = new byte[] { 0x01, 0x02, 0x04 };
            var exponent = new byte[] { 0x01, 0x00, 0x01 };
            
            var fingerprint1 = KeeShareTrustSettings.CalculateKeyFingerprint(modulus1, exponent);
            var fingerprint2 = KeeShareTrustSettings.CalculateKeyFingerprint(modulus2, exponent);
            
            Assert.That(fingerprint1, Is.Not.EqualTo(fingerprint2), "Different keys should produce different fingerprints");
        }
        
        [Test]
        public void TestCalculateKeyFingerprintNullInputs()
        {
            Assert.That(KeeShareTrustSettings.CalculateKeyFingerprint(null, new byte[] { 0x01 }), Is.Null);
            Assert.That(KeeShareTrustSettings.CalculateKeyFingerprint(new byte[] { 0x01 }, null), Is.Null);
            Assert.That(KeeShareTrustSettings.CalculateKeyFingerprint(null, null), Is.Null);
        }
        
        [Test]
        public void TestFingerprintFromRealRsaKey()
        {
            using (var rsa = RSA.Create(2048))
            {
                var parameters = rsa.ExportParameters(false);
                var fingerprint = KeeShareTrustSettings.CalculateKeyFingerprint(parameters.Modulus, parameters.Exponent);
                
                Assert.That(fingerprint, Is.Not.Null);
                Assert.That(fingerprint.Length, Is.EqualTo(64));
            }
        }
    }
}
