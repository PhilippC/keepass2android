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

using System.Collections.Generic;
using Android.App;
using Android.Content;
using KeePassLib;
using KeePassLib.Cryptography.KeyDerivation;
using KeePassLib.Serialization;
using KeePassLib.Keys;

namespace keepass2android
{
	
	public class CreateDb : RunnableOnFinish {
	    private readonly IOConnectionInfo _ioc;
		private readonly bool _dontSave;
		private readonly Activity _ctx;
        private readonly IKp2aApp _app;
		private CompositeKey _key;

		public CreateDb(IKp2aApp app, Activity ctx, IOConnectionInfo ioc, OnFinish finish, bool dontSave): base(ctx, finish) {
			_ctx = ctx;
			_ioc = ioc;
			_dontSave = dontSave;
            _app = app;
		}

		public CreateDb(IKp2aApp app, Activity ctx, IOConnectionInfo ioc, OnFinish finish, bool dontSave, CompositeKey key)
			: base(ctx, finish)
		{
			_ctx = ctx;
			_ioc = ioc;
			_dontSave = dontSave;
			_app = app;
			_key = key;
		}
		

		public override void Run() {
			StatusLogger.UpdateMessage(UiStringKey.progress_create);
			Database db = _app.CreateNewDatabase();

			db.KpDatabase = new KeePassLib.PwDatabase();
			
			if (_key == null)
			{
				_key = new CompositeKey(); //use a temporary key which should be changed after creation
			}
			
			db.KpDatabase.New(_ioc, _key);

			db.KpDatabase.KdfParameters = (new AesKdf()).GetDefaultParameters();
			db.KpDatabase.Name = "Keepass2Android Password Database";
			//re-set the name of the root group because the PwDatabase uses UrlUtil which is not appropriate for all file storages:
			db.KpDatabase.RootGroup.Name = _app.GetFileStorage(_ioc).GetFilenameWithoutPathAndExt(_ioc);
			
			// Set Database state
			db.Root = db.KpDatabase.RootGroup;
			db.SearchHelper = new SearchDbHelper(_app);

			// Add a couple default groups
			AddGroup internet = AddGroup.GetInstance(_ctx, _app, "Internet", 1, null, db.KpDatabase.RootGroup, null, true);
			internet.Run();
			AddGroup email = AddGroup.GetInstance(_ctx, _app, "eMail", 19, null, db.KpDatabase.RootGroup, null, true);
			email.Run();

			List<PwEntry> addedEntries;
			AddTemplateEntries addTemplates = new AddTemplateEntries(_ctx, _app, null);
			addTemplates.AddTemplates(out addedEntries);
			
			// Commit changes
			SaveDb save = new SaveDb(_ctx, _app, db, OnFinishToRun, _dontSave);
			save.SetStatusLogger(StatusLogger);
			_onFinishToRun = null;
			save.Run();
			
			
		}
		
	}

}

