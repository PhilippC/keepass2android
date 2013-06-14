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
using KeePassLib;
using KeePassLib.Keys;

namespace keepass2android
{
	public class SetPassword : RunnableOnFinish {
		
		private String mPassword;
		private String mKeyfile;
		private Database mDb;
		private bool mDontSave;
		private Context mCtx;
		
		public SetPassword(Context ctx, Database db, String password, String keyfile, OnFinish finish): base(finish) {
			mCtx = ctx;
			mDb = db;
			mPassword = password;
			mKeyfile = keyfile;
			mDontSave = false;
		}
		
		public SetPassword(Context ctx, Database db, String password, String keyfile, OnFinish finish, bool dontSave): base(finish) {
			mCtx = ctx;
			mDb = db;
			mPassword = password;
			mKeyfile = keyfile;
			mDontSave = dontSave;
		}
		
		
		public override void run ()
		{
			PwDatabase pm = mDb.pm;
			CompositeKey newKey = new CompositeKey ();
			if (String.IsNullOrEmpty (mPassword) == false) {
				newKey.AddUserKey (new KcpPassword (mPassword)); 
			}
			if (String.IsNullOrEmpty (mKeyfile) == false) {
				try {
					newKey.AddUserKey (new KcpKeyFile (mKeyfile));
				} catch (Exception exKF) {
					//TODO MessageService.ShowWarning (strKeyFile, KPRes.KeyFileError, exKF);
					return;
				}
			}

			DateTime previousMasterKeyChanged = pm.MasterKeyChanged;
			CompositeKey previousKey = pm.MasterKey;

			pm.MasterKeyChanged = DateTime.Now;
			pm.MasterKey = newKey;

			// Save Database
			mFinish = new AfterSave(previousKey, previousMasterKeyChanged, pm, mFinish);
			SaveDB save = new SaveDB(mCtx, mDb, mFinish, mDontSave);
			save.run();
		}
		
		private class AfterSave : OnFinish {
			private CompositeKey mBackup;
			private DateTime mPreviousKeyChanged;
			private PwDatabase mDb;
			
			public AfterSave(CompositeKey backup, DateTime previousKeyChanged, PwDatabase db, OnFinish finish): base(finish) {
				mPreviousKeyChanged = previousKeyChanged;
				mBackup = backup;
				mDb = db;
			}
			
			public override void run() {
				if ( ! mSuccess ) {
					mDb.MasterKey = mBackup;
					mDb.MasterKeyChanged = mPreviousKeyChanged;
				}
				
				base.run();
			}
			
		}

		
	}

}

