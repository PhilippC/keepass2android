namespace KeeShare.Tests.TestHelpers
{
    /// <summary>
    /// Copy of KeeShare KeePassXC compatibility logic for testing without Android dependencies.
    /// Kept in sync with KeeShare.cs in the app project (lines 197-308).
    /// Last synced: 2025-01-27
    /// </summary>
    public static class KeeShareCompatibilityLogic
    {
        /// <summary>
        /// Checks if a group has KeePassXC-style KeeShare configuration.
        /// KeePassXC stores share info in CustomData with keys like "KeeShareReference.Path", etc.
        /// </summary>
        public static bool HasKeePassXCFormat(PwGroupStub? group)
        {
            if (group == null) return false;

            // Check for KeePassXC's CustomData keys
            return group.CustomData.Exists("KeeShareReference.Path") ||
                   group.CustomData.Exists("KPXC_KeeShare_Path") ||
                   (group.CustomData.Exists("KeeShare") &&
                    !group.CustomData.Exists(KeeShareConfigLogic.ActiveKey)); // Has old KeeShare key but not our format
        }

        /// <summary>
        /// Attempts to import KeePassXC KeeShare configuration into KP2A format.
        /// This allows groups created in KeePassXC to work in KP2A.
        /// Does NOT overwrite existing KP2A configuration.
        /// </summary>
        /// <returns>True if configuration was imported, false otherwise.</returns>
        public static bool TryImportKeePassXCConfig(PwGroupStub? group)
        {
            if (group == null || !HasKeePassXCFormat(group)) return false;

            // Don't overwrite existing KP2A configuration
            if (group.CustomData.Exists(KeeShareConfigLogic.ActiveKey)) return false;

            try
            {
                string? path = null;
                string type = "Synchronize"; // KeePassXC default
                string? password = null;

                // Try to extract path from various KeePassXC formats
                if (group.CustomData.Exists("KeeShareReference.Path"))
                {
                    path = group.CustomData.Get("KeeShareReference.Path");
                }
                else if (group.CustomData.Exists("KPXC_KeeShare_Path"))
                {
                    path = group.CustomData.Get("KPXC_KeeShare_Path");
                }
                else if (group.CustomData.Exists("KeeShare"))
                {
                    // Try to parse XML or structured format
                    string? data = group.CustomData.Get("KeeShare");
                    if (data != null)
                    {
                        // Simple heuristic: look for path= pattern
                        int pathIdx = data.IndexOf("path=", StringComparison.OrdinalIgnoreCase);
                        if (pathIdx >= 0)
                        {
                            pathIdx += 5; // skip "path="
                            if (pathIdx < data.Length)
                            {
                                char quote = data[pathIdx];
                                if (quote == '"' || quote == '\'')
                                {
                                    pathIdx++;
                                    int endIdx = data.IndexOf(quote, pathIdx);
                                    if (endIdx > pathIdx)
                                    {
                                        path = data.Substring(pathIdx, endIdx - pathIdx);
                                    }
                                }
                            }
                        }
                    }
                }

                // Try to extract type
                if (group.CustomData.Exists("KeeShareReference.Type"))
                {
                    string? xtype = group.CustomData.Get("KeeShareReference.Type");
                    if (xtype != null)
                    {
                        // Map KeePassXC types to KP2A types
                        if (xtype.Equals("Export", StringComparison.OrdinalIgnoreCase))
                            type = "Export";
                        else if (xtype.Equals("Import", StringComparison.OrdinalIgnoreCase))
                            type = "Import";
                        else if (xtype.Equals("Sync", StringComparison.OrdinalIgnoreCase) ||
                                 xtype.Equals("Synchronize", StringComparison.OrdinalIgnoreCase))
                            type = "Synchronize";
                    }
                }
                else if (group.CustomData.Exists("KPXC_KeeShare_Type"))
                {
                    string? xtype = group.CustomData.Get("KPXC_KeeShare_Type");
                    if (xtype != null)
                    {
                        if (xtype.Equals("0")) type = "Export";
                        else if (xtype.Equals("1")) type = "Import";
                        else if (xtype.Equals("2")) type = "Synchronize";
                    }
                }

                // Try to extract password
                if (group.CustomData.Exists("KeeShareReference.Password"))
                {
                    password = group.CustomData.Get("KeeShareReference.Password");
                }
                else if (group.CustomData.Exists("KPXC_KeeShare_Password"))
                {
                    password = group.CustomData.Get("KPXC_KeeShare_Password");
                }

                if (!string.IsNullOrEmpty(path))
                {
                    KeeShareConfigLogic.EnableKeeShare(group, type, path, password);
                    return true;
                }
            }
            catch
            {
                // Silently fail on import errors
            }

            return false;
        }
    }
}
