namespace KeeShare.Tests.TestHelpers
{
    /// <summary>
    /// Minimal stub of PwGroup for testing HasKeeShareGroups logic without Android dependencies.
    /// </summary>
    public class PwGroupStub
    {
        public CustomDataStub CustomData { get; } = new CustomDataStub();
        public List<PwGroupStub> Groups { get; } = new List<PwGroupStub>();

        public void AddGroup(PwGroupStub group, bool takeOwnership = true)
        {
            Groups.Add(group);
        }
    }

    /// <summary>
    /// Minimal stub of CustomData for testing.
    /// </summary>
    public class CustomDataStub
    {
        private readonly Dictionary<string, string> _data = new Dictionary<string, string>();

        public void Set(string key, string value)
        {
            _data[key] = value;
        }

        public string? Get(string key)
        {
            return _data.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <summary>
    /// Copy of HasKeeShareGroups logic for testing without Android dependencies.
    /// Kept in sync with KeeShare.HasKeeShareGroups in the app project.
    /// </summary>
    public static class KeeShareLogic
    {
        public static bool HasKeeShareGroups(PwGroupStub group)
        {
            if (group.CustomData.Get("KeeShare.Active") == "true")
                return true;
            
            foreach (var sub in group.Groups)
            {
                if (HasKeeShareGroups(sub)) return true;
            }
            return false;
        }
    }
}


