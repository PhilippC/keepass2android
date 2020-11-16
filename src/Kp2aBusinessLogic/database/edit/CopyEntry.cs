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
using KeePassLib.Security;

namespace keepass2android.database.edit
{
	public class CopyEntry: AddEntry
	{
		public CopyEntry(Activity ctx, IKp2aApp app, PwEntry entry, OnFinish finish, Database db)
			: base(ctx, db, app, CreateCopy(entry, app), entry.ParentGroup, finish)
		{
		}

		private static PwEntry CreateCopy(PwEntry entry, IKp2aApp app)
		{
			var newEntry = entry.CloneDeep();
			newEntry.SetUuid(new PwUuid(true), true); // Create new UUID
			string strTitle = newEntry.Strings.ReadSafe(PwDefs.TitleField);
			newEntry.Strings.Set(PwDefs.TitleField, new ProtectedString(
					false, strTitle + " - " + app.GetResourceString(UiStringKey.DuplicateTitle)));

			return newEntry;
		}
	}
}