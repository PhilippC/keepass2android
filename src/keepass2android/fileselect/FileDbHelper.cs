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
using Android.Database;
using Android.Database.Sqlite;
using KeePassLib.Serialization;

namespace keepass2android
{
	public class FileDbHelper {
		
		public const String LAST_FILENAME = "lastFile";
		public const String LAST_KEYFILE = "lastKey";
		
		private const String DATABASE_NAME = "keepass2android";
		private const String FILE_TABLE = "files";
		private const int DATABASE_VERSION = 1;
		
		private const int MAX_FILES = 5;
		
		public const String KEY_FILE_ID = "_id";
		public const String KEY_FILE_FILENAME = "fileName";
		public const String KEY_FILE_USERNAME = "username";
		public const String KEY_FILE_PASSWORD = "password";
		public const String KEY_FILE_CREDSAVEMODE = "credSaveMode";
		public const String KEY_FILE_KEYFILE = "keyFile";
		public const String KEY_FILE_UPDATED = "updated";
		
		private const String DATABASE_CREATE = 
			"create table " + FILE_TABLE + " ( " + KEY_FILE_ID + " integer primary key autoincrement, " 
				+ KEY_FILE_FILENAME + " text not null, " 
				+ KEY_FILE_KEYFILE + " text, "
				+ KEY_FILE_USERNAME + " text, "
				+ KEY_FILE_PASSWORD + " text, "
				+ KEY_FILE_CREDSAVEMODE + " integer not null,"
				+ KEY_FILE_UPDATED + " integer not null);";
		
		private readonly Android.Content.Context mCtx;
		private DatabaseHelper mDbHelper;
		private SQLiteDatabase mDb;
		
		private class DatabaseHelper : SQLiteOpenHelper {
			private readonly Android.Content.Context mCtx;
			
			public DatabaseHelper(Android.Content.Context ctx): base(ctx, DATABASE_NAME, null, DATABASE_VERSION) {

				mCtx = ctx;
			}
			
			
			public override void OnCreate(SQLiteDatabase db) {
				db.ExecSQL(DATABASE_CREATE);
			
			}
			
			
			public override void OnUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
				// Only one database version so far
			}

		}
		
		public FileDbHelper(Context ctx) {
			mCtx = ctx;
		}
		
		public FileDbHelper open() {
			mDbHelper = new DatabaseHelper(mCtx);
			mDb = mDbHelper.WritableDatabase;
			return this;
		}
		
		public bool isOpen() {
			return mDb.IsOpen;
		}
		
		public void close() {
			mDb.Close();
		}
		
		public long createFile(IOConnectionInfo ioc, String keyFile) {
			
			// Check to see if this filename is already used
			ICursor cursor;
			try {
				cursor = mDb.Query(true, FILE_TABLE, new String[] {KEY_FILE_ID}, 
				KEY_FILE_FILENAME + "=?", new String[] {ioc.Path}, null, null, null, null);
			} catch (Exception ) {
				return -1;
			}

			IOConnectionInfo iocToStore = ioc.CloneDeep();
			if (ioc.CredSaveMode != IOCredSaveMode.SaveCred)
				iocToStore.Password = "";
			if (ioc.CredSaveMode == IOCredSaveMode.NoSave)
				iocToStore.UserName = "";

			iocToStore.Obfuscate(true);
			
			long result;
			// If there is an existing entry update it
			if ( cursor.Count > 0 ) {
				cursor.MoveToFirst();
				long id = cursor.GetLong(cursor.GetColumnIndexOrThrow(KEY_FILE_ID));
				
				ContentValues vals = new ContentValues();
				vals.Put(KEY_FILE_KEYFILE, keyFile);
				vals.Put(KEY_FILE_UPDATED, Java.Lang.JavaSystem.CurrentTimeMillis());

				vals.Put(KEY_FILE_USERNAME, iocToStore.UserName);
				vals.Put(KEY_FILE_PASSWORD, iocToStore.Password);
				vals.Put(KEY_FILE_CREDSAVEMODE, (int)iocToStore.CredSaveMode);
				
				result = mDb.Update(FILE_TABLE, vals, KEY_FILE_ID + " = " + id, null);
				
				// Otherwise add the new entry
			} else {
				ContentValues vals = new ContentValues();
				vals.Put(KEY_FILE_FILENAME, ioc.Path);
				vals.Put(KEY_FILE_KEYFILE, keyFile);
				vals.Put(KEY_FILE_USERNAME, iocToStore.UserName);
				vals.Put(KEY_FILE_PASSWORD, iocToStore.Password);
				vals.Put(KEY_FILE_CREDSAVEMODE, (int)iocToStore.CredSaveMode);
				vals.Put(KEY_FILE_UPDATED, Java.Lang.JavaSystem.CurrentTimeMillis());
				
				result = mDb.Insert(FILE_TABLE, null, vals);
				
			}
			// Delete all but the last five records
			try {
				deleteAllBut(MAX_FILES);
			} catch (Exception ex) {
				Android.Util.Log.Error("ex",ex.StackTrace); 

			}
			
			cursor.Close();
			
			return result;
			
		}
		
