using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using keepass2android.Io;
using keepass2android.view;
using Object = Java.Lang.Object;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.KeyboardHidden , Theme="@style/NoTitleBar")]		
	public class FileStorageSelectionActivity : ListActivity
	{
		private FileStorageAdapter _fileStorageAdapter;

		public const string AllowThirdPartyAppGet = "AllowThirdPartyAppGet";
		public const string AllowThirdPartyAppSend = "AllowThirdPartyAppSend";

		class FileStorageAdapter: BaseAdapter
		{

			private readonly FileStorageSelectionActivity _context;

			private readonly List<string> _protocolIds = new List<string>(); 

			public FileStorageAdapter(FileStorageSelectionActivity context)
			{
				_context = context;
				//show all supported protocols:
				foreach (IFileStorage fs in App.Kp2a.FileStorages)
					_protocolIds.AddRange(fs.SupportedProtocols);
				//put file:// to the top
				_protocolIds.Remove("file");
				_protocolIds.Insert(0, "file");
				if (context.Intent.GetBooleanExtra(AllowThirdPartyAppGet, false))
					_protocolIds.Add("androidget");
				if (context.Intent.GetBooleanExtra(AllowThirdPartyAppSend, false))
					_protocolIds.Add("androidsend");
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
			ReturnProtocol(protocolId);
			
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


			SetContentView(Resource.Layout.filestorage_selection);

			_fileStorageAdapter = new FileStorageAdapter(this);
			ListAdapter = _fileStorageAdapter;

			FindViewById<ListView>(Android.Resource.Id.List).ItemClick +=
				(sender, args) => OnItemSelected((string)_fileStorageAdapter.GetItem(args.Position));
		}


	}
}