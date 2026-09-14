using KeeShare.Tests.TestHelpers;

namespace KeeShare.Tests
{
    public class ConfigurationTests
    {
        public ConfigurationTests()
        {
            // Reset device ID before each test
            KeeShareConfigLogic.TestDeviceId = "test-device-id";
        }

        #region GetDeviceFilePathKey Tests

        [Fact]
        public void GetDeviceFilePathKey_ReturnsKeyWithPrefix()
        {
            string key = KeeShareConfigLogic.GetDeviceFilePathKey();
            Assert.StartsWith(KeeShareConfigLogic.DeviceFilePathKeyPrefix, key);
        }

        [Fact]
        public void GetDeviceFilePathKey_ContainsDeviceId()
        {
            KeeShareConfigLogic.TestDeviceId = "my-unique-device";
            string key = KeeShareConfigLogic.GetDeviceFilePathKey();
            Assert.Equal("KeeShare.FilePath.my-unique-device", key);
        }

        [Fact]
        public void GetDeviceFilePathKey_ChangesWithDeviceId()
        {
            KeeShareConfigLogic.TestDeviceId = "device-1";
            string key1 = KeeShareConfigLogic.GetDeviceFilePathKey();

            KeeShareConfigLogic.TestDeviceId = "device-2";
            string key2 = KeeShareConfigLogic.GetDeviceFilePathKey();

            Assert.NotEqual(key1, key2);
        }

        #endregion

        #region GetEffectiveFilePath Tests

        [Fact]
        public void GetEffectiveFilePath_NullGroup_ReturnsNull()
        {
            string? result = KeeShareConfigLogic.GetEffectiveFilePath(null);
            Assert.Null(result);
        }

        [Fact]
        public void GetEffectiveFilePath_DevicePathExists_ReturnsDevicePath()
        {
            var group = new PwGroupStub();
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "/device/specific/path.kdbx");
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/original/path.kdbx");

            string? result = KeeShareConfigLogic.GetEffectiveFilePath(group);

            Assert.Equal("/device/specific/path.kdbx", result);
        }

        [Fact]
        public void GetEffectiveFilePath_NoDevicePath_FallsBackToOriginal()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/original/path.kdbx");

            string? result = KeeShareConfigLogic.GetEffectiveFilePath(group);

