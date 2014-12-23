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

	public class UpdateEntry : RunnableOnFinish {
		private readonly IKp2aApp _app;
		private readonly Context _ctx;
		
		public UpdateEntry(Context ctx, IKp2aApp app, PwEntry oldE, PwEntry newE, OnFinish finish):base(finish) {
			_ctx = ctx;
			_app = app;

			_onFinishToRun = new AfterUpdate(oldE, newE, app, finish);
		}
		
		
		public override void Run() {
			// Commit to disk
			SaveDb save = new SaveDb(_ctx, _app, OnFinishToRun);
			save.SetStatusLogger(StatusLogger);
			save.Run();
		}
		
		private class AfterUpdate : OnFinish {
			private readonly PwEntry _backup;
			private readonly PwEntry _updatedEntry;
			private readonly IKp2aApp _app;
			
			public AfterUpdate(PwEntry backup, PwEntry updatedEntry, IKp2aApp app, OnFinish finish):base(finish) {
				_backup = backup;
				_updatedEntry = updatedEntry;
				_app = app;
			}
			
			public override void Run() {
				if ( Success ) {
					// Mark parent group dirty. Even only the last modification date changed, this might affect sort order
					PwGroup parent = _updatedEntry.ParentGroup;
					if ( parent != null ) {

						// Mark parent group dirty
						_app.GetDb().Dirty.Add(parent);
							
					}
					
				} else {
					StatusLogger.UpdateMessage(UiStringKey.UndoingChanges);
					// If we fail to save, back out changes to global structure
					//TODO test fail
					_updatedEntry.AssignProperties(_backup, false, true, false);
				}
				
				base.Run();
			}
			
		}
		
		
	}

}

