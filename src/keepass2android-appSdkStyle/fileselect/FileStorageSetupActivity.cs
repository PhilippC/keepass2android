using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib.Serialization;
using keepass2android.Io;

namespace keepass2android.fileselect
{
    [Activity(Label = "@string/filestorage_setup_title", Theme = "@style/MyTheme_ActionBar", ConfigurationChanges = ConfigChanges.Orientation |
	           ConfigChanges.KeyboardHidden)]
	public class FileStorageSetupActivity : Activity, IFileStorageSetupActivity
#if !EXCLUDE_JAVAFILESTORAGE
#if !NoNet
		,Keepass2android.Javafilestorage.IJavaFileStorageFileStorageSetupActivity
#endif
#endif
	{
		private bool _isRecreated = false;

		private ActivityDesign _design;

		public FileStorageSetupActivity()
		{
			_design = new ActivityDesign(this);
		}

		protected override void OnCreate(Bundle bundle)
		{
			_design.ApplyTheme();
			base.OnCreate(bundle);
			

			SetContentView(Resource.Layout.file_storage_setup);

			Ioc = new IOConnectionInfo();
			Util.SetIoConnectionFromIntent(Ioc, Intent);

			Kp2aLog.Log("FSSA.OnCreate");

			ProcessName = Intent.GetStringExtra(FileStorageSetupDefs.ExtraProcessName);
			IsForSave = Intent.GetBooleanExtra(FileStorageSetupDefs.ExtraIsForSave, false);
			if (bundle == null)
				State = new Bundle();
			else
			{
				State = (Bundle) bundle.Clone();
				_isRecreated = true;
			}

			if (!_isRecreated)
				App.Kp2a.GetFileStorage(Ioc).OnCreate(this, bundle);

		}

		protected override void OnRestart()
		{
			base.OnRestart();
			_isRecreated = true;
		}

		protected override void OnStart()
		{
			base.OnStart();
			if (!_isRecreated)
				App.Kp2a.GetFileStorage(Ioc).OnStart(this);
		}

		protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
			App.Kp2a.GetFileStorage(Ioc).OnResume(this);
		}

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			base.OnActivityResult(requestCode, resultCode, data);
			App.Kp2a.GetFileStorage(Ioc).OnActivityResult(this, requestCode, (int) resultCode, data);
		}

	    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
	    {
		    base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		    var fileStorage = App.Kp2a.GetFileStorage(Ioc);
		    if (fileStorage is IPermissionRequestingFileStorage)
		    {
				((IPermissionRequestingFileStorage)fileStorage).OnRequestPermissionsResult(this, requestCode, permissions, grantResults);    
		    }
		    
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