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

namespace keepass2android
{

	public class SaveDb : RunnableOnFinish {
		private readonly Database _db;
		private readonly bool _dontSave;
		private readonly Context _ctx;
		
		public SaveDb(Context ctx, Database db, OnFinish finish, bool dontSave): base(finish) {
			_ctx = ctx;
			_db = db;
			_dontSave = dontSave;
		}

		public SaveDb(Context ctx, Database db, OnFinish finish):base(finish) {
			_ctx = ctx;
			_db = db;
			_dontSave = false;
		}
		
		
		public override void Run ()
		{
			
			if (! _dontSave) {
				try {
					_db.SaveData (_ctx);
					if (_db.Ioc.IsLocalFile())
						_db.LastChangeDate = System.IO.File.GetLastWriteTimeUtc(_db.Ioc.Path);
				} catch (Exception e) {
					/* TODO KPDesktop:
					 * catch(Exception exSave)
			{
				MessageService.ShowSaveWarning(pd.IOConnectionInfo, exSave, true);
				bSuccess = false;
			}
*/
					Finish (false, e.Message);
					return;
				} 
			}
			
			Finish(true);
		}
		
	}

}

