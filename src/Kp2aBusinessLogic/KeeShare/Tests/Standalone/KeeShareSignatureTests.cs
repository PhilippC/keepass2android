using NUnit.Framework;
using keepass2android.KeeShare;
using System;
using System.Security.Cryptography;
using System.Text;

namespace KeeShare.Tests
{
    [TestFixture]
    public class KeeShareSignatureTests
    {
        [Test]
        public void TestParseAndVerifySignature()
        {
            // Generate a real key pair for testing
            using (var rsa = RSA.Create(2048))
            {
                var privateParams = rsa.ExportParameters(true);
                var publicParams = rsa.ExportParameters(false);

                // Construct the "ssh-rsa" format key manually
                // [len][ssh-rsa][len][e][len][n]
                // Note: ssh-rsa usually requires e and n to be positive mpints (leading zero if high bit set)
                // But our parser follows the C++ impl which writes raw bytes.
                // Wait, C++ writes: stream.writeBytes(reinterpret_cast<const char*>(rsaE.data()), rsaE.size());
                // QDataStream writes uint32 len + data.
                // Botan rsaE.data() is raw big-endian bytes.

                var e = publicParams.Exponent;
                var n = publicParams.Modulus;

                using (var ms = new System.IO.MemoryStream())
                using (var writer = new System.IO.BinaryWriter(ms))
                {
                    WriteString(writer, "ssh-rsa");
                    WriteBytes(writer, e);
                    WriteBytes(writer, n);

                    var keyBytes = ms.ToArray();
                    var keyBase64 = Convert.ToBase64String(keyBytes);

                    // Create data and sign it
                    var data = Encoding.UTF8.GetBytes("Test Data");
                    var sigBytes = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    var sigHex = BitConverter.ToString(sigBytes).Replace("-", "").ToLower();

                    var xml = $@"<KeeShare>
                                   <Signature>rsa|{sigHex}</Signature>
                                   <Certificate>
                                     <Signer>TestUser</Signer>
                                     <Key>{keyBase64}</Key>
                                   </Certificate>
                                 </KeeShare>";

                    var sigObj = KeeShareSignature.Parse(xml);
                    Assert.That(sigObj, Is.Not.Null);
                    Assert.That(sigObj.Signer, Is.EqualTo("TestUser"));
                    Assert.That(sigObj.Key, Is.Not.Null);

                    // Verify
                    var parsedKey = sigObj.Key.Value;
                    Assert.That(parsedKey.Exponent, Is.EqualTo(e));
                    Assert.That(parsedKey.Modulus, Is.EqualTo(n));

                    // Manually verify
                    using (var rsaVerify = RSA.Create())
                    {
                        rsaVerify.ImportParameters(parsedKey);
                        var valid = rsaVerify.VerifyData(data, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        Assert.That(valid, Is.True);
                    }
                }
            }
        }

        private void WriteString(System.IO.BinaryWriter writer, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            WriteBytes(writer, bytes);
        }

        private void WriteBytes(System.IO.BinaryWriter writer, byte[] bytes)
        {
            var len = (uint)bytes.Length;
            var lenBytes = BitConverter.GetBytes(len);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
            writer.Write(lenBytes);
            writer.Write(bytes);
        }
    }
}
