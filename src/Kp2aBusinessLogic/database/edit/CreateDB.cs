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
using KeePassLib.Serialization;
using KeePassLib.Keys;

namespace keepass2android
{
	
	public class CreateDb : RunnableOnFinish {
		
		private const int DefaultEncryptionRounds = 1000;
		
		private readonly IOConnectionInfo _ioc;
		private readonly bool _dontSave;
		private readonly Context _ctx;
        private readonly IKp2aApp _app;
		
		public CreateDb(IKp2aApp app, Context ctx, IOConnectionInfo ioc, OnFinish finish, bool dontSave): base(finish) {
			_ctx = ctx;
			_ioc = ioc;
			_dontSave = dontSave;
            _app = app;
		}
		

		public override void Run() {
			StatusLogger.UpdateMessage(UiStringKey.progress_create);
			Database db = _app.CreateNewDatabase();

			db.KpDatabase = new KeePassLib.PwDatabase();
			//Key will be changed/created immediately after creation:
			CompositeKey tempKey = new CompositeKey();
			db.KpDatabase.New(_ioc, tempKey);


			db.KpDatabase.KeyEncryptionRounds = DefaultEncryptionRounds;
			db.KpDatabase.Name = "Keepass2Android Password Database";

			
			// Set Database state
			db.Root = db.KpDatabase.RootGroup;
			db.Loaded = true;
			db.SearchHelper = new SearchDbHelper(_app);

			// Add a couple default groups
			AddGroup internet = AddGroup.GetInstance(_ctx, _app, "Internet", 1, db.KpDatabase.RootGroup, null, true);
			internet.Run();
			AddGroup email = AddGroup.GetInstance(_ctx, _app, "eMail", 19, db.KpDatabase.RootGroup, null, true);
			email.Run();
			
			// Commit changes
			SaveDb save = new SaveDb(_ctx, _app, OnFinishToRun, _dontSave);
			save.SetStatusLogger(StatusLogger);
			_onFinishToRun = null;
			save.Run();
			
			
		}
		
	}

}

