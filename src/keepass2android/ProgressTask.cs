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
using Java.Lang;

namespace keepass2android
{
	public class ProgressTask {
		private Context mCtx;
		private Handler mHandler;
		private RunnableOnFinish mTask;
		private ProgressDialog mPd;

		public ProgressTask(Context ctx, RunnableOnFinish task, int messageId) {
			mCtx = ctx;
			mTask = task;
			mHandler = new Handler();
			
			// Show process dialog
			mPd = new ProgressDialog(mCtx);
			mPd.SetTitle(ctx.GetText(Resource.String.progress_title));
			mPd.SetMessage(ctx.GetText(messageId));
			
			// Set code to run when this is finished
			mTask.setStatus(new UpdateStatus(ctx, mHandler, mPd));
			mTask.mFinish = new AfterTask(task.mFinish, mHandler, mPd);
			
		}
		
		public void run() {
			// Show process dialog
			mPd.Show();
			
			
			// Start Thread to Run task
			Thread t = new Thread(mTask.run);
			t.Start();
			
		}
		
		private class AfterTask : OnFinish {

			ProgressDialog mPd;

			public AfterTask (OnFinish finish, Handler handler, ProgressDialog pd): base(finish, handler)
			{
				mPd = pd;
			}

			public override void run() {
				base.run();
				
				// Remove the progress dialog
				mHandler.Post(delegate() {mPd.Dismiss();});
				
			}
			
		}
		
	}
}

