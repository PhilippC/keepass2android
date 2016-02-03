/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.Globalization;
using Android.Content;
using Android.Database;
using Android.Database.Sqlite;
using KeePassLib.Serialization;

namespace keepass2android
{
	/// <summary>
	/// Class to store the recent files in a database
	/// </summary>
	public class FileDbHelper {
		
		public const String LastFilename = "lastFile";
		public const String LastKeyfile = "lastKey";
		
		private const String DatabaseName = "keepass2android";
		private const String FileTable = "files";
		private const int DatabaseVersion = 1;
		
		private const int MaxFiles = 5;
		
		public const String KeyFileId = "_id";
		public const String KeyFileFilename = "fileName";
		public const String KeyFileUsername = "username";
		public const String KeyFilePassword = "password";
		public const String KeyFileCredsavemode = "credSaveMode";
		public const String KeyFileKeyfile = "keyFile";
		public const String KeyFileUpdated = "updated";
		
		private const String DatabaseCreate = 
			"create table " + FileTable + " ( " + KeyFileId + " integer primary key autoincrement, " 
				+ KeyFileFilename + " text not null, " 
				+ KeyFileKeyfile + " text, "
				+ KeyFileUsername + " text, "
				+ KeyFilePassword + " text, "
				+ KeyFileCredsavemode + " integer not null,"
				+ KeyFileUpdated + " integer not null);";
		
		private readonly Context mCtx;
		private DatabaseHelper mDbHelper;
		private SQLiteDatabase mDb;
		
		private class DatabaseHelper : SQLiteOpenHelper {
		    public DatabaseHelper(Context ctx): base(ctx, FileDbHelper.DatabaseName, null, DatabaseVersion) {
			}
			
			
			public override void OnCreate(SQLiteDatabase db) {
				db.ExecSQL(DatabaseCreate);
			
			}
			
			
			public override void OnUpgrade(SQLiteDatabase db, int oldVersion, int newVersion) {
				// Only one database version so far
			}

		}
		
		public FileDbHelper(Context ctx) {
			mCtx = ctx;
		}
		
		public FileDbHelper Open() {
			mDbHelper = new DatabaseHelper(mCtx);
			mDb = mDbHelper.WritableDatabase;
			return this;
		}
		
		public bool IsOpen() {
			return mDb.IsOpen;
		}
		
		public void Close() {
			mDb.Close();
		}
		
		public long CreateFile(IOConnectionInfo ioc, String keyFile) {
			
			// Check to see if this filename is already used
			ICursor cursor;
			try {
				cursor = mDb.Query(true, FileTable, new[] {KeyFileId}, 
				KeyFileFilename + "=?", new[] {ioc.Path}, null, null, null, null);
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
				long id = cursor.GetLong(cursor.GetColumnIndexOrThrow(KeyFileId));
				
				var vals = new ContentValues();
				vals.Put(KeyFileKeyfile, keyFile);
				vals.Put(KeyFileUpdated, Java.Lang.JavaSystem.CurrentTimeMillis());

				vals.Put(KeyFileUsername, iocToStore.UserName);
				vals.Put(KeyFilePassword, iocToStore.Password);
				vals.Put(KeyFileCredsavemode, (int)iocToStore.CredSaveMode);
				
				result = mDb.Update(FileTable, vals, KeyFileId + " = " + id, null);
				
				// Otherwise add the new entry
			} else {
				var vals = new ContentValues();
				vals.Put(KeyFileFilename, ioc.Path);
				vals.Put(KeyFileKeyfile, keyFile);
				vals.Put(KeyFileUsername, iocToStore.UserName);
				vals.Put(KeyFilePassword, iocToStore.Password);
				vals.Put(KeyFileCredsavemode, (int)iocToStore.CredSaveMode);
				vals.Put(KeyFileUpdated, Java.Lang.JavaSystem.CurrentTimeMillis());
				
				result = mDb.Insert(FileTable, null, vals);
				
			}
			// Delete all but the last five records
			try {
				DeleteAllBut(MaxFiles);
			} catch (Exception ex) {
				Android.Util.Log.Error("ex",ex.StackTrace); 

			}
			
			cursor.Close();
			
			return result;
			
		}
		
