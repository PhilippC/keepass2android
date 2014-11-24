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
using System.IO;
using Android.Content;
using Java.Lang;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using keepass2android.Io;
using Exception = System.Exception;
using String = System.String;

namespace keepass2android
{

	public class Database {
		

		public Dictionary<PwUuid, PwGroup> Groups = new Dictionary<PwUuid, PwGroup>(new PwUuidEqualityComparer());
		public Dictionary<PwUuid, PwEntry> Entries = new Dictionary<PwUuid, PwEntry>(new PwUuidEqualityComparer());
		public HashSet<PwGroup> Dirty = new HashSet<PwGroup>(new PwGroupEqualityFromIdComparer());
		public PwGroup Root;
		public PwDatabase KpDatabase;
		public IOConnectionInfo Ioc 
		{
			get
			{
				return KpDatabase == null ? null : KpDatabase.IOConnectionInfo;
			}
		}

		/// <summary>
		/// Information about the last opened entry. Includes the entry but also transformed fields.
		/// </summary>
		public PwEntryOutput LastOpenedEntry { get; set; }

		/// <summary>
		/// if an OTP key was used, this property tells the location of the OTP auxiliary file.
		/// Must be set after loading.
		/// </summary>
		public IOConnectionInfo OtpAuxFileIoc { get; set; }

		public string LastFileVersion;
		public SearchDbHelper SearchHelper;
		
		public IDrawableFactory DrawableFactory;

		readonly IKp2aApp _app;

        public Database(IDrawableFactory drawableFactory, IKp2aApp app)
        {
            DrawableFactory = drawableFactory;
            _app = app;
			CanWrite = true; //default
        }
		
		private bool _loaded;

        private bool _reloadRequested;

        public bool ReloadRequested
        {
            get { return _reloadRequested; }
            set { _reloadRequested = value; }
        }

		public bool Loaded {
			get { return _loaded;}
			set { _loaded = value; }
		}

		public bool DidOpenFileChange()
		{
			if (Loaded == false)
			{
				return false;
			}
			return _app.GetFileStorage(Ioc).CheckForFileChangeFast(Ioc, LastFileVersion);
			
		}


		/// <summary>
		/// Do not call this method directly. Call App.Kp2a.LoadDatabase instead.
		/// </summary>
		public void LoadData(IKp2aApp app, IOConnectionInfo iocInfo, MemoryStream databaseData, CompositeKey compositeKey, ProgressDialogStatusLogger status, IDatabaseLoader databaseLoader)
		{
			PwDatabase pwDatabase = new PwDatabase();

			IFileStorage fileStorage = _app.GetFileStorage(iocInfo);
			Stream s = databaseData ?? fileStorage.OpenFileForRead(iocInfo);
			var fileVersion = _app.GetFileStorage(iocInfo).GetCurrentFileVersionFast(iocInfo);
			PopulateDatabaseFromStream(pwDatabase, s, iocInfo, compositeKey, status, databaseLoader);
			LastFileVersion = fileVersion;
			
			status.UpdateSubMessage("");

			Root = pwDatabase.RootGroup;
			PopulateGlobals(Root);


			Loaded = true;
			KpDatabase = pwDatabase;
			SearchHelper = new SearchDbHelper(app);

			CanWrite = databaseLoader.CanWrite && !fileStorage.IsReadOnly(iocInfo);
		}

		/// <summary>
		/// Indicates whether it is possible to make changes to this database
		/// </summary>
		public bool CanWrite { get; set; }

		protected  virtual void PopulateDatabaseFromStream(PwDatabase pwDatabase, Stream s, IOConnectionInfo iocInfo, CompositeKey compositeKey, ProgressDialogStatusLogger status, IDatabaseLoader databaseLoader)
		{
			IFileStorage fileStorage = _app.GetFileStorage(iocInfo);
			var filename = fileStorage.GetFilenameWithoutPathAndExt(iocInfo);
			pwDatabase.Open(s, filename, iocInfo, compositeKey, status, databaseLoader);
		}


		public PwGroup SearchForText(String str) {
			PwGroup group = SearchHelper.SearchForText(this, str);
			
			return group;
			
		}

		public PwGroup Search(SearchParameters searchParams, IDictionary<PwUuid, KeyValuePair<string, string>> resultContexts)
		{
			return SearchHelper.Search(this, searchParams, resultContexts);
		}

		
		public PwGroup SearchForExactUrl(String url) {
			PwGroup group = SearchHelper.SearchForExactUrl(this, url);
			
			return group;
			
		}

		public PwGroup SearchForHost(String url, bool allowSubdomains) {
			PwGroup group = SearchHelper.SearchForHost(this, url, allowSubdomains);
			
			return group;
			
		}


		public virtual void SaveData(Context ctx)  {
            
			KpDatabase.UseFileTransactions = _app.GetBooleanPreference(PreferenceKey.UseFileTransactions);
			using (IWriteTransaction trans = _app.GetFileStorage(Ioc).OpenWriteTransaction(Ioc, KpDatabase.UseFileTransactions))
			{
				KpDatabase.Save(trans.OpenFile(), null);
				trans.CommitWrite();
			}
			
		}

		
		private void PopulateGlobals (PwGroup currentGroup)
		{
			
			var childGroups = currentGroup.Groups;
			var childEntries = currentGroup.Entries;

			foreach (PwEntry e in childEntries) {
				Entries [e.Uuid] = e;
			}
			foreach (PwGroup g in childGroups) {
				Groups[g.Uuid] = g;
				PopulateGlobals(g);
			}
		}
		
		public void Clear() {
			Groups.Clear();
			Entries.Clear();
			Dirty.Clear();
			DrawableFactory.Clear();
			
			Root = null;
			KpDatabase = null;
			_loaded = false;
			CanWrite = true;
			_reloadRequested = false;
			OtpAuxFileIoc = null;
		}
		
		public void MarkAllGroupsAsDirty() {
			foreach ( PwGroup group in Groups.Values ) {
				Dirty.Add(group);
			}
			

		}
		
		
	}


}