            Assert.Equal("/original/path.kdbx", result);
        }

        [Fact]
        public void GetEffectiveFilePath_NoPaths_ReturnsNull()
        {
            var group = new PwGroupStub();
            string? result = KeeShareConfigLogic.GetEffectiveFilePath(group);
            Assert.Null(result);
        }

        [Fact]
        public void GetEffectiveFilePath_EmptyDevicePath_FallsBackToOriginal()
        {
            var group = new PwGroupStub();
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "");
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/original/path.kdbx");

            string? result = KeeShareConfigLogic.GetEffectiveFilePath(group);

            Assert.Equal("/original/path.kdbx", result);
        }

        #endregion

        #region SetDeviceFilePath Tests

        [Fact]
        public void SetDeviceFilePath_NullGroup_DoesNotThrow()
        {
            // Should be a no-op
            var ex = Record.Exception(() => KeeShareConfigLogic.SetDeviceFilePath(null, "/some/path"));
            Assert.Null(ex);
        }

        [Fact]
        public void SetDeviceFilePath_SetsPath()
        {
            var group = new PwGroupStub();
            KeeShareConfigLogic.SetDeviceFilePath(group, "/new/device/path.kdbx");

            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            Assert.Equal("/new/device/path.kdbx", group.CustomData.Get(deviceKey));
        }

        [Fact]
        public void SetDeviceFilePath_CallsTouch()
        {
            var group = new PwGroupStub();
            KeeShareConfigLogic.SetDeviceFilePath(group, "/some/path.kdbx");

            Assert.True(group.WasTouched);
            Assert.Equal((true, false), group.LastTouchArgs);
        }

        [Fact]
        public void SetDeviceFilePath_NullPath_RemovesKey()
        {
            var group = new PwGroupStub();
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "/existing/path.kdbx");

            KeeShareConfigLogic.SetDeviceFilePath(group, null);

            Assert.False(group.CustomData.Exists(deviceKey));
        }

        [Fact]
        public void SetDeviceFilePath_EmptyPath_RemovesKey()
        {
            var group = new PwGroupStub();
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "/existing/path.kdbx");

            KeeShareConfigLogic.SetDeviceFilePath(group, "");

            Assert.False(group.CustomData.Exists(deviceKey));
        }

        [Fact]
        public void SetDeviceFilePath_UpdatesExisting()
        {
            var group = new PwGroupStub();
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "/old/path.kdbx");

            KeeShareConfigLogic.SetDeviceFilePath(group, "/new/path.kdbx");

            Assert.Equal("/new/path.kdbx", group.CustomData.Get(deviceKey));
        }

        #endregion

        #region EnableKeeShare Tests

        [Fact]
        public void EnableKeeShare_NullGroup_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                KeeShareConfigLogic.EnableKeeShare(null, "Export", "/path.kdbx"));
        }

        [Fact]
        public void EnableKeeShare_NullType_ThrowsArgumentException()
        {
            var group = new PwGroupStub();
            Assert.Throws<ArgumentException>(() =>
                KeeShareConfigLogic.EnableKeeShare(group, null, "/path.kdbx"));
        }

        [Fact]
        public void EnableKeeShare_EmptyType_ThrowsArgumentException()
        {
            var group = new PwGroupStub();
            Assert.Throws<ArgumentException>(() =>
                KeeShareConfigLogic.EnableKeeShare(group, "", "/path.kdbx"));
        }

        [Fact]
        public void EnableKeeShare_InvalidType_ThrowsArgumentException()
        {
            var group = new PwGroupStub();
            Assert.Throws<ArgumentException>(() =>
                KeeShareConfigLogic.EnableKeeShare(group, "Invalid", "/path.kdbx"));
        }

        [Theory]
        [InlineData("Export")]
        [InlineData("Import")]
        [InlineData("Synchronize")]
        public void EnableKeeShare_ValidType_SetsAllFields(string type)
        {
            var group = new PwGroupStub();
            KeeShareConfigLogic.EnableKeeShare(group, type, "/path/to/share.kdbx", "password123");

            Assert.Equal("true", group.CustomData.Get(KeeShareConfigLogic.ActiveKey));
            Assert.Equal(type, group.CustomData.Get(KeeShareConfigLogic.TypeKey));
            Assert.Equal("/path/to/share.kdbx", group.CustomData.Get(KeeShareConfigLogic.FilePathKey));
            Assert.Equal("password123", group.CustomData.Get(KeeShareConfigLogic.PasswordKey));
        }

        [Fact]
        public void EnableKeeShare_NullPassword_RemovesPasswordKey()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.PasswordKey, "old-password");

            KeeShareConfigLogic.EnableKeeShare(group, "Export", "/path.kdbx", null);

            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.PasswordKey));
        }

        [Fact]
        public void EnableKeeShare_EmptyPassword_RemovesPasswordKey()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.PasswordKey, "old-password");

            KeeShareConfigLogic.EnableKeeShare(group, "Export", "/path.kdbx", "");

            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.PasswordKey));
        }

        [Fact]
        public void EnableKeeShare_CallsTouch()
        {
            var group = new PwGroupStub();
            KeeShareConfigLogic.EnableKeeShare(group, "Export", "/path.kdbx");

            Assert.True(group.WasTouched);
            Assert.Equal((true, false), group.LastTouchArgs);
        }

        [Fact]
        public void EnableKeeShare_NullFilePath_DoesNotSetFilePathKey()
        {
            var group = new PwGroupStub();
            KeeShareConfigLogic.EnableKeeShare(group, "Export", null);

            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.FilePathKey));
        }

        #endregion

        #region UpdateKeeShareConfig Tests

        [Fact]
        public void UpdateKeeShareConfig_NullGroup_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                KeeShareConfigLogic.UpdateKeeShareConfig(null, "Export", "/path.kdbx", null));
        }

        [Fact]
        public void UpdateKeeShareConfig_InvalidType_ThrowsArgumentException()
        {
            var group = new PwGroupStub();
            Assert.Throws<ArgumentException>(() =>
                KeeShareConfigLogic.UpdateKeeShareConfig(group, "Invalid", "/path.kdbx", null));
        }

        [Fact]
        public void UpdateKeeShareConfig_UpdatesType()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");

            KeeShareConfigLogic.UpdateKeeShareConfig(group, "Import", null, null);

            Assert.Equal("Import", group.CustomData.Get(KeeShareConfigLogic.TypeKey));
        }

        [Fact]
        public void UpdateKeeShareConfig_UpdatesFilePath()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/old/path.kdbx");

            KeeShareConfigLogic.UpdateKeeShareConfig(group, null, "/new/path.kdbx", null);

            Assert.Equal("/new/path.kdbx", group.CustomData.Get(KeeShareConfigLogic.FilePathKey));
        }

        [Fact]
        public void UpdateKeeShareConfig_UpdatesPassword()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.PasswordKey, "old-pass");

            KeeShareConfigLogic.UpdateKeeShareConfig(group, null, null, "new-pass");

            Assert.Equal("new-pass", group.CustomData.Get(KeeShareConfigLogic.PasswordKey));
        }

        [Fact]
        public void UpdateKeeShareConfig_NullPassword_RemovesPasswordKey()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.PasswordKey, "old-pass");

            KeeShareConfigLogic.UpdateKeeShareConfig(group, null, null, null);

            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.PasswordKey));
        }

        [Fact]
        public void UpdateKeeShareConfig_NullType_DoesNotUpdateType()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");

            KeeShareConfigLogic.UpdateKeeShareConfig(group, null, "/path.kdbx", null);

            Assert.Equal("Export", group.CustomData.Get(KeeShareConfigLogic.TypeKey));
        }

        [Fact]
        public void UpdateKeeShareConfig_CallsTouch()
        {
            var group = new PwGroupStub();
            KeeShareConfigLogic.UpdateKeeShareConfig(group, "Export", null, null);

            Assert.True(group.WasTouched);
        }

        #endregion

        #region DisableKeeShare Tests

        [Fact]
        public void DisableKeeShare_NullGroup_DoesNotThrow()
        {
            var ex = Record.Exception(() => KeeShareConfigLogic.DisableKeeShare(null));
            Assert.Null(ex);
        }

        [Fact]
        public void DisableKeeShare_RemovesAllKeeShareKeys()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, "Export");
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/path.kdbx");
            group.CustomData.Set(KeeShareConfigLogic.PasswordKey, "password");
            group.CustomData.Set(KeeShareConfigLogic.TrustedCertificateKey, "cert");
            string deviceKey = KeeShareConfigLogic.GetDeviceFilePathKey();
            group.CustomData.Set(deviceKey, "/device/path.kdbx");

            KeeShareConfigLogic.DisableKeeShare(group);

            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.ActiveKey));
            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.TypeKey));
            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.FilePathKey));
            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.PasswordKey));
            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.TrustedCertificateKey));
            Assert.False(group.CustomData.Exists(deviceKey));
        }

        [Fact]
        public void DisableKeeShare_PreservesOtherCustomData()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            group.CustomData.Set("SomeOtherKey", "SomeValue");

            KeeShareConfigLogic.DisableKeeShare(group);

            Assert.False(group.CustomData.Exists(KeeShareConfigLogic.ActiveKey));
            Assert.Equal("SomeValue", group.CustomData.Get("SomeOtherKey"));
        }

        [Fact]
        public void DisableKeeShare_CallsTouch()
        {
            var group = new PwGroupStub();
            KeeShareConfigLogic.DisableKeeShare(group);

            Assert.True(group.WasTouched);
            Assert.Equal((true, false), group.LastTouchArgs);
        }

        #endregion
    }
}
