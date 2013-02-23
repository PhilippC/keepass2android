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
using Android.Preferences;
using KeePassLib.Serialization;

namespace keepass2android
{
	public class LoadDB : RunnableOnFinish {
		private IOConnectionInfo mIoc;
		private String mPass;
		private String mKey;
		private Database mDb;
		private Context mCtx;
		private bool mRememberKeyfile;
		
		public LoadDB(Database db, Context ctx, IOConnectionInfo ioc, String pass, String key, OnFinish finish): base(finish)
		{
			mDb = db;
			mCtx = ctx;
			mIoc = ioc;
			mPass = pass;
			mKey = key;
			
			ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);
			mRememberKeyfile = prefs.GetBoolean(ctx.GetString(Resource.String.keyfile_key), ctx.Resources.GetBoolean(Resource.Boolean.keyfile_default));
		}
		
		
		public override void run ()
		{
			try {
				mDb.LoadData (mCtx, mIoc, mPass, mKey, mStatus);
				
				saveFileData (mIoc, mKey);
				
			} catch (KeyFileException) {
				finish(false, /*TODO Localize: use Keepass error text KPRes.KeyFileError (including "or invalid format")*/ mCtx.GetString(Resource.String.keyfile_does_not_exist));
			}
			catch (Exception e) {
				finish(false, "An error occured: " + e.Message);
				return;
			} 
		 /* catch (InvalidPasswordException e) {
				finish(false, mCtx.GetString(Resource.String.InvalidPassword));
				return;
			} catch (FileNotFoundException e) {
				finish(false, mCtx.GetString(Resource.String.FileNotFound));
				return;
			} catch (IOException e) {
				finish(false, e.getMessage());
				return;
			} catch (KeyFileEmptyException e) {
				finish(false, mCtx.GetString(Resource.String.keyfile_is_empty));
				return;
			} catch (InvalidAlgorithmException e) {
				finish(false, mCtx.GetString(Resource.String.invalid_algorithm));
				return;
			} catch (InvalidKeyFileException e) {
				finish(false, mCtx.GetString(Resource.String.keyfile_does_not_exist));
				return;
			} catch (InvalidDBSignatureException e) {
				finish(false, mCtx.GetString(Resource.String.invalid_db_sig));
				return;
			} catch (InvalidDBVersionException e) {
				finish(false, mCtx.GetString(Resource.String.unsupported_db_version));
				return;
			} catch (InvalidDBException e) {
				finish(false, mCtx.GetString(Resource.String.error_invalid_db));
				return;
			} catch (OutOfMemoryError e) {
				finish(false, mCtx.GetString(Resource.String.error_out_of_memory));
				return;
			}
			*/
			finish(true);
		}
		
		private void saveFileData(IOConnectionInfo ioc, String key) {
			FileDbHelper db = App.fileDbHelper;
			
			if ( ! mRememberKeyfile ) {
				key = "";
			}
			
			db.createFile(ioc, key);
		}
		
		
		
	}

}

