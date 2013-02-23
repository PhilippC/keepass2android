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
using Android.Util;

namespace keepass2android.view
{
	
	public class EntrySection : LinearLayout {
		
		public EntrySection(Context context): base(context, null) {
			inflate (context,null, null);
		}
		
		public EntrySection(Context context, IAttributeSet attrs): base(context, attrs) {
			inflate (context,null, null);
		}
		
		public EntrySection(Context context, IAttributeSet attrs, String title, String value): base(context, attrs) {
			
			inflate(context, title, value);
		}

		public EntrySection (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}
		
		
		private void inflate(Context context, String title, String value) {
			LayoutInflater inflater = (LayoutInflater) Context.GetSystemService(Context.LayoutInflaterService);
			inflater.Inflate(Resource.Layout.entry_section, this);
			
			setText(Resource.Id.title, title);
			setText(Resource.Id.value, value);
		}
		
		private void setText(int resId, String str) {
			if (str != null) {
				TextView tvTitle = (TextView) FindViewById(resId);
				tvTitle.Text = str;
			}
			
		}
	}

}

