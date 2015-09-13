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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;

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
			currIconGridView.Adapter = new ImageAdapter(this);
			
			currIconGridView.ItemClick += (sender, e) =>           {
			
					Intent intent = new Intent();
					
					intent.PutExtra(KeyIconId, e.Position);
					SetResult((Result)EntryEditActivity.ResultOkIconPicker, intent);
					
					Finish();
				};
		}
		
		public class ImageAdapter : BaseAdapter
		{
			readonly IconPickerActivity _act;
			
			public ImageAdapter(IconPickerActivity act)
			{
				_act = act;
			}
			
			public override int Count
			{
				get
				{
					/* Return number of KeePass icons */
					return Icons.Count();
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
				tv.Text = "" + position;
				ImageView iv = (ImageView) currView.FindViewById(Resource.Id.icon_image);
				iv.SetImageResource(Icons.IconToResId((KeePassLib.PwIcon)position, false));
				
				return currView;
			}
		}
	}

}

