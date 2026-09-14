namespace KeeShare.Tests.TestHelpers
{
    /// <summary>
    /// Represents a KeeShare group item for testing, mirroring KeeShareItem from production.
    /// </summary>
    public class KeeShareItemStub
    {
        public PwGroupStub Group { get; }
        public PwDatabaseStub Database { get; }

        public KeeShareItemStub(PwGroupStub group, PwDatabaseStub database)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public string Type => Group.CustomData.Get(KeeShareConfigLogic.TypeKey) ?? "";
        public string OriginalPath => Group.CustomData.Get(KeeShareConfigLogic.FilePathKey) ?? "";
        public string Password => Group.CustomData.Get(KeeShareConfigLogic.PasswordKey) ?? "";
        public bool IsActive => Group.CustomData.Get(KeeShareConfigLogic.ActiveKey) == "true";
    }

    /// <summary>
    /// Copy of KeeShare state checking logic for testing without Android dependencies.
    /// Kept in sync with KeeShare.cs in the app project (lines 358-542).
    /// Last synced: 2025-01-27
    /// </summary>
    public static class KeeShareStateLogic
    {
        /// <summary>
        /// Checks if this device has a configured path for the KeeShare group.
        /// </summary>
        public static bool HasDeviceFilePath(PwGroupStub? group)
        {
            if (group == null) return false;
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            return !string.IsNullOrEmpty(group.CustomData.Get(deviceKey));
        }

        /// <summary>
        /// Gets whether the KeeShare group is enabled on this device.
        /// A group is considered enabled if it has either a device-specific path or an original path.
        /// </summary>
        public static bool IsEnabledOnThisDevice(PwGroupStub? group)
        {
            return !string.IsNullOrEmpty(KeeShareConfigLogic.GetEffectiveFilePath(group));
        }

        /// <summary>
        /// Gets all KeeShare groups from the database.
        /// </summary>
        public static List<KeeShareItemStub> GetKeeShareItems(PwDatabaseStub? db)
        {
            var items = new List<KeeShareItemStub>();
            if (db == null || !db.IsOpen) return items;
            if (db.RootGroup == null) return items;

            CollectKeeShareGroups(db.RootGroup, db, items);
            return items;
        }

        private static void CollectKeeShareGroups(PwGroupStub group, PwDatabaseStub db, List<KeeShareItemStub> items)
        {
            if (group.CustomData.Get(KeeShareConfigLogic.ActiveKey) == "true")
            {
                items.Add(new KeeShareItemStub(group, db));
            }

            foreach (var sub in group.Groups)
            {
                CollectKeeShareGroups(sub, db, items);
            }
        }

        /// <summary>
        /// Checks if the database has any KeeShare groups that need to be exported on save.
        /// (Export or Synchronize type groups with a configured path)
        /// </summary>
        public static bool HasExportableKeeShareGroups(PwDatabaseStub? db)
        {
            if (db == null || !db.IsOpen) return false;
            if (db.RootGroup == null) return false;
            return HasExportableKeeShareGroups(db.RootGroup);
        }

        private static bool HasExportableKeeShareGroups(PwGroupStub group)
        {
            if (group.CustomData.Get(KeeShareConfigLogic.ActiveKey) == "true")
            {
                string? type = group.CustomData.Get(KeeShareConfigLogic.TypeKey);
                if (type == "Export" || type == "Synchronize")
                {
                    string? path = KeeShareConfigLogic.GetEffectiveFilePath(group);
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
        /// Checks if a group is read-only because it's a KeeShare Import group
        /// or is contained within one. Import groups replace their contents on sync,
        /// so local modifications would be lost.
        /// </summary>
        public static bool IsReadOnlyBecauseKeeShareImport(PwGroupStub? group)
        {
            if (group == null) return false;

            PwGroupStub? current = group;
            while (current != null)
            {
                if (current.CustomData.Get(KeeShareConfigLogic.ActiveKey) == "true" &&
                    current.CustomData.Get(KeeShareConfigLogic.TypeKey) == "Import")
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
        public static bool IsReadOnlyBecauseKeeShareImport(PwEntryStub? entry)
        {
            return entry?.ParentGroup != null && IsReadOnlyBecauseKeeShareImport(entry.ParentGroup);
        }
    }
}
