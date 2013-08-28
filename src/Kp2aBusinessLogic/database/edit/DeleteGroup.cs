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
	
	public class DeleteGroup : DeleteRunnable {
		
		private PwGroup _group;
		protected bool DontSave;

        public DeleteGroup(Context ctx, IKp2aApp app, PwGroup group, OnFinish finish)
            : base(finish, app)
        {
			SetMembers(ctx, app, group, false);
		}
        /*
        public DeleteGroup(Context ctx, Database db, PwGroup group, Activity act, OnFinish finish, bool dontSave)
            : base(finish)
        {
			SetMembers(ctx, db, group, act, dontSave);
		}
        
		public DeleteGroup(Context ctx, Database db, PwGroup group, OnFinish finish, bool dontSave):base(finish) {
			SetMembers(ctx, db, group, null, dontSave);
		}
        */
        private void SetMembers(Context ctx, IKp2aApp app, PwGroup group, bool dontSave)
        {
			base.SetMembers(ctx, app.GetDb());

			_group = group;
	        DontSave = dontSave;
            
		}

		public override bool CanRecycle
		{
			get
			{
				return CanRecycleGroup(_group);
			}
		}

		protected override UiStringKey QuestionsResourceId
		{
			get
			{
				return UiStringKey.AskDeletePermanentlyGroup;
			}
		}
		
		
		public override void Run() {
			StatusLogger.UpdateMessage(UiStringKey.DeletingGroup);
			//from KP Desktop
			PwGroup pg = _group;
			PwGroup pgParent = pg.ParentGroup;
			if(pgParent == null) return; // Can't remove virtual or root group
			
			PwDatabase pd = Db.KpDatabase;
			PwGroup pgRecycleBin = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);
			
			pgParent.Groups.Remove(pg);
			
			if ((DeletePermanently) || (!CanRecycle))
			{
				pg.DeleteAllObjects(pd);
				
				PwDeletedObject pdo = new PwDeletedObject(pg.Uuid, DateTime.Now);
				pd.DeletedObjects.Add(pdo);
				_onFinishToRun = new AfterDeletePermanently(OnFinishToRun, App, _group);
			}
			else // Recycle
			{
				bool bDummy = false;
				EnsureRecycleBin(ref pgRecycleBin, ref bDummy);
				
				pgRecycleBin.AddGroup(pg, true, true);
				pg.Touch(false);
				_onFinishToRun = new ActionOnFinish((success, message) => 
				                             {
					if ( success ) {
						// Mark new parent (Recycle bin) dirty
						PwGroup parent = _group.ParentGroup;
						if ( parent != null ) {
							Db.Dirty.Add(parent);
						}
						//Mark old parent dirty:
						Db.Dirty.Add(pgParent);
					} else {
						// Let's not bother recovering from a failure to save a deleted group.  It is too much work.
						App.LockDatabase(false);
					}
				}, OnFinishToRun);
			}

			// Save
			SaveDb save = new SaveDb(Ctx, App, OnFinishToRun, DontSave);
			save.SetStatusLogger(StatusLogger);
			save.Run();
			
		}

		
		private class AfterDeletePermanently : OnFinish {
			readonly IKp2aApp _app;

			readonly PwGroup _group;

			public AfterDeletePermanently(OnFinish finish, IKp2aApp app, PwGroup group):base(finish) {
				_app = app;
				_group = group;
			}
			
			public override void Run() {
				if ( Success ) {
					// Remove from group global
                    _app.GetDb().Groups.Remove(_group.Uuid);
					
					// Remove group from the dirty global (if it is present), not a big deal if this fails (doesn't throw)
                    _app.GetDb().Dirty.Remove(_group);
					
					// Mark parent dirty
					PwGroup parent = _group.ParentGroup;
					if ( parent != null ) {
                        _app.GetDb().Dirty.Add(parent);
					}
				} else {
					// Let's not bother recovering from a failure to save a deleted group.  It is too much work.
					_app.LockDatabase(false);
				}
				
				base.Run();
				
			}
			
		}
	}

}

