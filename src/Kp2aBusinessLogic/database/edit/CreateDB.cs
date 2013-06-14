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
using KeePassLib.Serialization;
using KeePassLib.Keys;

namespace keepass2android
{
	
	public class CreateDB : RunnableOnFinish {
		
		private const int DEFAULT_ENCRYPTION_ROUNDS = 1000;
		
		private IOConnectionInfo mIoc;
		private bool mDontSave;
		private Context mCtx;
        private IKp2aApp mApp;
		
		public CreateDB(IKp2aApp app, Context ctx, IOConnectionInfo ioc, OnFinish finish, bool dontSave): base(finish) {
			mCtx = ctx;
			mIoc = ioc;
			mDontSave = dontSave;
            mApp = app;
		}
		

		public override void run() {
			Database db = mApp.CreateNewDatabase();

			db.pm = new KeePassLib.PwDatabase();
			//Key will be changed/created immediately after creation:
			CompositeKey tempKey = new CompositeKey();
			db.pm.New(mIoc, tempKey);


			db.pm.KeyEncryptionRounds = DEFAULT_ENCRYPTION_ROUNDS;
			db.pm.Name = "Keepass2Android Password Database";

			
			// Set Database state
			db.root = db.pm.RootGroup;
			db.mIoc = mIoc;
			db.Loaded = true;
			db.searchHelper = new SearchDbHelper(mApp);

			// Add a couple default groups
			AddGroup internet = AddGroup.getInstance(mCtx, db, "Internet", 1, db.pm.RootGroup, null, true);
			internet.run();
			AddGroup email = AddGroup.getInstance(mCtx, db, "eMail", 19, db.pm.RootGroup, null, true);
			email.run();
			
			// Commit changes
			SaveDB save = new SaveDB(mCtx, db, mFinish, mDontSave);
			mFinish = null;
			save.run();
			
			
		}
		
	}

}

