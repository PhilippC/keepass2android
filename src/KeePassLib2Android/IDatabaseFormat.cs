using System.IO;
using KeePassLib.Interfaces;
using KeePassLib.Keys;

namespace KeePassLib
{
	public interface IDatabaseFormat
	{
		void PopulateDatabaseFromStream(PwDatabase db, Stream s, IStatusLogger slLogger);

		byte[] HashOfLastStream { get; }

		bool CanWrite { get;  }
		string SuccessMessage { get; }
		void Save(PwDatabase kpDatabase, Stream stream);

		bool CanHaveEntriesInRootGroup { get; }
		bool CanHaveMultipleAttachments { get;  }
		bool CanHaveCustomFields { get;  }
		bool HasDefaultUsername { get; }
		bool HasDatabaseName { get; }
		bool SupportsAttachmentKeys { get;  }
		bool SupportsTags { get; }
		bool SupportsOverrideUrl { get; }
		bool CanRecycle { get; }
		bool SupportsTemplates { get; }
	}
}