using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
#if !EXCLUDE_KEYTRANSFORM
using Android.App;
using Com.Keepassdroid.Database;
using Com.Keepassdroid.Database.Exception;
#endif
using Com.Keepassdroid.Database.Save;
using Java.Util;
using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Security;
using Exception = System.Exception;
using PwIcon = KeePassLib.PwIcon;
using Random = System.Random;

namespace keepass2android
{
	public class KdbDatabaseFormat: IDatabaseFormat
	{
		private readonly IKp2aApp _app;
		private Dictionary<PwUuid, AdditionalGroupData> _groupData = new Dictionary<PwUuid, AdditionalGroupData>();
		private static readonly DateTime _expireNever = new DateTime(2999,12,28,23,59,59);
		private List<PwEntryV3> _metaStreams;

		public KdbDatabaseFormat(IKp2aApp app)
		{
			_app = app;
		}

		public void PopulateDatabaseFromStream(PwDatabase db, Stream s, IStatusLogger slLogger)
		{
			#if !EXCLUDE_KEYTRANSFORM
			var importer = new Com.Keepassdroid.Database.Load.ImporterV3();

			var hashingStream = new HashingStreamEx(s, false, new SHA256Managed());

			_metaStreams = new List<PwEntryV3>();

			string password = "";//no need to distinguish between null and "" because empty passwords are invalid (and null is not allowed)
			KcpPassword passwordKey = (KcpPassword)db.MasterKey.GetUserKey(typeof(KcpPassword));
			if (passwordKey != null)
			{
				password = passwordKey.Password.ReadString();
			}

			KcpKeyFile passwordKeyfile = (KcpKeyFile)db.MasterKey.GetUserKey(typeof(KcpKeyFile));
			MemoryStream keyfileStream = null;
			if (passwordKeyfile != null)
			{
				keyfileStream = new MemoryStream(passwordKeyfile.RawFileData.ReadData());
			}


			try
			{
				var dbv3 = importer.OpenDatabase(hashingStream, password, keyfileStream);

				db.Name = dbv3.Name;
				db.KeyEncryptionRounds = (ulong) dbv3.NumKeyEncRounds;
				db.RootGroup = ConvertGroup(dbv3.RootGroup);
				if (dbv3.Algorithm == PwEncryptionAlgorithm.Rjindal)
				{
					db.DataCipherUuid = StandardAesEngine.AesUuid;
				}
				else
				{
					db.DataCipherUuid = new PwUuid(TwofishCipher.TwofishCipherEngine.TwofishCipherUuidBytes);
				}
				
				
			}
			catch (Java.IO.FileNotFoundException e)
			{
				throw new FileNotFoundException(
					e.Message, e);
			}  
			catch (Java.Lang.Exception e)
			{
				if (e.Message == "Invalid key!")
					throw new InvalidCompositeKeyException();
				throw new Exception(e.LocalizedMessage ??
				e.Message ??
				e.GetType().Name, e);
			}
			
			HashOfLastStream = hashingStream.Hash;
			if (HashOfLastStream == null)
				throw new Exception("hashing didn't work"); //todo remove
#else
			throw new Exception("Kdb is excluded with Key transform!");
#endif
		}

			#if !EXCLUDE_KEYTRANSFORM

		private PwGroup ConvertGroup(PwGroupV3 groupV3)
		{
			PwGroup pwGroup = new PwGroup(true, false);
			pwGroup.Uuid = CreateUuidFromGroupId(groupV3.Id.Id);

			//check if we have group data for this group already (from loading in a previous pass).
			//then use the same UUID (important for merging)
			var gdForGroup = _groupData.Where(g => g.Value.Id == groupV3.Id.Id).ToList();
			if (gdForGroup.Count == 1)
			{
				pwGroup.Uuid = gdForGroup.Single().Key;
			}
			
			pwGroup.Name = groupV3.Name;
			Android.Util.Log.Debug("KP2A", "load kdb: group " + groupV3.Name);
			pwGroup.CreationTime = ConvertTime(groupV3.TCreation);
			pwGroup.LastAccessTime = ConvertTime(groupV3.TLastAccess);
			pwGroup.LastModificationTime = ConvertTime(groupV3.TLastMod);
			pwGroup.ExpiryTime = ConvertTime(groupV3.TExpire);
			pwGroup.Expires = !(Math.Abs((pwGroup.ExpiryTime - _expireNever).TotalMilliseconds) < 500); ;

			if (groupV3.Icon != null)
				pwGroup.IconId = (PwIcon) groupV3.Icon.IconId;
			_groupData[pwGroup.Uuid] = new AdditionalGroupData
				{
					Flags = groupV3.Flags,
					Id = groupV3.Id.Id
				};


			for (int i = 0; i < groupV3.ChildGroups.Count;i++)
			{
				pwGroup.AddGroup(ConvertGroup(groupV3.GetGroupAt(i)), true);
			}
			for (int i = 0; i < groupV3.ChildEntries.Count; i++)
			{
				var entry = groupV3.GetEntryAt(i);
				if (entry.IsMetaStream)
				{
					_metaStreams.Add(entry);
					continue;
				}
					
				pwGroup.AddEntry(ConvertEntry(entry), true);
			}
			
			return pwGroup;
		}

