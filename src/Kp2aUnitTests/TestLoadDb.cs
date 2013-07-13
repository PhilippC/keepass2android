using System.Linq;
using System.Threading;
using Android.App;
using Android.OS;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;

namespace Kp2aUnitTests
{
	[TestClass]
	internal partial class TestLoadDb : TestBase
	{
		private void RunLoadTest(string filenameWithoutDir, string password, string keyfile)
		{
			Android.Util.Log.Debug("KP2ATest", "Starting for " + filenameWithoutDir+" with " + password+"/"+keyfile);

			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();
			bool loadSuccesful = false;
			LoadDb task = new LoadDb(app, new IOConnectionInfo() { Path = TestDbDirectory+filenameWithoutDir },
				password, keyfile, new ActionOnFinish((success, message) =>
					{
						if (!success)
							Android.Util.Log.Debug("KP2ATest", "error loading db: " + message);
						loadSuccesful = success; 		
					})
				);
			ProgressTask pt = new ProgressTask(app, Application.Context, task);
			Android.Util.Log.Debug("KP2ATest", "Running ProgressTask");
			pt.Run();
			pt.JoinWorkerThread();
			Android.Util.Log.Debug("KP2ATest", "PT.run finished");
			Assert.IsTrue(loadSuccesful, "didn't succesfully load database :-(");
			
			Assert.AreEqual(6,app.GetDb().KpDatabase.RootGroup.Groups.Count());
			Assert.AreEqual(2,app.GetDb().KpDatabase.RootGroup.Entries.Count());
		}


		[TestMethod]
		public void TestLoadWithPasswordOnly()
		{
			RunLoadTest("passwordonly.kdbx", DefaultPassword, "");
		}
		[TestMethod]
		public void TestLoadWithKeyfileOnly()
		{
			RunLoadTest("keyfileonly.kdbx", "", TestDbDirectory + "keyfile.txt");
		}

		[TestMethod]
		public void TestLoadWithPasswordAndKeyfile()
		{
			RunLoadTest("PasswordAndKeyfile.kdbx", DefaultPassword, TestDbDirectory + "keyfile.txt");
		}

		[TestMethod]
		public void TestLoadWithEmptyPassword()
		{
			RunLoadTest("EmptyPasswordAndKeyfile.kdbx", "", TestDbDirectory + "keyfile.txt");
		}


		[TestMethod]
		public void TestLoadWithEmptyPasswordOnly()
		{
			RunLoadTest("EmptyPassword.kdbx", "", "");
		}
		[TestMethod]
		public void LoadFromRemoteWithDomain()
		{
			var ioc = RemoteDomainIoc; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();
			
			bool loadSuccesful = false;
			LoadDb task = new LoadDb(app, ioc, "a", null, new ActionOnFinish((success, message) =>
				{
					if (!success)
						Android.Util.Log.Debug("KP2ATest", "error loading db: " + message);
					loadSuccesful = success;
				})
				);
			ProgressTask pt = new ProgressTask(app, Application.Context, task);
			Android.Util.Log.Debug("KP2ATest", "Running ProgressTask");
			pt.Run();
			pt.JoinWorkerThread();
			Android.Util.Log.Debug("KP2ATest", "PT.run finished");
			Assert.IsTrue(loadSuccesful, "didn't succesfully load database :-(");
			
		}

		[TestMethod]
		public void TestLoadKdbpWithPasswordOnly()
		{
			RunLoadTest("passwordonly.kdbp", DefaultPassword, "");
		}

	}
}