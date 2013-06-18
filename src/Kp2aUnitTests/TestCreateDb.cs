using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Java.IO;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;

namespace Kp2aUnitTests
{
	[TestClass]
	class TestCreateDb
	{
		[TestMethod]
		public void CreateAndSaveLocal()
		{
			IKp2aApp app = new TestKp2aApp();
			IOConnectionInfo ioc = new IOConnectionInfo {Path = "/mnt/sdcard/kp2atest/savetest.kdbx"};

			File outputDir = new File("/mnt/sdcard/kp2atest/");
			outputDir.Mkdirs();
			File targetFile = new File(ioc.Path);
			if (targetFile.Exists())
				targetFile.Delete();

			bool createSuccesful = false;
			//create the task:
			CreateDb createDb = new CreateDb(app, Application.Context, ioc, new ActionOnFinish((success, message) =>
				{ createSuccesful = success; if (!success) 
					Assert.Fail(message);
				}), false);
			//run it:
			createDb.Run();
			//check expectations:
			Assert.IsTrue(createSuccesful);
			Assert.IsNotNull(app.GetDb());
			Assert.IsNotNull(app.GetDb().KpDatabase);
			//the create task should create two groups:
			Assert.AreEqual(2, app.GetDb().KpDatabase.RootGroup.Groups.Count());

			//ensure the the database can be loaded from file:
			PwDatabase loadedDb = new PwDatabase();
			loadedDb.Open(ioc, new CompositeKey(), null);

			
		}
	}

	internal class TestKp2aApp : IKp2aApp
	{
		private Database _db;

		public void SetShutdown()
		{
			
		}

		public Database GetDb()
		{
			return _db;
		}

		public void StoreOpenedFileAsRecent(IOConnectionInfo ioc, string keyfile)
		{
			
		}

		public Database CreateNewDatabase()
		{
			TestDrawableFactory testDrawableFactory = new TestDrawableFactory();
			_db = new Database(testDrawableFactory, new TestKp2aApp());
			return _db;

		}

		public string GetResourceString(UiStringKey stringKey)
		{
			return stringKey.ToString();
		}

		public bool GetBooleanPreference(PreferenceKey key)
		{
			return true;
		}

		public void AskYesNoCancel(UiStringKey titleKey, UiStringKey messageKey, EventHandler<DialogClickEventArgs> yesHandler, EventHandler<DialogClickEventArgs> noHandler,
		                           EventHandler<DialogClickEventArgs> cancelHandler, Context ctx)
		{
			yesHandler(null, null);
		}
	}
}
