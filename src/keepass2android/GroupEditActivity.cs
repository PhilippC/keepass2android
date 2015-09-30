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
using Android.Runtime;
using Android.Widget;
using KeePassLib;
using KeePassLib.Utility;

namespace keepass2android
{
	[Activity (Label = "@string/app_name", Theme="@style/Dialog")]			
	public class GroupEditActivity : LifecycleDebugActivity
	{
		public const String KeyParent = "parent";
		public const String KeyName = "name";
		public const String KeyIconId = "icon_id";
		public const String KeyCustomIconId = "custom_icon_id";
		public const string KeyGroupUuid = "group_uuid";

		private ActivityDesign _design;
		
		private int _selectedIconId;
		private PwUuid _selectedCustomIconId = PwUuid.Zero;
		private PwGroup _groupToEdit;

		public GroupEditActivity()
		{
			_design = new ActivityDesign(this);
		}

		protected GroupEditActivity(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}


		public static void Launch(Activity act, PwGroup parentGroup)
		{
			Intent i = new Intent(act, typeof(GroupEditActivity));
			
			PwGroup parent = parentGroup;
			i.PutExtra(KeyParent, parent.Uuid.ToHexString());
			
			act.StartActivityForResult(i, 0);
		}

		public static void Launch(Activity act, PwGroup parentGroup, PwGroup groupToEdit)
		{
			Intent i = new Intent(act, typeof(GroupEditActivity));

			PwGroup parent = parentGroup;
			i.PutExtra(KeyParent, parent.Uuid.ToHexString());
			i.PutExtra(KeyGroupUuid, groupToEdit.Uuid.ToHexString());

			act.StartActivityForResult(i, 0);
		}
		
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			_design.ApplyDialogTheme();
			SetContentView (Resource.Layout.group_edit);

			ImageButton iconButton = (ImageButton)FindViewById (Resource.Id.icon_button);
            iconButton.SetScaleType(ImageView.ScaleType.FitXy);
			iconButton.Click += (sender, e) => 
			{
				IconPickerActivity.Launch (this);
			};
			_selectedIconId = (int) PwIcon.FolderOpen;
			iconButton.SetImageResource(Icons.IconToResId((PwIcon)_selectedIconId, true));

			Button okButton = (Button)FindViewById (Resource.Id.ok);
			okButton.Click += (sender, e) => {
				TextView nameField = (TextView)FindViewById (Resource.Id.group_name);
				String name = nameField.Text;
				
				if (name.Length > 0) {
					Intent intent = new Intent ();
					
					intent.PutExtra (KeyName, name);
					intent.PutExtra (KeyIconId, _selectedIconId);
					if (_selectedCustomIconId != null)
						intent.PutExtra(KeyCustomIconId, MemUtil.ByteArrayToHexString(_selectedCustomIconId.UuidBytes));
					if (_groupToEdit != null)
						intent.PutExtra(KeyGroupUuid, MemUtil.ByteArrayToHexString(_groupToEdit.Uuid.UuidBytes));

					SetResult (Result.Ok, intent);
					
					Finish ();
				} else {
					Toast.MakeText (this, Resource.String.error_no_name, ToastLength.Long).Show ();
				}
			};

			if (Intent.HasExtra(KeyGroupUuid))
			{
				string groupUuid = Intent.Extras.GetString(KeyGroupUuid);
				_groupToEdit = App.Kp2a.GetDb().Groups[new PwUuid(MemUtil.HexStringToByteArray(groupUuid))];
				_selectedIconId = (int) _groupToEdit.IconId;
				_selectedCustomIconId = _groupToEdit.CustomIconUuid;
				TextView nameField = (TextView)FindViewById(Resource.Id.group_name);
				nameField.Text = _groupToEdit.Name;
				App.Kp2a.GetDb().DrawableFactory.AssignDrawableTo(iconButton, Resources, App.Kp2a.GetDb().KpDatabase, _groupToEdit.IconId, _groupToEdit.CustomIconUuid, false);
				SetTitle(Resource.String.edit_group_title);
			}
			else
			{
				SetTitle(Resource.String.add_group_title);
			}

			     
			
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
					_selectedIconId = data.Extras.GetInt(IconPickerActivity.KeyIconId, (int) PwIcon.Key);
					String customIconIdString = data.Extras.GetString(IconPickerActivity.KeyCustomIconId);
					if (!String.IsNullOrEmpty(customIconIdString))
						_selectedCustomIconId = new PwUuid(MemUtil.HexStringToByteArray(customIconIdString));

					ImageButton currIconButton = (ImageButton) FindViewById(Resource.Id.icon_button);
					App.Kp2a.GetDb().DrawableFactory.AssignDrawableTo(currIconButton, Resources, App.Kp2a.GetDb().KpDatabase, (PwIcon) _selectedIconId, _selectedCustomIconId, false);
					break;
			}
		}

		protected override void OnResume()
		{
			base.OnResume();
			//DON'T: _design.ReapplyTheme();
			// (This causes endless loop creating/recreating. More correct: ReapplyDialogTheme (which doesn't exist) Not required anyways...)
		}
	}
}

