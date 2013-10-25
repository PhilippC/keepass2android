using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace keepass2android.fileselect
{
	[Activity(Label = "@string/filestorage_setup_title",Theme="@style/Base")]
	public class FileStorageSetupActivity : Activity, IFileStorageSetupActivity
#if !EXCLUDE_JAVAFILESTORAGE
		,Keepass2android.Javafilestorage.IJavaFileStorageFileStorageSetupActivity
#endif
	{
		private bool isRecreated = false;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			Ioc = new IOConnectionInfo();
			PasswordActivity.SetIoConnectionFromIntent(Ioc, Intent);

			Kp2aLog.Log("FSSA.OnCreate with " + Ioc.Path);

			ProcessName = Intent.GetStringExtra(FileStorageSetupDefs.ExtraProcessName);
			IsForSave = Intent.GetBooleanExtra(FileStorageSetupDefs.ExtraIsForSave, false);
			if (bundle == null)
				State = new Bundle();
			else
			{
				State = (Bundle) bundle.Clone();
				isRecreated = true;
			}

			if (!isRecreated)
				App.Kp2a.GetFileStorage(Ioc).OnCreate(this, bundle);

		}

		protected override void OnStart()
		{
			base.OnStart();
			if (!isRecreated)
				App.Kp2a.GetFileStorage(Ioc).OnStart(this);
		}

		protected override void OnResume()
		{
			base.OnResume();
			App.Kp2a.GetFileStorage(Ioc).OnResume(this);
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);
			App.Kp2a.GetFileStorage(Ioc).OnActivityResult(this, requestCode, (int) resultCode, data);
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);

			outState.PutAll(State);
		}

		public IOConnectionInfo Ioc { get; private set; }
		public string Path 
		{
			get
			{
				return App.Kp2a.GetFileStorage(Ioc).IocToPath(Ioc);
			}
		}
		public string ProcessName { get; private set; }
		public bool IsForSave { get; private set; }
		public Bundle State { get; private set; }
	}
}