		private void DeleteAllBut(int limit) {
			ICursor cursor = mDb.Query(FileTable, new[] {KeyFileUpdated}, null, null, null, null, KeyFileUpdated);
			
			if ( cursor.Count > limit ) {
				cursor.MoveToFirst();
				long time = cursor.GetLong(cursor.GetColumnIndexOrThrow(KeyFileUpdated));
				
				mDb.ExecSQL("DELETE FROM " + FileTable + " WHERE " + KeyFileUpdated + "<" + time + ";");
			}
			
			cursor.Close();
			
		}
		
		public void DeleteAllKeys() {
			var vals = new ContentValues();
			vals.Put(KeyFileKeyfile, "");
			
			mDb.Update(FileTable, vals, null, null);
		}
		
		public void DeleteFile(String filename) {
			mDb.Delete(FileTable, KeyFileFilename + " = ?", new[] {filename});
		}
		public void DeleteAll()
		{
			mDb.Delete(FileTable, null, null);
		}

		static string[] GetColumnList()
		{
			return new[] {
				KeyFileId,
				KeyFileFilename,
				KeyFileKeyfile,
				KeyFileUsername,
				KeyFilePassword,
				KeyFileCredsavemode
			};
		}		
		
		public ICursor FetchAllFiles()
		{
		    ICursor ret = mDb.Query(FileTable, GetColumnList(),
		                            null, null, null, null, KeyFileUpdated + " DESC", MaxFiles.ToString(CultureInfo.InvariantCulture));
		    return ret;
		}

	    public ICursor FetchFile(long fileId) {
			ICursor cursor = mDb.Query(true, FileTable, GetColumnList(),
			KeyFileId + "=" + fileId, null, null, null, null, null);
			
			if ( cursor != null ) {
				cursor.MoveToFirst();
			}
			
			return cursor;
			
		}

		public ICursor FetchFileByName(string fileName)
		{

			ICursor cursor = mDb.Query(true, FileTable, GetColumnList(),
			                           KeyFileFilename + " like " + DatabaseUtils.SqlEscapeString(fileName) , null, null, null, null, null);

			if ( cursor != null ) {
				cursor.MoveToFirst();
			}
			
			return cursor;

		}
		
		public String GetKeyFileForFile(String name) {
			ICursor cursor = mDb.Query(true, FileTable, GetColumnList(),
			KeyFileFilename + "= ?", new[] {name}, null, null, null, null);
			
			if ( cursor == null ) {
				return null;
			}
			
			String keyfileFilename;
			
			if ( cursor.MoveToFirst() ) {
				keyfileFilename = cursor.GetString(cursor.GetColumnIndexOrThrow(KeyFileKeyfile));
			} else {
				// Cursor is empty
				keyfileFilename = null;
			}
			cursor.Close();
			if (keyfileFilename == "")
				return null;
			return keyfileFilename;
		}
		
		public bool HasRecentFiles()
		{
			return NumberOfRecentFiles() > 0;
		}

		public int NumberOfRecentFiles()
		{
			ICursor cursor = FetchAllFiles();

			int numRecent = cursor.Count;
			cursor.Close();

			return numRecent; 
		}

		public IOConnectionInfo CursorToIoc(ICursor cursor)
		{
			if (cursor == null)
				return null;
			var ioc = new IOConnectionInfo
			    {
			        Path =  cursor.GetString(cursor
			                                    .GetColumnIndexOrThrow(KeyFileFilename)),
			        UserName = cursor.GetString(cursor
			                                        .GetColumnIndexOrThrow(KeyFileUsername)),
			        Password = cursor.GetString(cursor
			                                        .GetColumnIndexOrThrow(KeyFilePassword)),
			        CredSaveMode = (IOCredSaveMode) cursor.GetInt(cursor
			                                                          .GetColumnIndexOrThrow(KeyFileCredsavemode)),
			        CredProtMode = IOCredProtMode.Obf
			    };

			ioc.Obfuscate(false);

			App.Kp2a.GetFileStorage(ioc).ResolveAccount(ioc);

			return ioc;
		}
	}

}

