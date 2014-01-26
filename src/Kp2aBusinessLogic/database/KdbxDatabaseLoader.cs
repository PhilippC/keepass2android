using System.IO;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android
{
	public class KdbxDatabaseLoader : IDatabaseLoader
	{
		private readonly KdbxFormat _format;

		public KdbxDatabaseLoader(KdbxFormat format)
		{
			_format = format;
		}

		public void PopulateDatabaseFromStream(PwDatabase db, CompositeKey key, Stream s, IStatusLogger slLogger)
		{
			KdbxFile kdbx = new KdbxFile(db);
			kdbx.DetachBinaries = db.DetachBinaries;

			kdbx.Load(s, _format, slLogger);
			HashOfLastStream = kdbx.HashOfFileOnDisk;
			s.Close();

		}

		public byte[] HashOfLastStream { get; private set; }
		public bool CanWrite { get { return true; } }
	}
}