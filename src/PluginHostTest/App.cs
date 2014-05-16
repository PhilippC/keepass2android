using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace keepass2android
{
	public class App
	{

		public class Kp2A
		{
			private static Db _mDb;

			public  class Db
			{
				public PwEntryOutput LastOpenedEntry { get; set; }

				public void SetEntry(PwEntry e)
				{
					KpDatabase = new PwDatabase();
					KpDatabase.New(new IOConnectionInfo(), new CompositeKey());

					KpDatabase.RootGroup.AddEntry(e, true);
				}

				public PwDatabase KpDatabase
				{
					get; set;
				}
			}

			public static Db GetDb()
			{
				if (_mDb == null)
					_mDb = new Db();
				return _mDb;
			}
		}
	}
}