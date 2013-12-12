using System;
using System.Linq;
using Android.App;
using Android.OS;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;

namespace Kp2aUnitTests
{
	internal class TestBase
	{
		private PwGroup mailGroup;
		private PwEntry mailEntry;

		/// <summary>
		/// Compares the two databases. uses Asserts 
		/// TODO: implement this with many more checks or use serialization?
		/// </summary>
		protected void AssertDatabasesAreEqual(PwDatabase db1, PwDatabase db2)
		{
			db1.RootGroup.GetObjects(true, null)
			   .ForEach(
				   item =>
					   {
						   IStructureItem foundItem = db2.RootGroup.FindObject(item.Uuid, true, null);
						   Assert.IsNotNull(foundItem, "didn't find item with uuid="+item.Uuid.ToHexString());
						   Assert.IsTrue(item.ParentGroup.Uuid.EqualsValue(foundItem.ParentGroup.Uuid), "item.ParentGroup.Uuid ("+item.ParentGroup.Uuid+") != " + foundItem.ParentGroup.Uuid);
					   }
				);

			Assert.AreEqual(db1.RootGroup.GetObjects(true,null).Count(),db2.RootGroup.GetObjects(true,null).Count(), "Wrong Object Count");
		}

		protected static string DefaultDirectory
		{
			get { return "/mnt/sdcard/kp2atest/"; }
		}

		protected static string DefaultFilename
		{
			get { return "/mnt/sdcard/kp2atest/savetest.kdbx"; }
		}

		protected string DefaultKeyfile
		{
			get { return DefaultDirectory + "keyfile.txt"; }
		}

		protected string DefaultPassword
		{
			get { return "secretpassword!"; }
		}

		protected string TestDbDirectory
		{
			get { return DefaultDirectory + "savedWithDesktop/"; }
		}

		protected TestKp2aApp LoadDatabase(string filename, string password, string keyfile)
		{
			var app = CreateTestKp2aApp();
			app.CreateNewDatabase();
			bool loadSuccesful = false;
			LoadDb task = new LoadDb(app, new IOConnectionInfo() { Path = filename }, null, CreateKey(password, keyfile), keyfile, new ActionOnFinish((success, message) =>
				{
					if (!success)
						Kp2aLog.Log(message);
					loadSuccesful = success; 
						
				})
				);
			ProgressTask pt = new ProgressTask(app, Application.Context, task);
			pt.Run();
			pt.JoinWorkerThread();
			Assert.IsTrue(loadSuccesful);
			return app;
		}
		protected static CompositeKey CreateKey(string password, string keyfile)
		{
			CompositeKey key = new CompositeKey();
			key.AddUserKey(new KcpPassword(password));
			if (!String.IsNullOrEmpty(keyfile))
				key.AddUserKey(new KcpKeyFile(keyfile));
			return key;
		}
		protected static CompositeKey CreateKey(string password)
		{
			CompositeKey key = new CompositeKey();
			key.AddUserKey(new KcpPassword(password));
			return key;
		}
		protected virtual TestKp2aApp CreateTestKp2aApp()
		{
			TestKp2aApp app = new TestKp2aApp();
			return app;
		}

		protected void SaveDatabase(IKp2aApp app)
		{
			bool saveSuccesful = TrySaveDatabase(app);

			Assert.IsTrue(saveSuccesful);
		}

		public static bool TrySaveDatabase(IKp2aApp app)
		{
			bool saveSuccesful = false;
			SaveDb save = new SaveDb(Application.Context, app, new ActionOnFinish((success, message) =>
				{
					saveSuccesful = success;
					if (!success)
					{
						Kp2aLog.Log("Error during TestBase.SaveDatabase: " + message);
					}
				}), false);
			save.Run();
			save.JoinWorkerThread();
			return saveSuccesful;
		}

		protected TestKp2aApp SetupAppWithDefaultDatabase()
		{
			string filename = DefaultFilename;

			return SetupAppWithDatabase(filename);
		}

		protected TestKp2aApp SetupAppWithDatabase(string filename)
		{
			TestKp2aApp app = CreateTestKp2aApp();

			IOConnectionInfo ioc = new IOConnectionInfo {Path = filename};
			Database db = app.CreateNewDatabase();

			db.KpDatabase = new PwDatabase();

			CompositeKey compositeKey = new CompositeKey();
			compositeKey.AddUserKey(new KcpPassword(DefaultPassword));
			if (!String.IsNullOrEmpty(DefaultKeyfile))
				compositeKey.AddUserKey(new KcpKeyFile(DefaultKeyfile));
			db.KpDatabase.New(ioc, compositeKey);


			db.KpDatabase.KeyEncryptionRounds = 3;
			db.KpDatabase.Name = "Keepass2Android Testing Password Database";


			// Set Database state
			db.Root = db.KpDatabase.RootGroup;
			db.Loaded = true;
			db.SearchHelper = new SearchDbHelper(app);

			// Add a couple default groups
			db.KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "Internet", PwIcon.Key), true);

			mailGroup = new PwGroup(true, true, "eMail", PwIcon.UserCommunication);
			db.KpDatabase.RootGroup.AddGroup(mailGroup, true);

			mailGroup.AddGroup(new PwGroup(true, true, "business", PwIcon.BlackBerry), true);

			mailEntry = new PwEntry(true, true);
			mailEntry.Strings.Set(PwDefs.UserNameField, new ProtectedString(
				                                            true, "me@there.com"));
			mailEntry.Strings.Set(PwDefs.TitleField, new ProtectedString(
				                                         true, "me@there.com Entry"));
			mailGroup.AddEntry(mailEntry, true);

			db.KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "eMail2", PwIcon.UserCommunication), true);

			return app;
		}
	}
}