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
	/// <summary>
	/// StatusLogger implementation which shows the progress in a progress dialog
	/// </summary>
	public class ProgressDialogStatusLogger: IStatusLogger {
		private readonly IProgressDialog _progressDialog;
		readonly IKp2aApp _app;
		private readonly Handler _handler;
		private string _message = "";

		public ProgressDialogStatusLogger() {
			
		}
		
		public ProgressDialogStatusLogger(IKp2aApp app, Handler handler, IProgressDialog pd) {
			_app = app;
			_progressDialog = pd;
			_handler = handler;
		}
		
		public void UpdateMessage(UiStringKey stringKey) {
			if (_app != null)
				UpdateMessage(_app.GetResourceString(stringKey));
		}

		public void UpdateMessage (String message)
		{
			_message = message;
			if ( _app!= null && _progressDialog != null && _handler != null ) {
				_handler.Post(() => {_progressDialog.SetMessage(message); } );
			}
		}

		public void UpdateSubMessage(String submessage)
		{
			if (_app != null && _progressDialog != null && _handler != null)
			{
				_handler.Post(() => 
				{ 
					if (String.IsNullOrEmpty(submessage))
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

		public bool ContinueWork ()
		{
			return true;
		}

		#endregion

	}
}

