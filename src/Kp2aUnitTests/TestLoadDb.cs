using System;
using System.IO;
using System.Linq;
using Android.App;
using KeePassLib;
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
			var app = PerformLoad(filenameWithoutDir, password, keyfile);

			Assert.AreEqual(6,app.GetDb().KpDatabase.RootGroup.Groups.Count());
			Assert.AreEqual(2,app.GetDb().KpDatabase.RootGroup.Entries.Count());
		}

		private IKp2aApp PerformLoad(string filenameWithoutDir, string password, string keyfile)
		{
			Android.Util.Log.Debug("KP2ATest", "Starting for " + filenameWithoutDir + " with " + password + "/" + keyfile);

			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();
			bool loadSuccesful = false;
			var key = CreateKey(password, keyfile);
			string loadErrorMessage = "";

			LoadDb task = new LoadDb(app, new IOConnectionInfo {Path = TestDbDirectory + filenameWithoutDir}, null,
			                         key, keyfile, new ActionOnFinish((success, message) =>
				                         {
					                         loadErrorMessage = message;
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
			Assert.IsTrue(loadSuccesful, "didn't succesfully load database :-( " + loadErrorMessage);
			return app;
		}


		[TestMethod]
		public void TestLoadWithPasswordOnly()
		{
			RunLoadTest("passwordonly.kdbx", DefaultPassword, "");
			

		}


		[TestMethod]
		public void TestLoadKdb1()
		{
			var app = PerformLoad("keepass.kdb", "test", "");

			//contents of the kdb file are a little different because the root group cannot have entries
			Assert.AreEqual(6, app.GetDb().KpDatabase.RootGroup.Groups.Count());
			PwGroup generalGroup = app.GetDb().KpDatabase.RootGroup.Groups.Single(g => g.Name == "General");
			Assert.AreEqual(2, generalGroup.Entries.Count());
			foreach (PwEntry e in generalGroup.Entries)
				Assert.IsFalse(e.Binaries.Any());
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
			var app = new TestKp2aApp();
			app.ServerCertificateErrorResponse = true; //accept invalid cert
			app.CreateNewDatabase();
			
			bool loadSuccesful = false;
			LoadDb task = new LoadDb(app, ioc, null, CreateKey("a"), null, new ActionOnFinish((success, message) =>
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
		public void LoadErrorWithCertificateTrustFailure()
		{
			var ioc = RemoteCertFailureIoc; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			var app = new TestKp2aApp();
			app.ServerCertificateErrorResponse = false;
			app.CreateNewDatabase();

			bool loadSuccesful = false;
			string theMessage = "";
			LoadDb task = new LoadDb(app, ioc, null, CreateKey("test"), null, new ActionOnFinish((success, message) =>
			{
				if (!success)
					Android.Util.Log.Debug("KP2ATest", "error loading db: " + message);
				loadSuccesful = success;
				theMessage = message;
			})
				);


			ProgressTask pt = new ProgressTask(app, Application.Context, task);
			Android.Util.Log.Debug("KP2ATest", "Running ProgressTask");
			pt.Run();
			pt.JoinWorkerThread();
			Android.Util.Log.Debug("KP2ATest", "PT.run finished");
			Assert.IsFalse(loadSuccesful, "database should not be loaded because invalid certificates are not accepted");
			Assert.AreEqual(theMessage, UiStringKey.ErrorOcurred +" "+UiStringKey.CertificateFailure);

		}

		[TestMethod]
		public void LoadWithAcceptedCertificateTrustFailure()
		{
			var ioc = RemoteCertFailureIoc; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			var app = new TestKp2aApp();
			app.ServerCertificateErrorResponse = true;
			app.CreateNewDatabase();

			bool loadSuccesful = false;
			LoadDb task = new LoadDb(app, ioc, null, CreateKey("test"), null, new ActionOnFinish((success, message) =>
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
			Assert.IsTrue(loadSuccesful, "database should be loaded because invalid certificates are accepted");

		}

		[TestMethod]
		public void LoadFromRemote1And1()
		{
			var ioc = RemoteIoc1and1; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();
			
			bool loadSuccesful = false;
			LoadDb task = new LoadDb(app, ioc, null, CreateKey("test"), null, new ActionOnFinish((success, message) =>
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
		public void LoadFromRemote1And1NonExisting()
		{
			var ioc = RemoteIoc1and1NonExisting; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();

			bool loadSuccesful = false;
			bool gotError = false;
			LoadDb task = new LoadDb(app, ioc, null, CreateKey("test"), null, new ActionOnFinish((success, message) =>
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
		public void LoadFromRemote1And1WrongCredentials()
		{
			var ioc = RemoteIoc1and1WrongCredentials; //note: this property is defined in "TestLoadDbCredentials.cs" which is deliberately excluded from Git because the credentials are not public!
			IKp2aApp app = new TestKp2aApp();
			app.CreateNewDatabase();

			bool loadSuccesful = false;
			bool gotError = false;
			LoadDb task = new LoadDb(app, ioc, null, CreateKey("test"), null, new ActionOnFinish((success, message) =>
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
			var fileStorage = new BuiltInFileStorage(new TestKp2aApp());
			
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