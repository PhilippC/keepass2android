using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Com.Keepassdroid.Database;
using Com.Keepassdroid.Database.Exception;
using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Security;
using Exception = System.Exception;
using PwIcon = KeePassLib.PwIcon;

namespace keepass2android
{
	class KdbDatabaseLoader: IDatabaseLoader
	{
		private Dictionary<PwUuid, AdditionalGroupData> _groupData = new Dictionary<PwUuid, AdditionalGroupData>();

		public void PopulateDatabaseFromStream(PwDatabase db, CompositeKey key, Stream s, IStatusLogger slLogger)
		{
			var importer = new Com.Keepassdroid.Database.Load.ImporterV3();

			var hashingStream = new HashingStreamEx(s, false, new SHA256Managed());

			string password = "";//no need to distinguish between null and "" because empty passwords are invalid (and null is not allowed)
			KcpPassword passwordKey = (KcpPassword)key.GetUserKey(typeof(KcpPassword));
			if (passwordKey != null)
			{
				password = passwordKey.Password.ReadString();
			}

			KcpKeyFile passwordKeyfile = (KcpKeyFile)key.GetUserKey(typeof(KcpKeyFile));
			string keyfile = ""; 
			if (passwordKeyfile != null)
			{
				keyfile = passwordKeyfile.Path;
			}


			try
			{
				var dbv3 = importer.OpenDatabase(hashingStream, password, keyfile);

				db.Name = dbv3.Name;
				db.RootGroup = ConvertGroup(dbv3.RootGroup);
				
			}
			catch (InvalidPasswordException e) {
			
				return;
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
		}

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

		private PwEntry ConvertEntry(PwEntryV3 entryV3)
		{
			PwEntry pwEntry = new PwEntry(false, false);
			pwEntry.Uuid = new PwUuid(entryV3.Uuid.ToArray());
			pwEntry.CreationTime = ConvertTime(entryV3.TCreation);
			pwEntry.LastAccessTime = ConvertTime(entryV3.TLastAccess);
			pwEntry.LastModificationTime = ConvertTime(entryV3.TLastMod);

			pwEntry.Expires = entryV3.Expires();
			if (pwEntry.Expires)
				pwEntry.ExpiryTime = ConvertTime(entryV3.TExpire);

			if (entryV3.Icon != null)
				pwEntry.IconId = (PwIcon) entryV3.Icon.IconId;
			SetFieldIfAvailable(pwEntry, PwDefs.TitleField, false, entryV3.Title);
			SetFieldIfAvailable(pwEntry, PwDefs.UserNameField, false, entryV3.Username);
			SetFieldIfAvailable(pwEntry, PwDefs.UrlField, false, entryV3.Url);
			SetFieldIfAvailable(pwEntry, PwDefs.PasswordField, true, entryV3.Password);
			SetFieldIfAvailable(pwEntry, PwDefs.NotesField, true, entryV3.Additional);

			if (entryV3.GetBinaryData() != null)
			{
				pwEntry.Binaries.Set(entryV3.BinaryDesc, new ProtectedBinary(true, entryV3.GetBinaryData()));
			}
			return pwEntry;
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
			return new DateTime(1970, 1, 1).AddMilliseconds(javatime);

		}

		public byte[] HashOfLastStream { get; private set; }
		public bool CanWrite { get { return false; } }
	}

	internal class AdditionalGroupData	
	{
		public int Id { get; set; }
		public int Flags { get; set; }
	}
}