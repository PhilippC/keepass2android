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
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Java.Lang;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using keepass2android.Io;
using KeePassLib.Interfaces;
using KeePassLib.Utility;
using keepass2android.KeeShare;
using Exception = System.Exception;
using String = System.String;

namespace keepass2android
{

  public class Database
  {
    public HashSet<IStructureItem> Elements = new HashSet<IStructureItem>();
    public Dictionary<PwUuid, PwGroup> GroupsById = new Dictionary<PwUuid, PwGroup>(new PwUuidEqualityComparer());
    public Dictionary<PwUuid, PwEntry> EntriesById = new Dictionary<PwUuid, PwEntry>(new PwUuidEqualityComparer());
    public PwGroup Root;
    public PwDatabase KpDatabase;
    public IOConnectionInfo Ioc
    {
      get
      {

        return KpDatabase?.IOConnectionInfo;

      }
    }

    /// <summary>
    /// if an OTP key was used, this property tells the location of the OTP auxiliary file.
    /// Must be set after loading.
    /// </summary>
    public IOConnectionInfo OtpAuxFileIoc { get; set; }

    public string LastFileVersion;
    public SearchDbHelper SearchHelper;

    public IDrawableFactory DrawableFactory;

    readonly IKp2aApp _app;

    /// <summary>
    /// Time when this database was last loaded/synchronized
    /// </summary>
    private DateTime lastSyncTime_ = DateTime.MaxValue;
    public DateTime LastSyncTime
    {
      get => lastSyncTime_;
      set
      {
        lastSyncTime_ = value;
        SynchronizationPending = false;
        SynchronizationRunning = false;
      }
    }

    /// <summary>
    /// If true, background synchronization was requested but not performed e.g. due to network connectivity
    /// </summary>
    public bool SynchronizationPending { get; set; } = false;

    /// <summary>
    /// True while a synchronization is running
    /// </summary>
    public bool SynchronizationRunning { get; set; } = false;

    public Database(IDrawableFactory drawableFactory, IKp2aApp app)
    {
      DrawableFactory = drawableFactory;
      _app = app;
      CanWrite = true; //default
    }

    private IDatabaseFormat _databaseFormat = new KdbxDatabaseFormat(KdbxFormat.Default);
    private bool? _hasTotpEntries;

    public bool ReloadRequested { get; set; }

    public bool DidOpenFileChange()
    {
      return _app.GetFileStorage(Ioc).CheckForFileChangeFast(Ioc, LastFileVersion);
    }


    /// <summary>
    /// Do not call this method directly. Call App.Kp2a.LoadDatabase instead.
    /// </summary>
    public void LoadData(IKp2aApp app, IOConnectionInfo iocInfo, MemoryStream databaseData, CompositeKey compositeKey, IKp2aStatusLogger status, IDatabaseFormat databaseFormat)
    {
      PwDatabase pwDatabase = new PwDatabase();

      IFileStorage fileStorage = _app.GetFileStorage(iocInfo);
      Kp2aLog.Log("LoadData: Retrieving stream");
      Stream s = databaseData ?? fileStorage.OpenFileForRead(iocInfo);
      Kp2aLog.Log("LoadData: GetCurrentFileVersion");
      var fileVersion = _app.GetFileStorage(iocInfo).GetCurrentFileVersionFast(iocInfo);
      Kp2aLog.Log("LoadData: PopulateDatabaseFromStream");
      PopulateDatabaseFromStream(pwDatabase, s, iocInfo, compositeKey, status, databaseFormat);
      LastFileVersion = fileVersion;

      status.UpdateSubMessage("");

      Root = pwDatabase.RootGroup;
      PopulateGlobals(Root);


      KpDatabase = pwDatabase;
      SearchHelper = new SearchDbHelper(app);

      _databaseFormat = databaseFormat;
      _hasTotpEntries = null;

      CanWrite = databaseFormat.CanWrite && !fileStorage.IsReadOnly(iocInfo);

      Kp2aLog.Log("LoadData: Checking for KeeShare references");
      KeeShareImporter.CheckAndImport(this, app);
    }

    /// <summary>
    /// Indicates whether it is possible to make changes to this database
    /// </summary>
    public bool CanWrite { get; set; }

    public IDatabaseFormat DatabaseFormat
    {
      get { return _databaseFormat; }
      set { _databaseFormat = value; }
    }

    public string IocAsHexString()
    {
      return IoUtil.IocAsHexString(Ioc);
    }

