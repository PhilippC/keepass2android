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

namespace Kp2aUnitTests
{
	[TestClass]
	class TestSaveDb: TestBase
	{
		private string newFilename;

		[TestMethod]
		public void TestLoadEditSave()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			IOConnection.DeleteFile(new IOConnectionInfo { Path = DefaultFilename });
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			//modify the database by adding a group:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			//save the database again:
			SaveDatabase(app);

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//ensure the change was saved:
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);
		}

		[TestMethod]
		public void TestLoadEditSaveWithSync()
		{
			TestSync(DefaultFilename);
		}

		[TestMethod]
		public void TestLoadEditSaveWithSyncKdbp()
		{
			TestSync(DefaultDirectory+"savetest.kdbp");
		}


		private void TestSync(string filename)
		{
//create the default database:
			IKp2aApp app = SetupAppWithDatabase(filename);
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(filename, DefaultPassword, DefaultKeyfile);
			//load it once again:
			IKp2aApp app2 = LoadDatabase(filename, DefaultPassword, DefaultKeyfile);

			//modify the database by adding a group in both databases:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			var group2 = new PwGroup(true, true, "TestGroup2", PwIcon.Energy);
			app2.GetDb().KpDatabase.RootGroup.AddGroup(group2, true);
			//save the database from app 1:
			SaveDatabase(app);

			((TestKp2aApp) app2).SetYesNoCancelResult(TestKp2aApp.YesNoCancelResult.Yes);

			//save the database from app 2: This save operation must detect the changes made from app 1 and ask if it should sync:
			SaveDatabase(app2);

			//make sure the right question was asked
			Assert.AreEqual(UiStringKey.TitleSyncQuestion, ((TestKp2aApp) app2).LastYesNoCancelQuestionTitle);

			//add group 2 to app 1:
			app.GetDb().KpDatabase.RootGroup.AddGroup(group2, true);

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(filename, DefaultPassword, DefaultKeyfile);

			//ensure the sync was successful:
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);
		}


		[TestMethod]
		public void TestLoadEditSaveWithSyncOverwrite()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			//load it once again:
			IKp2aApp app2 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//modify the database by adding a group in both databases:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			var group2 = new PwGroup(true, true, "TestGroup2", PwIcon.Energy);
			app2.GetDb().KpDatabase.RootGroup.AddGroup(group2, true);
			//save the database from app 1:
			SaveDatabase(app);

			//the user clicks the "no" button when asked if the sync should be performed -> overwrite expected!
			((TestKp2aApp)app2).SetYesNoCancelResult(TestKp2aApp.YesNoCancelResult.No);

			//save the database from app 2: This save operation must detect the changes made from app 1 and ask if it should sync:
			SaveDatabase(app2);

			//make sure the right question was asked
			Assert.AreEqual(UiStringKey.TitleSyncQuestion, ((TestKp2aApp)app2).LastYesNoCancelQuestionTitle);

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//ensure the sync was NOT performed (overwrite expected!):
			AssertDatabasesAreEqual(app2.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);

		}


		[TestMethod]
		public void TestLoadEditSaveWithSyncOverwriteBecauseOfNoCheck()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			//load it once again:
			IKp2aApp app2 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//modify the database by adding a group in both databases:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			var group2 = new PwGroup(true, true, "TestGroup2", PwIcon.Energy);
			app2.GetDb().KpDatabase.RootGroup.AddGroup(group2, true);
			//save the database from app 1:
			SaveDatabase(app);

			//the user doesn't want to perform check for file change:
			((TestKp2aApp) app2).SetPreference(PreferenceKey.CheckForFileChangesOnSave, false);

			//save the database from app 2: This save operation must detect the changes made from app 1 and ask if it should sync:
			SaveDatabase(app2);

			//make sure no question was asked
			Assert.AreEqual(null, ((TestKp2aApp)app2).LastYesNoCancelQuestionTitle);

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//ensure the sync was NOT performed (overwrite expected!):
			AssertDatabasesAreEqual(app2.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);

		}

		[TestMethod]
		public void TestLoadEditSaveWithSyncCancel()
		{
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			//load it once again:
			IKp2aApp app2 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//modify the database by adding a group in both databases:
			app.GetDb().KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "TestGroup", PwIcon.Apple), true);
			var group2 = new PwGroup(true, true, "TestGroup2", PwIcon.Energy);
			app2.GetDb().KpDatabase.RootGroup.AddGroup(group2, true);
			//save the database from app 1:
			SaveDatabase(app);

			//the user clicks the "cancel" button when asked if the sync should be performed
			((TestKp2aApp)app2).SetYesNoCancelResult(TestKp2aApp.YesNoCancelResult.Cancel);

			//save the database from app 2: This save operation must detect the changes made from app 1 and ask if it should sync:
			Assert.AreEqual(false, TrySaveDatabase(app2));

			//make sure the right question was asked
			Assert.AreEqual(UiStringKey.TitleSyncQuestion, ((TestKp2aApp)app2).LastYesNoCancelQuestionTitle);

			//load database to a new app instance:
			IKp2aApp resultApp = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//ensure the sync was NOT performed (cancel expected!):
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, resultApp.GetDb().KpDatabase);

		}


		[TestMethod]
		public void TestLoadEditSaveWithSyncConflict()
		{
		
			//create the default database:
			IKp2aApp app = SetupAppWithDefaultDatabase();
			//save it and reload it so we have a base version
			SaveDatabase(app);
			app = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);
			//load it once again:
			IKp2aApp app2 = LoadDatabase(DefaultFilename, DefaultPassword, DefaultKeyfile);

			//modify the database by renaming the same group in both databases:
			app.GetDb().KpDatabase.RootGroup.Groups.Single(g => g.Name == "Internet").Name += "abc";
			app2.GetDb().KpDatabase.RootGroup.Groups.Single(g => g.Name == "Internet").Name += "abcde";
			//app1 also changes the master password:
			var compositeKey = app.GetDb().KpDatabase.MasterKey;
			compositeKey.RemoveUserKey(compositeKey.GetUserKey(typeof (KcpPassword)));
			compositeKey.AddUserKey(new KcpPassword("abc"));
			
			//save the database from app 1:
			SaveDatabase(app);


			((TestKp2aApp)app2).SetYesNoCancelResult(TestKp2aApp.YesNoCancelResult.Yes);

			//save the database from app 2: This save operation must fail because the target file cannot be loaded:
			Assert.IsFalse(TrySaveDatabase(app2));

			//make sure the right question was asked
			Assert.AreEqual(UiStringKey.TitleSyncQuestion, ((TestKp2aApp)app2).LastYesNoCancelQuestionTitle);

		}



		[TestMethod]
		public void TestSaveAsWhenReadOnly()
		{
			Assert.Fail("TODO: Test ");
		}


		[TestMethod]
		public void TestReloadWhenCancelSync()
		{
			//when a change is detected and the user cancels saving, the app should display the "file was modified - reload?" question.
			Assert.Fail("TODO: Test ");
		}

		[TestMethod]
		public void TestSaveAsWhenSyncError()
		{
			Assert.Fail("TODO: Test ");
		}

		[TestMethod]
		public void TestLoadAndSave_TestIdenticalFiles()
		{
			IKp2aApp app = LoadDatabase(DefaultDirectory + "complexDb.kdbx", "test", null);
			var kdbxXml = DatabaseToXml(app);
			
			newFilename = TestDbDirectory + "tmp_complexDb.kdbx";
			if (File.Exists(newFilename))
				File.Delete(newFilename);
			app.GetDb().KpDatabase.IOConnectionInfo.Path = newFilename;
			app.GetDb().SaveData(Application.Context);


			IKp2aApp appReloaded = LoadDatabase(newFilename, "test", null);
			
			var kdbxReloadedXml = DatabaseToXml(appReloaded);

			Assert.AreEqual(kdbxXml,kdbxReloadedXml);
			


		}

		[TestMethod]
		public void TestLoadKdbxAndSaveKdbp_TestIdenticalFiles()
		{
			IKp2aApp app = LoadDatabase(DefaultDirectory + "complexDb.kdbx", "test", null);
			//string kdbxXml = DatabaseToXml(app);

			newFilename = TestDbDirectory + "tmp_complexDb.kdbp";
			if (File.Exists(newFilename))
				File.Delete(newFilename);
			app.GetDb().KpDatabase.IOConnectionInfo.Path = newFilename;
			app.GetDb().SaveData(Application.Context);

			
			IKp2aApp appReloaded = LoadDatabase(newFilename, "test", null);
			/*
			 * Unfortunately we cannot compare the xml because there are slight differences:
			 *  - the order of ProtectedStrings in the xml is different (which is ok)
			 *  - the CustomIconUuids are serialized as Zeros instead of not being serialized (which is ok as well)
			string kdbxReloadedXml = DatabaseToXml(appReloaded);

			bool areEqual = kdbxXml.Equals(kdbxReloadedXml);

			if (!areEqual)
			{
				using (StreamWriter w1 = File.CreateText(TestDbDirectory + "FromOriginalKdbx.xml"))
				{
					w1.Write(kdbxXml);
				}
				using (StreamWriter w2 = File.CreateText(TestDbDirectory + "FromKdbp.xml"))
				{
					w2.Write(kdbxReloadedXml);	
				}
				
			}
			Assert.IsTrue(areEqual, "reloaded->xml differs from loaded->xml");
			*/
			AssertDatabasesAreEqual(app.GetDb().KpDatabase, appReloaded.GetDb().KpDatabase);



		}

		private class OnCloseToStringMemoryStream : MemoryStream
		{
			public string Text { get; private set; }
			private bool _closed;
			public override void Close()
			{
				if (!_closed)
				{
					Position = 0;
					Text = new StreamReader(this).ReadToEnd();	
				}
				base.Close();
				_closed = true;

			}
		}

		private static string DatabaseToXml(IKp2aApp app)
		{
			KdbxFile kdb = new KdbxFile(app.GetDb().KpDatabase);
			var sOutput = new OnCloseToStringMemoryStream();
			kdb.Save(sOutput, app.GetDb().KpDatabase.RootGroup, KdbxFormat.PlainXml, null);
			return sOutput.Text;
		}
	}

	
}
