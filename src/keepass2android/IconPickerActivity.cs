/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Preferences;
using Android.Views;
using Android.Widget;
using KeePassLib;
using KeePassLib.Utility;
using FileNotFoundException = Java.IO.FileNotFoundException;
using IOException = Java.IO.IOException;

namespace keepass2android
{
    [Activity(Label = "@string/app_name", Theme = "@style/MyTheme_ActionBar")]			
	public class IconPickerActivity : LockCloseActivity
	{
		public const String KeyIconId = "icon_id";
		public const String KeyCustomIconId = "custom_icon_id";
		
		public static void Launch(Activity act)
		{
			Intent i = new Intent(act, typeof(IconPickerActivity));
			act.StartActivityForResult(i, 0);
		}
		
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			
			SetContentView(Resource.Layout.icon_picker);
			
			GridView currIconGridView = (GridView)FindViewById(Resource.Id.IconGridView);
			currIconGridView.Adapter = new ImageAdapter(this, App.Kp2a.GetDb().KpDatabase);
			
			currIconGridView.ItemClick += (sender, e) =>
			{

				Intent intent = new Intent();

				if (((ImageAdapter) currIconGridView.Adapter).IsCustomIcon(e.Position))
				{
					intent.PutExtra(KeyCustomIconId,
						MemUtil.ByteArrayToHexString(((ImageAdapter) currIconGridView.Adapter).GetCustomIcon(e.Position).Uuid.UuidBytes));
				}
				else
				{
					intent.PutExtra(KeyIconId, e.Position);
				}
				SetResult((Result)EntryEditActivity.ResultOkIconPicker, intent);
					
				Finish();
			};
		}

	    private const int AddCustomIconId = 1;
	    private const int RequestCodePickImage = 2;

	    public override bool OnCreateOptionsMenu(IMenu menu)
	    {
		    
			base.OnCreateOptionsMenu(menu);
			 
			menu.Add(0, AddCustomIconId, 0, GetString(Resource.String.AddCustomIcon)); 

			return true;
    
	    }

	    public override bool OnOptionsItemSelected(IMenuItem item)
	    {
		    if (item.ItemId == AddCustomIconId)
		    {
				Intent intent = new Intent();
				intent.SetType("image/*");
				intent.SetAction(Intent.ActionGetContent);
				intent.AddCategory(Intent.CategoryOpenable);
				StartActivityForResult(intent, RequestCodePickImage);
		    }

		    return base.OnOptionsItemSelected(item);
	    }

	    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
	    {

		    base.OnActivityResult(requestCode, resultCode, data);

			if (requestCode == RequestCodePickImage && resultCode == Result.Ok)
				try
				{
					var stream = ContentResolver.OpenInputStream(data.Data);
					var bitmap = BitmapFactory.DecodeStream(stream);
					
					stream.Close();

					float maxSize = 128;

					using (MemoryStream ms = new MemoryStream())
					{
						if ((bitmap.Width > maxSize) || (bitmap.Height > maxSize))
						{
							float scale = Math.Min(maxSize / bitmap.Width, maxSize / bitmap.Height);
							var scaleWidth = (int)(bitmap.Width * scale);
							var scaleHeight = (int)(bitmap.Height * scale);
							var scaledBitmap = Bitmap.CreateScaledBitmap(bitmap, scaleWidth, scaleHeight, true);
							Bitmap newRectBitmap = Bitmap.CreateBitmap((int)maxSize, (int)maxSize, Bitmap.Config.Argb8888);
							
							Canvas c = new Canvas(newRectBitmap);
							c.DrawBitmap(scaledBitmap, (maxSize - scaledBitmap.Width)/2.0f, (maxSize - scaledBitmap.Height)/2.0f, null);
							bitmap = newRectBitmap;
						}
						;
						bitmap.Compress(Bitmap.CompressFormat.Png, 90, ms);
						PwCustomIcon pwci = new PwCustomIcon(new PwUuid(true), ms.ToArray());

						App.Kp2a.GetDb().KpDatabase.CustomIcons.Add(pwci);
						
					}
					var gridView = ((GridView)FindViewById(Resource.Id.IconGridView));
					((BaseAdapter)gridView.Adapter).NotifyDataSetInvalidated();
					gridView.SmoothScrollToPosition(((BaseAdapter)gridView.Adapter).Count-1);
				}
				catch (FileNotFoundException e)
				{
					e.PrintStackTrace();
				}
				catch (IOException e)
				{
					e.PrintStackTrace();
				}
	    }

	    public class ImageAdapter : BaseAdapter
		{
			readonly IconPickerActivity _act;
			private readonly PwDatabase _db;

			public ImageAdapter(IconPickerActivity act, PwDatabase db)
			{
				_act = act;
				_db = db;
			}

			public override int Count
			{
				get
				{
					return (int)PwIcon.Count + _db.CustomIcons.Count;
				}
			}
			
			public override Java.Lang.Object GetItem(int position)
			{
				return null;
			}
			
			public override long GetItemId(int position)
			{
				return 0;
			}

			public override View GetView(int position, View convertView, ViewGroup parent)
			{
				View currView;
				if(convertView == null)
				{
					LayoutInflater li = (LayoutInflater) _act.GetSystemService(LayoutInflaterService); 
					currView = li.Inflate(Resource.Layout.icon, null);
				}
				else
				{
					currView = convertView;
				}
				TextView tv = (TextView) currView.FindViewById(Resource.Id.icon_text);
				ImageView iv = (ImageView) currView.FindViewById(Resource.Id.icon_image);
						
				if (position < (int)PwIcon.Count)
				{
					tv.Text = "" + position;
					var drawable = App.Kp2a.GetDb()
						.DrawableFactory.GetIconDrawable(_act, App.Kp2a.GetDb().KpDatabase, (KeePassLib.PwIcon) position, null, false);
					drawable = new BitmapDrawable(Util.DrawableToBitmap(drawable));
					iv.SetImageDrawable(drawable);
					//App.Kp2a.GetDb().DrawableFactory.AssignDrawableTo(iv, _act, App.Kp2a.GetDb().KpDatabase, (KeePassLib.PwIcon) position, null, false);

					if (
						PreferenceManager.GetDefaultSharedPreferences(currView.Context)
							.GetString("IconSetKey", currView.Context.PackageName) == currView.Context.PackageName)
					{
						Android.Graphics.PorterDuff.Mode mMode = Android.Graphics.PorterDuff.Mode.SrcAtop;
						Color color = new Color(189, 189, 189);
						iv.SetColorFilter(color, mMode);	
					}
					
				}
				else
				{
					int pos = position - (int)PwIcon.Count;
					var icon = _db.CustomIcons[pos];
					tv.Text = pos.ToString();
					iv.SetColorFilter(null);
					iv.SetImageBitmap(icon.Image);
					
				}

				return currView;
			}

			public bool IsCustomIcon(int position)
			{
				return position >= (int)PwIcon.Count;
			}

			public PwCustomIcon GetCustomIcon(int position)
			{
				if (!IsCustomIcon(position))
					return null;
				return _db.CustomIcons[position - (int)PwIcon.Count];
			}
		}
	}

}

