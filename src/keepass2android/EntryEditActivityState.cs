using System.Collections.Generic;
using KeePassLib;

namespace keepass2android
{
	interface IEditMode
	{
		bool IsVisible(string fieldKey);

		IEnumerable<string> SortExtraFieldKeys(IEnumerable<string> keys);

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

		public IEditMode EditMode { get; set; }
	}
}

