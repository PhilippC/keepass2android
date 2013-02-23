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

	public class AddGroup : RunnableOnFinish {
		internal Database mDb;
		private String mName;
		private int mIconID;
		internal PwGroup mGroup;
		internal PwGroup mParent;
		protected bool mDontSave;
		Context mCtx;
		
		
		public static AddGroup getInstance(Context ctx, Database db, String name, int iconid, PwGroup parent, OnFinish finish, bool dontSave) {
			return new AddGroup(ctx, db, name, iconid, parent, finish, dontSave);
		}
		
		
		private AddGroup(Context ctx, Database db, String name, int iconid, PwGroup parent, OnFinish finish, bool dontSave): base(finish) {
			mCtx = ctx;
			mDb = db;
			mName = name;
			mIconID = iconid;
			mParent = parent;
			mDontSave = dontSave;
			
			mFinish = new AfterAdd(this, mFinish);
		}
		
		
		public override void run() {
			PwDatabase pm = mDb.pm;
			
			// Generate new group
			mGroup = new PwGroup(true, true, mName, (PwIcon)mIconID);
			mParent.AddGroup(mGroup, true);

			// Commit to disk
			SaveDB save = new SaveDB(mCtx, mDb, mFinish, mDontSave);
			save.run();
		}
		
		private class AfterAdd : OnFinish {

			AddGroup addGroup;

			public AfterAdd(AddGroup addGroup,OnFinish finish): base(finish) {
				this.addGroup = addGroup;
			}
				

			public override void run() {
				
				if ( mSuccess ) {
					// Mark parent group dirty
					addGroup.mDb.dirty.Add(addGroup.mParent);
					
					// Add group to global list
					addGroup.mDb.groups[addGroup.mGroup.Uuid] = addGroup.mGroup;
				} else {
					addGroup.mParent.Groups.Remove(addGroup.mGroup);
				}
				
				base.run();
			}
			
		}
		
		
	}

}

