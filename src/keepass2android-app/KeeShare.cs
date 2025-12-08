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
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace keepass2android
{
    /// <summary>
    /// Represents a KeeShare group item for UI display and configuration.
    /// </summary>
    public sealed class KeeShareItem
    {
        public PwGroup Group { get; }
        public PwDatabase Database { get; }

        public KeeShareItem(PwGroup group, PwDatabase database)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public string Type => Group.CustomData.Get("KeeShare.Type") ?? "";
        public string OriginalPath => Group.CustomData.Get("KeeShare.FilePath") ?? "";
        public string Password => Group.CustomData.Get("KeeShare.Password") ?? "";
        public bool IsActive => Group.CustomData.Get("KeeShare.Active") == "true";
    }

    public class KeeShare
    {
        /// <summary>
        /// Custom data key prefix for device-specific file paths.
        /// Format: KeeShare.FilePath.{DeviceId}
        /// </summary>
        public const string DeviceFilePathKeyPrefix = "KeeShare.FilePath.";

        /// <summary>
        /// Gets the device-specific custom data key for storing file paths.
        /// </summary>
        public static string GetDeviceFilePathKey()
        {
            return DeviceFilePathKeyPrefix + KeeAutoExecExt.ThisDeviceId;
        }

        /// <summary>
        /// Gets the effective file path for a KeeShare group on this device.
        /// First checks for a device-specific path, then falls back to the original path.
        /// </summary>
        public static string GetEffectiveFilePath(PwGroup group)
        {
            if (group == null) return null;

            string deviceKey = GetDeviceFilePathKey();
            string devicePath = group.CustomData.Get(deviceKey);
            
            if (!string.IsNullOrEmpty(devicePath))
                return devicePath;

            return group.CustomData.Get("KeeShare.FilePath");
        }

        /// <summary>
        /// Sets the device-specific file path for a KeeShare group.
        /// </summary>
        public static void SetDeviceFilePath(PwGroup group, string path)
        {
            if (group == null) return;

            string deviceKey = GetDeviceFilePathKey();
            if (string.IsNullOrEmpty(path))
            {
                group.CustomData.Remove(deviceKey);
            }
            else
            {
                group.CustomData.Set(deviceKey, path);
            }
            group.Touch(true, false);
        }

        /// <summary>
        /// Resolves a file path, handling relative paths for local files.
        /// Relative paths are resolved relative to the current database's directory.
        /// For remote databases, relative paths are treated as-is (a warning is logged).
        /// </summary>
        internal static IOConnectionInfo ResolvePath(IKp2aApp app, string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Contains("://") || path.StartsWith("/"))
            {
                return IOConnectionInfo.FromPath(path);
            }

            if (app == null)
            {
                Kp2aLog.Log("KeeShare: Cannot resolve relative path - app is null. Using path as-is: " + path);
                return IOConnectionInfo.FromPath(path);
            }

            if (app.CurrentDb == null)
            {
                Kp2aLog.Log("KeeShare: Cannot resolve relative path - CurrentDb is null. Using path as-is: " + path);
                return IOConnectionInfo.FromPath(path);
            }

            var currentIoc = app.CurrentDb.Ioc;
            if (currentIoc == null)
            {
                Kp2aLog.Log("KeeShare: Cannot resolve relative path - CurrentDb.Ioc is null. Using path as-is: " + path);
                return IOConnectionInfo.FromPath(path);
            }

            if (currentIoc.IsLocalFile())
            {
                string dir = Path.GetDirectoryName(currentIoc.Path);
                if (string.IsNullOrEmpty(dir))
                {
                    Kp2aLog.Log("KeeShare: Cannot resolve relative path - database directory is empty. Using path as-is: " + path);
                    return IOConnectionInfo.FromPath(path);
                }
                string fullPath = Path.Combine(dir, path);
                return IOConnectionInfo.FromPath(fullPath);
            }
            else
            {
                try
                {
                    var fileStorage = app.GetFileStorage(currentIoc);
                    IOConnectionInfo parentPath = fileStorage.GetParentPath(currentIoc);
                    return fileStorage.GetFilePath(parentPath, path);
                }
                catch (Exception ex)
                {
                    Kp2aLog.Log("KeeShare: Failed to resolve relative path using IFileStorage methods: " + ex.Message + ". Using path as-is: " + path);
                    return IOConnectionInfo.FromPath(path);
                }
            }
        }

        /// <summary>
        /// Checks if this device has a configured path for the KeeShare group.
        /// </summary>
        public static bool HasDeviceFilePath(PwGroup group)
        {
            if (group == null) return false;
            string deviceKey = GetDeviceFilePathKey();
            return !string.IsNullOrEmpty(group.CustomData.Get(deviceKey));
        }

        /// <summary>
        /// Gets whether the KeeShare group is enabled on this device.
        /// A group is considered enabled if it has either a device-specific path or an original path.
        /// </summary>
        public static bool IsEnabledOnThisDevice(PwGroup group)
        {
            return !string.IsNullOrEmpty(GetEffectiveFilePath(group));
        }

        /// <summary>
        /// Gets all KeeShare groups from the database.
        /// </summary>
        public static List<KeeShareItem> GetKeeShareItems(PwDatabase db)
        {
            var items = new List<KeeShareItem>();
            if (db == null || !db.IsOpen) return items;

            CollectKeeShareGroups(db.RootGroup, db, items);
            return items;
        }

        private static void CollectKeeShareGroups(PwGroup group, PwDatabase db, List<KeeShareItem> items)
        {
            if (group.CustomData.Get("KeeShare.Active") == "true")
            {
                items.Add(new KeeShareItem(group, db));
            }

            foreach (var sub in group.Groups)
            {
                CollectKeeShareGroups(sub, db, items);
            }
        }

        /// <summary>
        /// Checks for KeeShare groups and processes them. This is called after database load
        /// and can also be triggered by "Synchronize Database" action.
        /// Uses non-blocking background operations for fast database loading (similar to database sync in 1.15+).
        /// </summary>
        /// <param name="app">The application instance</param>
        /// <param name="nextHandler">Handler to call when operation completes</param>
        public static void Check(IKp2aApp app, OnOperationFinishedHandler nextHandler)
        {
            if (app == null)
            {
                nextHandler?.Run();
                return;
            }

            var db = app.CurrentDb;
            if (db == null)
            {
                nextHandler?.Run();
                return;
            }

            if (db.KpDatabase == null || !db.KpDatabase.IsOpen)
            {
                nextHandler?.Run();
                return;
            }

            if (db.KpDatabase.RootGroup == null)
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
        /// Currently delegates to Check for consistency. If future differentiation is needed
        /// (e.g., full sync vs. incremental check), this method can be updated accordingly.
        /// </summary>
        public static void SyncInBackground(IKp2aApp app, OnOperationFinishedHandler onFinished)
        {
            if (app == null)
            {
                onFinished?.Run();
                return;
            }
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

        /// <summary>
        /// Checks if the database has any KeeShare groups that need to be exported on save.
        /// (Export or Synchronize type groups)
        /// </summary>
        public static bool HasExportableKeeShareGroups(PwDatabase db)
        {
            if (db == null || !db.IsOpen) return false;
            if (db.RootGroup == null) return false;
            return HasExportableKeeShareGroups(db.RootGroup);
        }

        private static bool HasExportableKeeShareGroups(PwGroup group)
        {
            if (group.CustomData.Get("KeeShare.Active") == "true")
            {
                string type = group.CustomData.Get("KeeShare.Type");
                if (type == "Export" || type == "Synchronize")
                {
                    string path = GetEffectiveFilePath(group);
                    if (!string.IsNullOrEmpty(path))
                        return true;
                }
            }
            
            foreach (var sub in group.Groups)
            {
                if (HasExportableKeeShareGroups(sub)) return true;
            }
            return false;
        }

        /// <summary>
        /// Exports all KeeShare "Export" and "Synchronize" type groups to their shared files.
        /// Called after SaveDb completes successfully.
        /// </summary>
        public static void ExportOnSave(IKp2aApp app, OnOperationFinishedHandler nextHandler)
        {
            if (app == null)
            {
                nextHandler?.Run();
                return;
            }

            var db = app.CurrentDb;
            if (db == null)
            {
                nextHandler?.Run();
                return;
            }

            if (db.KpDatabase == null || !db.KpDatabase.IsOpen)
            {
                nextHandler?.Run();
                return;
            }

            if (!HasExportableKeeShareGroups(db.KpDatabase))
            {
                nextHandler?.Run();
                return;
            }

            var op = new KeeShareExportOperation(app, nextHandler);
            OperationRunner.Instance.Run(app, op);
        }

        /// <summary>
        /// Exports a KeeShare group to a file. Creates a new database containing only
        /// the entries and subgroups from the source group, then saves it to the specified path.
        /// </summary>
        internal static void ExportGroupToFile(IKp2aApp app, PwGroup sourceGroup, string path, string password, IKp2aStatusLogger statusLogger = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                Kp2aLog.Log("KeeShare: No file path configured for export group " + sourceGroup.Name + ". Skipping.");
                return;
            }

            statusLogger?.UpdateMessage("Exporting KeeShare database group " + sourceGroup.Name);

            IOConnectionInfo ioc = ResolvePath(app, path);

            PwDatabase exportDb = null;
            try
            {
                exportDb = new PwDatabase();
                exportDb.New(new IOConnectionInfo(), new CompositeKey(), sourceGroup.Name);

                CompositeKey key = new CompositeKey();
                if (!string.IsNullOrEmpty(password))
                {
                    key.AddUserKey(new KcpPassword(password));
                }
                else
                {
                    key.AddUserKey(new KcpPassword(""));
                }
                exportDb.MasterKey = key;

                foreach (var entry in sourceGroup.Entries)
                {
                    PwEntry clonedEntry = entry.CloneDeep();
                    exportDb.RootGroup.AddEntry(clonedEntry, true);
                }

                foreach (var subGroup in sourceGroup.Groups)
                {
                    PwGroup clonedGroup = subGroup.CloneDeep();
                    exportDb.RootGroup.AddGroup(clonedGroup, true);
                }

                var fileStorage = app.GetFileStorage(ioc);
                using (var writeTransaction = fileStorage.OpenWriteTransaction(ioc, app.GetBooleanPreference(PreferenceKey.UseFileTransactions)))
                {
                    KdbxFile kdbx = new KdbxFile(exportDb);
                    kdbx.Save(writeTransaction.OpenFile(), null, KdbxFormat.Default, null);
                    writeTransaction.CommitWrite();
                }

                Kp2aLog.Log("KeeShare: Exported group " + sourceGroup.Name + " to " + path);
            }
            finally
            {
                exportDb?.Close();
            }
        }
    }

    /// <summary>
    /// Operation to export KeeShare groups to their shared files.
    /// </summary>
    public class KeeShareExportOperation : OperationWithFinishHandler
    {
        private readonly IKp2aApp _app;

        public KeeShareExportOperation(IKp2aApp app, OnOperationFinishedHandler handler)
            : base(app, handler)
        {
            _app = app;
        }

        public override void Run()
        {
            try
            {
                if (_app == null)
                {
                    Kp2aLog.LogUnexpectedError(new InvalidOperationException("KeeShare export: _app is null"));
                    Finish(false, "KeeShare export error: database unavailable");
                    return;
                }
                if (_app.CurrentDb == null)
                {
                    Kp2aLog.LogUnexpectedError(new InvalidOperationException("KeeShare export: CurrentDb is null"));
                    Finish(false, "KeeShare export error: database unavailable");
                    return;
                }
                if (_app.CurrentDb.KpDatabase == null)
                {
                    Kp2aLog.LogUnexpectedError(new InvalidOperationException("KeeShare export: KpDatabase is null"));
                    Finish(false, "KeeShare export error: database unavailable");
                    return;
                }
                if (_app.CurrentDb.KpDatabase.RootGroup == null)
                {
                    Kp2aLog.LogUnexpectedError(new InvalidOperationException("KeeShare export: RootGroup is null"));
                    Finish(false, "KeeShare export error: database unavailable");
                    return;
                }
                ProcessGroup(_app.CurrentDb.KpDatabase.RootGroup);
                Finish(true);
            }
            catch (Exception ex)
            {
                Kp2aLog.LogUnexpectedError(ex);
                Finish(false, "KeeShare export error: " + ex.Message);
            }
        }

        private void ProcessGroup(PwGroup group)
        {
            if (group.CustomData.Get("KeeShare.Active") == "true")
            {
                string type = group.CustomData.Get("KeeShare.Type");
                if (type == "Export" || type == "Synchronize")
                {
                    ExportGroup(group);
                }
            }

            foreach (var sub in group.Groups.ToList())
            {
                ProcessGroup(sub);
            }
        }

        private void ExportGroup(PwGroup sourceGroup)
        {
            string path = KeeShare.GetEffectiveFilePath(sourceGroup);
            string password = sourceGroup.CustomData.Get("KeeShare.Password") ?? "";

            StatusLogger.UpdateMessage(_app.GetResourceString(UiStringKey.saving_database) + ": " + sourceGroup.Name);

            KeeShare.ExportGroupToFile(_app, sourceGroup, path, password, StatusLogger);
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
                if (_app == null)
                {
                    Kp2aLog.LogUnexpectedError(new InvalidOperationException("KeeShare check: _app is null"));
                    Finish(false, "KeeShare error: database is null");
                    return;
                }
                if (_app.CurrentDb == null)
                {
                    Kp2aLog.LogUnexpectedError(new InvalidOperationException("KeeShare check: CurrentDb is null"));
                    Finish(false, "KeeShare error: database is null");
                    return;
                }
                if (_app.CurrentDb.KpDatabase == null)
                {
                    Kp2aLog.LogUnexpectedError(new InvalidOperationException("KeeShare check: KpDatabase is null"));
                    Finish(false, "KeeShare error: database is null");
                    return;
                }
                if (_app.CurrentDb.KpDatabase.RootGroup == null)
                {
                    Kp2aLog.LogUnexpectedError(new InvalidOperationException("KeeShare check: RootGroup is null"));
                    Finish(false, "KeeShare error: database is null");
                    return;
                }
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
                ProcessKeeShare(group);
            }

            foreach (var sub in group.Groups.ToList())
            {
                ProcessGroup(sub);
            }
        }

        private void ProcessKeeShare(PwGroup group)
        {
            string type = group.CustomData.Get("KeeShare.Type");
            string path = KeeShare.GetEffectiveFilePath(group);
            string password = group.CustomData.Get("KeeShare.Password");

            if (string.IsNullOrEmpty(path))
            {
                Kp2aLog.Log("KeeShare: No file path configured for group " + group.Name + " on this device. Skipping.");
                return;
            }

            if (type == "Import" || type == "Synchronize")
            {
                StatusLogger.UpdateMessage(_app.GetResourceString(UiStringKey.OpeningDatabase) + ": " + group.Name);
                Import(group, path, password, type);
            }
            else if (type == "Export")
            {
                StatusLogger.UpdateMessage(_app.GetResourceString(UiStringKey.saving_database) + ": " + group.Name);
                Export(group, path, password);
            }
        }

        private void Export(PwGroup sourceGroup, string path, string password)
        {
            KeeShare.ExportGroupToFile(_app, sourceGroup, path, password, StatusLogger);
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
                                                kdbxMem.Dispose();
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
                                            kdbxMem.Dispose();
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
        /// Note: Deletions are not added to the database's DeletedObjects collection.
        /// This is intentional for Import mode since we're performing a complete replacement
        /// of the group's contents - the old entries/groups will be replaced by the imported
        /// content via MergeIn, so tracking deletions would be unnecessary and could cause
        /// synchronization issues.
        /// </summary>
        private void ClearGroupContents(PwGroup group)
        {
            group.Entries.Clear();
            group.Groups.Clear();
        }

        private IOConnectionInfo ResolvePath(string path)
        {
            return KeeShare.ResolvePath(_app, path);
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
                    string pemText = trustedCertificate.Trim();
                    
                    if (pemText.StartsWith("-----BEGIN CERTIFICATE-----"))
                    {
                        var cert = X509Certificate2.CreateFromPem(pemText);
                        publicKeyBytes = cert.GetPublicKey();
                    }
                    else if (pemText.StartsWith("-----BEGIN PUBLIC KEY-----"))
                    {
                        var lines = pemText.Split('\n');
                        var base64Lines = lines
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("-----"));
                        string base64Content = string.Join("", base64Lines);
                        publicKeyBytes = Convert.FromBase64String(base64Content);
                    }
                    else
                    {
                        publicKeyBytes = Convert.FromBase64String(pemText);
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
