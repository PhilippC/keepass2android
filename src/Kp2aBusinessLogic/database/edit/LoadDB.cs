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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KeePassLib.Serialization;

namespace keepass2android
{
	public class LoadDb : RunnableOnFinish {
		private readonly IOConnectionInfo _ioc;
		private readonly Task<MemoryStream> _databaseData;
		private readonly String _pass;
		private readonly String _key;
		private readonly IKp2aApp _app;
		private readonly bool _rememberKeyfile;
		
		public LoadDb(IKp2aApp app, IOConnectionInfo ioc, Task<MemoryStream> databaseData, String pass, String key, OnFinish finish): base(finish)
		{
			_app = app;
			_ioc = ioc;
			_databaseData = databaseData;
			_pass = pass;
			_key = key;

            
			_rememberKeyfile = app.GetBooleanPreference(PreferenceKey.remember_keyfile); 
		}
		
		
		public override void Run ()
		{
			try
			{
				StatusLogger.UpdateMessage(UiStringKey.loading_database);
				MemoryStream memoryStream = _databaseData == null ? null : _databaseData.Result;
				_app.LoadDatabase(_ioc, memoryStream, _pass, _key, StatusLogger);
				SaveFileData (_ioc, _key);
				
			} catch (KeyFileException) {
				Kp2aLog.Log("KeyFileException");
				Finish(false, /*TODO Localize: use Keepass error text KPRes.KeyFileError (including "or invalid format")*/ _app.GetResourceString(UiStringKey.keyfile_does_not_exist));
			}
			catch (AggregateException e)
			{
				string message = e.Message;
				foreach (var innerException in e.InnerExceptions)
				{
					message = innerException.Message; // Override the message shown with the last (hopefully most recent) inner exception
					Kp2aLog.Log("Exception: " + message);
				}
				Finish(false, "An error occured: " + message);
				return;
			}
			catch (Exception e)
			{
				Kp2aLog.Log("Exception: " + e);
				Finish(false, "An error occured: " + e.Message);
				return;
			}
			

		 /* catch (InvalidPasswordException e) {
				finish(false, Ctx.GetString(Resource.String.InvalidPassword));
				return;
			} catch (FileNotFoundException e) {
				finish(false, Ctx.GetString(Resource.String.FileNotFound));
				return;
			} catch (IOException e) {
				finish(false, e.getMessage());
				return;
			} catch (KeyFileEmptyException e) {
				finish(false, Ctx.GetString(Resource.String.keyfile_is_empty));
				return;
			} catch (InvalidAlgorithmException e) {
				finish(false, Ctx.GetString(Resource.String.invalid_algorithm));
				return;
			} catch (InvalidKeyFileException e) {
				finish(false, Ctx.GetString(Resource.String.keyfile_does_not_exist));
				return;
			} catch (InvalidDBSignatureException e) {
				finish(false, Ctx.GetString(Resource.String.invalid_db_sig));
				return;
			} catch (InvalidDBVersionException e) {
				finish(false, Ctx.GetString(Resource.String.unsupported_db_version));
				return;
			} catch (InvalidDBException e) {
				finish(false, Ctx.GetString(Resource.String.error_invalid_db));
				return;
			} catch (OutOfMemoryError e) {
				finish(false, Ctx.GetString(Resource.String.error_out_of_memory));
				return;
			}
			*/
			Kp2aLog.Log("LoadDB OK");
			Finish(true);
		}
		
		private void SaveFileData(IOConnectionInfo ioc, String key) {

            if (!_rememberKeyfile)
            {
                key = "";
            }
            _app.StoreOpenedFileAsRecent(ioc, key);
		}
		
		
		
	}

}

