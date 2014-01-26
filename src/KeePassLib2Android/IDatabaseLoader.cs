using System.IO;
using KeePassLib.Interfaces;
using KeePassLib.Keys;

namespace KeePassLib
{
	public interface IDatabaseLoader
	{
		void PopulateDatabaseFromStream(PwDatabase db, CompositeKey key, Stream s, IStatusLogger slLogger);

		byte[] HashOfLastStream { get; }

		bool CanWrite { get;  }
	}
}