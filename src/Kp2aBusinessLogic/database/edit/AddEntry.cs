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

using Android.Content;
using KeePassLib;

namespace keepass2android
{
	public class AddEntry : RunnableOnFinish {
		protected Database Db
		{
			get { return _app.GetDb(); }
		}

		private readonly IKp2aApp _app;
		private readonly PwEntry _entry;
		private readonly PwGroup _parentGroup;
		private readonly Context _ctx;
		
		public static AddEntry GetInstance(Context ctx, IKp2aApp app, PwEntry entry, PwGroup parentGroup, OnFinish finish) {

			return new AddEntry(ctx, app, entry, parentGroup, finish);
		}
		
		protected AddEntry(Context ctx, IKp2aApp app, PwEntry entry, PwGroup parentGroup, OnFinish finish):base(finish) {
			_ctx = ctx;
			_parentGroup = parentGroup;
			_app = app;
			_entry = entry;
			
			_onFinishToRun = new AfterAdd(app.GetDb(), entry, OnFinishToRun);
		}
		
		
		public override void Run() {	
			StatusLogger.UpdateMessage(UiStringKey.AddingEntry);
			_parentGroup.AddEntry(_entry, true);
			
			// Commit to disk
			SaveDb save = new SaveDb(_ctx, _app, OnFinishToRun);
			save.SetStatusLogger(StatusLogger);
			save.Run();
		}
		
		private class AfterAdd : OnFinish {
			private readonly Database _db;
			private readonly PwEntry _entry;

			public AfterAdd(Database db, PwEntry entry, OnFinish finish):base(finish) {
				_db = db;
				_entry = entry;

			}
			


			public override void Run() {
				if ( Success ) {
					
					PwGroup parent = _entry.ParentGroup; 
					
					// Mark parent group dirty
					_db.Dirty.Add(parent);
					
					// Add entry to global
					_db.Entries[_entry.Uuid] = _entry;
					
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

