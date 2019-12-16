using System.Collections.Generic;
using KeePassLib;
using KeePassLib.Security;

namespace keepass2android
{
	public abstract class EditModeBase
	{
       

        public virtual bool IsVisible(string fieldKey)
	    {
	        return true;
	    }

	    public virtual IEnumerable<string> SortExtraFieldKeys(IEnumerable<string> keys)
	    {
	        return keys;
	    }

        protected bool? manualShowAddAttachments = null;

        public virtual bool ShowAddAttachments
        {
            get
            {
                if (manualShowAddAttachments != null) return (bool)manualShowAddAttachments;
                return true; }
            set { manualShowAddAttachments = value; }
        }


        protected bool? manualShowAddExtras = null;

        public virtual bool ShowAddExtras
        {
            get
            {
                if (manualShowAddExtras != null) return (bool) manualShowAddExtras; 
                return true;
            }
            set { manualShowAddExtras = value; }
        }

        public virtual string GetTitle(string key)
	    {
	        return key;
	    }

	    public virtual string GetFieldType(string key)
	    {
	        return "";
	    }

	    public virtual void InitializeEntry(PwEntry entry)
	    {
        }

	    public virtual void PrepareForSaving(PwEntry entry)
	    {
	    }
	}

    /// <summary>
    /// Holds the state of the EntrryEditActivity. This is required to be able to keep a partially modified entry in memory
    /// through the App variable. Serializing this state (especially the Entry/EntryInDatabase) can be a performance problem
    /// when there are big attachements.
    /// </summary>
    internal class EntryEditActivityState
	{
		internal PwEntry Entry, EntryInDatabase;
		internal bool ShowPassword = false;
		internal bool IsNew;
		internal PwIcon SelectedIconId;
		internal PwUuid SelectedCustomIconId = PwUuid.Zero;
		internal bool SelectedIcon = false;
		
		internal PwGroup ParentGroup;
		
		internal bool EntryModified;

		public EditModeBase EditMode { get; set; }
		
		//the key of the extra field to which the last triggered file selection process belongs
	    public string LastTriggeredFileSelectionProcessKey;
	}
}

