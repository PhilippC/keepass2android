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
	class KdbDatabaseFormat: IDatabaseFormat
	{
		private Dictionary<PwUuid, AdditionalGroupData> _groupData = new Dictionary<PwUuid, AdditionalGroupData>();
		private static readonly DateTime _expireNever = new DateTime(2999,12,28,23,59,59);

		public void PopulateDatabaseFromStream(PwDatabase db, CompositeKey key, Stream s, IStatusLogger slLogger)
		{
			#if !EXCLUDE_KEYTRANSFORM
			var importer = new Com.Keepassdroid.Database.Load.ImporterV3();

			var hashingStream = new HashingStreamEx(s, false, new SHA256Managed());

			string password = "";//no need to distinguish between null and "" because empty passwords are invalid (and null is not allowed)
			KcpPassword passwordKey = (KcpPassword)key.GetUserKey(typeof(KcpPassword));
			if (passwordKey != null)
			{
				password = passwordKey.Password.ReadString();
			}

			KcpKeyFile passwordKeyfile = (KcpKeyFile)key.GetUserKey(typeof(KcpKeyFile));
			MemoryStream keyfileStream = null;
			if (passwordKeyfile != null)
			{
				keyfileStream = new MemoryStream(passwordKeyfile.RawFileData.ReadData());
			}


			try
			{
				var dbv3 = importer.OpenDatabase(hashingStream, password, keyfileStream);

				db.Name = dbv3.Name;
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
			pwGroup.Name = groupV3.Name;
			
			pwGroup.CreationTime = ConvertTime(groupV3.TCreation);
			pwGroup.LastAccessTime = ConvertTime(groupV3.TLastAccess);
			pwGroup.LastModificationTime = ConvertTime(groupV3.TLastMod);
			pwGroup.Expires = !PwGroupV3.NeverExpire.Equals(groupV3.TExpire);
			if (pwGroup.Expires)
				pwGroup.ExpiryTime = ConvertTime(groupV3.TExpire);

			if (groupV3.Icon != null)
				pwGroup.IconId = (PwIcon) groupV3.Icon.IconId;
			_groupData.Add(pwGroup.Uuid, new AdditionalGroupData
				{
					Flags = groupV3.Flags,
					Id = groupV3.Id.Id
				});


			for (int i = 0; i < groupV3.ChildGroups.Count;i++)
			{
				pwGroup.AddGroup(ConvertGroup(groupV3.GetGroupAt(i)), true);
			}
			for (int i = 0; i < groupV3.ChildEntries.Count; i++)
			{
				var entry = groupV3.GetEntryAt(i);
				if (entry.IsMetaStream)
					continue;
				pwGroup.AddEntry(ConvertEntry(entry), true);
			}
			
			return pwGroup;
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
		public string SuccessMessage { get
		{
			return "";
		} }

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
			db.RootGroup = new PwGroupV3() { Level = -1};
			AssignParent(kpDatabase.RootGroup, db, groupV3s);
			
			

			foreach (PwEntry e in kpDatabase.RootGroup.GetEntries(true))
			{
				PwEntryV3 entryV3 = ConvertEntry(e, db);
				entryV3.Parent = groupV3s[_groupData[e.ParentGroup.Uuid].Id];
				entryV3.Parent.ChildEntries.Add(entryV3);
				entryV3.GroupId = entryV3.Parent.Id.Id;
				db.Entries.Add(entryV3);
			}


			PwDbV3Output output = new PwDbV3Output(db, stream);
			output.Output();
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

			foreach (PwGroup g in kpParent.Groups)
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

			toGroup.TCreation = new PwDate(ConvertTime(fromGroup.CreationTime));
			toGroup.TLastAccess= new PwDate(ConvertTime(fromGroup.LastAccessTime));
			toGroup.TLastMod = new PwDate(ConvertTime(fromGroup.LastModificationTime));
			if (fromGroup.Expires)
			{
				toGroup.TExpire = new PwDate(ConvertTime(fromGroup.ExpiryTime));
			}
			else
			{
				toGroup.TExpire = new PwDate(PwGroupV3.NeverExpire);
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
			toEntry.SetUsername(GetString(fromEntry, PwDefs.UserNameField), dbTo);
			toEntry.SetUrl(GetString(fromEntry, PwDefs.UrlField), dbTo);
			toEntry.SetPassword(GetString(fromEntry, PwDefs.PasswordField), dbTo);
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
				return null;
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