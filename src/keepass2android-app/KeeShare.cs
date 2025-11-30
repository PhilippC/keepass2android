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

            // We must iterate over a copy of the groups list because ProcessKeeShare (Import) might modify subgroups
            // However, Import usually modifies the *content* of the group, replacing subgroups.
            // If we replace subgroups, we shouldn't recurse into the *old* subgroups?
            // Or should we recurse into the *new* subgroups?
            // KeeShare groups are usually leaf nodes in terms of configuration (you don't have nested KeeShare groups usually).
            // But just in case, let's recurse first? No, if I import, I overwrite.
            
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
                    s.CopyTo(ms);
                    ms.Position = 0;

                    Stream kdbxStream = ms;
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
                                if (kdbxEntry != null)
                                {
                                    // Extract to a new memory stream because KdbxFile might close it or we need a clean stream
                                    MemoryStream kdbxMem = new MemoryStream();
                                    using (var es = kdbxEntry.Open())
                                    {
                                        es.CopyTo(kdbxMem);
                                    }
                                    kdbxMem.Position = 0;
                                    kdbxStream = kdbxMem;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Kp2aLog.Log("Failed to treat file as zip: " + ex.Message);
                            ms.Position = 0; // Rewind and try as KDBX directly
                            kdbxStream = ms;
                        }
                    }

                    // Load the KDBX
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
                    // We need a null status logger or similar
                    kdbx.Load(kdbxStream, KdbxFormat.Default, key);

                    // Now copy content from shareDb.RootGroup to targetGroup
                    SyncGroups(shareDb.RootGroup, targetGroup);
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
    }
}
