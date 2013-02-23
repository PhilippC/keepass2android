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

using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;

using System;
using Android.Runtime;


namespace keepass2android.view
{
	
	public class EntryContentsView : LinearLayout {

		public EntryContentsView (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{

		}

		public EntryContentsView(Context context):base(context, null) {
			inflate(context);
		}
		
		public EntryContentsView(Context context, IAttributeSet attrs): base(context, attrs) {
			
			inflate(context);
		}
		
		private void inflate(Context context) {
			LayoutInflater inflater = (LayoutInflater) Context.GetSystemService(Context.LayoutInflaterService);
			inflater.Inflate(Resource.Layout.entry_view_contents, this);

		}

		/*
		 * doesn't compile with mono for android
		 * 
		protected override LayoutParams GenerateDefaultLayoutParams() {
			return new LayoutParams(LayoutParams.FillParent, LayoutParams.WrapContent);
		}
		*/
	}

}

