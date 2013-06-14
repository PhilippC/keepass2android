/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

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

	public class UpdateEntry : RunnableOnFinish {
		private Database mDb;
		private PwEntry mOldE;
		private PwEntry mNewE;
		private Context mCtx;
		
		public UpdateEntry(Context ctx, Database db, PwEntry oldE, PwEntry newE, OnFinish finish):base(finish) {
			mCtx = ctx;
			mDb = db;
			mOldE = oldE;
			mNewE = newE;

			mFinish = new AfterUpdate(oldE, newE, db, finish);
		}
		
		
		public override void run() {
			// Commit to disk
			SaveDB save = new SaveDB(mCtx, mDb, mFinish);
			save.run();
		}
		
		private class AfterUpdate : OnFinish {
			private PwEntry mBackup;
			private PwEntry mUpdatedEntry;
			private Database mDb;
			
			public AfterUpdate(PwEntry backup, PwEntry updatedEntry, Database db, OnFinish finish):base(finish) {
				mBackup = backup;
				mUpdatedEntry = updatedEntry;
				mDb = db;
			}
			
			public override void run() {
				if ( mSuccess ) {
					// Mark group dirty if title, icon or Expiry stuff changes
					if ( ! mBackup.Strings.ReadSafe (PwDefs.TitleField).Equals(mUpdatedEntry.Strings.ReadSafe (PwDefs.TitleField)) 
					    || ! mBackup.IconId.Equals(mUpdatedEntry.IconId) 
					    || ! mBackup.CustomIconUuid.EqualsValue(mUpdatedEntry.CustomIconUuid)
					    || mBackup.Expires != mUpdatedEntry.Expires
					    || (mBackup.Expires && (! mBackup.ExpiryTime.Equals(mUpdatedEntry.ExpiryTime)))
					    )
					
					{
						PwGroup parent = mUpdatedEntry.ParentGroup;
						if ( parent != null ) {

							// Mark parent group dirty
							mDb.dirty.Add(parent);
							
						}
					}
				} else {
					// If we fail to save, back out changes to global structure
					//TODO test fail
					mUpdatedEntry.AssignProperties(mBackup, false, true, false);
				}
				
				base.run();
			}
			
		}
		
		
	}

}

