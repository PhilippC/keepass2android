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
						   Assert.IsNotNull(foundItem);
						   Assert.IsTrue(item.ParentGroup.Uuid.EqualsValue(foundItem.ParentGroup.Uuid));
					   }
				);

			Assert.AreEqual(db1.RootGroup.GetObjects(true,null).Count(),db2.RootGroup.GetObjects(true,null).Count());
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

		protected IKp2aApp LoadDatabase(string defaultFilename, string password, string keyfile)
		{
			IKp2aApp app = new TestKp2aApp();
			Handler handler = new Handler(Looper.MainLooper);
			bool loadSuccesful = false;
			LoadDb task = new LoadDb(app, new IOConnectionInfo() { Path = defaultFilename }, password, keyfile, new ActionOnFinish((success, message) =>
				{
					loadSuccesful = success; if (!success)
						Assert.Fail(message);
				})
				);
			ProgressTask pt = new ProgressTask(app, Application.Context, task, UiStringKey.loading_database);
			pt.Run();
			Assert.IsTrue(loadSuccesful);
			return app;
		}

		protected void SaveDatabase(IKp2aApp app)
		{
			bool saveSuccesful = false;
			SaveDb save = new SaveDb(Application.Context, app.GetDb(), new ActionOnFinish((success, message) =>
				{
					saveSuccesful = success; if (!success)
						Assert.Fail(message);
				}), false);
			save.Run();

			Assert.IsTrue(saveSuccesful);
		}

		protected IKp2aApp SetupAppWithDefaultDatabase()
		{
			IKp2aApp app = new TestKp2aApp();
			IOConnectionInfo ioc = new IOConnectionInfo {Path = DefaultFilename};
			Database db = app.CreateNewDatabase();

			db.KpDatabase = new PwDatabase();
			//Key will be changed/created immediately after creation:
			CompositeKey tempKey = new CompositeKey();
			db.KpDatabase.New(ioc, tempKey);


			db.KpDatabase.KeyEncryptionRounds = 3;
			db.KpDatabase.Name = "Keepass2Android Testing Password Database";


			// Set Database state
			db.Root = db.KpDatabase.RootGroup;
			db.Ioc = ioc;
			db.Loaded = true;
			db.SearchHelper = new SearchDbHelper(app);

			// Add a couple default groups
			db.KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "Internet", PwIcon.Key), true);
			
			mailGroup = new PwGroup(true, true, "eMail", PwIcon.UserCommunication);
			db.KpDatabase.RootGroup.AddGroup(mailGroup, true);

			mailGroup.AddGroup(new PwGroup(true, true, "business", PwIcon.BlackBerry), true );

			mailEntry = new PwEntry(true, true);
			mailEntry.Strings.Set(PwDefs.UserNameField, new ProtectedString(
						true, "me@there.com"));
			mailEntry.Strings.Set(PwDefs.TitleField, new ProtectedString(
						true, "me@there.com Entry"));
			mailGroup.AddEntry(mailEntry , true);

			db.KpDatabase.RootGroup.AddGroup(new PwGroup(true, true, "eMail2", PwIcon.UserCommunication), true);

			return app;
		}
	}
}