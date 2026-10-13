using KeeShare.Tests.TestHelpers;

namespace KeeShare.Tests
{
    public class KeeShareItemTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_NullGroup_ThrowsArgumentNullException()
        {
            var db = new PwDatabaseStub();
            Assert.Throws<ArgumentNullException>(() => new KeeShareItemStub(null!, db));
        }

        [Fact]
        public void Constructor_NullDatabase_ThrowsArgumentNullException()
        {
            var group = new PwGroupStub();
            Assert.Throws<ArgumentNullException>(() => new KeeShareItemStub(group, null!));
        }

        [Fact]
        public void Constructor_ValidArguments_SetsProperties()
        {
            var group = new PwGroupStub();
            var db = new PwDatabaseStub();

            var item = new KeeShareItemStub(group, db);

            Assert.Same(group, item.Group);
            Assert.Same(db, item.Database);
        }

        #endregion

        #region Type Property Tests

        [Fact]
        public void Type_NoTypeSet_ReturnsEmptyString()
        {
            var group = new PwGroupStub();
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.Equal("", item.Type);
        }

        [Theory]
        [InlineData("Export")]
        [InlineData("Import")]
        [InlineData("Synchronize")]
        public void Type_TypeSet_ReturnsType(string type)
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.TypeKey, type);
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.Equal(type, item.Type);
        }

        #endregion

        #region OriginalPath Property Tests

        [Fact]
        public void OriginalPath_NoPathSet_ReturnsEmptyString()
        {
            var group = new PwGroupStub();
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.Equal("", item.OriginalPath);
        }

        [Fact]
        public void OriginalPath_PathSet_ReturnsPath()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.FilePathKey, "/path/to/share.kdbx");
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.Equal("/path/to/share.kdbx", item.OriginalPath);
        }

        #endregion

        #region Password Property Tests

        [Fact]
        public void Password_NoPasswordSet_ReturnsEmptyString()
        {
            var group = new PwGroupStub();
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.Equal("", item.Password);
        }

        [Fact]
        public void Password_PasswordSet_ReturnsPassword()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.PasswordKey, "secret123");
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.Equal("secret123", item.Password);
        }

        #endregion

        #region IsActive Property Tests

        [Fact]
        public void IsActive_NoActiveKey_ReturnsFalse()
        {
            var group = new PwGroupStub();
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.False(item.IsActive);
        }

        [Fact]
        public void IsActive_ActiveKeyTrue_ReturnsTrue()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "true");
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.True(item.IsActive);
        }

        [Fact]
        public void IsActive_ActiveKeyFalse_ReturnsFalse()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "false");
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.False(item.IsActive);
        }

        [Fact]
        public void IsActive_ActiveKeyOtherValue_ReturnsFalse()
        {
            var group = new PwGroupStub();
            group.CustomData.Set(KeeShareConfigLogic.ActiveKey, "yes");
            var item = new KeeShareItemStub(group, new PwDatabaseStub());

            Assert.False(item.IsActive);
        }

        #endregion
    }
}
