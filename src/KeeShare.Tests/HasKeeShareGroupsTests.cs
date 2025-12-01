using KeePassLib;
using keepass2android;

namespace KeeShare.Tests
{
    public class HasKeeShareGroupsTests
    {
        [Fact]
        public void HasKeeShareGroups_WithNoGroups_ReturnsFalse()
        {
            var root = new PwGroup();
            bool result = keepass2android.KeeShare.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithKeeShareActiveTrue_ReturnsTrue()
        {
            var root = new PwGroup();
            root.CustomData.Set("KeeShare.Active", "true");
            bool result = keepass2android.KeeShare.HasKeeShareGroups(root);
            Assert.True(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithKeeShareActiveFalse_ReturnsFalse()
        {
            var root = new PwGroup();
            root.CustomData.Set("KeeShare.Active", "false");
            bool result = keepass2android.KeeShare.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithNestedKeeShareGroup_ReturnsTrue()
        {
            var root = new PwGroup();
            var child1 = new PwGroup();
            var child2 = new PwGroup();
            child2.CustomData.Set("KeeShare.Active", "true");
            child1.AddGroup(child2, true);
            root.AddGroup(child1, true);
            bool result = keepass2android.KeeShare.HasKeeShareGroups(root);
            Assert.True(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithMultipleNonKeeShareGroups_ReturnsFalse()
        {
            var root = new PwGroup();
            root.AddGroup(new PwGroup(), true);
            root.AddGroup(new PwGroup(), true);
            root.AddGroup(new PwGroup(), true);
            bool result = keepass2android.KeeShare.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithDeeplyNestedKeeShareGroup_ReturnsTrue()
        {
            var root = new PwGroup();
            var level1 = new PwGroup();
            var level2 = new PwGroup();
            var level3 = new PwGroup();
            level3.CustomData.Set("KeeShare.Active", "true");
            level2.AddGroup(level3, true);
            level1.AddGroup(level2, true);
            root.AddGroup(level1, true);
            bool result = keepass2android.KeeShare.HasKeeShareGroups(root);
            Assert.True(result);
        }
    }
}
