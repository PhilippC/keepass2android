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
using KeePassLib.Interfaces;

namespace keepass2android
{
	public class UpdateStatus: IStatusLogger {
		private ProgressDialog mPD;
		private Context mCtx;
		private Handler mHandler;
		
		public UpdateStatus() {
			
		}
		
		public UpdateStatus(Context ctx, Handler handler, ProgressDialog pd) {
			mCtx = ctx;
			mPD = pd;
			mHandler = handler;
		}
		
		public void updateMessage(int resId) {
			if ( mCtx != null && mPD != null && mHandler != null ) {
				mHandler.Post( () => {mPD.SetMessage(mCtx.GetString(resId));});
			}
		}

		public void updateMessage (String message)
		{
			if ( mCtx != null && mPD != null && mHandler != null ) {
				mHandler.Post(() => {mPD.SetMessage(message); } );
			}
		}

		#region IStatusLogger implementation

		public void StartLogging (string strOperation, bool bWriteOperationToLog)
		{

		}

		public void EndLogging ()
		{

		}

		public bool SetProgress (uint uPercent)
		{
			return true;
		}

		public bool SetText (string strNewText, LogStatusType lsType)
		{
			updateMessage(strNewText);
			return true;
		}

		public bool ContinueWork ()
		{
			return true;
		}

		#endregion

	}
}

