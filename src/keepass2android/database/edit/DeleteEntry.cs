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
	public class DeleteEntry : DeleteRunnable {
		
		private PwEntry mEntry;

		public DeleteEntry(Context ctx, Database db, PwEntry entry, OnFinish finish):base(finish) {
			mCtx = ctx;
			mDb = db;
			mEntry = entry;
			
		}

		public override bool CanRecycle
		{
			get
			{
				return CanRecycleGroup(mEntry.ParentGroup);
			}
		}

		protected override int QuestionsResourceId
		{
			get
			{
				return Resource.String.AskDeletePermanentlyEntry;
			}
		}

		public override void run() {

			PwDatabase pd = mDb.pm;

			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);

			bool bUpdateGroupList = false;
			DateTime dtNow = DateTime.Now;
			PwEntry pe = mEntry;
			PwGroup pgParent = pe.ParentGroup;
			if(pgParent != null)
			{
				pgParent.Entries.Remove(pe);
				

				if ((DeletePermanently) || (!CanRecycle))
				{
					PwDeletedObject pdo = new PwDeletedObject(pe.Uuid, dtNow);
					pd.DeletedObjects.Add(pdo);

					mFinish = new ActionOnFinish( (success, message) => 
					                             {
						if ( success ) {
							// Mark parent dirty
							if ( pgParent != null ) {
								mDb.dirty.Add(pgParent);
							}
						} else {
							// Let's not bother recovering from a failure to save a deleted entry.  It is too much work.
							App.setShutdown();
						}

					}, this.mFinish);
				}
				else // Recycle
				{
					EnsureRecycleBin(ref pgRecycleBin, ref bUpdateGroupList);
					
					pgRecycleBin.AddEntry(pe, true, true);
					pe.Touch(false);

					mFinish = new ActionOnFinish( (success, message) => 
					                             {
						if ( success ) {
							// Mark previous parent dirty
							if ( pgParent != null ) {
								mDb.dirty.Add(pgParent);
							}
							// Mark new parent dirty
							mDb.dirty.Add(pgRecycleBin);
						} else {
							// Let's not bother recovering from a failure to save a deleted entry.  It is too much work.
							App.setShutdown();
						}
						
					}, this.mFinish);
				}
			}

			// Commit database
			SaveDB save = new SaveDB(mCtx, mDb, mFinish, false);
			save.run();
			
			
		}
		
	}

}

