using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace keepass2android.KeeShare
{
    /// <summary>
    /// Result of a KeeShare import operation
    /// </summary>
    public class KeeShareImportResult
    {
        public enum StatusCode
        {
            Success,
            FileNotFound,
            SignatureInvalid,
            SignerNotTrusted,
            PasswordIncorrect,
            MergeFailed,
            Error
        }
        
        public StatusCode Status { get; set; }
        public string Message { get; set; }
        public string SharePath { get; set; }
        public string SignerName { get; set; }
        public string KeyFingerprint { get; set; }
        public int EntriesImported { get; set; }
        
        public bool IsSuccess => Status == StatusCode.Success;
    }
    
    public class KeeShareImporter
    {
        private const string SignatureFileName = "container.share.signature";
        private const string ContainerFileName = "container.share.kdbx";

        /// <summary>
        /// Checks all groups for KeeShare references and imports them.
        /// Uses default behavior (rejects untrusted signers, no UI).
        /// </summary>
        public static List<KeeShareImportResult> CheckAndImport(Database db, IKp2aApp app)
        {
            return CheckAndImport(db, app, null);
        }
        
        /// <summary>
        /// Checks all groups for KeeShare references and imports them.
        /// Returns a list of import results for each share that was processed.
        /// </summary>
        /// <param name="db">The database to check for KeeShare references</param>
        /// <param name="app">The app context for file access</param>
        /// <param name="userInteraction">Optional UI handler for trust prompts. If null, untrusted signers are rejected.</param>
        public static List<KeeShareImportResult> CheckAndImport(Database db, IKp2aApp app, IKeeShareUserInteraction userInteraction)
        {
            var results = new List<KeeShareImportResult>();
            var handler = userInteraction ?? new DefaultKeeShareUserInteraction();
            
            // Check if auto-import is enabled
            if (!handler.IsAutoImportEnabled)
            {
                Kp2aLog.Log("KeeShare: Auto-import disabled by user preference");
                return results;
            }
            
            if (db == null || db.Root == null) return results;

            // Iterate over all groups to find share references
            var groupsToProcess = new List<Tuple<PwGroup, KeeShareSettings.Reference>>();

            // Collect groups first to avoid modification during iteration if that were an issue (though we only merge content)
            var allGroups = db.Root.GetGroups(true);
            allGroups.Add(db.Root); // Include root? Usually shares are sub-groups.

            foreach (var group in allGroups)
            {
                var reference = KeeShareSettings.GetReference(group);
                if (reference != null && reference.IsImporting)
                {
                    groupsToProcess.Add(new Tuple<PwGroup, KeeShareSettings.Reference>(group, reference));
                }
            }

            foreach (var tuple in groupsToProcess)
            {
                var group = tuple.Item1;
                var reference = tuple.Item2;
                var result = ImportShare(db, app, group, reference);
                results.Add(result);
                
                // Log result
                if (result.IsSuccess)
                {
                    Kp2aLog.Log($"KeeShare: Successfully imported from {result.SharePath}");
                }
                else
                {
                    Kp2aLog.Log($"KeeShare: Import failed for {result.SharePath}: {result.Message}");
                }
            }
            
            return results;
        }

        private static KeeShareImportResult ImportShare(Database db, IKp2aApp app, PwGroup targetGroup, KeeShareSettings.Reference reference)
        {
            var result = new KeeShareImportResult
            {
                SharePath = reference.Path,
                Status = KeeShareImportResult.StatusCode.Error
            };
            
            try
            {
                // Resolve Path
                string path = reference.Path;

                IOConnectionInfo ioc = ResolvePath(db.Ioc, path, app);
                if (ioc == null)
                {
                    result.Status = KeeShareImportResult.StatusCode.FileNotFound;
                    result.Message = "Could not resolve share path";
                    return result;
                }

                byte[] dbData = null;
                KeeShareSignature signature = null;

                IFileStorage storage = app.GetFileStorage(ioc);

                using (var stream = storage.OpenFileForRead(ioc))
                {
                    if (stream == null)
                    {
                        result.Status = KeeShareImportResult.StatusCode.FileNotFound;
                        result.Message = "Share file not found or cannot be opened";
                        return result;
                    }

                    // Read into memory because we might need random access (Zip) or read twice
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        ms.Position = 0;

                        if (IsZipFile(ms))
                        {
                            var containerResult = ReadFromContainer(ms, reference, db.KpDatabase);
                            dbData = containerResult.Item1;
                            signature = containerResult.Item2;
                            
                            if (containerResult.Item3 != null) // Error status
                            {
                                result.Status = containerResult.Item3.Value;
                                result.Message = containerResult.Item4;
                                result.SignerName = signature?.Signer;
                                result.KeyFingerprint = containerResult.Item5;
                                return result;
                            }
                        }
                        else
                        {
                            // Assume plain KDBX (no signature verification for non-container files)
                            dbData = ms.ToArray();
                        }
                    }
                }

                if (dbData != null)
                {
                    int entriesBeforeMerge = CountEntries(targetGroup);
                    
                    var mergeResult = MergeDatabase(db, targetGroup, dbData, reference.Password);
                    if (!mergeResult.Item1)
                    {
                        result.Status = mergeResult.Item2;
                        result.Message = mergeResult.Item3;
                        return result;
                    }
                    
                    int entriesAfterMerge = CountEntries(targetGroup);
                    
                    result.Status = KeeShareImportResult.StatusCode.Success;
                    result.Message = "Import completed successfully";
                    result.SignerName = signature?.Signer;
                    result.EntriesImported = Math.Max(0, entriesAfterMerge - entriesBeforeMerge);
                }
                else
                {
                    result.Status = KeeShareImportResult.StatusCode.Error;
                    result.Message = "No database data found in share";
                }
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare Import Error: " + ex.Message);
                result.Status = KeeShareImportResult.StatusCode.Error;
                result.Message = ex.Message;
            }
            
            LogAudit(result);
            return result;
        }

        private static void LogAudit(KeeShareImportResult result)
        {
            var action = result.IsSuccess 
                ? KeeShareAuditLog.AuditAction.ImportSuccess 
                : KeeShareAuditLog.AuditAction.ImportFailure;
            
            KeeShareAuditLog.Log(action, result.SharePath, 
                result.IsSuccess ? $"Imported {result.EntriesImported} entries" : result.Message, 
                result.KeyFingerprint);
        }
        
        private static int CountEntries(PwGroup group)
        {
            int count = group.Entries.UCount;
            foreach (var subgroup in group.Groups)
            {
                count += CountEntries(subgroup);
            }
            return (int)count;
        }

        private static bool IsZipFile(Stream stream)
        {
            if (stream.Length < 4) return false;
            var buf = new byte[4];
            stream.Read(buf, 0, 4);
            stream.Position = 0;
            return buf[0] == 0x50 && buf[1] == 0x4B && buf[2] == 0x03 && buf[3] == 0x04;
        }

        /// <summary>
        /// Reads and verifies a .share container
        /// Returns: (dbData, signature, errorStatus, errorMessage, keyFingerprint)
        /// </summary>
        private static Tuple<byte[], KeeShareSignature, KeeShareImportResult.StatusCode?, string, string> 
            ReadFromContainer(MemoryStream zipStream, KeeShareSettings.Reference reference, PwDatabase database)
        {
            try
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    var sigEntry = archive.GetEntry(SignatureFileName);
                    var dbEntry = archive.GetEntry(ContainerFileName);

                    if (dbEntry == null)
                    {
                        return Tuple.Create<byte[], KeeShareSignature, KeeShareImportResult.StatusCode?, string, string>(
                            null, null, KeeShareImportResult.StatusCode.Error, "Container missing kdbx file", null);
                    }

                    byte[] dbData;
                    using (var s = dbEntry.Open())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        dbData = ms.ToArray();
                    }

                    KeeShareSignature signature = null;
                    string keyFingerprint = null;
                    
                    if (sigEntry != null)
                    {
                        string sigXml;
                        using (var s = sigEntry.Open())
                        using (var sr = new StreamReader(s, Encoding.UTF8))
                        {
                            sigXml = sr.ReadToEnd();
                        }

                        signature = KeeShareSignature.Parse(sigXml);
                        
                        // Verify signature
                        var verifyResult = VerifySignatureWithTrust(dbData, signature, database);
                        if (!verifyResult.Item1)
                        {
                            return Tuple.Create<byte[], KeeShareSignature, KeeShareImportResult.StatusCode?, string, string>(
                                null, signature, verifyResult.Item2, verifyResult.Item3, verifyResult.Item4);
                        }
                        
                        keyFingerprint = verifyResult.Item4;
                    }

                    return Tuple.Create<byte[], KeeShareSignature, KeeShareImportResult.StatusCode?, string, string>(
                        dbData, signature, null, null, keyFingerprint);
                }
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Error reading container: " + ex.Message);
                return Tuple.Create<byte[], KeeShareSignature, KeeShareImportResult.StatusCode?, string, string>(
                    null, null, KeeShareImportResult.StatusCode.Error, "Error reading container: " + ex.Message, null);
            }
        }

        /// <summary>
        /// Verifies signature and checks if the signer is trusted.
        /// Returns: (success, errorStatus, errorMessage, keyFingerprint)
        /// </summary>
        private static Tuple<bool, KeeShareImportResult.StatusCode, string, string> 
            VerifySignatureWithTrust(byte[] data, KeeShareSignature sig, PwDatabase database)
        {
            if (sig == null || sig.Key == null || string.IsNullOrEmpty(sig.Signature))
            {
                return Tuple.Create(false, KeeShareImportResult.StatusCode.SignatureInvalid, 
                    "Missing or incomplete signature", (string)null);
            }

            try
            {
                var rsaParams = sig.Key.Value;
                
                // Calculate key fingerprint
                string keyFingerprint = KeeShareTrustSettings.CalculateKeyFingerprint(rsaParams.Modulus, rsaParams.Exponent);
                
                // Check if key is trusted
                var trustSettings = new KeeShareTrustSettings(database);
                if (!trustSettings.IsKeyTrusted(keyFingerprint))
                {
                    // Key not trusted - reject import and require explicit trust
                    // The caller should surface this to the user with fingerprint info
                    string shortFingerprint = keyFingerprint?.Length >= 16 
                        ? keyFingerprint.Substring(0, 16) + "..." 
                        : keyFingerprint;
                    Kp2aLog.Log($"KeeShare: Rejected untrusted signer '{sig.Signer}' with fingerprint {shortFingerprint}");
                    KeeShareAuditLog.Log(KeeShareAuditLog.AuditAction.TrustDecision, "Unknown", 
                        $"Rejected untrusted signer '{sig.Signer}'", keyFingerprint);
                    
                    return Tuple.Create(false, KeeShareImportResult.StatusCode.SignerNotTrusted,
                        $"Signer '{sig.Signer}' is not trusted. Fingerprint: {shortFingerprint}. " +
                        "Add this key to trusted keys to allow import.", keyFingerprint);
                }
                
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(rsaParams);

                    // Signature format is "rsa|HEX_ENCODED_SIGNATURE"
                    if (!sig.Signature.StartsWith("rsa|"))
                    {
                        return Tuple.Create(false, KeeShareImportResult.StatusCode.SignatureInvalid,
                            "Invalid signature format (expected rsa|...)", keyFingerprint);
                    }
                    
                    var hexSig = sig.Signature.Substring(4);
                    var sigBytes = HexStringToByteArray(hexSig);
                    if (sigBytes == null || sigBytes.Length == 0)
                    {
                        Kp2aLog.Log("KeeShare: Invalid signature format (hex parsing failed)");
                        return Tuple.Create(false, KeeShareImportResult.StatusCode.SignatureInvalid,
                            "Invalid signature format (hex parsing failed)", keyFingerprint);
                    }

                    bool isValid = rsa.VerifyData(data, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    if (!isValid)
                    {
                        return Tuple.Create(false, KeeShareImportResult.StatusCode.SignatureInvalid,
                            "Signature verification failed - data may have been tampered with", keyFingerprint);
                    }
                    
                    return Tuple.Create(true, KeeShareImportResult.StatusCode.Success, (string)null, keyFingerprint);
                }
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Verification exception: " + ex.Message);
                return Tuple.Create(false, KeeShareImportResult.StatusCode.SignatureInvalid,
                    "Signature verification error: " + ex.Message, (string)null);
            }
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0) return null;
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Merges the shared database into the target group.
        /// Returns: (success, errorStatus, errorMessage)
        /// </summary>
        private static Tuple<bool, KeeShareImportResult.StatusCode, string> 
            MergeDatabase(Database mainDb, PwGroup targetGroup, byte[] dbData, string password)
        {
            var pwDatabase = new PwDatabase();
            var compKey = new CompositeKey();
            if (!string.IsNullOrEmpty(password))
            {
                compKey.AddUserKey(new KcpPassword(password));
            }

            try
            {
                using (var ms = new MemoryStream(dbData))
                {
                    pwDatabase.Open(ms, compKey, null);
                }
            }
            catch (KeePassLib.Keys.InvalidCompositeKeyException)
            {
                return Tuple.Create(false, KeeShareImportResult.StatusCode.PasswordIncorrect,
                    "Incorrect password for shared database");
            }
            catch (Exception ex)
            {
                return Tuple.Create(false, KeeShareImportResult.StatusCode.Error,
                    "Failed to open shared database: " + ex.Message);
            }

            try
            {
                // Clone the target group structure for safe merging
                // This avoids sharing PwGroup instances across database objects
                var tempDb = new PwDatabase();
                tempDb.New(new IOConnectionInfo(), new CompositeKey(), "Temp");
                
                // Create a clone of the target group for the temp database
                var clonedGroup = targetGroup.CloneDeep();
                clonedGroup.ParentGroup = null;
                tempDb.RootGroup = clonedGroup;

                // Sync deleted objects and icons so MergeIn works correctly with existing state
                if (mainDb.KpDatabase.DeletedObjects != null)
                {
                    foreach (var del in mainDb.KpDatabase.DeletedObjects)
                    {
                        tempDb.DeletedObjects.Add(del);
                    }
                }

                if (mainDb.KpDatabase.CustomIcons != null)
                {
                    foreach (var icon in mainDb.KpDatabase.CustomIcons)
                    {
                        tempDb.CustomIcons.Add(icon);
                    }
                }

                // Ensure root UUID matches so MergeIn finds the root (targetGroup)
                if (!pwDatabase.RootGroup.Uuid.Equals(clonedGroup.Uuid))
                {
                    pwDatabase.RootGroup.Uuid = clonedGroup.Uuid;
                }

                // Perform the merge on the cloned group
                tempDb.MergeIn(pwDatabase, PwMergeMethod.Synchronize);

                // Now apply changes from cloned group back to target group
                // Clear target group and copy merged content
                targetGroup.Entries.Clear();
                foreach (var entry in clonedGroup.Entries)
                {
                    entry.ParentGroup = targetGroup;
                    targetGroup.Entries.Add(entry);
                }
                
                // Handle subgroups - update existing or add new
                var existingGroups = new Dictionary<PwUuid, PwGroup>();
                foreach (var g in targetGroup.Groups)
                {
                    existingGroups[g.Uuid] = g;
                }
                
                foreach (var mergedGroup in clonedGroup.Groups)
                {
                    if (existingGroups.TryGetValue(mergedGroup.Uuid, out var existing))
                    {
                        // Update existing group
                        CopyGroupContent(mergedGroup, existing);
                    }
                    else
                    {
                        // Add new group
                        mergedGroup.ParentGroup = targetGroup;
                        targetGroup.Groups.Add(mergedGroup);
                    }
                }

                // Propagate deleted objects back to mainDb
                if (mainDb.KpDatabase.DeletedObjects != null)
                {
                    var existingDeleted = new HashSet<PwUuid>();
                    foreach (var del in mainDb.KpDatabase.DeletedObjects)
                    {
                        existingDeleted.Add(del.Uuid);
                    }
                    
                    foreach (var del in tempDb.DeletedObjects)
                    {
                        if (!existingDeleted.Contains(del.Uuid))
                        {
                            mainDb.KpDatabase.DeletedObjects.Add(del);
                        }
                    }
                }

                // Propagate custom icons back to mainDb
                if (mainDb.KpDatabase.CustomIcons != null)
                {
                    var existingIcons = new HashSet<PwUuid>();
                    foreach (var icon in mainDb.KpDatabase.CustomIcons)
                    {
                        existingIcons.Add(icon.Uuid);
                    }
                    
                    foreach (var icon in tempDb.CustomIcons)
                    {
                        if (!existingIcons.Contains(icon.Uuid))
                        {
                            mainDb.KpDatabase.CustomIcons.Add(icon);
                        }
                    }

                    if (tempDb.UINeedsIconUpdate)
                        mainDb.KpDatabase.UINeedsIconUpdate = true;
                }

                // Update globals
                mainDb.UpdateGlobals();
                
                return Tuple.Create(true, KeeShareImportResult.StatusCode.Success, (string)null);
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Merge failed: " + ex.Message);
                return Tuple.Create(false, KeeShareImportResult.StatusCode.MergeFailed,
                    "Merge failed: " + ex.Message);
            }
        }
        
        private static void CopyGroupContent(PwGroup source, PwGroup target)
        {
            // Update entries
            target.Entries.Clear();
            foreach (var entry in source.Entries)
            {
                entry.ParentGroup = target;
                target.Entries.Add(entry);
            }
            
            // Recursively update subgroups
            var existingSubgroups = new Dictionary<PwUuid, PwGroup>();
            foreach (var g in target.Groups)
            {
                existingSubgroups[g.Uuid] = g;
            }
            
            foreach (var subgroup in source.Groups)
            {
                if (existingSubgroups.TryGetValue(subgroup.Uuid, out var existing))
                {
                    CopyGroupContent(subgroup, existing);
                }
                else
                {
                    subgroup.ParentGroup = target;
                    target.Groups.Add(subgroup);
                }
            }
        }

        private static IOConnectionInfo ResolvePath(IOConnectionInfo baseIoc, string path, IKp2aApp app)
        {
            var ioc = new IOConnectionInfo();
            ioc.Path = path;

            // Check if absolute
            if (path.StartsWith("/") || path.Contains("://"))
            {
                 if (!path.Contains("://"))
                 {
                     ioc.Path = path;
                     ioc.Plugin = "file";
                 }
                 return ioc;
            }

            // Relative path.
            try
            {
                string basePath = baseIoc.Path;
                string dir = Path.GetDirectoryName(basePath);
                string fullPath = Path.Combine(dir, path);
                ioc.Path = fullPath;
                ioc.Plugin = baseIoc.Plugin;
                ioc.UserName = baseIoc.UserName;
                ioc.Password = baseIoc.Password;
                return ioc;
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Failed to resolve path: " + ex.Message);
                return null;
            }
        }
    }
}
