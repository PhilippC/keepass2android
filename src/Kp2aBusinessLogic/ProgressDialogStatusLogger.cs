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
        string LastMessage { get; }
        string LastSubMessage { get; }
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

        private string _lastMessage;
        private string _lastSubMessage;
        public void UpdateMessage(string message)
        {
            _lastMessage = message;
        }

        public void UpdateSubMessage(string submessage)
        {
            _lastSubMessage = submessage;
        }

        public bool ContinueWork()
        {
            return true;
        }

        public void UpdateMessage(UiStringKey stringKey)
        {
            
        }

        public string LastMessage { get { return _lastMessage; } }
        public string LastSubMessage { get { return _lastSubMessage; } }
    }

    /// <summary>
    /// StatusLogger implementation which shows the progress in a progress dialog
    /// </summary>
    public class ProgressDialogUi: IProgressUi
    {
		private readonly IProgressDialog _progressDialog;
		
		private readonly Handler _handler;
		private string _message = "";
	    private string _submessage;
        private readonly IKp2aApp _app;

        public String LastSubMessage => _submessage;
	    public String LastMessage => _message;

		
		public ProgressDialogUi(IKp2aApp app, Handler handler, IProgressDialog pd)
        {
            _app = app;
			_progressDialog = pd;
			_handler = handler;
		}

        public void UpdateSubMessage(String submessage)
        {
            Kp2aLog.Log("status submessage: " + submessage);
            _submessage = submessage;
            if (_app != null && _progressDialog != null && _handler != null)
            {
                _handler.Post(() =>
                    {
                        Kp2aLog.Log("OPR: Starting posted SetMessage");
                        if (!String.IsNullOrEmpty(submessage))
                        {
                            _progressDialog.SetMessage(_message + " (" + submessage + ")");
                        }
                        else
                        {
                            _progressDialog.SetMessage(_message);
                        }
                        Kp2aLog.Log("OPR: Finished posted SetMessage");
                    }
                );
            }
        }

        public void Show()
        {
            _handler.Post(() =>
            {
                Kp2aLog.Log("OPR: Starting posted Show");
                _progressDialog?.Show();
                Kp2aLog.Log("OPR: Finished posted Show");

            });
        }

        public void Hide()
        {
            _handler.Post(() =>
            {
                Kp2aLog.Log("OPR: Starting posted Dismiss");
                _progressDialog?.Dismiss();
                Kp2aLog.Log("OPR: Finished posted Dismiss");

            });
        }

        public void UpdateMessage(string message)
        {
            Kp2aLog.Log("status message: " + message);
            _message = message;
            if (_app != null && _progressDialog != null && _handler != null)
            {
                _handler.Post(() =>
                {
                    Kp2aLog.Log("OPR: Starting posted SetMessage");
                    _progressDialog.SetMessage(message);
                    Kp2aLog.Log("OPR: Finishing posted SetMessage");
                });
            }
        }

    }


	
}

