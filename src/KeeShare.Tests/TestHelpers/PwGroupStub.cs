using System.Collections;

namespace KeeShare.Tests.TestHelpers
{
    /// <summary>
    /// Minimal stub of PwGroup for testing KeeShare logic without Android dependencies.
    /// </summary>
    public class PwGroupStub
    {
        public CustomDataStub CustomData { get; } = new CustomDataStub();
        public List<PwGroupStub> Groups { get; } = new List<PwGroupStub>();
        public List<PwEntryStub> Entries { get; } = new List<PwEntryStub>();

        public string Name { get; set; } = "";
        public PwGroupStub? ParentGroup { get; set; }

        /// <summary>
        /// Tracks whether Touch was called (for test verification).
        /// </summary>
        public bool WasTouched { get; private set; }

        /// <summary>
        /// The arguments passed to the last Touch call.
        /// </summary>
        public (bool bModified, bool bTouchParents)? LastTouchArgs { get; private set; }

        public void AddGroup(PwGroupStub group, bool takeOwnership = true)
        {
            Groups.Add(group);
            if (takeOwnership)
            {
                group.ParentGroup = this;
            }
        }

        public void AddEntry(PwEntryStub entry, bool takeOwnership = true)
        {
            Entries.Add(entry);
            if (takeOwnership)
            {
                entry.ParentGroup = this;
            }
        }

        public void Touch(bool bModified, bool bTouchParents = false)
        {
            WasTouched = true;
            LastTouchArgs = (bModified, bTouchParents);
        }

        /// <summary>
        /// Resets the touch tracking state for tests.
        /// </summary>
        public void ResetTouchTracking()
        {
            WasTouched = false;
            LastTouchArgs = null;
        }
    }

    /// <summary>
    /// Minimal stub of CustomData for testing.
    /// Implements IEnumerable for iteration support.
    /// </summary>
    public class CustomDataStub : IEnumerable<KeyValuePair<string, string>>
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

        public bool Exists(string key)
        {
            return _data.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return _data.Remove(key);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Gets the number of items (for test assertions).
        /// </summary>
        public int Count => _data.Count;
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














