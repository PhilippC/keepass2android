using System;
using System.Collections.Generic;
using System.Linq;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using keepass2android.Io;
using keepass2android.view;
using AlertDialog = Android.App.AlertDialog;
using Object = Java.Lang.Object;

namespace keepass2android
{
    [Activity(Label = "@string/app_name", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden, Theme = "@style/MyTheme_Blue")]
    public class FileStorageSelectionActivity : AndroidX.AppCompat.App.AppCompatActivity
	{
		private readonly ActivityDesign _design;

		private FileStorageAdapter _fileStorageAdapter;
	    private const int RequestExternalStoragePermission = 1;

	    public FileStorageSelectionActivity()
		{
			_design = new ActivityDesign(this);
		}

		public const string AllowThirdPartyAppGet = "AllowThirdPartyAppGet";
		public const string AllowThirdPartyAppSend = "AllowThirdPartyAppSend";

		class FileStorageAdapter: BaseAdapter
		{

			private readonly FileStorageSelectionActivity _context;

			private readonly List<string> _displayedProtocolIds = new List<string>(); 

			public FileStorageAdapter(FileStorageSelectionActivity context)
			{
				_context = context;
				//show all supported protocols:
				foreach (IFileStorage fs in App.Kp2a.FileStorages)
					_displayedProtocolIds.AddRange(fs.SupportedProtocols);
				
				//this is there for legacy reasons, new protocol is onedrive
				_displayedProtocolIds.Remove("skydrive");

                //onedrive was replaced by onedrive2 in a later implementation, but we still have the previous implementation to open existing connections (without the need to re-authenticate etc.)
                _displayedProtocolIds.Remove("onedrive");



                //special handling for local files:
                if (!Util.IsKitKatOrLater)
				{
					//put file:// to the top
					_displayedProtocolIds.Remove("file");
					_displayedProtocolIds.Insert(0, "file");
					
					//remove "content" (covered by androidget)
					//On KitKat, content is handled by AndroidContentStorage taking advantage 
					//of persistable permissions and ACTION_OPEN/CREATE_DOCUMENT
					_displayedProtocolIds.Remove("content");
					
				}
				else
				{
					_displayedProtocolIds.Remove("file");
				}


				//starting with Android 11, we don't show the Third party app option. Due to restricted permissions,
				//this no longer works.
				if ((int)Build.VERSION.SdkInt < 30)
                    if (context.Intent.GetBooleanExtra(AllowThirdPartyAppGet, false))
					_displayedProtocolIds.Add("androidget");
				if (context.Intent.GetBooleanExtra(AllowThirdPartyAppSend, false))
					_displayedProtocolIds.Add("androidsend");
#if NoNet
                //don't display "get regular version", is classified as deceptive ad by Google. Haha.
				//_displayedProtocolIds.Add("kp2a");
#endif
			    _displayedProtocolIds = _displayedProtocolIds.GroupBy(p => App.Kp2a.GetStorageMainTypeDisplayName(p))
			        .Select(g => string.Join(",", g)).ToList();

			}

			public override Object GetItem(int position)
			{
				return _displayedProtocolIds[position];
			}

			public override long GetItemId(int position)
			{
				return position;
			}



            public static float convertDpToPixel(float dp, Context context)
            {
	            return Util.convertDpToPixel(dp, context);
            }


			public override View GetView(int position, View convertView, ViewGroup parent)
			{

                Button btn;

                if (convertView == null)
                {  // if it's not recycled, initialize some attributes

                    btn = new Button(_context);
                    btn.LayoutParameters = new GridView.LayoutParams((int)convertDpToPixel(90, _context), (int)convertDpToPixel(110, _context));
                    btn.SetBackgroundResource(Resource.Drawable.storagetype_button_bg);
					btn.SetPadding((int)convertDpToPixel(4, _context),
						(int)convertDpToPixel(20, _context),
						(int)convertDpToPixel(4, _context),
						(int)convertDpToPixel(4, _context));
                    btn.SetTextSize(ComplexUnitType.Sp, 11);
                    btn.SetTextColor(new Color(115, 115, 115));
                    btn.SetSingleLine(false);
					btn.Gravity = GravityFlags.Center;
                    btn.Click += (sender, args) => _context.OnItemSelected( (string) ((Button)sender).Tag);
                }
                else
                {
                    btn = (Button)convertView;
                }
			    
			    var protocolId = _displayedProtocolIds[position];
                btn.Tag = protocolId;

			    string firstProtocolInList = protocolId.Split(",").First();


                Drawable drawable = App.Kp2a.GetStorageIcon(firstProtocolInList);

				String title =
					protocolId == "kp2a" ? App.Kp2a.GetResourceString("get_regular_version")
						:
						App.Kp2a.GetStorageMainTypeDisplayName(firstProtocolInList);
                var str = new SpannableString(title);

			    btn.TextFormatted = str;
                //var drawable = ContextCompat.GetDrawable(context, Resource.Drawable.Icon);
                btn.SetCompoundDrawablesWithIntrinsicBounds(null, drawable, null, null);

                return btn;
			}

