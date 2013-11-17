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
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android
{
	public class LoadDb : RunnableOnFinish {
		private readonly IOConnectionInfo _ioc;
		private readonly Task<MemoryStream> _databaseData;
		private readonly CompositeKey _compositeKey;
		private readonly string _keyfileOrProvider;
		private readonly IKp2aApp _app;
		private readonly bool _rememberKeyfile;
		
		public LoadDb(IKp2aApp app, IOConnectionInfo ioc, Task<MemoryStream> databaseData, CompositeKey compositeKey, String keyfileOrProvider, OnFinish finish): base(finish)
		{
			_app = app;
			_ioc = ioc;
			_databaseData = databaseData;
			_compositeKey = compositeKey;
			_keyfileOrProvider = keyfileOrProvider;


			_rememberKeyfile = app.GetBooleanPreference(PreferenceKey.remember_keyfile); 
		}
		
		
		public override void Run ()
		{
			try
			{
				StatusLogger.UpdateMessage(UiStringKey.loading_database);
				MemoryStream memoryStream = _databaseData == null ? null : _databaseData.Result;
				_app.LoadDatabase(_ioc, memoryStream, _compositeKey, StatusLogger);
				SaveFileData(_ioc, _keyfileOrProvider);

			}
			catch (KeyFileException)
			{
				Kp2aLog.Log("KeyFileException");
				Finish(false, /*TODO Localize: use Keepass error text KPRes.KeyFileError (including "or invalid format")*/
				       _app.GetResourceString(UiStringKey.keyfile_does_not_exist));
			}
			catch (AggregateException e)
			{
				string message = e.Message;
				foreach (var innerException in e.InnerExceptions)
				{
					message = innerException.Message;
						// Override the message shown with the last (hopefully most recent) inner exception
					Kp2aLog.Log("Exception: " + message);
				}
				Finish(false, _app.GetResourceString(UiStringKey.ErrorOcurred) + " " + message);
				return;
			}
			catch (OldFormatException )
			{
				Finish(false, "Cannot open Keepass 1.x database. As explained in the app description, Keepass2Android is for Keepass 2 only! Please use the desktop application to convert your database to the new file format!");
				return;
			}
			catch (Exception e)
			{
				Kp2aLog.Log("Exception: " + e);
				Finish(false, _app.GetResourceString(UiStringKey.ErrorOcurred) + " " + e.Message);
				return;
			}
			
			Kp2aLog.Log("LoadDB OK");
			Finish(true);
		}
		
		private void SaveFileData(IOConnectionInfo ioc, String keyfileOrProvider) {

            if (!_rememberKeyfile)
            {
                keyfileOrProvider = "";
            }
            _app.StoreOpenedFileAsRecent(ioc, keyfileOrProvider);
		}
		
		
		
	}

}

