/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
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
using Android.Widget;
using KeePassLib;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", Theme="@style/Dialog")]			
	public class GroupEditActivity : LifecycleDebugActivity
	{
		public const String KeyParent = "parent";
		public const String KeyName = "name";
		public const String KeyIconId = "icon_id";
		
		private int _selectedIconId;
		
		public static void Launch(Activity act, PwGroup pw)
		{
			Intent i = new Intent(act, typeof(GroupEditActivity));
			
			PwGroup parent = pw;
			i.PutExtra(KeyParent, parent.Uuid.ToHexString());
			
			act.StartActivityForResult(i, 0);
		}
		
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			SetContentView (Resource.Layout.group_edit);
			SetTitle (Resource.String.add_group_title);
			
			ImageButton iconButton = (ImageButton)FindViewById (Resource.Id.icon_button);
			iconButton.Click += (sender, e) => 
			{
				IconPickerActivity.Launch (this);
			};
			_selectedIconId = (int) PwIcon.FolderOpen;
			iconButton.SetImageResource(Icons.IconToResId((PwIcon)_selectedIconId));

			Button okButton = (Button)FindViewById (Resource.Id.ok);
			okButton.Click += (sender, e) => {
				TextView nameField = (TextView)FindViewById (Resource.Id.group_name);
				String name = nameField.Text;
				
				if (name.Length > 0) {
					Intent intent = new Intent ();
					
					intent.PutExtra (KeyName, name);
					intent.PutExtra (KeyIconId, _selectedIconId);
					SetResult (Result.Ok, intent);
					
					Finish ();
				} else {
					Toast.MakeText (this, Resource.String.error_no_name, ToastLength.Long).Show ();
				}
			};
			     
			
			Button cancel = (Button)FindViewById (Resource.Id.cancel);
			cancel.Click += (sender, e) => {
				Intent intent = new Intent ();
				SetResult (Result.Canceled, intent);
				
				Finish ();
			};
		}         
		
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			switch ((int)resultCode)
			{
			case EntryEditActivity.ResultOkIconPicker:
				_selectedIconId = data.Extras.GetInt(IconPickerActivity.KeyIconId);
				ImageButton currIconButton = (ImageButton) FindViewById(Resource.Id.icon_button);
				currIconButton.SetImageResource(Icons.IconToResId((PwIcon)_selectedIconId));
				break;
			}
		}
	}
}

