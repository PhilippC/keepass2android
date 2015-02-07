using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Java.IO;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;

namespace Kp2aUnitTests
{
	[TestClass]
	class TestCreateDb: TestBase
	{
		[TestMethod]
		public void CreateAndSaveLocal()
		{
			IKp2aApp app = new TestKp2aApp();
			IOConnectionInfo ioc = new IOConnectionInfo {Path = DefaultFilename};

			File outputDir = new File(DefaultDirectory);
			outputDir.Mkdirs();
			File targetFile = new File(ioc.Path);
			if (targetFile.Exists())
				targetFile.Delete();

			bool createSuccesful = false;
			//create the task:
			CreateDb createDb = new CreateDb(app, Application.Context, ioc, new ActionOnFinish((success, message) =>
				{ createSuccesful = success;
					if (!success)
						Android.Util.Log.Debug("KP2A_Test", message);
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
			loadedDb.Open(ioc, new CompositeKey(), null, new KdbxDatabaseFormat(KdbxFormat.Default));

			//Check whether the databases are equal
			AssertDatabasesAreEqual(loadedDb, app.GetDb().KpDatabase);
			
			
			
		}
	}
}
