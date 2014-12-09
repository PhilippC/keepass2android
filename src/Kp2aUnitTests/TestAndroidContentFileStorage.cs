using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Java.IO;
using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;
using keepass2android.Io;

namespace Kp2aUnitTests
{
	[TestClass]
	internal class TestBuiltInFileStorage
	{
		[TestMethod]
		public void ReadOnlyKitKat()
		{
			var storage = new BuiltInFileStorage(new TestKp2aApp());
			var extFile = "/storage/sdcard1/file.txt";
			Assert.IsTrue(storage.IsReadOnly(IOConnectionInfo.FromPath(extFile)));
			Assert.IsTrue(storage.IsReadOnly(IOConnectionInfo.FromPath(extFile)));

			Assert.IsFalse(storage.IsReadOnly(IOConnectionInfo.FromPath(Application.Context.GetExternalFilesDir(null).AbsolutePath+ "/file.txt")));
		}

	}
}