		private PwUuid CreateUuidFromGroupId(int id)
		{
			byte[] template = new byte[] { 0xd2, 0x18, 0x22, 0x93, 
										   0x8e, 0xa4, 0x43, 0xf2, 
										   0xb4, 0xb5, 0x2a, 0x49, 
										   0x00, 0x00, 0x00, 0x00};
			byte[] idBytes = BitConverter.GetBytes(id);
			for (int i = 0; i < 4; i++)
			{
				template[i + 12] = idBytes[i];
			}
			return new PwUuid(template);
		}

		private PwEntry ConvertEntry(PwEntryV3 fromEntry)
		{
			PwEntry toEntry = new PwEntry(false, false);
			toEntry.Uuid = new PwUuid(fromEntry.Uuid.ToArray());
			String modTime = Android.Text.Format.DateFormat.GetTimeFormat(Application.Context).Format(fromEntry.TCreation.JDate);
			Android.Util.Log.Debug("KP2A", modTime);
			toEntry.CreationTime = ConvertTime(fromEntry.TCreation);
			toEntry.LastAccessTime = ConvertTime(fromEntry.TLastAccess);
			toEntry.LastModificationTime = ConvertTime(fromEntry.TLastMod);

			toEntry.ExpiryTime = ConvertTime(fromEntry.TExpire);

			toEntry.ExpiryTime = new DateTime(toEntry.ExpiryTime.Year, toEntry.ExpiryTime.Month, toEntry.ExpiryTime.Day, toEntry.ExpiryTime.Hour, toEntry.ExpiryTime.Minute, toEntry.ExpiryTime.Second);
			toEntry.Expires = !(Math.Abs((toEntry.ExpiryTime - _expireNever).TotalMilliseconds) < 500);

			if (fromEntry.Icon != null)
				toEntry.IconId = (PwIcon) fromEntry.Icon.IconId;
			SetFieldIfAvailable(toEntry, PwDefs.TitleField, false, fromEntry.Title);
			Android.Util.Log.Debug("KP2A", "load kdb: entry " + toEntry.Strings.ReadSafe(PwDefs.TitleField));
			SetFieldIfAvailable(toEntry, PwDefs.UserNameField, false, fromEntry.Username);
			SetFieldIfAvailable(toEntry, PwDefs.UrlField, false, fromEntry.Url);
			SetFieldIfAvailable(toEntry, PwDefs.PasswordField, true, fromEntry.Password);
			SetFieldIfAvailable(toEntry, PwDefs.NotesField, true, fromEntry.Additional);

			if ((fromEntry.GetBinaryData() != null) && (fromEntry.GetBinaryData().Length > 0))
			{
				toEntry.Binaries.Set(fromEntry.BinaryDesc, new ProtectedBinary(true, fromEntry.GetBinaryData()));
			}
			return toEntry;
		}

		private void SetFieldIfAvailable(PwEntry pwEntry, string fieldName, bool makeProtected, string value)
		{
			if (value != null)
			{
				pwEntry.Strings.Set(fieldName, new ProtectedString(makeProtected, value));	
			}
			
		}

		private DateTime ConvertTime(PwDate date)
		{
			if (date == null)
				return PwDefs.DtDefaultNow;

			return JavaTimeToCSharp(date.JDate.Time);
		}

