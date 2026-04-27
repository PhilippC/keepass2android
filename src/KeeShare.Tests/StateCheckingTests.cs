using KeeShare.Tests.TestHelpers;

namespace KeeShare.Tests
{
    public class StateCheckingTests
    {
        public StateCheckingTests()
        {
            // Reset device ID before each test
            KeeShareConfigLogic.TestDeviceId = "test-device-id";
        }

        #region HasDeviceFilePath Tests

        [Fact]
        public void HasDeviceFilePath_NullGroup_ReturnsFalse()
        {
            bool result = KeeShareStateLogic.HasDeviceFilePath(null);
            Assert.False(result);
        }

        [Fact]
        public void HasDeviceFilePath_NoDeviceKey_ReturnsFalse()
        {
            var group = new PwGroupStub();
            bool result = KeeShareStateLogic.HasDeviceFilePath(group);
            Assert.False(result);
        }

        [Fact]
        public void HasDeviceFilePath_EmptyDeviceValue_ReturnsFalse()
        {
            var group = new PwGroupStub();
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "");

            bool result = KeeShareStateLogic.HasDeviceFilePath(group);

            Assert.False(result);
        }

        [Fact]
        public void HasDeviceFilePath_HasDeviceValue_ReturnsTrue()
        {
            var group = new PwGroupStub();
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "/device/path.kdbx");

            bool result = KeeShareStateLogic.HasDeviceFilePath(group);

