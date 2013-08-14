using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;
using keepass2android.Io;

namespace Kp2aUnitTests
{
	[TestClass]
	class TestSaveDbCached: TestBase
	{
		private TestCacheSupervisor _testCacheSupervisor = new TestCacheSupervisor();
		private TestFileStorage _testFileStorage = new TestFileStorage();

		protected override TestKp2aApp CreateTestKp2aApp()
		{
			TestKp2aApp app = base.CreateTestKp2aApp();
			app.FileStorage = new CachingFileStorage(_testFileStorage, "/mnt/sdcard/kp2atest/cache/", _testCacheSupervisor);
			return app;
		}

		[TestMethod]
		public void TestLoadEditSave()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			IOConnection.DeleteFile(new IOConnectionInfo { Path = DefaultFilename });
			//save it and reload it so we have a base version
			SaveDatabase(app);
			_testCacheSupervisor.AssertNoCall();
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.LoadedFromRemoteInSyncId);
			//modify the database by adding a group:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			//save the database again:
			SaveDatabase(app);
			Assert.IsNull(((TestKp2aApp)app).LastYesNoCancelQuestionTitle);
			_testCacheSupervisor.AssertNoCall();

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//ensure the change was saved:
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);
		}

		[TestMethod]
		public void TestLoadEditSaveWhenDeleted()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			IOConnection.DeleteFile(new IOConnectionInfo { Path = DefaultFilename });
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//delete the file:
			File.Delete(DefaultFilename);

			//modify the database by adding a group:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			//save the database again:
			_testCacheSupervisor.Reset();
			SaveDatabase(app);
			Assert.IsNull(((TestKp2aApp) app).LastYesNoCancelQuestionTitle);
			_testCacheSupervisor.AssertNoCall();

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//ensure the change was saved:
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);
		}


		[TestMethod]
		public void TestLoadEditSaveWhenModified()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			IOConnection.DeleteFile(new IOConnectionInfo { Path = DefaultFilename });
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			foreach (var group in app.GetDb().KpDatabase.RootGroup.Groups)
				Kp2aLog.Log("app c: " + group.Name);

			//load once more:
			var app2 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//modifiy once:
			PwGroup group2 = new PwGroup(true, true, "TestGroup2", PwIcon.Apple);
			app2.GetDb().KpDatabase.RootGroup.AddGroup(group2, true);

			foreach (var group in app.GetDb().KpDatabase.RootGroup.Groups)
				Kp2aLog.Log("app b: " + group.Name);

			SaveDatabase(app2);

			_testCacheSupervisor.Reset();

			foreach (var group in app.GetDb().KpDatabase.RootGroup.Groups)
				Kp2aLog.Log("app d: " + group.Name);
			Assert.IsNull(((TestKp2aApp)app).LastYesNoCancelQuestionTitle);
			_testCacheSupervisor.AssertNoCall();

			//modify the database by adding a group:
			PwGroup group1 = new PwGroup(true, true, "TestGroup", PwIcon.Apple);
			app.GetDb().KpDatabase.RootGroup.AddGroup(group1, true);

			foreach (var group in app.GetDb().KpDatabase.RootGroup.Groups)
				Kp2aLog.Log("app a: " + group.Name);


			//save the database again:
			_testCacheSupervisor.Reset();
			SaveDatabase(app);
			Assert.AreEqual(((TestKp2aApp)app).LastYesNoCancelQuestionTitle, UiStringKey.TitleSyncQuestion);
			_testCacheSupervisor.AssertNoCall();
			

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			app2.GetDb().KpDatabase.RootGroup.AddGroup(group1, true);
			foreach (var group in app.GetDb().KpDatabase.RootGroup.Groups)
				Kp2aLog.Log("app: "+group.Name);

			foreach (var group in resultApp.GetDb().KpDatabase.RootGroup.Groups)
				Kp2aLog.Log("resultApp: " + group.Name);

			//ensure the change was saved:
			AssertDatabasesAreEqual(app2.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);

		}
	}
}