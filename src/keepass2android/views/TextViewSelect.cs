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
using Android.Content;
using Android.Runtime;
using Android.Widget;
using Android.Text.Method;
using Android.Util;
using Java.Lang;

namespace keepass2android.view
{
	
	public class TextViewSelect : TextView {

		public TextViewSelect (IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			Initialize();
		}


		public TextViewSelect(Context context): base(context, null, Android.Resource.Attribute.TextViewStyle) {
			Initialize();
		}
		
		public TextViewSelect(Context context, IAttributeSet attrs): base(context, attrs, Android.Resource.Attribute.TextViewStyle) {
			Initialize();
		}

		void Initialize ()
		{
			Focusable = true;
			FocusableInTouchMode = true;
		}
		
		public TextViewSelect(Context context, IAttributeSet attrs, int defStyle): base(context, attrs, defStyle) {

			Initialize ();
		}
		
		
		
		protected override IMovementMethod DefaultMovementMethod
		{
			get
			{
				return ArrowKeyMovementMethod.Instance;
			}
		}
		
		
		protected override bool DefaultEditable
		{
			get
			{
				return false;
			}
		}
		
		
		public override void SetText(ICharSequence text, BufferType type) {
			base.SetText (text, BufferType.Editable);

		}
		
	}

}

