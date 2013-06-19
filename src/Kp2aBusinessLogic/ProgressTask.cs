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

using Android.App;
using Android.Content;
using Android.OS;
using Java.Lang;

namespace keepass2android
{
	/// <summary>
	/// Class to run a task while a progress dialog is shown
	/// </summary>
	public class ProgressTask {
		private readonly Handler _handler;
		private readonly RunnableOnFinish _task;
		private readonly ProgressDialog _progressDialog;
        private readonly IKp2aApp _app;

		public ProgressTask(IKp2aApp app, Context ctx, RunnableOnFinish task, UiStringKey messageKey) {
			_task = task;
			_handler = new Handler();
            _app = app;
			
			// Show process dialog
			_progressDialog = new ProgressDialog(ctx);
			_progressDialog.SetTitle(_app.GetResourceString(UiStringKey.progress_title));
            _progressDialog.SetMessage(_app.GetResourceString(messageKey));
			
			// Set code to run when this is finished
			_task.SetStatus(new UpdateStatus(_app, _handler, _progressDialog));
			_task.OnFinishToRun = new AfterTask(task.OnFinishToRun, _handler, _progressDialog);
			
		}
		
		public void Run() {
			// Show process dialog
			_progressDialog.Show();
			
			
			// Start Thread to Run task
			Thread t = new Thread(_task.Run);
			t.Start();
			
		}
		
		private class AfterTask : OnFinish {
			readonly ProgressDialog _progressDialog;

			public AfterTask (OnFinish finish, Handler handler, ProgressDialog pd): base(finish, handler)
			{
				_progressDialog = pd;
			}

			public override void Run() {
				base.Run();
				
				// Remove the progress dialog
				Handler.Post(delegate {_progressDialog.Dismiss();});
				
			}
			
		}
		
	}
}

