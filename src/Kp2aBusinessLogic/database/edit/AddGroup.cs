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

	public class AddGroup : OperationWithFinishHandler {
		internal Database Db
		{
			get { return _app.CurrentDb; }
		}

        public IKp2aApp App { get => _app; }

        private IKp2aApp _app;
		private readonly String _name;
		private readonly int _iconId;
		private readonly PwUuid _groupCustomIconId;
	    public PwGroup Group;
		internal PwGroup Parent;
		protected bool DontSave;


        public static AddGroup GetInstance(IKp2aApp app, string name, int iconid, PwUuid groupCustomIconId, PwGroup parent, OnOperationFinishedHandler operationFinishedHandler, bool dontSave) {
			return new AddGroup(app, name, iconid, groupCustomIconId, parent, operationFinishedHandler, dontSave);
		}


		private AddGroup(IKp2aApp app, String name, int iconid, PwUuid groupCustomIconId, PwGroup parent, OnOperationFinishedHandler operationFinishedHandler, bool dontSave)
			: base(app, operationFinishedHandler)
		{
			_name = name;
			_iconId = iconid;
			_groupCustomIconId = groupCustomIconId;
			Parent = parent;
			DontSave = dontSave;
			_app = app;

			_operationFinishedHandler = new AfterAdd(_app, this, operationFinishedHandler);
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
		    _app.CurrentDb.GroupsById[Group.Uuid] = Group;
		    _app.CurrentDb.Elements.Add(Group);

            // Commit to disk
            SaveDb save = new SaveDb(_app, _app.CurrentDb, operationFinishedHandler, DontSave, null);
			save.SetStatusLogger(StatusLogger);
			save.Run();
		}
		
		private class AfterAdd : OnOperationFinishedHandler {
			readonly AddGroup _addGroup;

			public AfterAdd(IKp2aApp app, AddGroup addGroup,OnOperationFinishedHandler operationFinishedHandler): base(app, operationFinishedHandler) {
				_addGroup = addGroup;
			}
				

			public override void Run() {
				
				if ( Success ) {
					// Mark parent group dirty
					_addGroup.App.DirtyGroups.Add(_addGroup.Parent);
					
					// Add group to global list
					_addGroup.Db.GroupsById[_addGroup.Group.Uuid] = _addGroup.Group;
				    _addGroup.Db.Elements.Add(_addGroup.Group);
				} else {
					StatusLogger.UpdateMessage(UiStringKey.UndoingChanges);
					_addGroup.Parent.Groups.Remove(_addGroup.Group);

				}
				
				base.Run();
			}
			
		}
		
		
	}

}

