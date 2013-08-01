using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.OS;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;
using keepass2android.Io;

namespace Kp2aUnitTests
{
	[TestClass]
	internal class TestSynchronizeCachedDatabase : TestBase
	{
		private TestCacheSupervisor _testCacheSupervisor = new TestCacheSupervisor();
		private TestFileStorage _testFileStorage = new TestFileStorage();

		[TestMethod]
		public void TestTodos()
		{
			Assert.IsFalse(true, "Wird immer ManagedTransform benutzt??");
			Assert.IsFalse(true, "TODOs in SyncDb");
			Assert.IsFalse(true, "FileNotFound");
			Assert.IsFalse(true, "Test merge files");
		}

		protected override TestKp2aApp CreateTestKp2aApp()
		{
			TestKp2aApp app = base.CreateTestKp2aApp();
			app.FileStorage = new CachingFileStorage(_testFileStorage, "/mnt/sdcard/kp2atest/cache/", _testCacheSupervisor);
			return app;
		}

		/// <summary>
		/// Tests that synchronizing works if 
		///  - no changes in remote and local db
		///  - remote is offline -> error
		///  - only local file was changed
		/// </summary>
		[TestMethod]
		public void TestSimpleSyncCases()
		{
			
			//create the default database:
			TestKp2aApp app = SetupAppWithDefaultDatabase();
			
			
			IOConnection.DeleteFile(new IOConnectionInfo {Path = DefaultFilename});
			//save it and reload it so we have a base version ("remote" and in the cache)
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
		
			string resultMessage;
			bool wasSuccessful;

			//sync without changes on any side:
			Synchronize(app, out wasSuccessful, out resultMessage);
			Assert.IsTrue(wasSuccessful);
			Assert.AreEqual(resultMessage, app.GetResourceString(UiStringKey.FilesInSync));

			//go offline:
			_testFileStorage.Offline = true;

			//sync when offline (->error)
			Synchronize(app, out wasSuccessful, out resultMessage);
			Assert.IsFalse(wasSuccessful);
			Assert.AreEqual(resultMessage, "offline");

			//modify the database by adding a group:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			//save the database again (will be saved locally only)
			SaveDatabase(app);
			Assert.IsTrue(_testCacheSupervisor.CouldntSaveToRemoteCalled);
			_testCacheSupervisor.CouldntSaveToRemoteCalled = false;

			//go online again:
			_testFileStorage.Offline = false;

			//sync with local changes only (-> upload):
			Synchronize(app, out wasSuccessful, out resultMessage);
			Assert.IsTrue(wasSuccessful);
			Assert.AreEqual(resultMessage, app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));

			//ensure both files are identical and up to date now:
			_testFileStorage.Offline = true;
			var appOfflineLoaded = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			_testCacheSupervisor.CouldntOpenFromRemoteCalled = false;
			_testFileStorage.Offline = false;
			var appRemoteLoaded = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			Assert.IsFalse(_testCacheSupervisor.CouldntOpenFromRemoteCalled);

			AssertDatabasesAreEqual(app.GetDb().KpDatabase, appOfflineLoaded.GetDb().KpDatabase);
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, appRemoteLoaded.GetDb().KpDatabase);
		}

		private void Synchronize(TestKp2aApp app, out bool wasSuccessful, out string resultMessage)
		{
			bool success = false;
			string result = null;
			var sync = new SynchronizeCachedDatabase(Application.Context, app, new ActionOnFinish((_success, _result) =>
				{ 
					success = _success;
					result = _result;
				}));
			sync.Run();
			wasSuccessful = success;
			resultMessage = result;
		}
	}
}
