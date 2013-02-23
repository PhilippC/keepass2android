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
	public class DeleteEntry : RunnableOnFinish {
		
		private Database mDb;
		private PwEntry mEntry;
		private Context mCtx;
		
		public DeleteEntry(Context ctx, Database db, PwEntry entry, OnFinish finish):base(finish) {
			mCtx = ctx;
			mDb = db;
			mEntry = entry;
			
		}

		
		public override void run() {

			PwDatabase pd = mDb.pm;
			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);
			bool bShiftPressed = false;

			bool bUpdateGroupList = false;
			DateTime dtNow = DateTime.Now;
			PwEntry pe = mEntry;
			PwGroup pgParent = pe.ParentGroup;
			if(pgParent != null)
			{
				pgParent.Entries.Remove(pe);
				
				bool bPermanent = true;
				if(pd.RecycleBinEnabled == false) bPermanent = true;
				else if(bShiftPressed) bPermanent = true;
				else if(pgRecycleBin == null) { } // Recycle
				else if(pgParent == pgRecycleBin) bPermanent = true;
				else if(pgParent.IsContainedIn(pgRecycleBin)) bPermanent = true;
				
				if(bPermanent)
				{
				/* TODO KP Desktop
				if(!MessageService.AskYesNo(bSingle ? KPRes.DeleteEntriesQuestionSingle :
					                            KPRes.DeleteEntriesQuestion, bSingle ? KPRes.DeleteEntriesTitleSingle :
					                            KPRes.DeleteEntriesTitle))
						return;
*/
					

					PwDeletedObject pdo = new PwDeletedObject(pe.Uuid, dtNow);
					pd.DeletedObjects.Add(pdo);
				}
				else // Recycle
				{
					DeleteGroup.EnsureRecycleBin(ref pgRecycleBin, pd, ref bUpdateGroupList, mCtx);
					
					pgRecycleBin.AddEntry(pe, true, true);
					pe.Touch(false);
				}
			}
			
			// Save
			mFinish = new AfterDelete(mFinish, pgParent, mEntry, mDb);
			
			// Commit database
			SaveDB save = new SaveDB(mCtx, mDb, mFinish, false);
			save.run();
			
			
		}
		
		private class AfterDelete : OnFinish {
			
			private PwGroup mParent;
			private PwEntry mEntry;
			Database mDb;
			
			public AfterDelete(OnFinish finish, PwGroup parent, PwEntry entry, Database db):base(finish) {
				mParent = parent;
				mEntry = entry;
				mDb = db;
			}
			
			public override void run() {
				if ( mSuccess ) {
					// Mark parent dirty
					if ( mParent != null ) {
						mDb.dirty.Add(mParent);
					}
				} else {
					// Let's not bother recovering from a failure to save a deleted entry.  It is too much work.
					App.setShutdown();

				}
				
				base.run();
				
			}
			
		}
		
	}

}

