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

namespace Kp2aUnitTests
{
	[TestClass]
	internal class TestIntentsAndBundles
	{
		[TestMethod]
		public void StringArray()
		{
			string[] dataIn = new string[] { "a","bcd"};
			Intent i= new Intent();
			i.PutExtra("key", dataIn);

			Bundle extras = i.Extras;
			var dataOut = extras.GetStringArray("key");
			Assert.AreEqual(dataIn.Length, dataOut.Length);
			Assert.AreEqual(dataIn[0], dataOut[0]);
			Assert.AreEqual(dataIn[1], dataOut[1]);
		}
	}
}