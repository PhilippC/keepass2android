/*
 * Integration Test Stubs
 * 
 * These stubs mirror the real KeePassLib API surface used by KeeShare.
 * They allow the integration tests to verify behavior patterns without
 * requiring the full Android build environment.
 * 
 * When building against the real KeePassLib, these stubs should be
 * removed and replaced with references to the actual classes.
 */

using System;
using System.Collections.Generic;

namespace KeeShare.Integration.Tests
{
    /// <summary>
    /// Test stub for PwEntry that mirrors the real API
    /// </summary>
    public class TestPwEntry
    {
        public string Title { get; set; }
        public Guid Uuid { get; set; } = Guid.NewGuid();
        
        public TestPwEntry CloneDeep()
        {
            return new TestPwEntry
            {
                Title = this.Title,
                Uuid = this.Uuid
            };
        }
    }
    
    /// <summary>
    /// Test stub for PwGroup that mirrors the real API including CloneDeep
    /// </summary>
    public class TestPwGroup
    {
        public string Name { get; set; }
        public Guid Uuid { get; set; } = Guid.NewGuid();
        
        private List<TestPwEntry> _entries = new List<TestPwEntry>();
        private List<TestPwGroup> _subgroups = new List<TestPwGroup>();
        
        public int EntryCount => _entries.Count;
        public IReadOnlyList<TestPwEntry> Entries => _entries;
        public IReadOnlyList<TestPwGroup> Groups => _subgroups;
        
        public void AddEntry(TestPwEntry entry) => _entries.Add(entry);
        public void AddSubgroup(TestPwGroup group) => _subgroups.Add(group);
        
        /// <summary>
        /// Mirrors PwGroup.CloneDeep() - creates a deep independent copy
        /// </summary>
        public TestPwGroup CloneDeep()
        {
            var clone = new TestPwGroup
            {
                Name = this.Name,
                Uuid = this.Uuid
            };
            
            foreach (var entry in _entries)
            {
                clone._entries.Add(entry.CloneDeep());
            }
            
            foreach (var subgroup in _subgroups)
            {
                clone._subgroups.Add(subgroup.CloneDeep());
            }
            
            return clone;
        }
        
        /// <summary>
        /// Gets total entry count including subgroups (recursive)
        /// </summary>
        public int GetTotalEntryCount()
        {
            int count = _entries.Count;
            foreach (var subgroup in _subgroups)
            {
                count += subgroup.GetTotalEntryCount();
            }
            return count;
        }
    }
    
    /// <summary>
    /// Test stub for PwDatabase CustomData storage
    /// </summary>
    public class TestPwDatabase : KeePassLib.PwDatabase
    {
        // Inherits from the stub in KeePassLib namespace
    }
}

namespace KeePassLib
{
    /// <summary>
    /// Minimal PwDatabase stub for trust settings tests
    /// </summary>
    public class PwDatabase
    {
        private Collections.StringDictionaryEx _customData = new Collections.StringDictionaryEx();
        
        public Collections.StringDictionaryEx CustomData => _customData;
    }
}

namespace KeePassLib.Collections
{
    /// <summary>
    /// String dictionary for CustomData storage
    /// </summary>
    public class StringDictionaryEx
    {
        private Dictionary<string, string> _dict = new Dictionary<string, string>();
        
        public void Set(string key, string value) => _dict[key] = value;
        
        public string Get(string key) => _dict.TryGetValue(key, out var val) ? val : null;
    }
}

namespace keepass2android
{
    /// <summary>
    /// Logging stub for tests
    /// </summary>
    public static class Kp2aLog
    {
        public static void Log(string message)
        {
            Console.WriteLine($"[Kp2aLog] {message}");
        }
    }
}
