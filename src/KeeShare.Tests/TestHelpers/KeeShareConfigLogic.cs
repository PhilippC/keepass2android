namespace KeeShare.Tests.TestHelpers
{
    /// <summary>
    /// Copy of KeeShare configuration logic for testing without Android dependencies.
    /// Kept in sync with KeeShare.cs in the app project (lines 46-195).
    /// Last synced: 2025-01-27
    /// </summary>
    public static class KeeShareConfigLogic
    {
        public const string DeviceFilePathKeyPrefix = "KeeShare.FilePath.";
        public const string ActiveKey = "KeeShare.Active";
        public const string TypeKey = "KeeShare.Type";
        public const string FilePathKey = "KeeShare.FilePath";
        public const string PasswordKey = "KeeShare.Password";
        public const string TrustedCertificateKey = "KeeShare.TrustedCertificate";

        /// <summary>
        /// Test device ID for testing. In production this comes from KeeAutoExecExt.ThisDeviceId.
        /// </summary>
        public static string TestDeviceId { get; set; } = "test-device-id";

        /// <summary>
        /// Gets the device-specific custom data key for storing file paths.
        /// </summary>
        public static string GetDeviceFilePathKey()
        {
            return DeviceFilePathKeyPrefix + TestDeviceId;
        }

        /// <summary>
        /// Gets the effective file path for a KeeShare group on this device.
        /// First checks for a device-specific path, then falls back to the original path.
        /// If the stored value is a serialized IOConnectionInfo, extracts the Path from it.
        /// </summary>
        public static string? GetEffectiveFilePath(PwGroupStub? group)
        {
            if (group == null) return null;

            string deviceKey = GetDeviceFilePathKey();
            string? devicePath = group.CustomData.Get(deviceKey);

            if (!string.IsNullOrEmpty(devicePath))
            {
                // Try to detect if this is a serialized IOConnectionInfo
                // The format typically includes encoded path info
                // For simplicity in tests, we'll just return the value as-is
                // unless it looks like a serialized IOC (contains specific markers)
                if (devicePath.Contains("s://") || devicePath.StartsWith("Path="))
                {
                    // This looks like a serialized IOConnectionInfo
                    // Extract just the path portion - simplified for testing
                    // In production, IOConnectionInfo.UnserializeFromString is used
                    int pathIndex = devicePath.IndexOf("Path=");
                    if (pathIndex >= 0)
                    {
                        int startIndex = pathIndex + 5;
                        int endIndex = devicePath.IndexOf('&', startIndex);
                        if (endIndex < 0) endIndex = devicePath.Length;
                        return Uri.UnescapeDataString(devicePath.Substring(startIndex, endIndex - startIndex));
                    }
                }
                return devicePath;
            }

            return group.CustomData.Get(FilePathKey);
        }

        /// <summary>
        /// Sets the device-specific file path for a KeeShare group.
        /// </summary>
        public static void SetDeviceFilePath(PwGroupStub? group, string? pathOrSerializedIoc)
        {
            if (group == null) return;

            string deviceKey = GetDeviceFilePathKey();
            if (string.IsNullOrEmpty(pathOrSerializedIoc))
            {
                group.CustomData.Remove(deviceKey);
            }
            else
            {
                group.CustomData.Set(deviceKey, pathOrSerializedIoc);
            }
            group.Touch(true, false);
        }

        /// <summary>
        /// Enables KeeShare on a group with the specified configuration.
        /// </summary>
        public static void EnableKeeShare(PwGroupStub? group, string? type, string? filePath, string? password = null)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("Type cannot be null or empty", nameof(type));
            if (type != "Export" && type != "Import" && type != "Synchronize")
                throw new ArgumentException("Type must be 'Export', 'Import', or 'Synchronize'", nameof(type));

            group.CustomData.Set(ActiveKey, "true");
            group.CustomData.Set(TypeKey, type);

            if (!string.IsNullOrEmpty(filePath))
            {
                group.CustomData.Set(FilePathKey, filePath);
            }

            if (!string.IsNullOrEmpty(password))
            {
                group.CustomData.Set(PasswordKey, password);
            }
            else
            {
                group.CustomData.Remove(PasswordKey);
            }

            group.Touch(true, false);
        }

        /// <summary>
        /// Updates KeeShare configuration on a group.
        /// </summary>
        public static void UpdateKeeShareConfig(PwGroupStub? group, string? type, string? filePath, string? password)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            if (!string.IsNullOrEmpty(type))
            {
                if (type != "Export" && type != "Import" && type != "Synchronize")
                    throw new ArgumentException("Type must be 'Export', 'Import', or 'Synchronize'", nameof(type));
                group.CustomData.Set(TypeKey, type);
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                group.CustomData.Set(FilePathKey, filePath);
            }

            if (!string.IsNullOrEmpty(password))
            {
                group.CustomData.Set(PasswordKey, password);
            }
            else
            {
                group.CustomData.Remove(PasswordKey);
            }

            group.Touch(true, false);
        }

        /// <summary>
        /// Disables KeeShare on a group, removing all KeeShare-related CustomData.
        /// </summary>
        public static void DisableKeeShare(PwGroupStub? group)
        {
            if (group == null) return;

            group.CustomData.Remove(ActiveKey);
            group.CustomData.Remove(TypeKey);
            group.CustomData.Remove(FilePathKey);
            group.CustomData.Remove(PasswordKey);
            group.CustomData.Remove(TrustedCertificateKey);

            string deviceKey = GetDeviceFilePathKey();
            group.CustomData.Remove(deviceKey);

            group.Touch(true, false);
        }
    }
}
