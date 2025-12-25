using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace keepass2android.KeeShare
{
    public class KeeShareSignature
    {
        public string Signature { get; set; }
        public string Signer { get; set; }
        public RSAParameters? Key { get; set; }

        public static KeeShareSignature Parse(string xml)
        {
            var sigObj = new KeeShareSignature();
            try
            {
                var doc = XDocument.Parse(xml);
                var root = doc.Root; // KeeShare
                if (root == null || root.Name != "KeeShare") return null;

                var sigElem = root.Element("Signature");
                if (sigElem != null)
                {
                    sigObj.Signature = sigElem.Value;
                }

                var certElem = root.Element("Certificate");
                if (certElem != null)
                {
                    var signerElem = certElem.Element("Signer");
                    if (signerElem != null)
                    {
                        sigObj.Signer = signerElem.Value;
                    }

                    var keyElem = certElem.Element("Key");
                    if (keyElem != null)
                    {
                        var keyBase64 = keyElem.Value;
                        var keyBytes = Convert.FromBase64String(keyBase64);
                        sigObj.Key = SshRsaKeyParser.Parse(keyBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("KeeShare: Failed to parse signature: " + ex.Message);
                return null;
            }
            return sigObj;
        }
    }

    public static class SshRsaKeyParser
    {
        public static RSAParameters? Parse(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Format is [len][ssh-rsa][len][e][len][n]
                // Lengths are big-endian uint32.

                try
                {
                    var type = ReadString(reader);
                    if (Encoding.UTF8.GetString(type) != "ssh-rsa")
                        return null;

                    var e = ReadMpValue(reader);
                    var n = ReadMpValue(reader);

                    return new RSAParameters
                    {
                        Exponent = e,
                        Modulus = n
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("KeeShare: Failed to parse SSH key: " + ex.Message);
                    return null;
                }
            }
        }

        private static byte[] ReadString(BinaryReader reader)
        {
            var lenBytes = reader.ReadBytes(4);
            if (lenBytes.Length < 4) throw new EndOfStreamException();
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
            uint len = BitConverter.ToUInt32(lenBytes, 0);

            var data = reader.ReadBytes((int)len);
            if (data.Length < len) throw new EndOfStreamException();
            return data;
        }

        private static byte[] ReadMpValue(BinaryReader reader)
        {
            // mpint is also length prefixed.
            // But sometimes it has a leading zero byte for sign which we might need to strip for RSAParameters?
            // "mpints are represented as a string with the value... The most significant bit of the first byte of data MUST be zero if the number is positive"
            // RSAParameters expects unsigned big-endian.

            // Wait, QDataStream writeBytes writes [len][data].
            // The C++ code:
            // rsaKey->get_e().binary_encode(rsaE.data());
            // stream.writeBytes(..., rsaE.size());

            // Botan's binary_encode writes raw big-endian bytes.
            // So this is NOT mpint (which is SSH format), but just raw bytes prefixed by length.
            // However, SSH keys often use mpint.
            // Let's re-read the C++ code carefully.

            /*
            QDataStream stream(&rsaKeySerialized, QIODevice::WriteOnly);
            stream.writeBytes("ssh-rsa", 7);
            stream.writeBytes(reinterpret_cast<const char*>(rsaE.data()), rsaE.size());
            stream.writeBytes(reinterpret_cast<const char*>(rsaN.data()), rsaN.size());
            */

            // QDataStream::writeBytes writes quint32 len + bytes.
            // So it IS [len][data].
            // And rsaE/rsaN are raw bytes from BigInt.

            return ReadString(reader);
        }
    }
}
