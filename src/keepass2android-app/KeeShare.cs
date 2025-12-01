using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.App;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using keepass2android.Io;
using KeePassLib.Utility;
using System.IO.Compression;
using Android.Content;
using System.Security.Cryptography;
using System.Text;

namespace keepass2android
{
    public class KeeShare
    {
        public static void Check(IKp2aApp app, OnOperationFinishedHandler nextHandler)
        {
            var db = app.CurrentDb;
            if (db == null || !db.KpDatabase.IsOpen)
            {
                nextHandler?.Run();
                return;
            }

            if (!HasKeeShareGroups(db.KpDatabase.RootGroup))
            {
                nextHandler?.Run();
                return;
            }

            var op = new KeeShareCheckOperation(app, nextHandler);
            new BlockingOperationStarter(app, op).Run();
        }

        internal static bool HasKeeShareGroups(PwGroup group)
        {
            if (group.CustomData.Get("KeeShare.Active") == "true")
                return true;
            
            foreach (var sub in group.Groups)
            {
                if (HasKeeShareGroups(sub)) return true;
            }
            return false;
        }
    }

    public class KeeShareCheckOperation : OperationWithFinishHandler
    {
        private readonly IKp2aApp _app;

        public KeeShareCheckOperation(IKp2aApp app, OnOperationFinishedHandler handler) 
            : base(app, handler)
        {
            _app = app;
        }

        public override void Run()
        {
            try
            {
                ProcessGroup(_app.CurrentDb.KpDatabase.RootGroup);
                Finish(true);
            }
            catch (Exception ex)
            {
                Kp2aLog.LogUnexpectedError(ex);
                Finish(false, "KeeShare error: " + ex.Message);
            }
        }

        private void ProcessGroup(PwGroup group)
        {
            if (group.CustomData.Get("KeeShare.Active") == "true")
            {
                try 
                {
                    ProcessKeeShare(group);
                }
                catch (Exception ex)
                {
                    Kp2aLog.Log("Error processing KeeShare for group " + group.Name + ": " + ex.ToString());
                    // Continue with other groups even if one fails
                }
            }

            // Process subgroups AFTER processing KeeShare, so we recurse into the newly imported groups
            // We must iterate over a copy of the groups list to avoid issues if ProcessGroup modifies the collection
            foreach (var sub in group.Groups.ToList())
            {
                ProcessGroup(sub);
            }
        }

        private void ProcessKeeShare(PwGroup group)
        {
            string type = group.CustomData.Get("KeeShare.Type");
            string path = group.CustomData.Get("KeeShare.FilePath");
            string password = group.CustomData.Get("KeeShare.Password");

            if (string.IsNullOrEmpty(path)) return;

            if (type == "Import" || type == "Synchronize")
            {
                StatusLogger.UpdateMessage(_app.GetResourceString(UiStringKey.OpeningDatabase) + ": " + group.Name);
                Import(group, path, password, type);
            }
        }

