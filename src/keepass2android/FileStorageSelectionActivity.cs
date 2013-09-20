using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using keepass2android.Io;
using keepass2android.view;
using Object = Java.Lang.Object;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden , Theme="@style/NoTitleBar")]		
	public class FileStorageSelectionActivity : ListActivity
	{
		private string _protocolToSetup;
		private FileStorageAdapter _fileStorageAdapter;

		class FileStorageAdapter: BaseAdapter
		{

			private readonly FileStorageSelectionActivity _context;

			private List<string> _protocolIds = new List<string>(); 

			public FileStorageAdapter(FileStorageSelectionActivity context)
			{
				_context = context;
				//show all supported protocols:
				foreach (IFileStorage fs in App.Kp2a.FileStorages)
					_protocolIds.AddRange(fs.SupportedProtocols);
				//except file://
				_protocolIds.Remove("file");
			}

			public override Object GetItem(int position)
			{
				return _protocolIds[position];
			}

			public override long GetItemId(int position)
			{
				return position;
			}

			public override View GetView(int position, View convertView, ViewGroup parent)
			{
				var view = new FileStorageView(_context, _protocolIds[position], position);
				return view;

			}

			public override int Count
			{
				get { return _protocolIds.Count; }
			}
		}

		private void OnItemSelected(string protocolId)
		{
			var fs = App.Kp2a.GetFileStorage(protocolId);
			IFileStorageSetup fssetup = fs.RequiredSetup;
			try
			{
				if ((fssetup == null) || (fssetup.TrySetup(this)))
				{
					ReturnProtocol(protocolId);
				}
				else
				{
					//setup not yet complete
					_protocolToSetup = protocolId;
				}
			}
			catch (Exception e)
			{
				Toast.MakeText(this, e.Message, ToastLength.Long).Show();
			}
			
		}

		private void ReturnProtocol(string protocolId)
		{
			Intent intent = new Intent();
			intent.PutExtra("protocolId", protocolId);
			SetResult(KeePass.ExitFileStorageSelectionOk, intent);
			Finish();
		}

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			if (bundle != null)
				_protocolToSetup = bundle.GetString("_protocolToSetup", null);

			SetContentView(Resource.Layout.filestorage_selection);

			_fileStorageAdapter = new FileStorageAdapter(this);
			this.ListAdapter = _fileStorageAdapter;

			FindViewById<ListView>(Android.Resource.Id.List).ItemClick +=
				(sender, args) => OnItemSelected((string)_fileStorageAdapter.GetItem(args.Position));
		}

		protected override void OnSaveInstanceState(Bundle outState)
		{
			base.OnSaveInstanceState(outState);
			outState.PutString("_protocolToSetup",_protocolToSetup);
		}

		protected override void OnResume()
		{
			base.OnResume();
			if (!String.IsNullOrEmpty(_protocolToSetup))
			{
				try
				{
					string protocolToSetup = _protocolToSetup;
					_protocolToSetup = null;
					
					IFileStorageSetupOnResume fsSetup = App.Kp2a.GetFileStorage(protocolToSetup).RequiredSetup as IFileStorageSetupOnResume;
					if ((fsSetup == null) || (fsSetup.TrySetupOnResume(this)))
					{
						ReturnProtocol(protocolToSetup);
					}
					
				}
				catch (Exception e)
				{
					Toast.MakeText(this, e.Message, ToastLength.Long).Show();
				}				
			}
		}
	}
}