			public override int Count
			{
				get { return _displayedProtocolIds.Count; }
			}
		}

		private void OnItemSelected(string protocolId)
		{
			if (protocolId == "kp2a")
			{
				//send user to market page of regular edition to get more protocols 
				Util.GotoUrl(this, GetString(Resource.String.MarketURL) + "keepass2android.keepass2android");
				return;
			}

		    if (protocolId.Contains(","))
		    {
                //bring up a selection dialog to select the variant of the file storage
		        AlertDialog.Builder builder = new AlertDialog.Builder(this);
		        
		        builder.SetItems(protocolId.Split(",").Select(singleProtocolId => App.Kp2a.GetStorageDisplayName(singleProtocolId)).ToArray(), 
		            delegate(object sender, DialogClickEventArgs args)
		            {
		                string[] singleProtocolIds = protocolId.Split(",");
                        OnItemSelected(singleProtocolIds[args.Which]);
		            });
		        builder.Show();
		        return;
		    }

			var field = typeof(Resource.String).GetField("filestoragehelp_" + protocolId);
			if (field == null)
			{
				//no help available
				ReturnProtocol(protocolId);
			}
            else
			{
				//set help:
				string help = GetString((int)field.GetValue(null));

				new AlertDialog.Builder(this)
					.SetTitle(GetString(Resource.String.app_name))
					.SetMessage(help)
					.SetPositiveButton(Android.Resource.String.Ok, (sender, args) => ReturnProtocol(protocolId))
					.Create()
					.Show();
			}
			

		}

	    private void ReturnProtocol(string protocolId)
	    {
		    if ((protocolId == "androidget") && ((int) Build.VERSION.SdkInt >= 23) &&
		       ( CheckSelfPermission(Manifest.Permission.WriteExternalStorage) != Permission.Granted))
			{
				RequestPermissions(new string[]{Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage},RequestExternalStoragePermission);
				return;
			}
			Intent intent = new Intent();
			intent.PutExtra("protocolId", protocolId);
			SetResult(KeePass.ExitFileStorageSelectionOk, intent);
			Finish();
		}

	    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
	    {
		    if ((requestCode == RequestExternalStoragePermission) && (grantResults[0] == Permission.Granted))
		    {
			    ReturnProtocol("androidget");
		    }
	    }

	    protected override void OnCreate(Bundle bundle)
		{
			_design.ApplyTheme(); 
			base.OnCreate(bundle);
			

			SetContentView(Resource.Layout.filestorage_selection);

            var toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.mytoolbar);
            
            SetSupportActionBar(toolbar);

		    SupportActionBar.Title = RemoveTrailingColon(GetString(Resource.String.select_storage_type));
            
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);
		    SupportActionBar.SetDisplayShowHomeEnabled(true);
		    toolbar.NavigationClick += (sender, args) => OnBackPressed();

			_fileStorageAdapter = new FileStorageAdapter(this);
			var gridView = FindViewById<GridView>(Resource.Id.gridview);
			gridView.ItemClick +=
				(sender, args) => OnItemSelected((string)_fileStorageAdapter.GetItem(args.Position));
		    gridView.Adapter = _fileStorageAdapter;
		    
		}

        private string RemoveTrailingColon(string str)
        {
            if (str.EndsWith(":"))
                return str.Substring(0, str.Length - 1);
            return str;
        }

        protected override void OnResume()
		{
			base.OnResume();
			_design.ReapplyTheme();
		}
	}
}