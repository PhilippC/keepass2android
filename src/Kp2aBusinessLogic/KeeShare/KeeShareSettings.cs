using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using KeePassLib;

namespace keepass2android.KeeShare
{
    public class KeeShareSettings
    {
        public const string KeeShareReferenceKey = "KeeShare/Reference";
        private const string DeviceFilePathPrefix = "KeeShare.FilePath.";
        
        /// <summary>
        /// Delegate to retrieve the current device ID. Must be initialized by the app.
        /// </summary>
        public static Func<string> DeviceIdProvider { get; set; }

        [Flags]
        public enum TypeFlag
        {
            Inactive = 0,
            ImportFrom = 1 << 0,
            ExportTo = 1 << 1,
            SynchronizeWith = ImportFrom | ExportTo
        }

        public class Reference
        {
            public TypeFlag Type { get; set; } = TypeFlag.Inactive;
            public PwUuid Uuid { get; set; }
            public string Path { get; set; }
            public string Password { get; set; }
            public bool KeepGroups { get; set; } = true;

            public bool IsImporting
            {
                get => (Type & TypeFlag.ImportFrom) != 0 && !string.IsNullOrEmpty(Path);
                set
                {
                    if (value) Type |= TypeFlag.ImportFrom;
                    else Type &= ~TypeFlag.ImportFrom;
                }
            }

            public bool IsExporting
            {
                get => (Type & TypeFlag.ExportTo) != 0;
                set
                {
                    if (value) Type |= TypeFlag.ExportTo;
                    else Type &= ~TypeFlag.ExportTo;
                }
            }
        }

        public static void SetReference(PwGroup group, Reference reference)
        {
            if (group == null || reference == null) return;

            try
            {
                var xml = SerializeReference(reference);
                var bytes = Encoding.UTF8.GetBytes(xml);
                var encoded = Convert.ToBase64String(bytes);
                group.CustomData.Set(KeeShareReferenceKey, encoded);
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Failed to set reference: " + ex.Message);
            }
        }

        private static string SerializeReference(Reference reference)
        {
            var root = new XElement("KeeShare",
                new XElement("Type",
                    (reference.Type & TypeFlag.ImportFrom) != 0 ? new XElement("Import") : null,
                    (reference.Type & TypeFlag.ExportTo) != 0 ? new XElement("Export") : null
                ),
                reference.Uuid != null ? new XElement("Group", Convert.ToBase64String(reference.Uuid.UuidBytes)) : null,
                !string.IsNullOrEmpty(reference.Path) ? new XElement("Path", Convert.ToBase64String(Encoding.UTF8.GetBytes(reference.Path))) : null,
                !string.IsNullOrEmpty(reference.Password) ? new XElement("Password", Convert.ToBase64String(Encoding.UTF8.GetBytes(reference.Password))) : null,
                new XElement("KeepGroups", reference.KeepGroups ? "True" : "False")
            );

            return root.ToString();
        }

        public static Reference GetReference(PwGroup group)
        {
            if (group == null || group.CustomData == null) return null;

            var encoded = group.CustomData.Get(KeeShareReferenceKey);
            if (string.IsNullOrEmpty(encoded)) return null;

            Reference refObj = null;
            try
            {
                var bytes = Convert.FromBase64String(encoded);
                var xml = Encoding.UTF8.GetString(bytes);
                refObj = ParseReference(xml);
            }
            catch (Exception ex)
            {
                Kp2aLog.Log("KeeShare: Failed to parse reference: " + ex.Message);
                return null;
            }

            if (refObj != null)
            {
                // Check for device-specific path override
                string deviceId = DeviceIdProvider?.Invoke();
                if (!string.IsNullOrEmpty(deviceId))
                {
                    string deviceKey = DeviceFilePathPrefix + deviceId;
                    string devicePath = group.CustomData.Get(deviceKey);
                    if (!string.IsNullOrEmpty(devicePath))
                    {
                        refObj.Path = devicePath;
                        Kp2aLog.Log("KeeShare: Using device-specific path for " + deviceId);
                    }
                }
            }

            return refObj;
        }

        private static Reference ParseReference(string xml)
        {
            var refObj = new Reference();
            // Wrap in a root element if missing, but the C++ code says it writes <KeeShare> root.
            // "writer.writeStartElement("KeeShare"); specific(writer);"

            try
            {
                var doc = XDocument.Parse(xml);
                var root = doc.Root; // KeeShare
                if (root == null || root.Name != "KeeShare") return null;

                var typeElem = root.Element("Type");
                if (typeElem != null)
                {
                    if (typeElem.Element("Import") != null) refObj.Type |= TypeFlag.ImportFrom;
                    if (typeElem.Element("Export") != null) refObj.Type |= TypeFlag.ExportTo;
                }

                var groupElem = root.Element("Group");
                if (groupElem != null)
                {
                    var uuidBytes = Convert.FromBase64String(groupElem.Value);
                    refObj.Uuid = new PwUuid(uuidBytes);
                }

                var pathElem = root.Element("Path");
                if (pathElem != null)
                {
                    refObj.Path = Encoding.UTF8.GetString(Convert.FromBase64String(pathElem.Value));
                }

                var passElem = root.Element("Password");
                if (passElem != null)
                {
                    refObj.Password = Encoding.UTF8.GetString(Convert.FromBase64String(passElem.Value));
                }

                var keepGroupsElem = root.Element("KeepGroups");
                if (keepGroupsElem != null)
                {
                    refObj.KeepGroups = string.Equals(keepGroupsElem.Value, "True", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("KeeShare: Failed to parse reference XML: " + ex.Message);
                return null;
            }

            return refObj;
        }
        /// <summary>
        /// Checks if the group is read-only because it is part of an incoming KeeShare (Import mode).
        /// Recursively checks parent groups.
        /// </summary>
        public static bool IsReadOnlyBecauseKeeShareImport(PwGroup group)
        {
            var current = group;
            while (current != null)
            {
                var reference = GetReference(current);
                if (reference != null)
                {
                    // If we find a reference:
                    // 1. If it's Import-only (ImportFrom set, ExportTo NOT set), then it's read-only.
                    // 2. If it's Synchronize (ImportFrom AND ExportTo), it's NOT read-only (bidirectional).
                    // 3. If it's Export-only, it's NOT read-only (we can add to it).
                    
                    if ((reference.Type & TypeFlag.ImportFrom) != 0 && (reference.Type & TypeFlag.ExportTo) == 0)
                    {
                        return true;
                    }
                }
                current = current.ParentGroup;
            }
            return false;
        }

        public static void RemoveReference(PwGroup group)
        {
            if (group != null && group.CustomData != null)
            {
                group.CustomData.Remove(KeeShareReferenceKey);
            }
        }

        public static IOConnectionInfo ResolvePath(IOConnectionInfo baseIoc, string path)
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