		private DateTime JavaTimeToCSharp(long javatime)
		{
			
			var utcTime = new DateTime(1970, 1, 1).AddMilliseconds(javatime);
			return TimeZoneInfo.ConvertTimeFromUtc(utcTime, TimeZoneInfo.Local);

		}
#endif
		public byte[] HashOfLastStream { get; private set; }
		public bool CanWrite { get { return true; } }
		public string SuccessMessage { get { return null; } }

		public void Save(PwDatabase kpDatabase, Stream stream)
		{
			PwDatabaseV3 db =new PwDatabaseV3();
			KcpPassword pwd = kpDatabase.MasterKey.GetUserKey<KcpPassword>();
			string password = pwd != null ? pwd.Password.ReadString() : "";
			KcpKeyFile keyfile = kpDatabase.MasterKey.GetUserKey<KcpKeyFile>();
			Stream keyfileContents = null;
			if (keyfile != null)
			{
				keyfileContents = new MemoryStream(keyfile.RawFileData.ReadData());
			}
			db.SetMasterKey(password, keyfileContents);
			db.NumRounds = (long) kpDatabase.KeyEncryptionRounds;
			db.Name = kpDatabase.Name;
			if (kpDatabase.DataCipherUuid.Equals(StandardAesEngine.AesUuid))
			{
				db.Algorithm = PwEncryptionAlgorithm.Rjindal;
			}
			else
			{
				db.Algorithm = PwEncryptionAlgorithm.Twofish;
			}
			
			//create groups
			db.Groups.Clear();
			var fromGroups = kpDatabase.RootGroup.GetGroups(true);
			Dictionary<int, PwGroupV3> groupV3s = new Dictionary<int, PwGroupV3>(fromGroups.Count());
			foreach (PwGroup g in fromGroups)
			{
				if (g == kpDatabase.RootGroup)
					continue;
				PwGroupV3 groupV3 = ConvertGroup(g, db);
				db.Groups.Add(groupV3);
				groupV3s[groupV3.Id.Id] = groupV3;
			}

			//traverse again and assign parents
			db.RootGroup = ConvertGroup(kpDatabase.RootGroup, db);
			db.RootGroup.Level = -1;

			AssignParent(kpDatabase.RootGroup, db, groupV3s);

			foreach (PwEntry e in kpDatabase.RootGroup.GetEntries(true))
			{
				PwEntryV3 entryV3 = ConvertEntry(e, db);
				entryV3.Parent = groupV3s[_groupData[e.ParentGroup.Uuid].Id];
				entryV3.Parent.ChildEntries.Add(entryV3);
				entryV3.GroupId = entryV3.Parent.Id.Id;
				db.Entries.Add(entryV3);
			}

			//add meta stream entries:
			if (db.Groups.Any())
			{
				foreach (var metaEntry in _metaStreams)
				{
					metaEntry.GroupId = db.Groups.First().Id.Id;
					db.Entries.Add(metaEntry);
				}
	
			}
			

			HashingStreamEx hashedStream = new HashingStreamEx(stream, true, null);
			PwDbV3Output output = new PwDbV3Output(db, hashedStream);
			output.Output();
			hashedStream.Close();
			HashOfLastStream = hashedStream.Hash;
			
			kpDatabase.HashOfLastIO = kpDatabase.HashOfFileOnDisk = HashOfLastStream;
			stream.Close();
		}

		public bool CanHaveEntriesInRootGroup
		{
			get { return false; }
		}

		public bool CanHaveMultipleAttachments
		{
			get { return false; }
		}

		public bool CanHaveCustomFields
		{
			get { return false; }
		}

		public bool HasDefaultUsername
		{
			get { return false; }
		}

		public bool HasDatabaseName
		{
			get { return false; }
		}

		public bool SupportsAttachmentKeys
		{
			get { return false; }
		}

		public bool SupportsTags
		{
			get { return false; }
		}

		public bool SupportsOverrideUrl
		{
			get { return false; }
		}

		public bool CanRecycle
		{
			get { return false; }
		}

		public bool SupportsTemplates
		{
			get { return false; }
		}

		private void AssignParent(PwGroup kpParent, PwDatabaseV3 dbV3, Dictionary<int, PwGroupV3> groupV3s)
		{
			PwGroupV3 parentV3;
			if (kpParent.ParentGroup == null)
			{
				parentV3 = dbV3.RootGroup;
			}
			else
			{
				parentV3 = groupV3s[_groupData[kpParent.Uuid].Id];
			}

			foreach (PwGroup g in kpParent.Groups.OrderBy(g => g.Name))
			{
				PwGroupV3 groupV3 = groupV3s[_groupData[g.Uuid].Id];
				
				parentV3.ChildGroups.Add(groupV3);
				groupV3.Parent = parentV3;

				AssignParent(g, dbV3, groupV3s);
			}
		}

