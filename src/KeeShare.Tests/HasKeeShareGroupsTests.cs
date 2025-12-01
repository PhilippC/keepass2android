namespace KeeShare.Tests
{
    public class HasKeeShareGroupsTests
    {
        [Fact]
        public void HasKeeShareGroups_WithNoGroups_ReturnsFalse()
        {
            var root = new MockPwGroup();
            bool result = KeeShareHelpers.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithKeeShareActiveTrue_ReturnsTrue()
        {
            var root = new MockPwGroup();
            root.CustomData["KeeShare.Active"] = "true";
            bool result = KeeShareHelpers.HasKeeShareGroups(root);
            Assert.True(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithKeeShareActiveFalse_ReturnsFalse()
        {
            var root = new MockPwGroup();
            root.CustomData["KeeShare.Active"] = "false";
            bool result = KeeShareHelpers.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithNestedKeeShareGroup_ReturnsTrue()
        {
            var root = new MockPwGroup();
            var child1 = new MockPwGroup();
            var child2 = new MockPwGroup();
            child2.CustomData["KeeShare.Active"] = "true";
            child1.Groups.Add(child2);
            root.Groups.Add(child1);
            bool result = KeeShareHelpers.HasKeeShareGroups(root);
            Assert.True(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithMultipleNonKeeShareGroups_ReturnsFalse()
        {
            var root = new MockPwGroup();
            root.Groups.Add(new MockPwGroup());
            root.Groups.Add(new MockPwGroup());
            root.Groups.Add(new MockPwGroup());
            bool result = KeeShareHelpers.HasKeeShareGroups(root);
            Assert.False(result);
        }

        [Fact]
        public void HasKeeShareGroups_WithDeeplyNestedKeeShareGroup_ReturnsTrue()
        {
            var root = new MockPwGroup();
            var level1 = new MockPwGroup();
            var level2 = new MockPwGroup();
            var level3 = new MockPwGroup();
            level3.CustomData["KeeShare.Active"] = "true";
            level2.Groups.Add(level3);
            level1.Groups.Add(level2);
            root.Groups.Add(level1);
            bool result = KeeShareHelpers.HasKeeShareGroups(root);
            Assert.True(result);
        }
    }

    public class MockPwGroup
    {
        public Dictionary<string, string> CustomData { get; set; } = new Dictionary<string, string>();
        public List<MockPwGroup> Groups { get; set; } = new List<MockPwGroup>();
    }

    public static class KeeShareHelpers
    {
        public static bool HasKeeShareGroups(MockPwGroup group)
        {
            if (group.CustomData.ContainsKey("KeeShare.Active") &&
                group.CustomData["KeeShare.Active"] == "true")
                return true;

            foreach (var sub in group.Groups)
            {
                if (HasKeeShareGroups(sub)) return true;
            }
            return false;
        }
    }
}