        private void Import(PwGroup targetGroup, string path, string password, string type)
        {
            IOConnectionInfo ioc = ResolvePath(path);
            
            try 
            {
                using (Stream s = OpenStream(ioc))
                {
                    if (s == null) return;

                    // Check if it's a Zip (KeeShare signed file)
                    // We can't seek on some streams, so we might need to copy to memory if we need random access
                    // But KdbxFile loads from stream. ZipArchive needs seekable stream usually.
                    
                    MemoryStream ms = new MemoryStream();
                    try
                    {
                        s.CopyTo(ms);
                        ms.Position = 0;

                        Stream kdbxStream = ms;
                        MemoryStream kdbxMem = null;
                        bool isZip = false;
                        
                        // Check for PK header (Zip)
                        if (ms.Length > 4)
                        {
                            byte[] header = new byte[4];
                            ms.Read(header, 0, 4);
                            ms.Position = 0;
                            if (header[0] == 0x50 && header[1] == 0x4b && header[2] == 0x03 && header[3] == 0x04)
                            {
                                isZip = true;
                            }
                        }

                        if (isZip)
                        {
                            try 
                            {
                                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Read, true))
                                {
                                    // Find .kdbx file
                                    var kdbxEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".kdbx", StringComparison.OrdinalIgnoreCase));
                                    if (kdbxEntry == null)
                                    {
                                        Kp2aLog.Log("KeeShare: No .kdbx file found in ZIP archive");
                                        return;
                                    }

                                    // Extract to a new memory stream because KdbxFile might close it or we need a clean stream
                                    kdbxMem = new MemoryStream();
                                    using (var es = kdbxEntry.Open())
                                    {
                                        es.CopyTo(kdbxMem);
                                    }
                                    
                                    // Store kdbx data for signature verification
                                    byte[] kdbxData = kdbxMem.ToArray();
                                    
                                    // Check for signature file (.sig) and verify if certificate is trusted
                                    var sigEntry = archive.Entries.FirstOrDefault(e => 
                                        e.Name.EndsWith(".sig", StringComparison.OrdinalIgnoreCase) ||
                                        e.Name.EndsWith(".signature", StringComparison.OrdinalIgnoreCase));
                                    
                                    // Check if a trusted certificate is configured
                                    string trustedCert = targetGroup.CustomData.Get("KeeShare.TrustedCertificate");
                                    bool hasTrustedCert = !string.IsNullOrEmpty(trustedCert);
                                    
                                    if (sigEntry != null)
                                    {
                                        // Only verify signature if a trusted certificate is configured
                                        if (hasTrustedCert)
                                        {
                                            // Extract signature for verification
                                            byte[] signatureData;
                                            using (var sigStream = sigEntry.Open())
                                            using (var sigMem = new MemoryStream())
                                            {
                                                sigStream.CopyTo(sigMem);
                                                signatureData = sigMem.ToArray();
                                            }

                                            // Verify signature - only import if certificate is trusted and signature is valid
                                            if (!VerifySignature(targetGroup, kdbxData, signatureData))
                                            {
                                                Kp2aLog.Log("KeeShare: Signature verification failed or certificate not trusted for " + path + ". Skipping import.");
                                                return;
                                            }
                                            else
                                            {
                                                Kp2aLog.Log("KeeShare: Signature verified successfully for " + path);
                                            }
                                        }
                                        else
                                        {
                                            // Signature file exists but no certificate configured - skip verification for backward compatibility
                                            Kp2aLog.Log("KeeShare: Signature file found but no trusted certificate configured for " + path + ". Continuing without signature verification (backward compatibility).");
                                        }
                                    }
                                    else
                                    {
                                        // If a trusted certificate is configured, we MUST have a signature file for security
                                        if (hasTrustedCert)
                                        {
                                            Kp2aLog.Log("KeeShare: Trusted certificate is configured but no signature file found in ZIP archive for " + path + ". Skipping import for security.");
                                            return;
                                        }
                                        else
                                        {
                                            Kp2aLog.Log("KeeShare: No signature file found in ZIP archive for " + path + ". Continuing without signature verification (backward compatibility).");
                                        }
                                    }
                                    
                                    // Reset stream position for KDBX loading
                                    kdbxMem.Position = 0;
                                    kdbxStream = kdbxMem;
                                }
                            }
                            catch (Exception ex)
                            {
                                Kp2aLog.Log("Failed to treat file as zip: " + ex.Message);
                                ms.Position = 0; // Rewind and try as KDBX directly
                                kdbxStream = ms;
                                // kdbxMem will be disposed in finally block if it was created
                            }
                        }

                        // Load the KDBX
                        PwDatabase? shareDb = null;
                        try
                        {
                            shareDb = new PwDatabase();
                            CompositeKey key = new CompositeKey();
                            if (!string.IsNullOrEmpty(password))
                            {
                                key.AddUserKey(new KcpPassword(password));
                            }
                            // If password is empty, KcpPassword("") is added? or just empty composite key?
                            // KeeShare without password implies empty password or no master key? 
                            // Usually shares have passwords. If empty, try empty password.
                            if (key.UserKeys.Count() == 0)
                                 key.AddUserKey(new KcpPassword(""));

                            KdbxFile kdbx = new KdbxFile(shareDb);
                            kdbx.Load(kdbxStream, KdbxFormat.Default, key);

                            // Now copy content from shareDb.RootGroup to targetGroup
                            SyncGroups(shareDb.RootGroup, targetGroup, type);
                        }
                        finally
                        {
                            // Close/dispose the shared database to release resources
                            shareDb?.Close();
                            
                            // Dispose kdbxMem if it was created (for ZIP files)
                            if (kdbxMem != null && kdbxMem != ms)
                            {
                                kdbxMem.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        // Dispose ms if it's not being used as kdbxStream (i.e., if kdbxMem was used instead)
                        // Note: If ms is used as kdbxStream, KdbxFile.Load will handle it, but we should still dispose
                        // Actually, ms is always used either directly or indirectly, so we dispose it here
                        ms.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare import failed for " + path + ": " + ex.Message);
                // Don't fail the whole operation, just log
            }
        }