    public static string GetFingerprintPrefKey(IOConnectionInfo ioc)
    {
      var iocAsHexString = IoUtil.IocAsHexString(ioc);

      return "kp2a_ioc_" + iocAsHexString;
    }


    public static string GetFingerprintModePrefKey(IOConnectionInfo ioc)
    {
      return GetFingerprintPrefKey(ioc) + "_mode";
    }

    public string CurrentFingerprintPrefKey
    {
      get { return GetFingerprintPrefKey(Ioc); }
    }

    public string CurrentFingerprintModePrefKey
    {
      get { return GetFingerprintModePrefKey(Ioc); }
    }

    protected virtual void PopulateDatabaseFromStream(PwDatabase pwDatabase, Stream s, IOConnectionInfo iocInfo, CompositeKey compositeKey, IKp2aStatusLogger status, IDatabaseFormat databaseFormat)
    {
      IFileStorage fileStorage = _app.GetFileStorage(iocInfo);
      var filename = fileStorage.GetFilenameWithoutPathAndExt(iocInfo);
      pwDatabase.Open(s, filename, iocInfo, compositeKey, status, databaseFormat);
    }


    public PwGroup SearchForText(String str)
    {
      PwGroup group = SearchHelper.SearchForText(this, str);

      return group;

    }

    public PwGroup Search(SearchParameters searchParams, IDictionary<PwUuid, KeyValuePair<string, string>> resultContexts)
    {
      if (SearchHelper == null)
        throw new Exception("SearchHelper is null");
      return SearchHelper.Search(this, searchParams, resultContexts);
    }


    public PwGroup SearchForExactUrl(String url)
    {
      PwGroup group = SearchHelper.SearchForExactUrl(this, url);

      return group;

    }
    public PwGroup SearchForUuid(String uuid)
    {
      PwGroup group = SearchHelper.SearchForUuid(this, uuid);

      return group;

    }

    public PwGroup SearchForHost(String url, bool allowSubdomains)
    {
      PwGroup group = SearchHelper.SearchForHost(this, url, allowSubdomains);

      return group;

    }


    public void SaveData(IFileStorage fileStorage)
    {

      using (IWriteTransaction trans = fileStorage.OpenWriteTransaction(Ioc, _app.GetBooleanPreference(PreferenceKey.UseFileTransactions)))
      {
        DatabaseFormat.Save(KpDatabase, trans.OpenFile());

        trans.CommitWrite();
      }
      _hasTotpEntries = null;

    }

    public bool HasTotpEntries
    {
      get
      {
        if (_hasTotpEntries == null)
        {
          _hasTotpEntries = true;
        }
        return _hasTotpEntries.Value;
      }
    }

    private void PopulateGlobals(PwGroup currentGroup, bool checkForDuplicateUuids)
    {

      var childGroups = currentGroup.Groups;
      var childEntries = currentGroup.Entries;

      foreach (PwEntry e in childEntries)
      {
        if (checkForDuplicateUuids)
        {
          if (EntriesById.ContainsKey(e.Uuid))
          {
            throw new DuplicateUuidsException("Same UUID for entries '" + EntriesById[e.Uuid].Strings.ReadSafe(PwDefs.TitleField) + "' and '" + e.Strings.ReadSafe(PwDefs.TitleField) + "'.");
          }

        }
        EntriesById[e.Uuid] = e;
        Elements.Add(e);
      }

      GroupsById[currentGroup.Uuid] = currentGroup;
      Elements.Add(currentGroup);
      foreach (PwGroup g in childGroups)
      {
        if (checkForDuplicateUuids)
        {
          /*Disable check. Annoying for users, no problem for KP2A.
          if (Groups.ContainsKey(g.Uuid))
          {
              throw new DuplicateUuidsException();
          }
           * */
        }
        PopulateGlobals(g);
      }
    }
    private void PopulateGlobals(PwGroup currentGroup)
    {
      PopulateGlobals(currentGroup, _app.CheckForDuplicateUuids);
    }


    public void UpdateGlobals()
    {
      EntriesById.Clear();
      GroupsById.Clear();
      Elements.Clear();
      PopulateGlobals(Root);
    }
  }

  [Serializable]
  public class DuplicateUuidsException : Exception
  {
    public DuplicateUuidsException()
    {
    }

    public DuplicateUuidsException(string message) : base(message)
    {
    }

    protected DuplicateUuidsException(
        SerializationInfo info,
        StreamingContext context) : base(info, context)
    {
    }
  }
}