            Assert.True(result);
        }

        #endregion

        #region IsEnabledOnThisDevice Tests

        [Fact]
        public void IsEnabledOnThisDevice_NullGroup_ReturnsFalse()
        {
            bool result = KeeShareStateLogic.IsEnabledOnThisDevice(null);
            Assert.False(result);
        }

        [Fact]
        public void IsEnabledOnThisDevice_NoEffectivePath_ReturnsFalse()
        {
            var group = new PwGroupStub();
            bool result = KeeShareStateLogic.IsEnabledOnThisDevice(group);
            Assert.False(result);
        }

        [Fact]
        public void IsEnabledOnThisDevice_HasDevicePath_ReturnsTrue()
        {
            var group = new PwGroupStub();
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "/device/path.kdbx");

            bool result = KeeShareStateLogic.IsEnabledOnThisDevice(group);

            Assert.True(result);
        }

        [Fact]
        public void IsEnabledOnThisDevice_HasFallbackPath_ReturnsTrue()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/fallback/path.kdbx");

            bool result = KeeShareStateLogic.IsEnabledOnThisDevice(group);

            Assert.True(result);
        }

        #endregion

        #region GetKeeShareItems Tests

        [Fact]
        public void GetKeeShareItems_NullDb_ReturnsEmptyList()
        {
            var items = KeeShareStateLogic.GetKeeShareItems(null);
            Assert.Empty(items);
        }

        [Fact]
        public void GetKeeShareItems_ClosedDb_ReturnsEmptyList()
        {
            var db = new PwDatabaseStub { IsOpen = false, RootGroup = new PwGroupStub() };
            var items = KeeShareStateLogic.GetKeeShareItems(db);
            Assert.Empty(items);
        }

        [Fact]
        public void GetKeeShareItems_NullRootGroup_ReturnsEmptyList()
        {
            var db = new PwDatabaseStub { IsOpen = true, RootGroup = null };
            var items = KeeShareStateLogic.GetKeeShareItems(db);
            Assert.Empty(items);
        }

        [Fact]
        public void GetKeeShareItems_NoKeeShareGroups_ReturnsEmptyList()
        {
            var db = new PwDatabaseStub { IsOpen = true, RootGroup = new PwGroupStub() };
            var items = KeeShareStateLogic.GetKeeShareItems(db);
            Assert.Empty(items);
        }

        [Fact]
        public void GetKeeShareItems_RootLevelKeeShare_ReturnsItem()
        {
            var root = new PwGroupStub();
            var keeShareGroup = new PwGroupStub { Name = "Shared" };
            keeShareGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            root.AddGroup(keeShareGroup);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            var items = KeeShareStateLogic.GetKeeShareItems(db);

            Assert.Single(items);
            Assert.Same(keeShareGroup, items[0].Group);
        }

        [Fact]
        public void GetKeeShareItems_NestedKeeShare_ReturnsItem()
        {
            var root = new PwGroupStub();
            var level1 = new PwGroupStub { Name = "Level1" };
            var level2 = new PwGroupStub { Name = "Shared" };
            level2.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            level1.AddGroup(level2);
            root.AddGroup(level1);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            var items = KeeShareStateLogic.GetKeeShareItems(db);

            Assert.Single(items);
            Assert.Same(level2, items[0].Group);
        }

        [Fact]
        public void GetKeeShareItems_MultipleKeeShareGroups_ReturnsAll()
        {
            var root = new PwGroupStub();
            var group1 = new PwGroupStub { Name = "Shared1" };
            group1.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            var group2 = new PwGroupStub { Name = "Shared2" };
            group2.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            root.AddGroup(group1);
            root.AddGroup(group2);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            var items = KeeShareStateLogic.GetKeeShareItems(db);

            Assert.Equal(2, items.Count);
        }

        [Fact]
        public void GetKeeShareItems_InactiveKeeShare_NotIncluded()
        {
            var root = new PwGroupStub();
            var group = new PwGroupStub { Name = "NotActive" };
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "false");
            root.AddGroup(group);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            var items = KeeShareStateLogic.GetKeeShareItems(db);

            Assert.Empty(items);
        }

        #endregion

        #region HasExportableKeeShareGroups Tests

        [Fact]
        public void HasExportableKeeShareGroups_NullDb_ReturnsFalse()
        {
            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(null);
            Assert.False(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_ClosedDb_ReturnsFalse()
        {
            var db = new PwDatabaseStub { IsOpen = false, RootGroup = new PwGroupStub() };
            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);
            Assert.False(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_NullRootGroup_ReturnsFalse()
        {
            var db = new PwDatabaseStub { IsOpen = true, RootGroup = null };
            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);
            Assert.False(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_NoKeeShareGroups_ReturnsFalse()
        {
            var db = new PwDatabaseStub { IsOpen = true, RootGroup = new PwGroupStub() };
            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);
            Assert.False(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_ImportOnlyGroup_ReturnsFalse()
        {
            var root = new PwGroupStub();
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Import");
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/path.kdbx");
            root.AddGroup(group);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);

            Assert.False(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_ExportGroupWithPath_ReturnsTrue()
        {
            var root = new PwGroupStub();
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/path.kdbx");
            root.AddGroup(group);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);

            Assert.True(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_SynchronizeGroupWithPath_ReturnsTrue()
        {
            var root = new PwGroupStub();
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Synchronize");
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/path.kdbx");
            root.AddGroup(group);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);

            Assert.True(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_ExportGroupWithoutPath_ReturnsFalse()
        {
            var root = new PwGroupStub();
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");
            // No file path set
            root.AddGroup(group);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);

            Assert.False(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_ExportGroupWithDevicePath_ReturnsTrue()
        {
            var root = new PwGroupStub();
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "/device/path.kdbx");
            root.AddGroup(group);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);

            Assert.True(result);
        }

        [Fact]
        public void HasExportableKeeShareGroups_NestedExportGroup_ReturnsTrue()
        {
            var root = new PwGroupStub();
            var level1 = new PwGroupStub();
            var exportGroup = new PwGroupStub();
            exportGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            exportGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");
            exportGroup.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/path.kdbx");
            level1.AddGroup(exportGroup);
            root.AddGroup(level1);

            var db = new PwDatabaseStub { IsOpen = true, RootGroup = root };

            bool result = KeeShareStateLogic.HasExportableKeeShareGroups(db);

            Assert.True(result);
        }

        #endregion

        #region IsReadOnlyBecauseKeeShareImport (Group) Tests

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_NullGroup_ReturnsFalse()
        {
            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport((PwGroupStub?)null);
            Assert.False(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_NotInKeeShare_ReturnsFalse()
        {
            var group = new PwGroupStub();
            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(group);
            Assert.False(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_InExportGroup_ReturnsFalse()
        {
            var exportGroup = new PwGroupStub();
            exportGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            exportGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");
            var child = new PwGroupStub();
            exportGroup.AddGroup(child);

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(child);

            Assert.False(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_InSynchronizeGroup_ReturnsFalse()
        {
            var syncGroup = new PwGroupStub();
            syncGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            syncGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Synchronize");
            var child = new PwGroupStub();
            syncGroup.AddGroup(child);

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(child);

            Assert.False(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_IsImportGroup_ReturnsTrue()
        {
            var importGroup = new PwGroupStub();
            importGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            importGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Import");

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(importGroup);

            Assert.True(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_ParentIsImport_ReturnsTrue()
        {
            var importGroup = new PwGroupStub();
            importGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            importGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Import");
            var child = new PwGroupStub();
            importGroup.AddGroup(child);

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(child);

            Assert.True(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_AncestorIsImport_ReturnsTrue()
        {
            var importGroup = new PwGroupStub();
            importGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            importGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Import");
            var level1 = new PwGroupStub();
            var level2 = new PwGroupStub();
            importGroup.AddGroup(level1);
            level1.AddGroup(level2);

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(level2);

            Assert.True(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_ActiveFalseImportType_ReturnsFalse()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "false");
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Import");

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(group);

            Assert.False(result);
        }

        #endregion

        #region IsReadOnlyBecauseKeeShareImport (Entry) Tests

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_NullEntry_ReturnsFalse()
        {
            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport((PwEntryStub?)null);
            Assert.False(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_EntryWithNullParent_ReturnsFalse()
        {
            var entry = new PwEntryStub { ParentGroup = null };
            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(entry);
            Assert.False(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_EntryInImportGroup_ReturnsTrue()
        {
            var importGroup = new PwGroupStub();
            importGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            importGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Import");
            var entry = new PwEntryStub();
            importGroup.AddEntry(entry);

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(entry);

            Assert.True(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_EntryInExportGroup_ReturnsFalse()
        {
            var exportGroup = new PwGroupStub();
            exportGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            exportGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");
            var entry = new PwEntryStub();
            exportGroup.AddEntry(entry);

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(entry);

            Assert.False(result);
        }

        [Fact]
        public void IsReadOnlyBecauseKeeShareImport_EntryInNestedImportGroup_ReturnsTrue()
        {
            var importGroup = new PwGroupStub();
            importGroup.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            importGroup.CustomData.Set(KeeShareConfigLogic.TypeKey, "Import");
            var childGroup = new PwGroupStub();
            importGroup.AddGroup(childGroup);
            var entry = new PwEntryStub();
            childGroup.AddEntry(entry);

            bool result = KeeShareStateLogic.IsReadOnlyBecauseKeeShareImport(entry);

            Assert.True(result);
        }

        #endregion
    }
}
