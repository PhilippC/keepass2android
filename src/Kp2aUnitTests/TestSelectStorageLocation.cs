using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using keepass2android;
using keepass2android.Io;

namespace Kp2aUnitTests
{

	class TemporaryFileStorage: IFileStorage
	{
		public IEnumerable<string> SupportedProtocols
		{
			get { 
				yield return "content";
				yield return "readonly";
			}
		}

		public void Delete(IOConnectionInfo ioc)
		{
			
		}

		public bool CheckForFileChangeFast(IOConnectionInfo ioc, string previousFileVersion)
		{
			return false;
		}

		public string GetCurrentFileVersionFast(IOConnectionInfo ioc)
		{
			return null;
		}

		public Stream OpenFileForRead(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public IWriteTransaction OpenWriteTransaction(IOConnectionInfo ioc, bool useFileTransaction)
		{
			throw new NotImplementedException();
		}

		public string GetFilenameWithoutPathAndExt(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public bool RequiresCredentials(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public void CreateDirectory(IOConnectionInfo ioc, string newDirName)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<FileDescription> ListContents(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public FileDescription GetFileDescription(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public bool RequiresSetup(IOConnectionInfo ioConnection)
		{
			return false;
		}

		public string IocToPath(IOConnectionInfo ioc)
		{
			return ioc.Path;
		}

		public void StartSelectFile(IFileStorageSetupInitiatorActivity activity, bool isForSave, int requestCode, string protocolId)
		{
			throw new NotImplementedException();
		}

		public void PrepareFileUsage(IFileStorageSetupInitiatorActivity activity, IOConnectionInfo ioc, int requestCode,
		                             bool alwaysReturnSuccess)
		{
			throw new NotImplementedException();
		}

		public void OnCreate(IFileStorageSetupActivity activity, Bundle savedInstanceState)
		{
			throw new NotImplementedException();
		}

		public void OnResume(IFileStorageSetupActivity activity)
		{
			throw new NotImplementedException();
		}

		public void OnStart(IFileStorageSetupActivity activity)
		{
			throw new NotImplementedException();
		}

		public void OnActivityResult(IFileStorageSetupActivity activity, int requestCode, int resultCode, Intent data)
		{
			throw new NotImplementedException();
		}

		public string GetDisplayName(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public string CreateFilePath(string parent, string newFilename)
		{
			throw new NotImplementedException();
		}

		public IOConnectionInfo GetParentPath(IOConnectionInfo ioc)
		{
			throw new NotImplementedException();
		}

		public IOConnectionInfo GetFilePath(IOConnectionInfo folderPath, string filename)
		{
			throw new NotImplementedException();
		}

		public bool IsPermanentLocation(IOConnectionInfo ioc)
		{
			return ioc.Path.StartsWith("content") == false;
		}

		public bool IsReadOnly(IOConnectionInfo ioc)
		{
			return true;
		}
	}

	class TestKp2aAppForSelectStorageLocation: TestKp2aApp
	{


		public override IFileStorage GetFileStorage(IOConnectionInfo iocInfo, bool allowCache)
		{
			if ((iocInfo.Path.StartsWith("content://")) || (iocInfo.Path.StartsWith("readonly://")))
			{
				return new TemporaryFileStorage();
			}
			return base.GetFileStorage(iocInfo);
		}
	}

	sealed class TestControllableSelectStorageLocationActivity: SelectStorageLocationActivityBase
	{

		public List<string> toasts = new List<string>();
		public WritableRequirements requestedWritableRequirements;
		public bool? _result;
		public IOConnectionInfo _resultIoc;
		public object _userAction;
		private IKp2aApp _app;

		public TestControllableSelectStorageLocationActivity(IKp2aApp app) : base(app)
		{
			_app = app;
			StartFileStorageSelection(RequestCodeFileStorageSelectionForPrimarySelect, true, false);
		}

		protected override void ShowToast(string text)
		{
			toasts.Add(text);
		}

		protected override void CopyFile(IOConnectionInfo targetIoc, IOConnectionInfo sourceIoc)
		{
			if (CopyFileShouldFail)
			{
				throw new Exception("CopyFile failed in test.");
			}
		}

		public bool CopyFileShouldFail { get; set; }

		protected override void ShowInvalidSchemeMessage(string dataString)
		{
			toasts.Add("invalid scheme: " + dataString);
		}

		protected override string IntentToFilename(Intent data)
		{
			return data.GetStringExtra("path");
		}

		protected override void SetIoConnectionFromIntent(IOConnectionInfo ioc, Intent data)
		{
			ioc.Path = data.GetStringExtra("path");
		}

		protected override Result ExitFileStorageSelectionOk
		{
			get { return Result.FirstUser + 825; }
		}

		protected override void StartSelectFile(bool isForSave, int browseRequestCode, string protocolId)
		{
			_userAction = new SelectFileAction(isForSave, browseRequestCode, protocolId, this);
		}

		public void HandleActivityResult(int requestCode, Result resultCode, Intent data)
		{
			OnActivityResult(requestCode, resultCode, data);
		}

		internal class SelectFileAction
		{
			private readonly bool _isForSave;
			private readonly int _browseRequestCode;
			private readonly string _protocolId;
			private readonly TestControllableSelectStorageLocationActivity _testControllableSelectStorageLocationActivity;

			public SelectFileAction(bool isForSave, int browseRequestCode, string protocolId, TestControllableSelectStorageLocationActivity testControllableSelectStorageLocationActivity)
			{
				_isForSave = isForSave;
				_browseRequestCode = browseRequestCode;
				_protocolId = protocolId;
				_testControllableSelectStorageLocationActivity = testControllableSelectStorageLocationActivity;
			}

			public bool IsForSave {
				get { return _isForSave; }
			}

			public int BrowseRequestCode
			{
				get { return _browseRequestCode; }
			}

			public string ProtocolId
			{
				get { return _protocolId; }
			}


			public void PerformManualFileSelect(string path)
			{
				_testControllableSelectStorageLocationActivity.PressOpenButton(path, _browseRequestCode);
			}

			public void Cancel()
			{
				_testControllableSelectStorageLocationActivity.ReturnCancel();
			}

			public void PrepareFileChooser(string protocolId)
			{
				Intent data = new Intent();
				data.PutExtra("path", protocolId+"://");
				_testControllableSelectStorageLocationActivity.HandleActivityResult(_browseRequestCode, (Result) FileStorageResults.FileChooserPrepared, data);
				
			}
		}

		private void PressOpenButton(string path, int browseRequestCode)
		{
			OnOpenButton(path, browseRequestCode);
		}


		internal class FileStorageSelectionAction
		{
			private readonly int _requestCode;
			private readonly bool _allowThirdPartyGet;
			private readonly bool _allowThirdPartySend;
			private readonly TestControllableSelectStorageLocationActivity _testControllableSelectStorageLocationActivity;

			public FileStorageSelectionAction(int requestCode, bool allowThirdPartyGet, bool allowThirdPartySend, TestControllableSelectStorageLocationActivity testControllableSelectStorageLocationActivity)
			{
				_requestCode = requestCode;
				_allowThirdPartyGet = allowThirdPartyGet;
				_allowThirdPartySend = allowThirdPartySend;
				_testControllableSelectStorageLocationActivity = testControllableSelectStorageLocationActivity;
			}

			public int RequestCode
			{
				get { return _requestCode; }
			}

			public bool AllowThirdPartyGet
			{
				get { return _allowThirdPartyGet; }
			}

			public bool AllowThirdPartySend
			{
				get { return _allowThirdPartySend; }
			}

			public void ReturnProtocol(string protocolId)
			{
				Intent intent = new Intent();
				intent.PutExtra("protocolId", protocolId);
				_testControllableSelectStorageLocationActivity.HandleActivityResult(_requestCode, Result.FirstUser + 825 /*fs select ok*/, intent);
			}

			public void Cancel()
			{
				_testControllableSelectStorageLocationActivity.HandleActivityResult(_requestCode, Result.Canceled, null);
			}
		}

		protected override void ShowAndroidBrowseDialog(int requestCode, bool isForSave)
		{
			_userAction = new AndroidBrowseDialogAction(requestCode, isForSave, this);
		}

		internal class AndroidBrowseDialogAction
		{
			private readonly int _requestCode;
			private readonly bool _isForSave;
			private readonly TestControllableSelectStorageLocationActivity _activity;

			public AndroidBrowseDialogAction(int requestCode, bool isForSave, TestControllableSelectStorageLocationActivity activity)
			{
				_requestCode = requestCode;
				_isForSave = isForSave;
				_activity = activity;
			}

			public int RequestCode
			{
				get { return _requestCode; }
			}

			public void ReturnSelectedFile(string selectedUri)
			{
				Intent data = new Intent();
				data.PutExtra("path", selectedUri);
				_activity.HandleActivityResult(_requestCode, Result.Ok, data);
			}

			public void Cancel()
			{
				_activity.HandleActivityResult(_requestCode, Result.Canceled, null);
			}
		}

		protected override bool IsStorageSelectionForSave { get { return SelectLocationForSave; } }

		private bool SelectLocationForSave { get; set; }

		protected override void PerformCopy(Func<Action> copyAndReturnPostExecute)
		{
			Action postExec = copyAndReturnPostExecute();
			postExec();
		}

		protected override void StartFileStorageSelection(int requestCode, bool allowThirdPartyGet, bool allowThirdPartySend)
		{
			_userAction = new FileStorageSelectionAction(requestCode, allowThirdPartyGet, allowThirdPartySend, this);
		}

		protected override void StartFileChooser(string path, int requestCode, bool isForSave)
		{
			_userAction = new FileChooserAction(path, requestCode, isForSave, this);
		}

		internal class FileChooserAction
		{
			private readonly string _path;
			private readonly int _requestCode;
			private readonly bool _isForSave;
			private readonly TestControllableSelectStorageLocationActivity _activity;

			public FileChooserAction(string path, int requestCode, bool isForSave, TestControllableSelectStorageLocationActivity activity)
			{
				_path = path;
				_requestCode = requestCode;
				_isForSave = isForSave;
				_activity = activity;
			}

			public string Path
			{
				get { return _path; }
			}

			public int RequestCode
			{
				get { return _requestCode; }
			}

			public bool IsForSave
			{
				get { return _isForSave; }
			}

			public void ReturnChosenFile(string path)
			{
				Intent data = new Intent();
				data.PutExtra("path", path);
				_activity.HandleActivityResult(_requestCode, Result.Ok, data);
			}

			public void Cancel()
			{
				_activity.HandleActivityResult(_requestCode, Result.Canceled, null);
			}
		}

		protected override void ShowAlertDialog(string message, EventHandler<DialogClickEventArgs> onOk, EventHandler<DialogClickEventArgs> onCancel)
		{
			_userAction = new ShowAlertDialogAction(message, onOk, onCancel);
		}

		internal class ShowAlertDialogAction
		{
			public string Message { get; set; }
			public EventHandler<DialogClickEventArgs> OnOk { get; set; }
			public EventHandler<DialogClickEventArgs> OnCancel { get; set; }

			public ShowAlertDialogAction(string message, EventHandler<DialogClickEventArgs> onOk, EventHandler<DialogClickEventArgs> onCancel)
			{
				Message = message;
				OnOk = onOk;
				OnCancel = onCancel;
			}

			public void Cancel()
			{
				OnCancel(this, null);
			}

			public void Ok()
			{
				OnOk(this, null);
			}
		}

		protected override WritableRequirements RequestedWritableRequirements
		{
			get { return requestedWritableRequirements; }
		}

		public IKp2aApp App
		{
			get { return _app; }
		}

		protected override void ReturnOk(IOConnectionInfo ioc)
		{
			_result = true;
			_resultIoc = ioc;
		}

		protected override void ReturnCancel()
		{
			_result = false;
		}
	}


	[TestClass]
	class TestSelectStorageLocation
	{
		[TestInitialize]
		public void Init()
		{
			try
			{
				Looper.Prepare();
			}
			catch (Exception)
			{
				
			}
			
		}

		[TestMethod]
		public void TestCancelFileStorageSelection()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action.Cancel();
			Assert.IsFalse((bool) testee._result);
		}

		[TestMethod]
		public void TestSimpleManualSelect()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction) testee._userAction;
			action.ReturnProtocol("ftp");

			Assert.IsNull(testee._result); //no result yet

			var action2 = (TestControllableSelectStorageLocationActivity.SelectFileAction)testee._userAction;
			string path = "ftp://crocoll.net/test.kdbx";
			action2.PerformManualFileSelect(path);

			Assert.IsTrue((bool) testee._result);
			Assert.AreEqual(testee._resultIoc.Path, path);

		}

		[TestMethod]
		public void TestCancelManualSelect()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action.ReturnProtocol("ftp");

			Assert.IsNull(testee._result); //no result yet

			var action2 = (TestControllableSelectStorageLocationActivity.SelectFileAction)testee._userAction;
			action2.Cancel();

			Assert.IsFalse((bool)testee._result);

		}


		[TestMethod]
		public void TestCancelAndroidBrowseDialog()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action.ReturnProtocol("androidget");

			Assert.IsNull(testee._result); //no result yet

			var action2 = (TestControllableSelectStorageLocationActivity.AndroidBrowseDialogAction)testee._userAction;
			action2.Cancel();

			Assert.IsFalse((bool)testee._result);

		}


		[TestMethod]
		public void TestCancelCopyTemporaryLocation()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action.ReturnProtocol("androidget");

			var action2 = (TestControllableSelectStorageLocationActivity.AndroidBrowseDialogAction)testee._userAction;
			action2.ReturnSelectedFile("content://abc.kdbx");

			var action3 = (TestControllableSelectStorageLocationActivity.ShowAlertDialogAction)testee._userAction;
			Assert.IsTrue(action3.Message.StartsWith(testee.App.GetResourceString(UiStringKey.FileIsTemporarilyAvailable)));
			Assert.IsNull(testee._result); //no result yet
			action3.Cancel();

			Assert.IsFalse((bool)testee._result);

		}

