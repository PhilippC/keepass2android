using KeeShare.Tests.TestHelpers;

namespace KeeShare.Tests
{
    public class KeePassXCCompatibilityTests
    {
        #region HasKeePassXCFormat Tests

        [Fact]
        public void HasKeePassXCFormat_NullGroup_ReturnsFalse()
        {
            bool result = KeeShareCompatibilityLogic.HasKeePassXCFormat(null);
            Assert.False(result);
        }

        [Fact]
        public void HasKeePassXCFormat_NoKeys_ReturnsFalse()
        {
            var group = new PwGroupStub();
            bool result = KeeShareCompatibilityLogic.HasKeePassXCFormat(group);
            Assert.False(result);
        }

        [Fact]
        public void HasKeePassXCFormat_WithKeeShareReferencePath_ReturnsTrue()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShareReference.Path", "/path/to/share.kdbx");

            bool result = KeeShareCompatibilityLogic.HasKeePassXCFormat(group);

            Assert.True(result);
        }

        [Fact]
        public void HasKeePassXCFormat_WithKPXCKeeSharePath_ReturnsTrue()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KPXC_KeeShare_Path", "/path/to/share.kdbx");

            bool result = KeeShareCompatibilityLogic.HasKeePassXCFormat(group);

            Assert.True(result);
        }

        [Fact]
        public void HasKeePassXCFormat_WithKeeShareKey_ReturnsTrue()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShare", "path=\"/share.kdbx\"");

            bool result = KeeShareCompatibilityLogic.HasKeePassXCFormat(group);

            Assert.True(result);
        }

        [Fact]
        public void HasKeePassXCFormat_WithKeeShareAndActiveKey_ReturnsFalse()
        {
            // If both old KeeShare key and our Active key exist, it's already in KP2A format
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShare", "path=\"/share.kdbx\"");
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");

            bool result = KeeShareCompatibilityLogic.HasKeePassXCFormat(group);

            Assert.False(result);
        }

        [Fact]
        public void HasKeePassXCFormat_WithOnlyActiveKey_ReturnsFalse()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");

            bool result = KeeShareCompatibilityLogic.HasKeePassXCFormat(group);

            Assert.False(result);
        }

        #endregion

        #region TryImportKeePassXCConfig Tests

        [Fact]
        public void TryImportKeePassXCConfig_NullGroup_ReturnsFalse()
        {
            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(null);
            Assert.False(result);
        }

        [Fact]
        public void TryImportKeePassXCConfig_NotKeePassXCFormat_ReturnsFalse()
        {
            var group = new PwGroupStub();
            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);
            Assert.False(result);
        }

        [Fact]
        public void TryImportKeePassXCConfig_AlreadyHasKP2AConfig_ReturnsFalse()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShareReference.Path", "/path/to/share.kdbx");
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");

            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.False(result);
        }

        [Fact]
        public void TryImportKeePassXCConfig_WithKeeShareReferencePath_ImportsSuccessfully()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShareReference.Path", "/path/to/share.kdbx");

            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.True(result);
            Assert.Equal("true", group.CustomData.Get(KeeShareConfigLogic.ActiveKey));
            Assert.Equal("/path/to/share.kdbx", group.CustomData.Get(KeeShareConfigLogic.FilePathKey));
        }

        [Fact]
        public void TryImportKeePassXCConfig_WithKPXCKeeSharePath_ImportsSuccessfully()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KPXC_KeeShare_Path", "/kpxc/share.kdbx");

            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.True(result);
            Assert.Equal("/kpxc/share.kdbx", group.CustomData.Get(KeeShareConfigLogic.FilePathKey));
        }

        [Fact]
        public void TryImportKeePassXCConfig_DefaultsToSynchronizeType()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShareReference.Path", "/path/to/share.kdbx");

            KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.Equal("Synchronize", group.CustomData.Get(KeeShareConfigLogic.TypeKey));
        }

        [Theory]
        [InlineData("Export", "Export")]
        [InlineData("export", "Export")]
        [InlineData("Import", "Import")]
        [InlineData("import", "Import")]
        [InlineData("Sync", "Synchronize")]
        [InlineData("sync", "Synchronize")]
        [InlineData("Synchronize", "Synchronize")]
        public void TryImportKeePassXCConfig_MapsKeeShareReferenceType(string inputType, string expectedType)
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShareReference.Path", "/path.kdbx");
            group.CustomData.Set("KeeShareReference.Type", inputType);

            KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.Equal(expectedType, group.CustomData.Get(KeeShareConfigLogic.TypeKey));
        }

        [Theory]
        [InlineData("0", "Export")]
        [InlineData("1", "Import")]
        [InlineData("2", "Synchronize")]
        public void TryImportKeePassXCConfig_MapsKPXCTypeNumeric(string numericType, string expectedType)
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KPXC_KeeShare_Path", "/path.kdbx");
            group.CustomData.Set("KPXC_KeeShare_Type", numericType);

            KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.Equal(expectedType, group.CustomData.Get(KeeShareConfigLogic.TypeKey));
        }

        [Fact]
        public void TryImportKeePassXCConfig_ExtractsKeeShareReferencePassword()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShareReference.Path", "/path.kdbx");
            group.CustomData.Set("KeeShareReference.Password", "mypassword");

            KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.Equal("mypassword", group.CustomData.Get(KeeShareConfigLogic.PasswordKey));
        }

        [Fact]
        public void TryImportKeePassXCConfig_ExtractsKPXCPassword()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KPXC_KeeShare_Path", "/path.kdbx");
            group.CustomData.Set("KPXC_KeeShare_Password", "kpxcpassword");

            KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.Equal("kpxcpassword", group.CustomData.Get(KeeShareConfigLogic.PasswordKey));
        }

        [Fact]
        public void TryImportKeePassXCConfig_ParsesPathFromKeeShareXml_DoubleQuotes()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShare", "type=\"sync\" path=\"/xml/path.kdbx\"");

            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.True(result);
            Assert.Equal("/xml/path.kdbx", group.CustomData.Get(KeeShareConfigLogic.FilePathKey));
        }

        [Fact]
        public void TryImportKeePassXCConfig_ParsesPathFromKeeShareXml_SingleQuotes()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShare", "type='sync' path='/xml/single.kdbx'");

            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.True(result);
            Assert.Equal("/xml/single.kdbx", group.CustomData.Get(KeeShareConfigLogic.FilePathKey));
        }

        [Fact]
        public void TryImportKeePassXCConfig_NoPathFound_ReturnsFalse()
        {
            var group = new PwGroupStub();
            // Use data that truly has no "path=" pattern
            group.CustomData.Set("KeeShare", "type='sync' location='here'");

            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.False(result);
        }

        [Fact]
        public void TryImportKeePassXCConfig_EmptyPath_ReturnsFalse()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShareReference.Path", "");

            bool result = KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.False(result);
        }

        [Fact]
        public void TryImportKeePassXCConfig_CallsTouch()
        {
            var group = new PwGroupStub();
            group.CustomData.Set("KeeShareReference.Path", "/path.kdbx");

            KeeShareCompatibilityLogic.TryImportKeePassXCConfig(group);

            Assert.True(group.WasTouched);
        }

        #endregion
    }
}
