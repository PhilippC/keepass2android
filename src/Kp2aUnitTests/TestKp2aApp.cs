using System;
using Android.Content;
using Android.OS;
using KeePassLib.Serialization;
using keepass2android;

namespace Kp2aUnitTests
{
	/// <summary>
	/// Very simple implementation of the Kp2aApp interface to be used in tests
	/// </summary>
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

		public Handler UiThreadHandler {
			get { return null; } //ensure everything runs in the same thread. Otherwise the OnFinish-callback would run after the test has already finished (with failure)
		}
		public IProgressDialog CreateProgressDialog(Context ctx)
		{
			return new ProgressDialogStub();
		}
	}
}