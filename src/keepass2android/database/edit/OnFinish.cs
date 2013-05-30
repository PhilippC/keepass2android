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

namespace keepass2android
{
	public abstract class OnFinish
	{
		protected bool mSuccess;
		protected String mMessage;
		
		protected OnFinish mOnFinish;
		protected Handler mHandler;
		
		public OnFinish() {
		}
		
		public OnFinish(Handler handler) {
			mOnFinish = null;
			mHandler = handler;
		}
		
		public OnFinish(OnFinish finish, Handler handler) {
			mOnFinish = finish;
			mHandler = handler;
		}
		
		public OnFinish(OnFinish finish) {
			mOnFinish = finish;
			mHandler = null;
		}
		
		public void setResult(bool success, String message) {
			mSuccess = success;
			mMessage = message;
		}
		
		public void setResult(bool success) {
			mSuccess = success;
		}
		
		public virtual void run() {
			if ( mOnFinish != null ) {
				// Pass on result on call finish
				mOnFinish.setResult(mSuccess, mMessage);
				
				if ( mHandler != null ) {
					mHandler.Post(mOnFinish.run); 
				} else {
					mOnFinish.run();
				}
			}
		}
		
		protected void displayMessage(Context ctx) {
			displayMessage(ctx, mMessage);
		}

		public static void displayMessage(Context ctx, string message)
		{
			if ( !String.IsNullOrEmpty(message) ) {
				Toast.MakeText(ctx, message, ToastLength.Long).Show();
			}
		}
	}
}

