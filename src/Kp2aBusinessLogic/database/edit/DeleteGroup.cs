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
using Android.App;
using Android.Content;
using KeePassLib;

namespace keepass2android
{
	
	public class DeleteGroup : DeleteRunnable {
		
		private PwGroup _group;
		protected bool DontSave;

        public DeleteGroup(Activity activity, IKp2aApp app, PwGroup group, OnFinish finish)
            : base(activity, finish, app)
        {
			SetMembers(activity, app, group, false);
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
        private void SetMembers(Activity activity, IKp2aApp app, PwGroup group, bool dontSave)
        {
			base.SetMembers(activity, app.GetDb());

			_group = group;
	        DontSave = dontSave;
            
		}

		public override bool CanRecycle
		{
			get
			{
				return App.GetDb().DatabaseFormat.CanRecycle && CanRecycleGroup(_group);
			}
		}

		protected override UiStringKey QuestionRecycleResourceId
		{
			get
			{
				return UiStringKey.AskDeletePermanentlyGroup;
			}
		}

		protected override UiStringKey QuestionNoRecycleResourceId
		{
			get { return UiStringKey.AskDeletePermanentlyGroupNoRecycle; }
		}

		protected override void PerformDelete(List<PwGroup> touchedGroups, List<PwGroup> permanentlyDeletedGroups)
	    {
	        DoDeleteGroup(_group, touchedGroups, permanentlyDeletedGroups);
	    }

	    public override UiStringKey StatusMessage
	    {
	        get { return UiStringKey.DeletingGroup; }
	    }
	}

}

