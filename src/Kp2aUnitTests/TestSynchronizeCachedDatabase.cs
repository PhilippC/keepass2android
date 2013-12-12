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
		[TestInitialize]
		public void InitTests()
		{
			TestFileStorage.Offline = false;
		}

		private TestCacheSupervisor _testCacheSupervisor = new TestCacheSupervisor();

		protected override TestKp2aApp CreateTestKp2aApp()
		{
			TestKp2aApp app = base.CreateTestKp2aApp();
			app.TestFileStorage = new TestFileStorage(app);

			app.FileStorage = new CachingFileStorage(app.TestFileStorage, "/mnt/sdcard/kp2atest/cache/", _testCacheSupervisor);
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
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.LoadedFromRemoteInSyncId);
		
			string resultMessage;
			bool wasSuccessful;

			//sync without changes on any side:
			Synchronize(app, out wasSuccessful, out resultMessage);
			Assert.IsTrue(wasSuccessful);
			Assert.AreEqual(resultMessage, app.GetResourceString(UiStringKey.FilesInSync));

			//go offline:
			TestFileStorage.Offline = true;

			//sync when offline (->error)
			Synchronize(app, out wasSuccessful, out resultMessage);
			Assert.IsFalse(wasSuccessful);
			Assert.AreEqual(resultMessage, "offline");

			//modify the database by adding a group:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			//save the database again (will be saved locally only)
			SaveDatabase(app);

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.CouldntSaveToRemoteId);

			//go online again:
			TestFileStorage.Offline = false;

			//sync with local changes only (-> upload):
			Synchronize(app, out wasSuccessful, out resultMessage);
			Assert.IsTrue(wasSuccessful);
			Assert.AreEqual(resultMessage, app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));

			//ensure both files are identical and up to date now:
			TestFileStorage.Offline = true;
			var appOfflineLoaded = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.CouldntOpenFromRemoteId);
			TestFileStorage.Offline = false;
			var appRemoteLoaded = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.LoadedFromRemoteInSyncId);

			AssertDatabasesAreEqual(app.GetDb().KpDatabase, appOfflineLoaded.GetDb().KpDatabase);
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, appRemoteLoaded.GetDb().KpDatabase);
		}

		

		[TestMethod]
		public void TestSyncWhenRemoteDeleted()
		{
			//create the default database:
			TestKp2aApp app = SetupAppWithDefaultDatabase();

			IOConnection.DeleteFile(new IOConnectionInfo {Path = DefaultFilename});
			//save it and reload it so we have a base version ("remote" and in the cache)
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.LoadedFromRemoteInSyncId);

			//delete remote:
			IOConnection.DeleteFile(new IOConnectionInfo { Path = DefaultFilename });

			string resultMessage;
			bool wasSuccessful;

			//sync:
			Synchronize(app, out wasSuccessful, out resultMessage);
			Assert.IsTrue(wasSuccessful);
			Assert.AreEqual(resultMessage, app.GetResourceString(UiStringKey.SynchronizedDatabaseSuccessfully));

			//ensure the file is back here:
			var app2 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, app2.GetDb().KpDatabase);
		}

		[TestMethod]
		public void TestSyncWhenConflict()
		{
			//create the default database:
			TestKp2aApp app = SetupAppWithDefaultDatabase();

			IOConnection.DeleteFile(new IOConnectionInfo {Path = DefaultFilename});
			//save it and reload it so we have a base version ("remote" and in the cache)
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.LoadedFromRemoteInSyncId);

			var app2 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			app2.FileStorage = app.TestFileStorage; //give app2 direct access to the remote file
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.LoadedFromRemoteInSyncId);

			//go offline:
			TestFileStorage.Offline = true;


			string resultMessage;
			bool wasSuccessful;

			//modify the database by adding a group in both apps:
			PwGroup newGroup1 = new PwGroup(true, true, "TestGroup", PwIcon.Apple);
			app.GetDb().KpDatabase.RootGroup.AddGroup(newGroup1, true);
			PwGroup newGroup2 = new PwGroup(true, true, "TestGroupApp2", PwIcon.Apple);
			app2.GetDb().KpDatabase.RootGroup.AddGroup(newGroup2, true);
			//save the database again (will be saved locally only for "app")
			SaveDatabase(app);
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.CouldntSaveToRemoteId);

			//go online again:
			TestFileStorage.Offline = false;
			
			//...and remote only for "app2":
			SaveDatabase(app2);

			//try to sync:
			Synchronize(app, out wasSuccessful, out resultMessage);

			Assert.IsTrue(wasSuccessful);
			Assert.AreEqual(UiStringKey.SynchronizedDatabaseSuccessfully.ToString(), resultMessage);

			//build app2 with the newGroup1:
			app2.GetDb().KpDatabase.RootGroup.AddGroup(newGroup1, true);

			var app3 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			AssertDatabasesAreEqual(app.GetDb().KpDatabase, app2.GetDb().KpDatabase);
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, app3.GetDb().KpDatabase);



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
			sync.JoinWorkerThread();
			wasSuccessful = success;
			resultMessage = result;
		}
	}
}
