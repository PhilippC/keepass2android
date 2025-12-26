using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android.KeeShare
{
    /// <summary>
    /// KeeShare operation mode for a group
    /// </summary>
    public enum KeeShareMode
    {
        /// <summary>Import entries from share file into this group</summary>
        Import,
        /// <summary>Export entries from this group to share file</summary>
        Export,
        /// <summary>Bidirectional sync between group and share file</summary>
        Synchronize
    }

    /// <summary>
    /// Result of a KeeShare export operation
    /// </summary>
    public class KeeShareExportResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string SharePath { get; set; }
        public int EntriesExported { get; set; }
        public DateTime ExportTime { get; set; }
    }

    /// <summary>
    /// Exports KeePass groups to .kdbx or signed .share containers
    /// </summary>
    public class KeeShareExporter
    {
        /// <summary>
        /// Exports a group to a .kdbx file (uncontainerized)
        /// </summary>
        public static KeeShareExportResult ExportToKdbx(
            PwDatabase sourceDb,
            PwGroup groupToExport,
            string targetPath,
            CompositeKey targetKey,
            IStatusLogger logger = null)
        {
            var result = new KeeShareExportResult
            {
                SharePath = targetPath,
                ExportTime = DateTime.UtcNow
            };

            try
            {
                // Create a new database with only the target group content
                var exportDb = new PwDatabase();
                exportDb.New(new IOConnectionInfo(), targetKey);
                
                // Copy database settings
                exportDb.Name = groupToExport.Name;
                exportDb.Description = $"KeeShare export from {sourceDb.Name}";

                // Copy group content to root
                CopyGroupContent(groupToExport, exportDb.RootGroup, sourceDb, exportDb);

                // Count exported entries
                result.EntriesExported = CountEntries(exportDb.RootGroup);

                // Save the database
                var ioc = IOConnectionInfo.FromPath(targetPath);
                exportDb.SaveAs(ioc, false, logger);
                
                result.IsSuccess = true;
                Kp2aLog.Log($"KeeShare: Exported {result.EntriesExported} entries to {targetPath}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                Kp2aLog.Log($"KeeShare: Export failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Exports a group to a signed .share container
        /// </summary>
        public static KeeShareExportResult ExportToContainer(
            PwDatabase sourceDb,
            PwGroup groupToExport,
            string targetPath,
            CompositeKey innerKey,
            RSAParameters privateKey,
            string signerName,
            IStatusLogger logger = null)
        {
            var result = new KeeShareExportResult
            {
                SharePath = targetPath,
                ExportTime = DateTime.UtcNow
            };

            try
            {
                // First export to a temp .kdbx
                using (var tempStream = new MemoryStream())
                {
                    // Create export database
                    var exportDb = new PwDatabase();
                    exportDb.New(new IOConnectionInfo(), innerKey);
                    exportDb.Name = groupToExport.Name;

                    // Copy content
                    CopyGroupContent(groupToExport, exportDb.RootGroup, sourceDb, exportDb);
                    result.EntriesExported = CountEntries(exportDb.RootGroup);

                    // Save to memory stream
                    var format = new KdbxFile(exportDb);
                    format.Save(tempStream, null, KdbxFormat.Default, logger);
                    var kdbxData = tempStream.ToArray();

                    // Sign the data
                    var signature = SignData(kdbxData, privateKey);
                    var signatureXml = CreateSignatureXml(signature, signerName, privateKey);

                    // Create the .share container (ZIP with signature.xml and db.kdbx)
                    CreateShareContainer(targetPath, kdbxData, signatureXml);
                }

                result.IsSuccess = true;
                Kp2aLog.Log($"KeeShare: Exported {result.EntriesExported} entries to container {targetPath}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                Kp2aLog.Log($"KeeShare: Container export failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Creates the RSA signature for the database content
        /// </summary>
        private static byte[] SignData(byte[] data, RSAParameters privateKey)
        {
            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(privateKey);
                return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        /// <summary>
        /// Creates the signature.xml content for the .share container
        /// </summary>
        private static string CreateSignatureXml(byte[] signature, string signerName, RSAParameters key)
        {
            // Serialize public key in SSH format
            var publicKeyBase64 = SerializePublicKeyToSsh(key);
            var signatureBase64 = Convert.ToBase64String(signature);

            var doc = new XDocument(
                new XElement("KeeShare",
                    new XElement("Signature", signatureBase64),
                    new XElement("Certificate",
                        new XElement("Signer", signerName),
                        new XElement("Key", publicKeyBase64)
                    )
                )
            );

            return doc.ToString();
        }

        /// <summary>
        /// Serializes RSA public key to SSH format (ssh-rsa)
        /// </summary>
        private static string SerializePublicKeyToSsh(RSAParameters key)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write "ssh-rsa" type
                WriteBytes(writer, Encoding.UTF8.GetBytes("ssh-rsa"));
                // Write exponent
                WriteBytes(writer, key.Exponent);
                // Write modulus
                WriteBytes(writer, key.Modulus);

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>
        /// Writes length-prefixed bytes (big-endian uint32 length)
        /// </summary>
        private static void WriteBytes(BinaryWriter writer, byte[] data)
        {
            var lenBytes = BitConverter.GetBytes((uint)data.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lenBytes);
            writer.Write(lenBytes);
            writer.Write(data);
        }

        /// <summary>
        /// Creates a .share ZIP container with signature and database
        /// </summary>
        private static void CreateShareContainer(string path, byte[] kdbxData, string signatureXml)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                // Add signature.xml
                var sigEntry = archive.CreateEntry("signature.xml", CompressionLevel.Optimal);
                using (var sigStream = sigEntry.Open())
                using (var writer = new StreamWriter(sigStream, Encoding.UTF8))
                {
                    writer.Write(signatureXml);
                }

                // Add database.kdbx
                var dbEntry = archive.CreateEntry("database.kdbx", CompressionLevel.Optimal);
                using (var dbStream = dbEntry.Open())
                {
                    dbStream.Write(kdbxData, 0, kdbxData.Length);
                }
            }
        }

        /// <summary>
        /// Copies content from source group to target group
        /// </summary>
        private static void CopyGroupContent(PwGroup source, PwGroup target, PwDatabase sourceDb, PwDatabase targetDb)
        {
            // Copy group properties
            target.Name = source.Name;
            target.Notes = source.Notes;
            target.IconId = source.IconId;
            target.CustomIconUuid = source.CustomIconUuid;

            // Copy entries (clone them)
            foreach (var entry in source.Entries)
            {
                var clone = entry.CloneDeep();
                clone.SetUuid(entry.Uuid, false); // Keep same UUID for sync
                target.AddEntry(clone, true);
            }

            // Recursively copy subgroups
            foreach (var subGroup in source.Groups)
            {
                // Skip KeeShare settings group
                if (subGroup.Name == "KeeShare" && subGroup.Notes.Contains("KeeShare.Settings"))
                    continue;

                var newSubGroup = new PwGroup(true, true, subGroup.Name, subGroup.IconId);
                target.AddGroup(newSubGroup, true);
                CopyGroupContent(subGroup, newSubGroup, sourceDb, targetDb);
            }
        }

        /// <summary>
        /// Counts entries recursively
        /// </summary>
        private static int CountEntries(PwGroup group)
        {
            int count = group.Entries.UCount;
            foreach (var subGroup in group.Groups)
            {
                count += CountEntries(subGroup);
            }
            return (int)count;
        }

        /// <summary>
        /// Generates a new RSA key pair for signing
        /// </summary>
        public static RSAParameters GenerateKeyPair(out RSAParameters privateKey)
        {
            using (var rsa = RSA.Create(2048))
            {
                privateKey = rsa.ExportParameters(true);
                return rsa.ExportParameters(false); // Public key only
            }
        }

        /// <summary>
        /// Computes SHA-256 fingerprint of a public key
        /// </summary>
        public static string ComputeKeyFingerprint(RSAParameters publicKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(
                Convert.ToBase64String(publicKey.Modulus) + 
                Convert.ToBase64String(publicKey.Exponent));
            
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(keyBytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
