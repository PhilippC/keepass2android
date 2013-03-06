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
	
	public class DeleteGroup : DeleteRunnable {
		
		private PwGroup mGroup;
		private GroupBaseActivity mAct;
		protected bool mDontSave;

		public DeleteGroup(Context ctx, Database db, PwGroup group, GroupBaseActivity act, OnFinish finish):base(finish) {
			setMembers(ctx, db, group, act, false);
		}
		
		public DeleteGroup(Context ctx, Database db, PwGroup group, GroupBaseActivity act, OnFinish finish, bool dontSave):base(finish) {
			setMembers(ctx, db, group, act, dontSave);
		}

		public DeleteGroup(Context ctx, Database db, PwGroup group, OnFinish finish, bool dontSave):base(finish) {
			setMembers(ctx, db, group, null, dontSave);
		}
		
		private void setMembers(Context ctx, Database db, PwGroup group, GroupBaseActivity act, bool dontSave) {
			base.setMembers(ctx, db);

			mGroup = group;
			mAct = act;
			mDontSave = dontSave;

		}

		public override bool CanRecycle
		{
			get
			{
				return CanRecycleGroup(mGroup);
			}
		}

		protected override int QuestionsResourceId
		{
			get
			{
				return Resource.String.AskDeletePermanentlyGroup;
			}
		}
		
		
		public override void run() {
			//from KP Desktop
			PwGroup pg = mGroup;
			PwGroup pgParent = pg.ParentGroup;
			if(pgParent == null) return; // Can't remove virtual or root group
			
			PwDatabase pd = mDb.pm;
			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);
			
			pgParent.Groups.Remove(pg);
			
			if ((DeletePermanently) || (!CanRecycle))
			{
				pg.DeleteAllObjects(pd);
				
				PwDeletedObject pdo = new PwDeletedObject(pg.Uuid, DateTime.Now);
				pd.DeletedObjects.Add(pdo);
				mFinish = new AfterDeletePermanently(mFinish, mDb, mGroup);
			}
			else // Recycle
			{
				bool bDummy = false;
				EnsureRecycleBin(ref pgRecycleBin, ref bDummy);
				
				pgRecycleBin.AddGroup(pg, true, true);
				pg.Touch(false);
				mFinish = new ActionOnFinish((success, message) => 
				                             {
					if ( success ) {
						// Mark new parent (Recycle bin) dirty
						PwGroup parent = mGroup.ParentGroup;
						if ( parent != null ) {
							mDb.dirty.Add(parent);
						}
						//Mark old parent dirty:
						mDb.dirty.Add(pgParent);
					} else {
						// Let's not bother recovering from a failure to save a deleted group.  It is too much work.
						App.setShutdown();
					}
				}, this.mFinish);
			}

			// Save
			SaveDB save = new SaveDB(mCtx, mDb, mFinish, mDontSave);
			save.run();
			
		}

		
		private class AfterDeletePermanently : OnFinish {
			Database mDb;

			PwGroup mGroup;

			public AfterDeletePermanently(OnFinish finish, Database db, PwGroup group):base(finish) {
				this.mDb = db;
				this.mGroup = group;
			}
			
			public override void run() {
				if ( mSuccess ) {
					// Remove from group global
					mDb.groups.Remove(mGroup.Uuid);
					
					// Remove group from the dirty global (if it is present), not a big deal if this fails (doesn't throw)
					mDb.dirty.Remove(mGroup);
					
					// Mark parent dirty
					PwGroup parent = mGroup.ParentGroup;
					if ( parent != null ) {
						mDb.dirty.Add(parent);
					}
				} else {
					// Let's not bother recovering from a failure to save a deleted group.  It is too much work.
					App.setShutdown();
				}
				
				base.run();
				
			}
			
		}
	}

}