		private void deleteAllBut(int limit) {
			ICursor cursor = mDb.Query(FILE_TABLE, new String[] {KEY_FILE_UPDATED}, null, null, null, null, KEY_FILE_UPDATED);
			
			if ( cursor.Count > limit ) {
				cursor.MoveToFirst();
				long time = cursor.GetLong(cursor.GetColumnIndexOrThrow(KEY_FILE_UPDATED));
				
				mDb.ExecSQL("DELETE FROM " + FILE_TABLE + " WHERE " + KEY_FILE_UPDATED + "<" + time + ";");
			}
			
			cursor.Close();
			
		}
		
		public void deleteAllKeys() {
			ContentValues vals = new ContentValues();
			vals.Put(KEY_FILE_KEYFILE, "");
			
			mDb.Update(FILE_TABLE, vals, null, null);
		}
		
		public void deleteFile(String filename) {
			mDb.Delete(FILE_TABLE, KEY_FILE_FILENAME + " = ?", new String[] {filename});
		}

		static string[] getColumnList()
		{
			return new String[] {
				KEY_FILE_ID,
				KEY_FILE_FILENAME,
				KEY_FILE_KEYFILE,
				KEY_FILE_USERNAME,
				KEY_FILE_PASSWORD,
				KEY_FILE_CREDSAVEMODE
			};
		}		
		
		public ICursor fetchAllFiles() {
			ICursor ret;
			ret = mDb.Query(FILE_TABLE, getColumnList(),
					null, null, null, null, KEY_FILE_UPDATED + " DESC", MAX_FILES.ToString());
			return ret;
		}
		
		public ICursor fetchFile(long fileId) {
			ICursor cursor = mDb.Query(true, FILE_TABLE, getColumnList(),
			KEY_FILE_ID + "=" + fileId, null, null, null, null, null);
			
			if ( cursor != null ) {
				cursor.MoveToFirst();
			}
			
			return cursor;
			
		}

		public ICursor fetchFileByName(string fileName)
		{

			ICursor cursor = mDb.Query(true, FILE_TABLE, getColumnList(),
			                           KEY_FILE_FILENAME + " like " + DatabaseUtils.SqlEscapeString(fileName) , null, null, null, null, null);

			if ( cursor != null ) {
				cursor.MoveToFirst();
			}
			
			return cursor;

		}
		
		public String getFileByName(String name) {
			ICursor cursor = mDb.Query(true, FILE_TABLE, getColumnList(),
			KEY_FILE_FILENAME + "= ?", new String[] {name}, null, null, null, null);
			
			if ( cursor == null ) {
				return "";
			}
			
			String keyfileFilename;
			
			if ( cursor.MoveToFirst() ) {
				keyfileFilename = cursor.GetString(cursor.GetColumnIndexOrThrow(KEY_FILE_KEYFILE));
			} else {
				// Cursor is empty
				keyfileFilename = "";
			}
			cursor.Close();
			return keyfileFilename;
		}
		
		public bool hasRecentFiles() {
			ICursor cursor = fetchAllFiles();
			
			bool hasRecent = cursor.Count > 0;
			cursor.Close();
			
			return hasRecent; 
		}

		public IOConnectionInfo cursorToIoc(Android.Database.ICursor cursor)
		{
			if (cursor == null)
				return null;
			IOConnectionInfo ioc = new IOConnectionInfo();
			ioc.Path = cursor.GetString(cursor
			                                .GetColumnIndexOrThrow(FileDbHelper.KEY_FILE_FILENAME));

			ioc.UserName = cursor.GetString(cursor
			                                .GetColumnIndexOrThrow(FileDbHelper.KEY_FILE_USERNAME));

			ioc.Password = cursor.GetString(cursor
			                                .GetColumnIndexOrThrow(FileDbHelper.KEY_FILE_PASSWORD));

			ioc.CredSaveMode = (IOCredSaveMode)cursor.GetInt(cursor
			                                                    .GetColumnIndexOrThrow(FileDbHelper.KEY_FILE_CREDSAVEMODE));
			ioc.CredProtMode = IOCredProtMode.Obf;
			ioc.Obfuscate(false);
			return ioc;
		}
	}

}

