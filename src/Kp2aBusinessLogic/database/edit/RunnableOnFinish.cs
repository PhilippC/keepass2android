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
using Android.App;
using Android.Content;

namespace keepass2android
{

	public abstract class RunnableOnFinish  {
		
		protected OnFinish _onFinishToRun;
		public ProgressDialogStatusLogger StatusLogger = new ProgressDialogStatusLogger(); //default: empty but not null
	    private Activity _activeActivity;

	    protected RunnableOnFinish(Activity activeActivity, OnFinish finish)
	    {
	        _activeActivity = activeActivity;
			_onFinishToRun = finish;
		}

		public OnFinish OnFinishToRun
		{
			get { return _onFinishToRun; }
			set { _onFinishToRun = value; }
		}

	    public Activity ActiveActivity
	    {
	        get { return _activeActivity; }
	        set
            {
	            _activeActivity = value;
	            if (_onFinishToRun != null)
	                _onFinishToRun.ActiveActivity = _activeActivity;
	        }
	    }

        protected void Finish(bool result, String message, bool importantMessage = false, Exception exception = null) {
			if ( OnFinishToRun != null ) {
				OnFinishToRun.SetResult(result, message, importantMessage, exception);
				OnFinishToRun.Run();
			}
		}
		
		protected void Finish(bool result) {
			if ( OnFinishToRun != null ) {
				OnFinishToRun.SetResult(result);
				OnFinishToRun.Run();
			}
		}
		
		public void SetStatusLogger(ProgressDialogStatusLogger status) {
			if (OnFinishToRun != null)
			{
				OnFinishToRun.StatusLogger = status;
			}
			StatusLogger = status;
		}
		
		public abstract void Run();
	}
}

