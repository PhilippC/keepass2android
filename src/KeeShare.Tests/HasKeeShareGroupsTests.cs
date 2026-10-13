using KeeShare.Tests.TestHelpers;

namespace KeeShare.Tests
{
    public class HasKeeShareGroupsTests
    {
        [Fact]
        public void HasKeeShareGroups_WithNoGroups_ReturnsFalse()
        {
            var root = new PwGroupStub();
            bool result = KeeShareLogic.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithKeeShareActiveTrue_ReturnsTrue()
        {
            var root = new PwGroupStub();
            root.CustomData.Set("KeeShare.Active", "true");
            bool result = KeeShareLogic.HasKeeShareGroups(root);
            Assert.True(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithKeeShareActiveFalse_ReturnsFalse()
        {
            var root = new PwGroupStub();
            root.CustomData.Set("KeeShare.Active", "false");
            bool result = KeeShareLogic.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithNestedKeeShareGroup_ReturnsTrue()
        {
            var root = new PwGroupStub();
            var child1 = new PwGroupStub();
            var child2 = new PwGroupStub();
            child2.CustomData.Set("KeeShare.Active", "true");
            child1.AddGroup(child2, true);
            root.AddGroup(child1, true);
            bool result = KeeShareLogic.HasKeeShareGroups(root);
            Assert.True(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithMultipleNonKeeShareGroups_ReturnsFalse()
        {
            var root = new PwGroupStub();
            root.AddGroup(new PwGroupStub(), true);
            root.AddGroup(new PwGroupStub(), true);
            root.AddGroup(new PwGroupStub(), true);
            bool result = KeeShareLogic.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithDeeplyNestedKeeShareGroup_ReturnsTrue()
        {
            var root = new PwGroupStub();
            var level1 = new PwGroupStub();
            var level2 = new PwGroupStub();
            var level3 = new PwGroupStub();
            level3.CustomData.Set("KeeShare.Active", "true");
            level2.AddGroup(level3, true);
            level1.AddGroup(level2, true);
            root.AddGroup(level1, true);
            bool result = KeeShareLogic.HasKeeShareGroups(root);
            Assert.True(result);
        }
    }
}
