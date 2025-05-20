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
using Android.App;
using Android.Content;
using KeePassLib;
using KeePassLib.Keys;

namespace keepass2android
{
	public class SetPassword : OperationWithFinishHandler {
		
		private readonly String _password;
		private readonly String _keyfile;
		private readonly IKp2aApp _app;
		private readonly bool _dontSave;
		
		public SetPassword(IKp2aApp app, String password, String keyfile, OnOperationFinishedHandler operationFinishedHandler): base(app, operationFinishedHandler) {
			
			_app = app;
			_password = password;
			_keyfile = keyfile;
			_dontSave = false;
		}

		public SetPassword(IKp2aApp app, String password, String keyfile, OnOperationFinishedHandler operationFinishedHandler, bool dontSave)
			: base(app, operationFinishedHandler)
		{
			_app = app;
			_password = password;
			_keyfile = keyfile;
			_dontSave = dontSave;
		}
		
		
		public override void Run ()
		{
			StatusLogger.UpdateMessage(UiStringKey.SettingPassword);
			PwDatabase pm = _app.CurrentDb.KpDatabase;
			CompositeKey newKey = new CompositeKey ();
			if (String.IsNullOrEmpty (_password) == false) {
				newKey.AddUserKey (new KcpPassword (_password)); 
			}
			if (String.IsNullOrEmpty (_keyfile) == false) {
				try {
					newKey.AddUserKey (new KcpKeyFile (_keyfile));
				} catch (Exception) {
					//TODO MessageService.ShowWarning (strKeyFile, KPRes.KeyFileError, exKF);
					return;
				}
			}

			DateTime previousMasterKeyChanged = pm.MasterKeyChanged;
			CompositeKey previousKey = pm.MasterKey;

			pm.MasterKeyChanged = DateTime.Now;
			pm.MasterKey = newKey;

			// Save Database
			_operationFinishedHandler = new AfterSave(_app, previousKey, previousMasterKeyChanged, pm, operationFinishedHandler);
			SaveDb save = new SaveDb(_app, _app.CurrentDb, operationFinishedHandler, _dontSave, null);
			save.SetStatusLogger(StatusLogger);
			save.Run();
		}
		
		private class AfterSave : OnOperationFinishedHandler {
			private readonly CompositeKey _backup;
			private readonly DateTime _previousKeyChanged;
			private readonly PwDatabase _db;
			
			public AfterSave(IActiveContextProvider activeContextProvider, CompositeKey backup, DateTime previousKeyChanged, PwDatabase db, OnOperationFinishedHandler operationFinishedHandler): base(activeContextProvider, operationFinishedHandler) {
				_previousKeyChanged = previousKeyChanged;
				_backup = backup;
				_db = db;
			}
			
			public override void Run() {
				if ( ! Success ) {
					_db.MasterKey = _backup;
					_db.MasterKeyChanged = _previousKeyChanged;
				}
				
				base.Run();
			}
			
		}

		
	}

}

