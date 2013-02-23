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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android.view
{
	public class FileNameView : RelativeLayout {
		
		public FileNameView(Context context):this(context,null) {

		}
		
		public FileNameView(Context context, Android.Util.IAttributeSet attrs) 
			:base(context, attrs)
		{
			inflate(context);
		}
		
		public FileNameView (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
		
		private void inflate(Context context) {
			LayoutInflater inflater = (LayoutInflater) context.GetSystemService(Context.LayoutInflaterService);
			inflater.Inflate(Resource.Layout.file_selection_filename, this);
		}
		
		public void updateExternalStorageWarning() {
			int warning = -1;
			String state = Android.OS.Environment.ExternalStorageState;
			if (state.Equals(Android.OS.Environment.MediaMountedReadOnly)) {
				warning = Resource.String.warning_read_only;
			} else if (!state.Equals(Android.OS.Environment.MediaMounted)) {
				warning = Resource.String.warning_unmounted;
			}
			
			TextView tv = (TextView) FindViewById(Resource.Id.label_warning);
			TextView label = (TextView) FindViewById(Resource.Id.label_open_by_filename);
			RelativeLayout.LayoutParams lp = new RelativeLayout.LayoutParams(LayoutParams.FillParent, LayoutParams.WrapContent);
			
			if (warning != -1) {
				tv.SetText(warning);
				tv.Visibility = ViewStates.Visible;
				
				lp.AddRule(LayoutRules.Below, Resource.Id.label_warning);
			} else {
				tv.Visibility = ViewStates.Invisible;

			}			
			label.LayoutParameters = lp;
		}
	}
}