        /// <summary>
        /// Synchronizes the target group with the source group from the shared database.
        /// 
        /// For "Import" mode: Performs a destructive replace - clears target and copies all
        /// content from source. Any local modifications will be lost.
        /// 
        /// For "Synchronize" mode: Performs a non-destructive merge - adds new entries/groups
        /// from source, and updates existing entries/groups if the source version is newer
        /// (based on LastModificationTime). Local entries not in source are preserved.
        /// 
        /// The following properties of the target group are always preserved:
        /// - UUID (group identity)
        /// - Parent (group hierarchy)
        /// - Name (local group name)
        /// - Icon (local group icon)
        /// - CustomData (KeeShare configuration)
        /// </summary>
        /// <param name="source">Source group from the shared database</param>
        /// <param name="target">Target group in the local database</param>
        /// <param name="type">KeeShare type: "Import" or "Synchronize"</param>
        private void SyncGroups(PwGroup source, PwGroup target, string type)
        {
            if (type == "Synchronize")
            {
                // Non-destructive merge: add new items, update existing if newer
                MergeGroupContents(source, target);
            }
            else
            {
                // Import mode: destructive replace
                ImportGroupContents(source, target);
            }
            
            target.Touch(true, false);
        }

        /// <summary>
        /// Performs a destructive import: clears target and copies all content from source.
        /// </summary>
        private void ImportGroupContents(PwGroup source, PwGroup target)
        {
            // Clear entries and subgroups
            target.Entries.Clear();
            target.Groups.Clear();
            
            // Copy entries from source
            foreach (var entry in source.Entries)
            {
                target.AddEntry(entry.CloneDeep(), true);
            }
            
            // Copy subgroups from source
            foreach (var group in source.Groups)
            {
                target.AddGroup(group.CloneDeep(), true);
            }
        }

        /// <summary>
        /// Performs a non-destructive merge: adds new entries/groups from source,
        /// updates existing if source is newer. Local items not in source are preserved.
        /// </summary>
        private void MergeGroupContents(PwGroup source, PwGroup target)
        {
            // Merge entries
            foreach (var sourceEntry in source.Entries)
            {
                var targetEntry = target.FindEntry(sourceEntry.Uuid, false);
                if (targetEntry == null)
                {
                    // Entry doesn't exist in target - add it
                    target.AddEntry(sourceEntry.CloneDeep(), true);
                }
                else
                {
                    // Entry exists - update if source is newer
                    // AssignProperties with bOnlyIfNewer=true will only update if source.LastMod > target.LastMod
                    targetEntry.AssignProperties(sourceEntry, true, false, false);
                }
            }
            
            // Merge subgroups recursively
            foreach (var sourceGroup in source.Groups)
            {
                var targetGroup = target.FindGroup(sourceGroup.Uuid, false);
                if (targetGroup == null)
                {
                    // Group doesn't exist in target - add it
                    target.AddGroup(sourceGroup.CloneDeep(), true);
                }
                else
                {
                    // Group exists - update properties if source is newer, then merge contents
                    targetGroup.AssignProperties(sourceGroup, true, false);
                    MergeGroupContents(sourceGroup, targetGroup);
                }
            }
        }

        private IOConnectionInfo ResolvePath(string path)
        {
            // Check if absolute
            if (path.Contains("://") || path.StartsWith("/"))
            {
                return IOConnectionInfo.FromPath(path);
            }

            // Try relative to current DB
            try 
            {
                var currentIoc = _app.CurrentDb.Ioc;
                var storage = _app.GetFileStorage(currentIoc);
                
                // This is a bit hacky as GetParentPath is not always supported or returns something valid
                // But let's try.
                
                // If it's a local file, we can use Path.Combine
                if (currentIoc.IsLocalFile())
                {
                    string dir = Path.GetDirectoryName(currentIoc.Path);
                    string fullPath = Path.Combine(dir, path);
                    return IOConnectionInfo.FromPath(fullPath);
                }
                
                // For other storages, it depends.
                // Assume path is relative to same storage
                // Many storages don't support relative paths easily without full URL manipulation
                // For now, return as is if not local, or try to reconstruct
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Error resolving relative path for " + path + ": " + ex.ToString());
            }
            
            return IOConnectionInfo.FromPath(path);
        }

