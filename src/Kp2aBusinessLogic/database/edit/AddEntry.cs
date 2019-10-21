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

using Android.App;
using Android.Content;
using KeePassLib;

namespace keepass2android
{
	public class AddEntry : RunnableOnFinish {
		protected Database Db
		{
			get { return _app.CurrentDb; }
		}

		private readonly IKp2aApp _app;
		private readonly PwEntry _entry;
		private readonly PwGroup _parentGroup;
		private readonly Activity _ctx;
		
		public static AddEntry GetInstance(Activity ctx, IKp2aApp app, PwEntry entry, PwGroup parentGroup, OnFinish finish) {

			return new AddEntry(ctx, app, entry, parentGroup, finish);
		}
		
		public AddEntry(Activity ctx, IKp2aApp app, PwEntry entry, PwGroup parentGroup, OnFinish finish):base(ctx, finish) {
			_ctx = ctx;
			_parentGroup = parentGroup;
			_app = app;
			_entry = entry;
			
			_onFinishToRun = new AfterAdd(ctx, app.CurrentDb, entry, app,OnFinishToRun);
		}
		
		
		public override void Run() {	
			StatusLogger.UpdateMessage(UiStringKey.AddingEntry);

			//make sure we're not adding the entry if it was added before.
			//(this might occur in very rare cases where the user dismissis the save dialog 
			//by rotating the screen while saving and then presses save again)
			if (_parentGroup.FindEntry(_entry.Uuid, false) == null)
			{
				_parentGroup.AddEntry(_entry, true);	
			}
			
			
			// Commit to disk
			SaveDb save = new SaveDb(_ctx, _app, _app.CurrentDb, OnFinishToRun);
			save.SetStatusLogger(StatusLogger);
			save.Run();
		}
		
		private class AfterAdd : OnFinish {
			private readonly Database _db;
			private readonly PwEntry _entry;
		    private readonly IKp2aApp _app;

		    public AfterAdd(Activity activity, Database db, PwEntry entry, IKp2aApp app, OnFinish finish):base(activity, finish) {
				_db = db;
				_entry = entry;
		        _app = app;
		    }
			


			public override void Run() {
				if ( Success ) {
					
					PwGroup parent = _entry.ParentGroup; 
					
					// Mark parent group dirty
					_app.DirtyGroups.Add(parent);
					
					// Add entry to global
					_db.EntriesById[_entry.Uuid] = _entry;
				    _db.Elements.Add(_entry);

				} else
				{
					StatusLogger.UpdateMessage(UiStringKey.UndoingChanges);
					//TODO test fail
					_entry.ParentGroup.Entries.Remove(_entry);
				}
				
				base.Run();
			}
		}
		
		
	}

}

