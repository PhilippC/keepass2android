using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using MonoDroidUnitTesting;
using System.Reflection;

namespace Kp2aUnitTests
{
    [Activity(Label = "MonoDroidUnit", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : GuiTestRunnerActivity
    {
        protected override TestRunner CreateTestRunner()
        {
            TestRunner runner = new TestRunner();
            // Run all tests from this assembly
            //runner.AddTests(Assembly.GetExecutingAssembly());
			//runner.AddTests(typeof(TestLoadDb).GetMethod("TestLoadKdb1WithKeyfileByDirectCall"));
			//runner.AddTests(typeof(TestLoadDb).GetMethod("TestLoadKdb1WithKeyfileOnly"));

			
			//runner.AddTests(new List<Type> { typeof(TestSelectStorageLocation) });
			//runner.AddTests(new List<Type> { typeof(TestSynchronizeCachedDatabase)});
			//runner.AddTests(typeof(TestLoadDb).GetMethod("LoadErrorWithCertificateTrustFailure"));
			//runner.AddTests(typeof(TestLoadDb).GetMethod("LoadWithAcceptedCertificateTrustFailure"));
			
			//runner.AddTests(new List<Type> { typeof(TestSaveDb) });
			//runner.AddTests(new List<Type> { typeof(TestCachingFileStorage) });
			//runner.AddTests(typeof(TestLoadDb).GetMethod("TestLoadKdb1WithKeyfileOnly"));
			runner.AddTests(typeof(TestSaveDb).GetMethod("TestLoadEditSaveWithSyncKdb"));
			runner.AddTests(typeof(TestSaveDb).GetMethod("TestLoadAndSave_TestIdenticalFiles_kdb"));
			runner.AddTests(typeof(TestSaveDb).GetMethod("TestCreateSaveAndLoad_TestIdenticalFiles_kdb"));
			//runner.AddTests(typeof(TestLoadDb).GetMethod("LoadAndSaveFromRemote1And1Ftp"));
			//runner.AddTests(typeof(TestLoadDb).GetMethod("TestLoadKdbpWithPasswordOnly"));
			//runner.AddTests(typeof(TestSaveDb).GetMethod("TestLoadKdbxAndSaveKdbp_TestIdenticalFiles"));
            return runner;
        }
    }

}

