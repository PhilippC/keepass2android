using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Android.App;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android.Io;

namespace Kp2aUnitTests
{
	[TestClass]
	class TestCachingFileStorage: TestBase
	{
		private TestFileStorage _testFileStorage;
		private CachingFileStorage _fileStorage;
		private static readonly string CachingTestFile = DefaultDirectory + "cachingTestFile.txt";
		private string _defaultCacheFileContents = "default contents";
		private TestCacheSupervisor _testCacheSupervisor;

		/// <summary>
		/// Tests correct behavior in case that either remote or cache are not available
		/// </summary>
		[TestMethod]
		public void TestMakeAccessibleWhenOffline()
		{
			SetupFileStorage();
			
			//read the file once. Should now be in the cache.
			MemoryStream fileContents = ReadToMemoryStream(_fileStorage, CachingTestFile);

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.UpdatedCachedFileOnLoadId);

			//check it's the correct data:
			Assert.AreEqual(MemoryStreamToString(fileContents), _defaultCacheFileContents);

			//let the base file storage go offline:
			_testFileStorage.Offline = true;

			//now try to read the file again:
			MemoryStream fileContents2 = ReadToMemoryStream(_fileStorage, CachingTestFile);

			AssertEqual(fileContents, fileContents2);

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.CouldntOpenFromRemoteId);
			

		}

		private string MemoryStreamToString(MemoryStream stream)
		{
			stream.Position = 0;
			StreamReader r = new StreamReader(stream);
			return r.ReadToEnd();
		}

		/// <summary>
		/// tests correct behaviour after modifiying the local cache (with the remote file being unchanged) and remote being 
		/// either unavailable or available
		/// </summary>
		[TestMethod]
		public void TestSyncOnLoadWhenLocalFileChanged()
		{
			SetupFileStorage();

			
			//read the file once. Should now be in the cache.
			ReadToMemoryStream(_fileStorage, CachingTestFile);

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.UpdatedCachedFileOnLoadId);

			//let the base file storage go offline:
			_testFileStorage.Offline = true;

			//write something to the cache:
			string newContent = "new content";
			WriteContentToCacheFile(newContent);

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.CouldntSaveToRemoteId);

			//now try to read the file again:
			MemoryStream fileContents2 = ReadToMemoryStream(_fileStorage, CachingTestFile);


			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.CouldntOpenFromRemoteId);

			//should return the written content:
			Assert.AreEqual(MemoryStreamToString(fileContents2), newContent);

			//now go online and read again. This should trigger a sync and the modified data must be returned
			_testFileStorage.Offline = false;
			MemoryStream fileContents3 = ReadToMemoryStream(_fileStorage, CachingTestFile);
			Assert.AreEqual(MemoryStreamToString(fileContents3), newContent);

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.UpdatedRemoteFileOnLoadId);

			//ensure the data on the remote was synced:
			MemoryStream fileContents4 = ReadToMemoryStream(_testFileStorage, CachingTestFile);
			Assert.AreEqual(MemoryStreamToString(fileContents4), newContent);
		}

		/// <summary>
		/// tests correct behaviour after modifiying both the local cache and the remote file
		/// </summary>
		[TestMethod]
		public void TestLoadLocalWhenBothFilesChanged()
		{
			SetupFileStorage();

			//read the file once. Should now be in the cache.
			ReadToMemoryStream(_fileStorage, CachingTestFile);

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.UpdatedCachedFileOnLoadId);

			//let the base file storage go offline:
			_testFileStorage.Offline = true;

			//write something to the cache:
			string newLocalContent = "new local content";
			WriteContentToCacheFile(newLocalContent);

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.CouldntSaveToRemoteId);

			//write something to the remote file:
			File.WriteAllText(CachingTestFile, "new remote content");

			//go online again:
			_testFileStorage.Offline = false;

			//now try to read the file again:
			MemoryStream fileContents2 = ReadToMemoryStream(_fileStorage, CachingTestFile);

			//should return the local content:
			Assert.AreEqual(MemoryStreamToString(fileContents2), newLocalContent);

			//but a notification about the conflict should be made:
			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.NotifyOpenFromLocalDueToConflictId);

		}


	
		[TestMethod]
		public void TestSaveToRemote()
		{
			SetupFileStorage();

			//read the file once. Should now be in the cache.
			ReadToMemoryStream(_fileStorage, CachingTestFile);
			_testCacheSupervisor.Reset();

			//write something to the cache:
			string newContent = "new content";
			WriteContentToCacheFile(newContent);

			_testCacheSupervisor.AssertNoCall();

			Assert.AreEqual(newContent, File.ReadAllText(CachingTestFile));			
		}


		[TestMethod]
		public void TestLoadFromRemoteWhenRemoteDeleted()
		{
			SetupFileStorage();

			//read the file once. Should now be in the cache.
			ReadToMemoryStream(_fileStorage, CachingTestFile);

			_testCacheSupervisor.Reset();

			//delete remote file:
			_testFileStorage.DeleteFile(IocForCacheFile);


			//read again. shouldn't throw and give the same result:
			var memStream = ReadToMemoryStream(_fileStorage, CachingTestFile);

			//check if we received the correct content:
			Assert.AreEqual(_defaultCacheFileContents, MemoryStreamToString(memStream));

			_testCacheSupervisor.AssertSingleCall(TestCacheSupervisor.CouldntOpenFromRemoteId);
			
		}

		private void WriteContentToCacheFile(string newContent)
		{
		
			using (var trans = _fileStorage.OpenWriteTransaction(IocForCacheFile, true))
			{
				StreamWriter sw = new StreamWriter(trans.OpenFile());
				sw.Write(newContent);
				sw.Flush();
				sw.Close();
				trans.CommitWrite();
			}
		}

		protected IOConnectionInfo IocForCacheFile
		{
			get { return new IOConnectionInfo() { Path = CachingTestFile }; }
		}

		private void SetupFileStorage()
		{
			_testFileStorage = new TestFileStorage();
			_testCacheSupervisor = new TestCacheSupervisor();
			//_fileStorage = new CachingFileStorage(_testFileStorage, Application.Context.CacheDir.Path, _testCacheSupervisor);
			_fileStorage = new CachingFileStorage(_testFileStorage, "/mnt/sdcard/kp2atest_cache", _testCacheSupervisor);
			_fileStorage.ClearCache();
			File.WriteAllText(CachingTestFile, _defaultCacheFileContents);
		}

		private static MemoryStream ReadToMemoryStream(IFileStorage fileStorage, string filename)
		{
			Stream fileStream = fileStorage.OpenFileForRead(new IOConnectionInfo() {Path = filename});
			MemoryStream fileContents = new MemoryStream();
			fileStream.CopyTo(fileContents);
			fileStream.Close();
			return fileContents;
		}


		static bool StreamEquals(Stream stream1, Stream stream2)
		{
			const int bufferSize = 2048;
			byte[] buffer1 = new byte[bufferSize]; //buffer size
			byte[] buffer2 = new byte[bufferSize];
			while (true)
			{
				int count1 = stream1.Read(buffer1, 0, bufferSize);
				int count2 = stream2.Read(buffer2, 0, bufferSize);

				if (count1 != count2)
					return false;

				if (count1 == 0)
					return true;

				// You might replace the following with an efficient "memcmp"
				if (!buffer1.Take(count1).SequenceEqual(buffer2.Take(count2)))
					return false;
			}
		}

		private void AssertEqual(MemoryStream s1, MemoryStream s2)
		{
			s1.Seek(0,0);
			s2.Seek(0, 0);
			Assert.AreEqual(s1.Length, s2.Length);
			Assert.AreEqual(0, s1.Position);
			Assert.AreEqual(0, s2.Position);
			Assert.IsTrue(StreamEquals(s1, s2));
		}

		//todo test delete
	}
}
