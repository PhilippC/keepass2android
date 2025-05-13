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
using Android.OS;
using KeePassLib.Interfaces;

namespace keepass2android
{
    public interface IKp2aStatusLogger : IStatusLogger
    {
        void UpdateMessage(UiStringKey stringKey);
    }

    public interface IProgressUi
    {
        void Show();
        void Hide();
        void UpdateMessage(String message);
        void UpdateSubMessage(String submessage);
    }

    public interface IProgressUiProvider
    {
        IProgressUi? ProgressUi { get; }
    }


    public class Kp2aNullStatusLogger : IKp2aStatusLogger
    {
        public void StartLogging(string strOperation, bool bWriteOperationToLog)
        {
            
        }

        public void EndLogging()
        {
        }

        public bool SetProgress(uint uPercent)
        {
            return true;
        }

        public bool SetText(string strNewText, LogStatusType lsType)
        {
            return true;
        }

        public void UpdateMessage(string message)
        {
            
        }

        public void UpdateSubMessage(string submessage)
        {
            
        }

        public bool ContinueWork()
        {
            return true;
        }

        public void UpdateMessage(UiStringKey stringKey)
        {
            
        }
    }

    public abstract class Kp2aAppStatusLogger : IKp2aStatusLogger
    {
        protected IKp2aApp _app;

        public Kp2aAppStatusLogger(IKp2aApp app)
        {
            _app = app;
        }

        #region IStatusLogger implementation

        public void StartLogging(string strOperation, bool bWriteOperationToLog)
        {

        }

        public void EndLogging()
        {

        }

        public bool SetProgress(uint uPercent)
        {
            return true;
        }

        public bool SetText(string strNewText, LogStatusType lsType)
        {
            if (strNewText.StartsWith("KP2AKEY_"))
            {
                UiStringKey key;
                if (Enum.TryParse(strNewText.Substring("KP2AKEY_".Length), true, out key))
                {
                    UpdateMessage(_app.GetResourceString(key), lsType);
                    return true;
                }
            }
            UpdateMessage(strNewText, lsType);

            return true;
        }

        public abstract void UpdateMessage(string message);
        public abstract void UpdateSubMessage(string submessage);

        private void UpdateMessage(string message, LogStatusType lsType)
        {
            if (lsType == LogStatusType.AdditionalInfo)
            {
                UpdateSubMessage(message);
            }
            else
            {
                UpdateMessage(message);
            }
        }

        public bool ContinueWork()
        {
            return true;
        }

        #endregion

        public void UpdateMessage(UiStringKey stringKey)
        {
            if (_app != null)
                UpdateMessage(_app.GetResourceString(stringKey));
        }

    }

    /// <summary>
    /// StatusLogger implementation which shows the progress in a progress dialog
    /// </summary>
    public class ProgressDialogStatusLogger: Kp2aAppStatusLogger
    {
		private readonly IProgressDialog _progressDialog;
		
		private readonly Handler _handler;
		private string _message = "";
	    private string _submessage;

        public String SubMessage => _submessage;
	    public String Message => _message;

		
		public ProgressDialogStatusLogger(IKp2aApp app, Handler handler, IProgressDialog pd)
        : base(app){
			_progressDialog = pd;
			_handler = handler;
		}
		
		
		public override void UpdateMessage (String message)
		{
		    Kp2aLog.Log("status message: " + message);
            _message = message;
			if ( _app!= null && _progressDialog != null && _handler != null ) {
				_handler.Post(() => {_progressDialog.SetMessage(message); } );
			}
		}

		public override void UpdateSubMessage(String submessage)
		{
		    Kp2aLog.Log("status submessage: " + submessage);
		    _submessage = submessage;
			if (_app != null && _progressDialog != null && _handler != null)
			{
				_handler.Post(() => 
				{ 
					if (!String.IsNullOrEmpty(submessage))
					{
						_progressDialog.SetMessage(_message + " (" + submessage + ")");
					}
					else
					{
						_progressDialog.SetMessage(_message);
					}
				}
			);
			}
		}


	}
}