        private Stream OpenStream(IOConnectionInfo ioc)
        {
            try
            {
                var storage = _app.GetFileStorage(ioc);
                return storage.OpenFileForRead(ioc);
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("Failed to open stream for " + ioc.Path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Verifies the signature of a KeeShare file.
        /// Returns true if signature is valid and certificate is trusted, false otherwise.
        /// </summary>
        private bool VerifySignature(PwGroup group, byte[] kdbxData, byte[] signatureData)
        {
            // Get trusted certificate (public key) from group CustomData
            string trustedCert = group.CustomData.Get("KeeShare.TrustedCertificate");
            
            if (string.IsNullOrEmpty(trustedCert))
            {
                Kp2aLog.Log("KeeShare: No trusted certificate configured for group " + group.Name);
                return false;
            }

            bool result = VerifySignatureCore(trustedCert, kdbxData, signatureData);
            
            if (result)
            {
                Kp2aLog.Log("KeeShare: Signature verified successfully for group " + group.Name);
            }
            else
            {
                Kp2aLog.Log("KeeShare: Signature verification failed for group " + group.Name);
            }
            
            return result;
        }

        /// <summary>
        /// Core signature verification logic that can be used by both production code and tests.
        /// Verifies a signature using the provided trusted certificate, KDBX data, and signature data.
        /// </summary>
        /// <param name="trustedCertificate">The trusted certificate (public key) as base64-encoded DER or PEM format</param>
        /// <param name="kdbxData">The KDBX file data that was signed</param>
        /// <param name="signatureData">The signature data in KeeShare format ("rsa|&lt;hex&gt;")</param>
        /// <returns>True if signature is valid, false otherwise</returns>
        internal static bool VerifySignatureCore(string trustedCertificate, byte[]? kdbxData, byte[]? signatureData)
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

                // KeeShare signature format: "rsa|<hex>" where hex is the RSA signature
                // The signature is computed over the kdbx file data using SHA-256
                string signatureText = Encoding.UTF8.GetString(signatureData).Trim();
                
                // Remove any whitespace/newlines
                signatureText = signatureText.Replace("\r", "").Replace("\n", "").Replace(" ", "");
                
                // Strip "rsa|" prefix if present
                const string rsaPrefix = "rsa|";
                if (signatureText.StartsWith(rsaPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    signatureText = signatureText.Substring(rsaPrefix.Length);
                }
                
                // Hex-decode the signature
                byte[] signatureBytes;
                try
                {
                    signatureBytes = HexStringToBytes(signatureText);
                }
                catch (Exception)
                {
                    return false;
                }
                
                if (signatureBytes == null || signatureBytes.Length == 0)
                {
                    return false;
                }

                // Parse the trusted certificate (public key)
                // Format: PEM-encoded public key or base64-encoded DER
                byte[] publicKeyBytes;
                try
                {
                    // Try to decode as base64 first
                    if (trustedCertificate.Contains("-----BEGIN"))
                    {
                        // PEM format - extract base64 content
                        var lines = trustedCertificate.Split('\n');
                        var base64Lines = lines.Where(l => !l.Contains("BEGIN") && !l.Contains("END") && !string.IsNullOrWhiteSpace(l));
                        string base64Content = string.Join("", base64Lines).Trim();
                        publicKeyBytes = Convert.FromBase64String(base64Content);
                    }
                    else
                    {
                        // Assume it's already base64-encoded DER
                        publicKeyBytes = Convert.FromBase64String(trustedCertificate);
                    }
                }
                catch (Exception)
                {
                    return false;
                }

                // Create RSA object from public key
                // Use using block to ensure deterministic disposal even if exceptions occur
                bool isValid = false;
                bool importFailed = false;
                using (RSA rsa = RSA.Create())
                {
                    // Try importing as SubjectPublicKeyInfo (standard format)
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
                            // Try importing as RSAPublicKey (PKCS#1 format)
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
                        // Compute hash of kdbx data
                        byte[] hash;
                        using (SHA256 sha256 = SHA256.Create())
                        {
                            hash = sha256.ComputeHash(kdbxData);
                        }

                        // Verify signature
                        isValid = rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    }
                }

                // Return false if import failed, otherwise return verification result
                return !importFailed && isValid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Converts a hexadecimal string to a byte array.
        /// </summary>
        /// <param name="hex">The hexadecimal string (case-insensitive, no separators)</param>
        /// <returns>The decoded byte array, or null if the input is invalid</returns>
        private static byte[]? HexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return null;
            }

            // Hex string must have even length
            if (hex.Length % 2 != 0)
            {
                return null;
            }

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                string byteStr = hex.Substring(i * 2, 2);
                if (!byte.TryParse(byteStr, System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                {
                    return null;
                }
            }

            return bytes;
        }
    }
}
