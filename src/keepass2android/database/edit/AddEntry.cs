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
	public class AddEntry : RunnableOnFinish {
		protected Database mDb;
		private PwEntry mEntry;
		private PwGroup mParentGroup;
		private Context mCtx;
		
		public static AddEntry getInstance(Context ctx, Database db, PwEntry entry, PwGroup parentGroup, OnFinish finish) {

			return new AddEntry(ctx, db, entry, parentGroup, finish);
		}
		
		protected AddEntry(Context ctx, Database db, PwEntry entry, PwGroup parentGroup, OnFinish finish):base(finish) {
			mCtx = ctx;
			mParentGroup = parentGroup;
			mDb = db;
			mEntry = entry;
			
			mFinish = new AfterAdd(db, entry, mFinish);
		}
		
		
		public override void run() {
			mParentGroup.AddEntry(mEntry, true);
			
			// Commit to disk
			SaveDB save = new SaveDB(mCtx, mDb, mFinish);
			save.run();
		}
		
		private class AfterAdd : OnFinish {
			protected Database mDb;
			private PwEntry mEntry;

			public AfterAdd(Database db, PwEntry entry, OnFinish finish):base(finish) {
				mDb = db;
				mEntry = entry;

			}
			


			public override void run() {
				if ( mSuccess ) {
					
					PwGroup parent = mEntry.ParentGroup; 
					
					// Mark parent group dirty
					mDb.dirty.Add(parent);
					
					// Add entry to global
					mDb.entries[mEntry.Uuid] = mEntry;
					
				} else {
					//TODO test fail
					mEntry.ParentGroup.Entries.Remove(mEntry);
				}
				
				base.run();
			}
		}
		
		
	}

}

