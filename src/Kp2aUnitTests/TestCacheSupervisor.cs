using System;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace Kp2aUnitTests
{
	class TestCacheSupervisor: ICacheSupervisor
	{
		public bool CouldntOpenFromRemoteCalled { get; set; }
		public bool CouldntSaveToRemoteCalled { get; set; }
		public bool NotifyOpenFromLocalDueToConflictCalled { get; set; }


		public void CouldntSaveToRemote(IOConnectionInfo ioc, Exception e)
		{
			CouldntSaveToRemoteCalled = true;
		}

		public void CouldntOpenFromRemote(IOConnectionInfo ioc, Exception ex)
		{
			CouldntOpenFromRemoteCalled = true;
		}

		public void NotifyOpenFromLocalDueToConflict(IOConnectionInfo ioc)
		{
			NotifyOpenFromLocalDueToConflictCalled = true;
		}
	}
}