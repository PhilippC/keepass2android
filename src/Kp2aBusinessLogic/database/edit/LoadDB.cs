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
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using keepass2android.database.edit;
using KeePassLib;
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
		IDatabaseFormat _format;
		
		public LoadDb(Activity activity, IKp2aApp app, IOConnectionInfo ioc, Task<MemoryStream> databaseData, CompositeKey compositeKey, String keyfileOrProvider, OnFinish finish): base(activity, finish)
		{
			_app = app;
			_ioc = ioc;
			_databaseData = databaseData;
			_compositeKey = compositeKey;
			_keyfileOrProvider = keyfileOrProvider;


			_rememberKeyfile = app.GetBooleanPreference(PreferenceKey.remember_keyfile); 
		}

	    protected bool success = false;
		
		public override void Run()
		{
			try
			{
				try
				{
                    //make sure the file data is stored in the recent files list even if loading fails
				    SaveFileData(_ioc, _keyfileOrProvider);


                    StatusLogger.UpdateMessage(UiStringKey.loading_database);
					//get the stream data into a single stream variable (databaseStream) regardless whether its preloaded or not:
					MemoryStream preloadedMemoryStream = _databaseData == null ? null : _databaseData.Result;
					MemoryStream databaseStream;
					if (preloadedMemoryStream != null)
						databaseStream = preloadedMemoryStream;
					else
					{
						using (Stream s = _app.GetFileStorage(_ioc).OpenFileForRead(_ioc))
						{
							databaseStream = new MemoryStream();
							s.CopyTo(databaseStream);
							databaseStream.Seek(0, SeekOrigin.Begin);
						}
					}

					//ok, try to load the database. Let's start with Kdbx format and retry later if that is the wrong guess:
					_format = new KdbxDatabaseFormat(KdbpFile.GetFormatToUse(_ioc));
					TryLoad(databaseStream);



				    success = true;
				}
				catch (Exception e)
				{
					this.Exception = e;
					throw;
				}
			}
			catch (KeyFileException)
			{
				Kp2aLog.Log("KeyFileException");
				Finish(false, /*TODO Localize: use Keepass error text KPRes.KeyFileError (including "or invalid format")*/
				       _app.GetResourceString(UiStringKey.keyfile_does_not_exist), Exception);
			}
			catch (AggregateException e)
			{
				string message = e.Message;
				foreach (var innerException in e.InnerExceptions)
				{
					message = innerException.Message;
					// Override the message shown with the last (hopefully most recent) inner exception
					Kp2aLog.LogUnexpectedError(innerException);
				}
				Finish(false, _app.GetResourceString(UiStringKey.ErrorOcurred) + " " + message, Exception);
				return;
			}
			catch (DuplicateUuidsException e)
			{
				Kp2aLog.Log(e.ToString());
				Finish(false, _app.GetResourceString(UiStringKey.DuplicateUuidsError) + " " + e.Message + _app.GetResourceString(UiStringKey.DuplicateUuidsErrorAdditional), Exception);
				return;
			}
			catch (Exception e)
			{
				if (!(e is InvalidCompositeKeyException))
					Kp2aLog.LogUnexpectedError(e);
				Finish(false, _app.GetResourceString(UiStringKey.ErrorOcurred) + " " + e.Message, Exception);
				return;
			}
			
			
		}

		/// <summary>
		/// Holds the exception which was thrown during execution (if any)
		/// </summary>
		public Exception Exception { get; set; }

		Database TryLoad(MemoryStream databaseStream)
		{
			//create a copy of the stream so we can try again if we get an exception which indicates we should change parameters
			//This is not optimal in terms of (short-time) memory usage but is hard to avoid because the Keepass library closes streams also in case of errors.
			//Alternatives would involve increased traffic (if file is on remote) and slower loading times, so this seems to be the best choice.
			MemoryStream workingCopy = new MemoryStream();
			databaseStream.CopyTo(workingCopy);
			workingCopy.Seek(0, SeekOrigin.Begin);
			//reset stream if we need to reuse it later:
			databaseStream.Seek(0, SeekOrigin.Begin);
			//now let's go:
			try
			{
                Database newDb = _app.LoadDatabase(_ioc, workingCopy, _compositeKey, StatusLogger, _format);
				Kp2aLog.Log("LoadDB OK");

			    //make sure the stored access time for the actual file is more recent than that of its backup
			    Thread.Sleep(10);
                SaveFileData(_ioc, _keyfileOrProvider);

                Finish(true, _format.SuccessMessage);
			    return newDb;
			}
			catch (OldFormatException)
			{
				_format = new KdbDatabaseFormat(_app);
				return TryLoad(databaseStream);
			}
			catch (InvalidCompositeKeyException)
			{
				KcpPassword passwordKey = (KcpPassword)_compositeKey.GetUserKey(typeof(KcpPassword));

				if ((passwordKey != null) && (passwordKey.Password.ReadString() == "") && (_compositeKey.UserKeyCount > 1))
				{
					//if we don't get a password, we don't know whether this means "empty password" or "no password"
					//retry without password:
					_compositeKey.RemoveUserKey(passwordKey);
					//retry:
					return TryLoad(databaseStream);
				}
				else throw;
			}
			
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

