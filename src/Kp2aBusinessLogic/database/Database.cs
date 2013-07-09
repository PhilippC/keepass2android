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
using System.IO;
using Android.Content;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace keepass2android
{

	public class Database {
		

		public Dictionary<PwUuid, PwGroup> Groups = new Dictionary<PwUuid, PwGroup>(new PwUuidEqualityComparer());
		public Dictionary<PwUuid, PwEntry> Entries = new Dictionary<PwUuid, PwEntry>(new PwUuidEqualityComparer());
		public HashSet<PwGroup> Dirty = new HashSet<PwGroup>(new PwGroupEqualityFromIdComparer());
		public PwGroup Root;
		public PwDatabase KpDatabase;
		public IOConnectionInfo Ioc { get { return KpDatabase.IOConnectionInfo; } }
		public string LastFileVersion;
		public SearchDbHelper SearchHelper;
		
		public IDrawableFactory DrawableFactory;

		readonly IKp2aApp _app;

        public Database(IDrawableFactory drawableFactory, IKp2aApp app)
        {
            DrawableFactory = drawableFactory;
            _app = app;
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

		public bool Open
		{
			get { return Loaded && (!Locked); }
		}

		bool _locked;
		public bool Locked
		{
			get
			{
				return _locked;
			}
			set
			{
				Kp2aLog.Log("Locked=" + _locked);
				_locked = value;
			}
		}
		
		public bool DidOpenFileChange()
		{
			if (Loaded == false)
			{
				return false;
			}
			return _app.GetFileStorage(Ioc).CheckForFileChangeFast(Ioc, LastFileVersion);
			
		}

		
		public void LoadData(IKp2aApp app, IOConnectionInfo iocInfo, String password, String keyfile, ProgressDialogStatusLogger status)
		{
			PwDatabase pwDatabase = new PwDatabase();

			CompositeKey compositeKey = new CompositeKey();
			compositeKey.AddUserKey(new KcpPassword(password));
			if (!String.IsNullOrEmpty(keyfile))
			{

				try
				{
					compositeKey.AddUserKey(new KcpKeyFile(keyfile));
				} catch (Exception e)
				{
					Kp2aLog.Log(e.ToString());
					throw new KeyFileException();
				}
			}
			
			try
			{
				IFileStorage fileStorage = _app.GetFileStorage(iocInfo);
				pwDatabase.Open(fileStorage.OpenFileForRead(iocInfo), fileStorage.GetFilenameWithoutPathAndExt(iocInfo), iocInfo, compositeKey, status);
			}
			catch (Exception)
			{
				if ((password == "") && (keyfile != null))
				{
					//if we don't get a password, we don't know whether this means "empty password" or "no password"
					//retry without password:
					compositeKey.RemoveUserKey(compositeKey.GetUserKey(typeof (KcpPassword)));
					pwDatabase.Open(iocInfo, compositeKey, status);
				}
				else throw;
			}
			
			status.UpdateSubMessage("");

			LastFileVersion = _app.GetFileStorage(iocInfo).GetCurrentFileVersionFast(iocInfo);

			Root = pwDatabase.RootGroup;
			PopulateGlobals(Root);


			Loaded = true;
			KpDatabase = pwDatabase;
			SearchHelper = new SearchDbHelper(app);
		}

		public bool QuickUnlockEnabled { get; set; }

		//KeyLength of QuickUnlock at time of loading the database.
		//This is important to not allow an attacker to set the length to 1 when QuickUnlock is started already.
		public int QuickUnlockKeyLength
		{
			get;
			set;
		}
		
		public PwGroup SearchForText(String str) {
			PwGroup group = SearchHelper.SearchForText(this, str);
			
			return group;
			
		}

		public PwGroup Search(SearchParameters searchParams, IDictionary<PwUuid, String> resultContexts)
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


		public void SaveData(Context ctx)  {
            
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
			_locked = false;
			_reloadRequested = false;
		}
		
		public void MarkAllGroupsAsDirty() {
			foreach ( PwGroup group in Groups.Values ) {
				Dirty.Add(group);
			}
			

		}
		
		
	}


}

