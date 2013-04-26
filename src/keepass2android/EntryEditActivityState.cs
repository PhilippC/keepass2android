
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;

namespace keepass2android
{
	/// <summary>
	/// Holds the state of the EntrryEditActivity. This is required to be able to keep a partially modified entry in memory
	/// through the App variable. Serializing this state (especially the mEntry/mEntryInDatabase) can be a performance problem
	/// when there are big attachements.
	/// </summary>
	internal class EntryEditActivityState
	{
		internal PwEntry mEntry, mEntryInDatabase;
		internal bool mShowPassword = false;
		internal bool mIsNew;
		internal PwIcon mSelectedIconID;
		internal PwUuid mSelectedCustomIconID = PwUuid.Zero;
		internal bool mSelectedIcon = false;
		
		internal PwGroup parentGroup;
		
		internal bool mEntryModified;

	}
}

