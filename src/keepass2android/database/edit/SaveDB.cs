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
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android
{

	public class SaveDB : RunnableOnFinish {
		private Database mDb;
		private bool mDontSave;
		private Context mCtx;
		
		public SaveDB(Context ctx, Database db, OnFinish finish, bool dontSave): base(finish) {
			mCtx = ctx;
			mDb = db;
			mDontSave = dontSave;
		}

		public SaveDB(Context ctx, Database db, OnFinish finish):base(finish) {
			mCtx = ctx;
			mDb = db;
			mDontSave = false;
		}
		
		
		public override void run ()
		{
			
			if (! mDontSave) {
				try {
					mDb.SaveData (mCtx);
					if (mDb.mIoc.IsLocalFile())
						mDb.mLastChangeDate = System.IO.File.GetLastWriteTimeUtc(mDb.mIoc.Path);
				} catch (Exception e) {
					/* TODO KPDesktop:
					 * catch(Exception exSave)
			{
				MessageService.ShowSaveWarning(pd.IOConnectionInfo, exSave, true);
				bSuccess = false;
			}
*/
					finish (false, e.Message);
					return;
				} 
			}
			
			finish(true);
		}
		
	}

}

