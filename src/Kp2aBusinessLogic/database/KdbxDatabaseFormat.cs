using System.IO;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android
{
	public class KdbxDatabaseFormat : IDatabaseFormat
	{
		private readonly KdbxFormat _format;

		public KdbxDatabaseFormat(KdbxFormat format)
		{
			_format = format;
		}

		public void PopulateDatabaseFromStream(PwDatabase db, Stream s, IStatusLogger slLogger)
		{
			KdbxFile kdbx = new KdbxFile(db);
			kdbx.DetachBinaries = db.DetachBinaries;

			kdbx.Load(s, _format, slLogger);
			HashOfLastStream = kdbx.HashOfFileOnDisk;
			s.Close();

		}

		public byte[] HashOfLastStream { get; private set; }
		public bool CanWrite { get { return true; } }
		public string SuccessMessage { get { return null; } }
		public void Save(PwDatabase kpDatabase, Stream stream)
		{
			kpDatabase.Save(stream, null);
		}

		public bool CanHaveEntriesInRootGroup
		{
			get { return true; }
		}

		public bool CanHaveMultipleAttachments
		{
			get { return true; }
		}

		public bool CanHaveCustomFields
		{
			get { return true; }
		}

		public bool HasDefaultUsername
		{
			get { return true; }
		}

		public bool HasDatabaseName
		{
			get { return true; }
		}

		public bool SupportsAttachmentKeys
		{
			get { return true; }
		}

		public bool SupportsTags
		{
			get { return true; }
		}

		public bool SupportsOverrideUrl
		{
			get { return true; }
		}

		public bool CanRecycle
		{
			get { return true; }
		}

		public bool SupportsTemplates
		{
			get { return true; }
		}
	}
}