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
		private readonly Database _db;
		private readonly Context _ctx;
		
		public UpdateEntry(Context ctx, Database db, PwEntry oldE, PwEntry newE, OnFinish finish):base(finish) {
			_ctx = ctx;
			_db = db;

			OnFinishToRun = new AfterUpdate(oldE, newE, db, finish);
		}
		
		
		public override void Run() {
			// Commit to disk
			SaveDb save = new SaveDb(_ctx, _db, OnFinishToRun);
			save.Run();
		}
		
		private class AfterUpdate : OnFinish {
			private readonly PwEntry _backup;
			private readonly PwEntry _updatedEntry;
			private readonly Database _db;
			
			public AfterUpdate(PwEntry backup, PwEntry updatedEntry, Database db, OnFinish finish):base(finish) {
				_backup = backup;
				_updatedEntry = updatedEntry;
				_db = db;
			}
			
			public override void Run() {
				if ( Success ) {
					// Mark group dirty if title, icon or Expiry stuff changes
					if ( ! _backup.Strings.ReadSafe (PwDefs.TitleField).Equals(_updatedEntry.Strings.ReadSafe (PwDefs.TitleField)) 
					    || ! _backup.IconId.Equals(_updatedEntry.IconId) 
					    || ! _backup.CustomIconUuid.EqualsValue(_updatedEntry.CustomIconUuid)
					    || _backup.Expires != _updatedEntry.Expires
					    || (_backup.Expires && (! _backup.ExpiryTime.Equals(_updatedEntry.ExpiryTime)))
					    )
					
					{
						PwGroup parent = _updatedEntry.ParentGroup;
						if ( parent != null ) {

							// Mark parent group dirty
							_db.Dirty.Add(parent);
							
						}
					}
				} else {
					// If we fail to save, back out changes to global structure
					//TODO test fail
					_updatedEntry.AssignProperties(_backup, false, true, false);
				}
				
				base.Run();
			}
			
		}
		
		
	}

}

