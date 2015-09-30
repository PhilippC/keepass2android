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

	public class AddGroup : RunnableOnFinish {
		internal Database Db
		{
			get { return _app.GetDb(); }
		}
		private IKp2aApp _app;
		private readonly String _name;
		private readonly int _iconId;
		private readonly PwUuid _groupCustomIconId;
		internal PwGroup Group;
		internal PwGroup Parent;
		protected bool DontSave;
		readonly Context _ctx;
		
		
		public static AddGroup GetInstance(Context ctx, IKp2aApp app, string name, int iconid, PwUuid groupCustomIconId, PwGroup parent, OnFinish finish, bool dontSave) {
			return new AddGroup(ctx, app, name, iconid, groupCustomIconId, parent, finish, dontSave);
		}


		private AddGroup(Context ctx, IKp2aApp app, String name, int iconid, PwUuid groupCustomIconId, PwGroup parent, OnFinish finish, bool dontSave)
			: base(finish)
		{
			_ctx = ctx;
			_name = name;
			_iconId = iconid;
			_groupCustomIconId = groupCustomIconId;
			Parent = parent;
			DontSave = dontSave;
			_app = app;

			_onFinishToRun = new AfterAdd(this, OnFinishToRun);
		}
		
		
		public override void Run() {
			StatusLogger.UpdateMessage(UiStringKey.AddingGroup);
			// Generate new group
			Group = new PwGroup(true, true, _name, (PwIcon)_iconId);
			if (_groupCustomIconId != null)
			{
				Group.CustomIconUuid = _groupCustomIconId;
			}
			Parent.AddGroup(Group, true);

			// Commit to disk
			SaveDb save = new SaveDb(_ctx, _app, OnFinishToRun, DontSave);
			save.SetStatusLogger(StatusLogger);
			save.Run();
		}
		
		private class AfterAdd : OnFinish {
			readonly AddGroup _addGroup;

			public AfterAdd(AddGroup addGroup,OnFinish finish): base(finish) {
				_addGroup = addGroup;
			}
				

			public override void Run() {
				
				if ( Success ) {
					// Mark parent group dirty
					_addGroup.Db.Dirty.Add(_addGroup.Parent);
					
					// Add group to global list
					_addGroup.Db.Groups[_addGroup.Group.Uuid] = _addGroup.Group;
				} else {
					StatusLogger.UpdateMessage(UiStringKey.UndoingChanges);
					_addGroup.Parent.Groups.Remove(_addGroup.Group);
				}
				
				base.Run();
			}
			
		}
		
		
	}

}

