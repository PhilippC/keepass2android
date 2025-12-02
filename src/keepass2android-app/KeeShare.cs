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
        /// <summary>
        /// Checks for KeeShare groups and processes them. This is called after database load
        /// and can also be triggered by "Synchronize Database" action.
        /// Uses non-blocking background operations for fast database loading (similar to database sync in 1.15+).
        /// </summary>
        /// <param name="app">The application instance</param>
        /// <param name="nextHandler">Handler to call when operation completes</param>
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
            OperationRunner.Instance.Run(app, op);
        }

        /// <summary>
        /// Triggers KeeShare synchronization in the background. 
        /// Called when user selects "Synchronize Database".
        /// </summary>
        public static void SyncInBackground(IKp2aApp app, OnOperationFinishedHandler onFinished)
        {
            Check(app, onFinished);
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

        /// <summary>
        /// Checks if a group is read-only because it's a KeeShare Import group
        /// or is contained within one. Import groups replace their contents on sync,
        /// so local modifications would be lost.
        /// </summary>
        public static bool IsReadOnlyBecauseKeeShareImport(PwGroup group)
        {
            if (group == null) return false;

            PwGroup current = group;
            while (current != null)
            {
                if (current.CustomData.Get("KeeShare.Active") == "true" &&
                    current.CustomData.Get("KeeShare.Type") == "Import")
                {
                    return true;
                }
                current = current.ParentGroup;
            }
            return false;
        }

        /// <summary>
        /// Checks if an entry is read-only because it's in a KeeShare Import group.
        /// </summary>
        public static bool IsReadOnlyBecauseKeeShareImport(PwEntry entry)
        {
            return entry?.ParentGroup != null && IsReadOnlyBecauseKeeShareImport(entry.ParentGroup);
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
                }
            }

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
                    
                    MemoryStream ms = new MemoryStream();
                    try
                    {
                        s.CopyTo(ms);
                        ms.Position = 0;

                        Stream kdbxStream = ms;
                        MemoryStream kdbxMem = null;
                        bool isZip = false;
                        
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
                                    var kdbxEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".kdbx", StringComparison.OrdinalIgnoreCase));
                                    if (kdbxEntry == null)
                                    {
                                        Kp2aLog.Log("KeeShare: No .kdbx file found in ZIP archive");
                                        return;
                                    }

                                    kdbxMem = new MemoryStream();
                                    using (var es = kdbxEntry.Open())
                                    {
                                        es.CopyTo(kdbxMem);
                                    }
                                    
                                    byte[] kdbxData = kdbxMem.ToArray();
                                    
                                    var sigEntry = archive.Entries.FirstOrDefault(e => 
                                        e.Name.EndsWith(".sig", StringComparison.OrdinalIgnoreCase) ||
                                        e.Name.EndsWith(".signature", StringComparison.OrdinalIgnoreCase));
                                    
                                    string trustedCert = targetGroup.CustomData.Get("KeeShare.TrustedCertificate");
                                    bool hasTrustedCert = !string.IsNullOrEmpty(trustedCert);
                                    
                                    if (sigEntry != null)
                                    {
                                        if (hasTrustedCert)
                                        {
                                            byte[] signatureData;
                                            using (var sigStream = sigEntry.Open())
                                            using (var sigMem = new MemoryStream())
                                            {
                                                sigStream.CopyTo(sigMem);
                                                signatureData = sigMem.ToArray();
                                            }

                                            if (!VerifySignature(targetGroup, kdbxData, signatureData))
                                            {
                                                Kp2aLog.Log("KeeShare: Signature verification failed or certificate not trusted for group " + targetGroup.Name + ". Skipping import.");
                                                return;
                                            }
                                            else
                                            {
                                                Kp2aLog.Log("KeeShare: Signature verified successfully for group " + targetGroup.Name);
                                            }
                                        }
                                        else
                                        {
                                            Kp2aLog.Log("KeeShare: Signature file found but no trusted certificate configured for group " + targetGroup.Name + ". Continuing without signature verification (backward compatibility).");
                                        }
                                    }
                                    else
                                    {
                                        if (hasTrustedCert)
                                        {
                                            Kp2aLog.Log("KeeShare: Trusted certificate is configured but no signature file found in ZIP archive for group " + targetGroup.Name + ". Skipping import for security.");
                                            return;
                                        }
                                        else
                                        {
                                            Kp2aLog.Log("KeeShare: No signature file found in ZIP archive for group " + targetGroup.Name + ". Continuing without signature verification (backward compatibility).");
                                        }
                                    }
                                    
                                    kdbxMem.Position = 0;
                                    kdbxStream = kdbxMem;
                                }
                            }
                            catch (Exception ex)
                            {
                                Kp2aLog.Log("Failed to treat file as zip: " + ex.Message);
                                ms.Position = 0;
                                kdbxStream = ms;
                            }
                        }

                        PwDatabase shareDb = null;
                        try
                        {
                            shareDb = new PwDatabase();
                            CompositeKey key = new CompositeKey();
                            if (!string.IsNullOrEmpty(password))
                            {
                                key.AddUserKey(new KcpPassword(password));
                            }
                            if (key.UserKeys.Count() == 0)
                                 key.AddUserKey(new KcpPassword(""));

                            KdbxFile kdbx = new KdbxFile(shareDb);
                            kdbx.Load(kdbxStream, KdbxFormat.Default, key);

                            SyncGroups(shareDb, targetGroup, type);
                        }
                        finally
                        {
                            shareDb?.Close();
                            
                            if (kdbxMem != null && kdbxMem != ms)
                            {
                                kdbxMem.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        ms.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare import failed for group " + targetGroup.Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Synchronizes the target group with the source database using PwDatabase.MergeIn.
        /// 
        /// For "Import" mode: Performs a destructive replace - clears target group first,
        /// then merges with OverwriteExisting. Any local modifications will be lost.
        /// 
        /// For "Synchronize" mode: Uses MergeIn with Synchronize method - adds new entries/groups
        /// from source, updates existing if source is newer. Handles entry history, deletions,
        /// and relocations properly.
        /// 
        /// Implementation note: MergeIn uses reference comparison for the source root group
        /// (pgSourceParent == pdSource.m_pgRootGroup), so we can't just swap UUIDs. Instead,
        /// we restructure the source database by creating a wrapper group with the target
        /// group's UUID and moving all content into it. This ensures MergeIn's FindGroup()
        /// call will find the correct local container.
        /// </summary>
        private void SyncGroups(PwDatabase shareDb, PwGroup targetGroup, string type)
        {
            PwDatabase mainDb = _app.CurrentDb.KpDatabase;
            
            // Save target group's CustomData before MergeIn - MergeIn will overwrite group
            // properties with the wrapper group's (empty) CustomData, losing KeeShare config.
            var savedCustomData = new Dictionary<string, string>();
            foreach (var kvp in targetGroup.CustomData)
            {
                savedCustomData[kvp.Key] = kvp.Value;
            }
            
            // Create a wrapper group with the target group's UUID.
            // MergeIn uses FindGroup(parentUuid) for non-root parents, so this ensures
            // content is placed in the target KeeShare group, not the database root.
            PwGroup wrapperGroup = new PwGroup(false, false);
            wrapperGroup.Uuid = targetGroup.Uuid;
            wrapperGroup.Name = targetGroup.Name;
            
            // Copy all important properties from target group to wrapper group.
            // When MergeIn processes the wrapper group, it will call AssignProperties
            // which copies properties from wrapper to target. By copying target's properties
            // to wrapper first, we ensure MergeIn preserves the target group's properties
            // (Icon, IconUuid, Notes, Tags, etc.) instead of overwriting them with defaults.
            wrapperGroup.Notes = targetGroup.Notes;
            wrapperGroup.IconId = targetGroup.IconId;
            wrapperGroup.CustomIconUuid = targetGroup.CustomIconUuid;
            wrapperGroup.DefaultAutoTypeSequence = targetGroup.DefaultAutoTypeSequence;
            wrapperGroup.EnableAutoType = targetGroup.EnableAutoType;
            wrapperGroup.EnableSearching = targetGroup.EnableSearching;
            wrapperGroup.Expires = targetGroup.Expires;
            wrapperGroup.ExpiryTime = targetGroup.ExpiryTime;
            wrapperGroup.LastTopVisibleEntry = targetGroup.LastTopVisibleEntry;
            foreach (string tag in targetGroup.Tags)
            {
                wrapperGroup.Tags.Add(tag);
            }
            
            // Move all entries from source root to wrapper group.
            // Use bTakeOwnership=true so entry.ParentGroup points to wrapperGroup,
            // otherwise MergeIn's check (pgSourceParent == pdSource.m_pgRootGroup) will
            // match and place entries in the database root instead of targetGroup.
            while (shareDb.RootGroup.Entries.UCount > 0)
            {
                PwEntry entry = shareDb.RootGroup.Entries.GetAt(0);
                shareDb.RootGroup.Entries.RemoveAt(0);
                wrapperGroup.AddEntry(entry, true);
            }
            
            // Move all subgroups from source root to wrapper group (bTakeOwnership=true)
            while (shareDb.RootGroup.Groups.UCount > 0)
            {
                PwGroup group = shareDb.RootGroup.Groups.GetAt(0);
                shareDb.RootGroup.Groups.RemoveAt(0);
                wrapperGroup.AddGroup(group, true);
            }
            
            // Add wrapper group as child of source root (bTakeOwnership=true)
            shareDb.RootGroup.AddGroup(wrapperGroup, true);
            
            if (type == "Import")
            {
                // For Import mode: clear target first, then merge with OverwriteExisting
                ClearGroupContents(targetGroup);
                mainDb.MergeIn(shareDb, PwMergeMethod.OverwriteExisting);
            }
            else
            {
                // For Synchronize mode: use MergeIn with Synchronize
                // This handles: update if newer, add new items, handle history, handle deletions
                mainDb.MergeIn(shareDb, PwMergeMethod.Synchronize);
            }
            
            // Restore target group's CustomData (KeeShare configuration)
            // MergeIn would have overwritten it with the wrapper's empty CustomData
            foreach (var kvp in savedCustomData)
            {
                targetGroup.CustomData.Set(kvp.Key, kvp.Value);
            }
            
            targetGroup.Touch(true, false);
        }

        /// <summary>
        /// Clears all entries and subgroups from a group (for Import mode).
        /// </summary>
        private void ClearGroupContents(PwGroup group)
        {
            group.Entries.Clear();
            group.Groups.Clear();
        }

        private IOConnectionInfo ResolvePath(string path)
        {
            if (path.Contains("://") || path.StartsWith("/"))
            {
                return IOConnectionInfo.FromPath(path);
            }

            try 
            {
                var currentIoc = _app.CurrentDb.Ioc;
                var storage = _app.GetFileStorage(currentIoc);
                
                if (currentIoc.IsLocalFile())
                {
                    string dir = Path.GetDirectoryName(currentIoc.Path);
                    string fullPath = Path.Combine(dir, path);
                    return IOConnectionInfo.FromPath(fullPath);
                }
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Error resolving relative path: " + ex.GetType().Name);
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
                Kp2aLog.Log("Failed to open KeeShare stream: " + ex.GetType().Name + " - " + ex.Message);
                return null;
            }
        }

        private bool VerifySignature(PwGroup group, byte[] kdbxData, byte[] signatureData)
        {
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

        internal static bool VerifySignatureCore(string trustedCertificate, byte[] kdbxData, byte[] signatureData)
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
                    signatureBytes = MemUtil.HexStringToByteArray(signatureText);
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
    }
}
