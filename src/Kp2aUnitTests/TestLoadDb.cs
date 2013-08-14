using System;
using System.IO;
using System.Linq;
using System.Threading;
using Android.App;
using Android.OS;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;
using keepass2android.Io;

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
			LoadDb task = new LoadDb(app, new IOConnectionInfo() { Path = TestDbDirectory+filenameWithoutDir }, null,
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
			LoadDb task = new LoadDb(app, ioc, null, "a", null, new ActionOnFinish((success, message) =>
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
		public void LoadFromRemote1and1()
		{
			var ioc = RemoteIoc1and1; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();
			
			bool loadSuccesful = false;
			LoadDb task = new LoadDb(app, ioc, null, "test", null, new ActionOnFinish((success, message) =>
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
		public void LoadFromRemote1and1NonExisting()
		{
			var ioc = RemoteIoc1and1NonExisting; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();

			bool loadSuccesful = false;
			bool gotError = false;
			LoadDb task = new LoadDb(app, ioc, null, "test", null, new ActionOnFinish((success, message) =>
			{
				if (!success)
				{
					Android.Util.Log.Debug("KP2ATest", "error loading db: " + message);
					gotError = true;
				}
				loadSuccesful = success;
			})
				);
			ProgressTask pt = new ProgressTask(app, Application.Context, task);
			Android.Util.Log.Debug("KP2ATest", "Running ProgressTask");
			pt.Run();
			pt.JoinWorkerThread();
			Android.Util.Log.Debug("KP2ATest", "PT.run finished");
			Assert.IsFalse(loadSuccesful);
			Assert.IsTrue(gotError);
		}

		[TestMethod]
		public void LoadFromRemote1and1WrongCredentials()
		{
			var ioc = RemoteIoc1and1WrongCredentials; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();

			bool loadSuccesful = false;
			bool gotError = false;
			LoadDb task = new LoadDb(app, ioc, null, "test", null, new ActionOnFinish((success, message) =>
			{
				if (!success)
				{
					Android.Util.Log.Debug("KP2ATest", "error loading db: " + message);
					gotError = true;
				}
				loadSuccesful = success;
			})
				);
			ProgressTask pt = new ProgressTask(app, Application.Context, task);
			Android.Util.Log.Debug("KP2ATest", "Running ProgressTask");
			pt.Run();
			pt.JoinWorkerThread();
			Android.Util.Log.Debug("KP2ATest", "PT.run finished");
			Assert.IsFalse(loadSuccesful);
			Assert.IsTrue(gotError);

		}

		[TestMethod]
		public void FileNotFoundExceptionWithWebDav()
		{
			var fileStorage = new BuiltInFileStorage();
			
			//should work:
			using (var stream = fileStorage.OpenFileForRead(RemoteIoc1and1))
			{
				stream.CopyTo(new MemoryStream());
			}
		
			//shouldn't give FileNotFound:
			bool gotException = false;
			try
			{
				using (var stream = fileStorage.OpenFileForRead(RemoteIoc1and1WrongCredentials))
				{
					stream.CopyTo(new MemoryStream());
				}
			}
			catch (FileNotFoundException)
			{
				Assert.Fail("shouldn't get FileNotFound with wrong credentials");
			}
			catch (Exception e)
			{
				Kp2aLog.Log("received "+e);
				gotException = true;
			}
			Assert.IsTrue(gotException);
			//should give FileNotFound:
			gotException = false;
			try
			{
				using (var stream = fileStorage.OpenFileForRead(RemoteIoc1and1NonExisting))
				{
					stream.CopyTo(new MemoryStream());
				}
			}
			catch (FileNotFoundException)
			{
				gotException = true;
			}
			Assert.IsTrue(gotException);
		}
		

		[TestMethod]
		public void TestLoadKdbpWithPasswordOnly()
		{
			RunLoadTest("passwordonly.kdbp", DefaultPassword, "");
		}

	}
}