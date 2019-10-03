namespace keepass2android.Io.ItemLocation
{
    public abstract class OneDrive2PrefixContainer
    {
        public abstract string Onedrive2ProtocolId { get; }
        public string Onedrive2Prefix { get { return Onedrive2ProtocolId + "://"; } }
    }

    //for permissions including all my files and all shared files
    public class OneDrive2FullPrefixContainer : OneDrive2PrefixContainer
    {
        public override string Onedrive2ProtocolId { get { return "onedrive2_full"; }}
    }

    //for permissions including all my files
    public class OneDrive2MyFilesPrefixContainer : OneDrive2PrefixContainer
    {
        public override string Onedrive2ProtocolId { get { return "onedrive2_myfiles"; } }
    }

    //for permissions to app folder only
    public class OneDrive2AppFolderPrefixContainer : OneDrive2PrefixContainer
    {
        public override string Onedrive2ProtocolId { get { return "onedrive2_appfolder"; } }
    }
}