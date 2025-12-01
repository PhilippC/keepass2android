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

        private static bool HasKeeShareGroups(PwGroup group)
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
                Import(group, path, password);
            }
        }

        private void Import(PwGroup targetGroup, string path, string password)
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
                        try
                        {
                            PwDatabase shareDb = new PwDatabase();
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
                            SyncGroups(shareDb.RootGroup, targetGroup);
                        }
                        finally
                        {
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

        private void SyncGroups(PwGroup source, PwGroup target)
        {
            // For minimal implementation: clear target and copy source
            // But we must preserve the CustomData of the target group (KeeShare config)!
            // And Name/Icon/Uuid of the target group should probably stay?
            // KeeShare says: "The group in your database is synchronized with the Shared Database"
            
            // We should keep: UUID, Parent, Name, Icon (maybe?), CustomData
            
            // Clear entries and subgroups
            target.Entries.Clear();
            target.Groups.Clear();
            
            // Copy entries
            foreach (var entry in source.Entries)
            {
                target.AddEntry(entry.CloneDeep(), true);
            }
            
            // Copy subgroups
            foreach (var group in source.Groups)
            {
                target.AddGroup(group.CloneDeep(), true);
            }
            
            // We might want to update Name/Icon/Notes from source if they changed?
            // For now, keeping it simple (only content).
            
            target.Touch(true, false);
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
            catch {}
            
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
            try
            {
                // Get trusted certificate (public key) from group CustomData
                string trustedCert = group.CustomData.Get("KeeShare.TrustedCertificate");
                
                if (string.IsNullOrEmpty(trustedCert))
                {
                    Kp2aLog.Log("KeeShare: No trusted certificate configured for group " + group.Name);
                    return false;
                }

                if (signatureData == null || signatureData.Length == 0)
                {
                    Kp2aLog.Log("KeeShare: Signature file is empty");
                    return false;
                }

                if (kdbxData == null || kdbxData.Length == 0)
                {
                    Kp2aLog.Log("KeeShare: KDBX data is empty");
                    return false;
                }

                // KeeShare signature format: base64-encoded RSA signature
                // The signature is computed over the kdbx file data using SHA-256
                string signatureText = Encoding.UTF8.GetString(signatureData).Trim();
                
                // Remove any whitespace/newlines
                signatureText = signatureText.Replace("\r", "").Replace("\n", "").Replace(" ", "");
                
                byte[] signatureBytes;
                try
                {
                    signatureBytes = Convert.FromBase64String(signatureText);
                }
                catch (Exception ex)
                {
                    Kp2aLog.Log("KeeShare: Failed to decode base64 signature: " + ex.Message);
                    return false;
                }

                // Parse the trusted certificate (public key)
                // Format: PEM-encoded public key or base64-encoded DER
                byte[] publicKeyBytes;
                try
                {
                    // Try to decode as base64 first
                    if (trustedCert.Contains("-----BEGIN"))
                    {
                        // PEM format - extract base64 content
                        var lines = trustedCert.Split('\n');
                        var base64Lines = lines.Where(l => !l.Contains("BEGIN") && !l.Contains("END") && !string.IsNullOrWhiteSpace(l));
                        string base64Content = string.Join("", base64Lines).Trim();
                        publicKeyBytes = Convert.FromBase64String(base64Content);
                    }
                    else
                    {
                        // Assume it's already base64-encoded DER
                        publicKeyBytes = Convert.FromBase64String(trustedCert);
                    }
                }
                catch (Exception ex)
                {
                    Kp2aLog.Log("KeeShare: Failed to parse trusted certificate: " + ex.Message);
                    return false;
                }

                // Create RSA object from public key
                RSA rsa = RSA.Create();
                try
                {
                    // Try importing as SubjectPublicKeyInfo (standard format)
                    rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out int bytesRead);
                    if (bytesRead == 0)
                    {
                        throw new CryptographicException("No bytes read from public key");
                    }
                }
                catch (Exception ex)
                {
                    Kp2aLog.Log("KeeShare: Failed to import public key as SubjectPublicKeyInfo: " + ex.Message);
                    try
                    {
                        // Try importing as RSAPublicKey (PKCS#1 format)
                        rsa.ImportRSAPublicKey(publicKeyBytes, out int bytesRead);
                        if (bytesRead == 0)
                        {
                            throw new CryptographicException("No bytes read from public key");
                        }
                    }
                    catch (Exception ex2)
                    {
                        Kp2aLog.Log("KeeShare: Failed to import public key as RSAPublicKey: " + ex2.Message);
                        rsa.Dispose();
                        return false;
                    }
                }

                // Compute hash of kdbx data
                byte[] hash;
                using (SHA256 sha256 = SHA256.Create())
                {
                    hash = sha256.ComputeHash(kdbxData);
                }

                // Verify signature
                bool isValid = false;
                try
                {
                    isValid = rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    
                    if (isValid)
                    {
                        Kp2aLog.Log("KeeShare: Signature verified successfully for group " + group.Name);
                    }
                    else
                    {
                        Kp2aLog.Log("KeeShare: Signature verification failed for group " + group.Name);
                    }
                }
                finally
                {
                    rsa.Dispose();
                }

                return isValid;
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Error verifying signature: " + ex.Message);
                Kp2aLog.Log("KeeShare: Stack trace: " + ex.StackTrace);
                return false;
            }
        }
    }
}
