namespace KeeShare.Tests.TestHelpers
{
    /// <summary>
    /// Minimal stub of PwDatabase for testing KeeShare logic without Android dependencies.
    /// </summary>
    public class PwDatabaseStub
    {
        public bool IsOpen { get; set; } = true;
        public PwGroupStub? RootGroup { get; set; }
    }
}