		[TestMethod]
		public void TestCopyTemporaryLocation()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action.ReturnProtocol("androidget");

			var action2 = (TestControllableSelectStorageLocationActivity.AndroidBrowseDialogAction)testee._userAction;
			action2.ReturnSelectedFile("content://abc.kdbx");

			var action3 = (TestControllableSelectStorageLocationActivity.ShowAlertDialogAction)testee._userAction;
			Assert.IsTrue(action3.Message.StartsWith(testee.App.GetResourceString(UiStringKey.FileIsTemporarilyAvailable)));
			Assert.IsNull(testee._result); //no result yet
			action3.Ok();


			var action4 = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action4.ReturnProtocol("ftp");

			Assert.IsNull(testee._result);

			var action5 = (TestControllableSelectStorageLocationActivity.SelectFileAction)testee._userAction;
			Assert.IsTrue(action5.IsForSave);
			string path = "ftp://crocoll.net/testtarget.kdbx";
			action5.PerformManualFileSelect(path);

			Assert.IsTrue((bool)testee._result);
			Assert.AreEqual(path, testee._resultIoc.Path);

		}


		[TestMethod]
		public void TestCopyTemporaryLocationWithFileBrowser()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action.ReturnProtocol("androidget");

			var action2 = (TestControllableSelectStorageLocationActivity.AndroidBrowseDialogAction)testee._userAction;
			action2.ReturnSelectedFile("content://abc.kdbx");

