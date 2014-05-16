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
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;
using PluginHostTest;

namespace keepass2android.view
{
	
	public class EntrySection : LinearLayout {
		
		public EntrySection(Context context): base(context, null) {
			InflateView (null, null);
		}
		
		public EntrySection(Context context, IAttributeSet attrs): base(context, attrs) {
			InflateView (null, null);
		}
		
		public EntrySection(Context context, IAttributeSet attrs, String title, String value): base(context, attrs) {
			
			InflateView(title, value);
		}

		public EntrySection (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
		
		
		private void InflateView(String title, String value) {
			LayoutInflater inflater = (LayoutInflater) Context.GetSystemService(Context.LayoutInflaterService);
			inflater.Inflate(Resource.Layout.entry_section, this);
			
			SetText(Resource.Id.title, title);

			FindViewById<TextView>(Resource.Id.value).Invalidate();
			SetText(Resource.Id.value, value);
			//TODO: this seems to cause a bug when rotating the device (and the activity gets destroyed)
			//After recreating the activity, the value fields all have the same content.
			if ((int)Android.OS.Build.VERSION.SdkInt >= 11)
				FindViewById<TextView>(Resource.Id.value).SetTextIsSelectable(true);
		}
		
		private void SetText(int resId, String str) {
			if (str != null) {
				TextView tvTitle = (TextView) FindViewById(resId);
				tvTitle.Text = str;
			}
			
		}
	}

}

