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
	
	public class DeleteGroup : RunnableOnFinish {
		
		private Database mDb;
		private PwGroup mGroup;
		private GroupBaseActivity mAct;
		private bool mDontSave;
		private Context mCtx;
		
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
			mCtx = ctx;
			mDb = db;
			mGroup = group;
			mAct = act;
			mDontSave = dontSave;
			
			mFinish = new AfterDelete(mFinish, mDb, mGroup);
		}
		
		
		
		
		public override void run() {
			//from KP Desktop
			PwGroup pg = mGroup;
			PwGroup pgParent = pg.ParentGroup;
			if(pgParent == null) return; // Can't remove virtual or root group
			
			PwDatabase pd = mDb.pm;
			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);
			bool bShiftPressed = false;
			
			bool bPermanent = true; //indicates whether we delete permanently or not
			//TODO use settings to enable Recycle Bin App-wide?
			if(pd.RecycleBinEnabled == false) bPermanent = true;
			else if(bShiftPressed) bPermanent = true;
			else if(pgRecycleBin == null) { }
			else if(pg == pgRecycleBin) bPermanent = true;
			else if(pg.IsContainedIn(pgRecycleBin)) bPermanent = true;
			else if(pgRecycleBin.IsContainedIn(pg)) bPermanent = true;
			
			if(bPermanent)
			{
				/* TODO KPDesktop?
				string strText = KPRes.DeleteGroupInfo + MessageService.NewParagraph +
					KPRes.DeleteGroupQuestion;
				if(!MessageService.AskYesNo(strText, KPRes.DeleteGroupTitle))
					return;
					*/
			}
			
			pgParent.Groups.Remove(pg);
			
			if(bPermanent)
			{
				pg.DeleteAllObjects(pd);
				
				PwDeletedObject pdo = new PwDeletedObject(pg.Uuid, DateTime.Now);
				pd.DeletedObjects.Add(pdo);
			}
			else // Recycle
			{
				bool bDummy = false;
				EnsureRecycleBin(ref pgRecycleBin, pd, ref bDummy, mCtx);
				
				pgRecycleBin.AddGroup(pg, true, true);
				pg.Touch(false);
			}

			// Save
			SaveDB save = new SaveDB(mCtx, mDb, mFinish, mDontSave);
			save.run();
			
		}

		public static void EnsureRecycleBin(ref PwGroup pgRecycleBin,
		                                     PwDatabase pdContext, ref bool bGroupListUpdateRequired, Context ctx)
		{
			if(pdContext == null) { return; }
			
			if(pgRecycleBin == pdContext.RootGroup)
			{
				pgRecycleBin = null;
			}
			
			if(pgRecycleBin == null)
			{
				pgRecycleBin = new PwGroup(true, true, ctx.GetString(Resource.String.RecycleBin),
				                           PwIcon.TrashBin);
				pgRecycleBin.EnableAutoType = false;
				pgRecycleBin.EnableSearching = false;
				pgRecycleBin.IsExpanded = false;
				pdContext.RootGroup.AddGroup(pgRecycleBin, true);
				
				pdContext.RecycleBinUuid = pgRecycleBin.Uuid;
				
				bGroupListUpdateRequired = true;
			}
			else { System.Diagnostics.Debug.Assert(pgRecycleBin.Uuid.EqualsValue(pdContext.RecycleBinUuid)); }
		}

		
		private class AfterDelete : OnFinish {
			Database mDb;

			PwGroup mGroup;

			public AfterDelete(OnFinish finish, Database db, PwGroup group):base(finish) {
				this.mDb = db;
				this.mGroup = group;
			}
			
			public override void run() {
				if ( mSuccess ) {
					// Remove from group global
					mDb.groups.Remove(mGroup.Uuid);
					
					// Remove group from the dirty global (if it is present), not a big deal if this fails
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

