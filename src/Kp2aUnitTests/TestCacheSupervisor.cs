using System;
using System.Collections.Generic;
using System.Linq;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android.Io;

namespace Kp2aUnitTests
{
	class TestCacheSupervisor: ICacheSupervisor
	{
		public const string CouldntOpenFromRemoteId = "CouldntOpenFromRemote";
		public const string CouldntSaveToRemoteId = "CouldntSaveToRemote";
		public const string NotifyOpenFromLocalDueToConflictId = "CouldntSaveToRemote";
		public const string UpdatedCachedFileOnLoadId = "UpdatedCachedFileOnLoad";
		public const string LoadedFromRemoteInSyncId = "LoadedFromRemoteInSync";
		public const string UpdatedRemoteFileOnLoadId = "UpdatedRemoteFileOnLoad";

		private HashSet<string> _callsMade = new HashSet<string>();
		
		public void Reset()
		{
			_callsMade.Clear();
		}

		public void AssertNoCall()
		{
			string allCalls = _callsMade.Aggregate("", (current, s) => current + s + ",");
			Assert.AreEqual("", allCalls);
		}

		public void AssertSingleCall(string id)
		{
			if ((_callsMade.Count == 1)
			    && _callsMade.Single() == id)
			{
				Reset();
				return;
			}
				

			Assert.Fail("expected only "+id+", but received: "+_callsMade.Aggregate("", (current, s) => current + s + ","));

		}

		public void CouldntSaveToRemote(IOConnectionInfo ioc, Exception e)
		{
			_callsMade.Add(CouldntSaveToRemoteId);
		}

		public void CouldntOpenFromRemote(IOConnectionInfo ioc, Exception ex)
		{
			_callsMade.Add(CouldntOpenFromRemoteId);
		}

		public void UpdatedCachedFileOnLoad(IOConnectionInfo ioc)
		{
			_callsMade.Add(UpdatedCachedFileOnLoadId);
		}

		public void UpdatedRemoteFileOnLoad(IOConnectionInfo ioc)
		{
			_callsMade.Add(UpdatedRemoteFileOnLoadId);
		}

		public void NotifyOpenFromLocalDueToConflict(IOConnectionInfo ioc)
		{
			_callsMade.Add(NotifyOpenFromLocalDueToConflictId);
		}

		public void LoadedFromRemoteInSync(IOConnectionInfo ioc)
		{
			_callsMade.Add(LoadedFromRemoteInSyncId);
		}

		
	}
}