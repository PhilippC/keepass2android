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
using Android.App;
using Android.Content;
using KeePassLib;

namespace keepass2android
{

	public class EditGroup : RunnableOnFinish {
		internal Database Db
		{
			get { return _app.GetDb(); }
		}
		private IKp2aApp _app;
		private readonly String _name;
		private readonly PwIcon _iconId;
		private readonly PwUuid _customIconId;
		internal PwGroup Group;
		readonly Activity _ctx;

		public EditGroup(Activity ctx, IKp2aApp app, String name, PwIcon iconid, PwUuid customIconId, PwGroup group, OnFinish finish)
			: base(ctx, finish)
		{
			_ctx = ctx;
			_name = name;
			_iconId = iconid;
			Group = group;
			_customIconId = customIconId;
			_app = app;

			_onFinishToRun = new AfterEdit(ctx, this, OnFinishToRun);
		}
		
		
		public override void Run() {
			// modify group:
			Group.Name = _name;
			Group.IconId = _iconId;
			Group.CustomIconUuid = _customIconId;
			Group.Touch(true);

			// Commit to disk
			SaveDb save = new SaveDb(_ctx, _app, OnFinishToRun);
			save.SetStatusLogger(StatusLogger);
			save.Run();
		}
		
		private class AfterEdit : OnFinish {
			readonly EditGroup _editGroup;

			public AfterEdit(Activity ctx, EditGroup editGroup, OnFinish finish)
				: base(ctx, finish)
			{
				_editGroup = editGroup;
			}
				

			public override void Run() {
				
				if ( Success ) {
					// Mark parent group dirty
					_editGroup.Db.Dirty.Add(_editGroup.Group.ParentGroup);
				} else
				{
					_editGroup._app.LockDatabase(false);
				}
				
				base.Run();
			}
			
		}
		
		
	}

}

