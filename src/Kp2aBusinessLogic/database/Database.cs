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

using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Serialization;

namespace keepass2android
{

	public class Database {
		

		public Dictionary<PwUuid, PwGroup> groups = new Dictionary<PwUuid, PwGroup>(new PwUuidEqualityComparer());
		public Dictionary<PwUuid, PwEntry> entries = new Dictionary<PwUuid, PwEntry>(new PwUuidEqualityComparer());
		public HashSet<PwGroup> dirty = new HashSet<PwGroup>(new PwGroupEqualityFromIdComparer());
		public PwGroup root;
		public PwDatabase pm;
		public IOConnectionInfo mIoc;
		public DateTime mLastChangeDate;
		public SearchDbHelper searchHelper;
		
		public IDrawableFactory drawableFactory;

        IKp2aApp app;

        public Database(IDrawableFactory drawableFactory, IKp2aApp app)
        {
            this.drawableFactory = drawableFactory;
            this.app = app;
        }
		
		private bool loaded = false;

        private bool mReloadRequested = false;

        public bool ReloadRequested
        {
            get { return mReloadRequested; }
            set { mReloadRequested = value; }
        }

		public bool Loaded {
			get { return loaded;}
			set { loaded = value; }
		}

		public bool Open
		{
			get { return Loaded && (!Locked); }
		}

		bool locked;
		public bool Locked
		{
			get
			{
				return locked;
			}
			set
			{
				locked = value;
			}
		}
		
		public bool DidOpenFileChange()
		{
			if ((Loaded == false) || (mIoc.IsLocalFile() == false))
			{
				return false;
			}
			return System.IO.File.GetLastWriteTimeUtc(mIoc.Path) > mLastChangeDate;
		}

		
		public void LoadData(IKp2aApp app, IOConnectionInfo iocInfo, String password, String keyfile, UpdateStatus status)
		{
			mIoc = iocInfo;

			KeePassLib.PwDatabase pwDatabase = new KeePassLib.PwDatabase();

			KeePassLib.Keys.CompositeKey key = new KeePassLib.Keys.CompositeKey();
			key.AddUserKey(new KeePassLib.Keys.KcpPassword(password));
			if (!String.IsNullOrEmpty(keyfile))
			{

				try
				{
					key.AddUserKey(new KeePassLib.Keys.KcpKeyFile(keyfile));
				} catch (Exception)
				{
					throw new KeyFileException();
				}
			}
			
			pwDatabase.Open(iocInfo, key, status);

			if (iocInfo.IsLocalFile())
			{
				mLastChangeDate = System.IO.File.GetLastWriteTimeUtc(iocInfo.Path);
			} else
			{
				mLastChangeDate  = DateTime.MinValue;
			}

			root = pwDatabase.RootGroup;
			populateGlobals(root);


			Loaded = true;
			pm = pwDatabase;
			searchHelper = new SearchDbHelper(app);
		}

		bool quickUnlockEnabled = false;
		public bool QuickUnlockEnabled
		{
			get
			{
				return quickUnlockEnabled;
			}
			set
			{
				quickUnlockEnabled = value;
			}
		}

		//KeyLength of QuickUnlock at time of loading the database.
		//This is important to not allow an attacker to set the length to 1 when QuickUnlock is started already.
		public int QuickUnlockKeyLength
		{
			get;
			set;
		}
		
		public PwGroup SearchForText(String str) {
			PwGroup group = searchHelper.searchForText(this, str);
			
			return group;
			
		}

		public PwGroup Search(SearchParameters searchParams)
		{
			return searchHelper.search(this, searchParams);
		}

		
		public PwGroup SearchForExactUrl(String url) {
			PwGroup group = searchHelper.searchForExactUrl(this, url);
			
			return group;
			
		}

		public PwGroup SearchForHost(String url, bool allowSubdomains) {
			PwGroup group = searchHelper.searchForHost(this, url, allowSubdomains);
			
			return group;
			
		}


		public void SaveData(Context ctx)  {
            
			pm.UseFileTransactions = app.GetBooleanPreference(PreferenceKey.UseFileTransactions);
			pm.Save(null);

		}
		class SaveStatusLogger: IStatusLogger
		{
			#region IStatusLogger implementation
			public void StartLogging (string strOperation, bool bWriteOperationToLog)
			{
			}
			public void EndLogging ()
			{
			}
			public bool SetProgress (uint uPercent)
			{
				Android.Util.Log.Debug("DEBUG", "Progress: " + uPercent+"%");
				return true;
			}
			public bool SetText (string strNewText, LogStatusType lsType)
			{
				Android.Util.Log.Debug("DEBUG", strNewText);
				return true;
			}
			public bool ContinueWork ()
			{
				return true;
			}
			#endregion
		}
		
		private void populateGlobals (PwGroup currentGroup)
		{
			
			var childGroups = currentGroup.Groups;
			var childEntries = currentGroup.Entries;

			foreach (PwEntry e in childEntries) {
				entries [e.Uuid] = e;
			}
			foreach (PwGroup g in childGroups) {
				groups[g.Uuid] = g;
				populateGlobals(g);
			}
		}
		
		public void Clear() {
			groups.Clear();
			entries.Clear();
			dirty.Clear();
			drawableFactory.Clear();
			
			root = null;
			pm = null;
			mIoc = null;
			loaded = false;
			locked = false;
			mReloadRequested = false;
		}
		
		public void markAllGroupsAsDirty() {
			foreach ( PwGroup group in groups.Values ) {
				dirty.Add(group);
			}
			

		}
		
		
	}


}