		private PwGroupV3 ConvertGroup(PwGroup fromGroup, PwDatabaseV3 dbTo)
		{
			PwGroupV3 toGroup = new PwGroupV3();
			toGroup.Name = fromGroup.Name;
			//todo remove
			Android.Util.Log.Debug("KP2A", "save kdb: group " + fromGroup.Name);

			toGroup.TCreation = new PwDate(ConvertTime(fromGroup.CreationTime));
			toGroup.TLastAccess= new PwDate(ConvertTime(fromGroup.LastAccessTime));
			toGroup.TLastMod = new PwDate(ConvertTime(fromGroup.LastModificationTime));
			if (fromGroup.Expires)
			{
				toGroup.TExpire = new PwDate(ConvertTime(fromGroup.ExpiryTime));
			}
			else
			{
				toGroup.TExpire = new PwDate(ConvertTime(_expireNever));
			}
			
			toGroup.Icon = dbTo.IconFactory.GetIcon((int) fromGroup.IconId);
			AdditionalGroupData groupData;
			if (_groupData.TryGetValue(fromGroup.Uuid, out groupData))
			{
				toGroup.Id = new PwGroupIdV3(groupData.Id);
				toGroup.Flags = groupData.Flags;
			}
			else
			{
				//group was added
				do
				{
					toGroup.Id = new PwGroupIdV3(new Random().Next());	
				} while (_groupData.Values.Any(gd => gd.Id == toGroup.Id.Id)); //retry if id already exists
				//store to block new id and reuse when saving again (without loading in between)
				_groupData[fromGroup.Uuid] = new AdditionalGroupData
					{
						Id = toGroup.Id.Id
					};

			}

			return toGroup;
		}

		private PwEntryV3 ConvertEntry(PwEntry fromEntry, PwDatabaseV3 dbTo)
		{
			PwEntryV3 toEntry = new PwEntryV3();
			toEntry.Uuid = fromEntry.Uuid.UuidBytes;
			toEntry.CreationTime = ConvertTime(fromEntry.CreationTime);
			toEntry.LastAccessTime = ConvertTime(fromEntry.LastAccessTime);
			toEntry.LastModificationTime = ConvertTime(fromEntry.LastModificationTime);

			if (fromEntry.Expires)
			{
				toEntry.ExpiryTime = ConvertTime(fromEntry.ExpiryTime);	
			}
			else
			{
				toEntry.ExpiryTime = ConvertTime(_expireNever);	
			}
			

			toEntry.Icon = dbTo.IconFactory.GetIcon((int) fromEntry.IconId);
			toEntry.SetTitle(GetString(fromEntry, PwDefs.TitleField), dbTo);
			Android.Util.Log.Debug("KP2A", "save kdb: entry " + fromEntry.Strings.ReadSafe(PwDefs.TitleField));
			toEntry.SetUsername(GetString(fromEntry, PwDefs.UserNameField), dbTo);
			toEntry.SetUrl(GetString(fromEntry, PwDefs.UrlField), dbTo);
			var pwd = GetString(fromEntry, PwDefs.PasswordField);
			if (pwd != null)
				toEntry.SetPassword(pwd, dbTo);
			toEntry.SetNotes(GetString(fromEntry, PwDefs.NotesField), dbTo);
			if (fromEntry.Binaries.Any())
			{
				var binaryData = fromEntry.Binaries.First().Value.ReadData();
				toEntry.SetBinaryData(binaryData, 0, binaryData.Length);
			}

			return toEntry;
		}

		private string GetString(PwEntry fromEntry, string id)
		{
			ProtectedString protectedString = fromEntry.Strings.Get(id);
			if (protectedString == null)
				return "";
			return protectedString.ReadString();
		}

		private Date ConvertTime(DateTime dateTime)
		{
			long timestamp = (long)(dateTime.ToUniversalTime().Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
			return new Date(timestamp);
		}
	}


	internal class AdditionalGroupData	
	{
		public int Id { get; set; }
		public int Flags { get; set; }
	}
}