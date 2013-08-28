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
using Android.Content;
using KeePassLib;

namespace keepass2android
{
	public class DeleteEntry : DeleteRunnable {

        private readonly PwEntry _entry;

		public DeleteEntry(Context ctx, IKp2aApp app, PwEntry entry, OnFinish finish):base(finish, app) {
			Ctx = ctx;
			Db = app.GetDb();
			_entry = entry;
			
		}

		public override bool CanRecycle
		{
			get
			{
				return CanRecycleGroup(_entry.ParentGroup);
			}
		}

		protected override UiStringKey QuestionsResourceId
		{
			get
			{
				return UiStringKey.AskDeletePermanentlyEntry;
			}
		}

		public override void Run()
		{
			StatusLogger.UpdateMessage(UiStringKey.DeletingEntry);
			PwDatabase pd = Db.KpDatabase;

			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);

			bool bUpdateGroupList = false;
			DateTime dtNow = DateTime.Now;
			PwEntry pe = _entry;
			PwGroup pgParent = pe.ParentGroup;
			if(pgParent != null)
			{
				pgParent.Entries.Remove(pe);
				

				if ((DeletePermanently) || (!CanRecycle))
				{
					PwDeletedObject pdo = new PwDeletedObject(pe.Uuid, dtNow);
					pd.DeletedObjects.Add(pdo);

					_onFinishToRun = new ActionOnFinish((success, message) =>
						{
							if (success)
							{
								// Mark parent dirty
								Db.Dirty.Add(pgParent);
							}
							else
							{
								// Let's not bother recovering from a failure to save a deleted entry.  It is too much work.
								App.LockDatabase(false);
							}
						}, OnFinishToRun);
				}
				else // Recycle
				{
					EnsureRecycleBin(ref pgRecycleBin, ref bUpdateGroupList);
					
					pgRecycleBin.AddEntry(pe, true, true);
					pe.Touch(false);

					_onFinishToRun = new ActionOnFinish( (success, message) => 
					                             {
						if ( success ) {
							// Mark previous parent dirty
							Db.Dirty.Add(pgParent);
							// Mark new parent dirty
							Db.Dirty.Add(pgRecycleBin);
						} else {
							// Let's not bother recovering from a failure to save a deleted entry.  It is too much work.
							App.LockDatabase(false);
						}
						
					}, OnFinishToRun);
				}
			}

			// Commit database
			SaveDb save = new SaveDb(Ctx, App, OnFinishToRun, false);
			save.SetStatusLogger(StatusLogger);
			save.Run();
			
			
		}
		
	}

}