			var action3 = (TestControllableSelectStorageLocationActivity.ShowAlertDialogAction)testee._userAction;
			Assert.IsTrue(action3.Message.StartsWith(testee.App.GetResourceString(UiStringKey.FileIsTemporarilyAvailable)));
			Assert.IsNull(testee._result); //no result yet
			action3.Ok();


			var action4 = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action4.ReturnProtocol("file");


			var action5 = (TestControllableSelectStorageLocationActivity.SelectFileAction)testee._userAction;
			Assert.IsTrue(action5.IsForSave);
			
			action5.PrepareFileChooser("file");

			Assert.IsNull(testee._result);


			var action6 = (TestControllableSelectStorageLocationActivity.FileChooserAction)testee._userAction;
			Assert.IsTrue(action5.IsForSave);
			string path = "file:///mnt/sdcard/testtarget.kdbx";
			
			action6.ReturnChosenFile(path);

			string expectedpath = "/mnt/sdcard/testtarget.kdbx";
			Assert.IsTrue((bool)testee._result);
			Assert.AreEqual(expectedpath, testee._resultIoc.Path);

		}


		[TestMethod]
		public void TestCopyTemporaryLocationWithCancelFileBrowser()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action.ReturnProtocol("androidget");

			var action2 = (TestControllableSelectStorageLocationActivity.AndroidBrowseDialogAction)testee._userAction;
			action2.ReturnSelectedFile("content://abc.kdbx");

			var action3 = (TestControllableSelectStorageLocationActivity.ShowAlertDialogAction)testee._userAction;
			Assert.IsTrue(action3.Message.StartsWith(testee.App.GetResourceString(UiStringKey.FileIsTemporarilyAvailable)));
			Assert.IsNull(testee._result); //no result yet
			action3.Ok();


			var action4 = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action4.ReturnProtocol("file");


			var action5 = (TestControllableSelectStorageLocationActivity.SelectFileAction)testee._userAction;
			Assert.IsTrue(action5.IsForSave);

			action5.PrepareFileChooser("file");

			Assert.IsNull(testee._result);


			var action6 = (TestControllableSelectStorageLocationActivity.FileChooserAction)testee._userAction;
			Assert.IsTrue(action5.IsForSave);
			string path = "file:///mnt/sdcard/testtarget.kdbx";

			action6.Cancel();

			Assert.IsFalse((bool)testee._result);
			

		}

		[TestMethod]
		public void TestCancelCopyReadOnlyLocation()
		{
			SelectStorageLocationActivityBase.WritableRequirements requestedWritableRequirements = SelectStorageLocationActivityBase.WritableRequirements.WriteDesired;
			string path;
			var testee = PrepareTesteeForCancelCopyReadOnly(requestedWritableRequirements, out path);

			Assert.IsTrue((bool)testee._result);
			Assert.AreEqual(path, testee._resultIoc.Path);

		}

		[TestMethod]
		public void TestCancelCopyReadOnlyLocationWriteRequired()
		{
			SelectStorageLocationActivityBase.WritableRequirements requestedWritableRequirements = SelectStorageLocationActivityBase.WritableRequirements.WriteDemanded;
			string path;
			var testee = PrepareTesteeForCancelCopyReadOnly(requestedWritableRequirements, out path);

			Assert.IsFalse((bool)testee._result);
			

		}

		private static TestControllableSelectStorageLocationActivity PrepareTesteeForCancelCopyReadOnly(
			SelectStorageLocationActivityBase.WritableRequirements requestedWritableRequirements, out string path)
		{
			var testee = CreateTestee();

			testee.requestedWritableRequirements = requestedWritableRequirements;
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction) testee._userAction;
			action.ReturnProtocol("androidget");

			var action2 = (TestControllableSelectStorageLocationActivity.AndroidBrowseDialogAction) testee._userAction;
			path = "readonly://abc.kdbx";
			action2.ReturnSelectedFile(path);

			var action3 = (TestControllableSelectStorageLocationActivity.ShowAlertDialogAction) testee._userAction;
			Assert.IsTrue(action3.Message.StartsWith(testee.App.GetResourceString(UiStringKey.FileIsReadOnly)));
			Assert.IsNull(testee._result); //no result yet
			action3.Cancel();
			return testee;
		}

		[TestMethod]
		public void TestOpenReadOnly()
		{
			var testee = CreateTestee();

			testee.requestedWritableRequirements = SelectStorageLocationActivityBase.WritableRequirements.ReadOnly;
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction) testee._userAction;
			action.ReturnProtocol("androidget");

			var action2 = (TestControllableSelectStorageLocationActivity.AndroidBrowseDialogAction) testee._userAction;
			var path = "readonly://abc.kdbx";
			action2.ReturnSelectedFile(path);

			Assert.IsTrue((bool)testee._result);
			Assert.AreEqual(path, testee._resultIoc.Path);

		}

		[TestMethod]
		public void TestCopyTemporaryLocationFails()
		{
			var testee = CreateTestee();
			var action = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action.ReturnProtocol("androidget");

			var action2 = (TestControllableSelectStorageLocationActivity.AndroidBrowseDialogAction)testee._userAction;
			action2.ReturnSelectedFile("content://abc.kdbx");

			var action3 = (TestControllableSelectStorageLocationActivity.ShowAlertDialogAction)testee._userAction;
			Assert.IsTrue(action3.Message.StartsWith(testee.App.GetResourceString(UiStringKey.FileIsTemporarilyAvailable)));
			Assert.IsNull(testee._result); //no result yet
			action3.Ok();


			var action4 = (TestControllableSelectStorageLocationActivity.FileStorageSelectionAction)testee._userAction;
			action4.ReturnProtocol("ftp");

			Assert.IsNull(testee._result);

			var action5 = (TestControllableSelectStorageLocationActivity.SelectFileAction)testee._userAction;
			Assert.IsTrue(action5.IsForSave);
			string path = "ftp://crocoll.net/testtarget.kdbx";

			testee.CopyFileShouldFail = true;

			action5.PerformManualFileSelect(path);

			Assert.IsFalse((bool)testee._result);

		}





		private static TestControllableSelectStorageLocationActivity CreateTestee()
		{
			return new TestControllableSelectStorageLocationActivity(new TestKp2aAppForSelectStorageLocation());
		}
	}
}