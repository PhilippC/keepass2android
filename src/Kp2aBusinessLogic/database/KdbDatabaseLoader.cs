using System;
using System.IO;
using System.Security.Cryptography;
using Android.Content;
using Com.Keepassdroid.Database;
using Com.Keepassdroid.Database.Exception;
using Java.Lang;
using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using Exception = System.Exception;

namespace keepass2android
{
	class KdbDatabaseLoader: IDatabaseLoader
	{
		private Context _ctx;

		public KdbDatabaseLoader(Context ctx)
		{
			_ctx = ctx;
		}

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

		public byte[] HashOfLastStream { get; private set; }
	}
}