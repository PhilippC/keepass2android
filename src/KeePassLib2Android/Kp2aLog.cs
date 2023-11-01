/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll.

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
using System.IO;
using Android;
using Android.App;
using Android.Content;
using Android.Preferences;
using KeePassLib.Serialization;

namespace keepass2android
{
	public static class Kp2aLog
	{
		private static bool? _logToFile;

		private static object _fileLocker = new object();

		public static void Log(string message)
		{
			if (message != null)
				Android.Util.Log.Debug("KP2A", message);
			if (LogToFile)
			{
				lock (_fileLocker)
				{
					try
					{
						using (var streamWriter = File.AppendText(LogFilename))
						{
							string stringToLog = DateTime.Now + ":" + DateTime.Now.Millisecond + " -- " + message;
							streamWriter.WriteLine(stringToLog);
						}
					}
					catch (Exception e)
					{
						Android.Util.Log.Debug("KP2A", "Couldn't write to log file. " + e);
					}
				}

			}

		}

		public static string LogFilename
		{
			get { return Application.Context.FilesDir.CanonicalPath +"/keepass2android.log"; }
		}

		public static bool LogToFile
		{
			get
			{
				if (_logToFile == null)
					_logToFile = File.Exists(LogFilename);
				return (bool) _logToFile;
			}
		}
		public static event EventHandler<Exception> OnUnexpectedError;

		public static void LogUnexpectedError(Exception exception)
		{
			Log(exception.ToString());
			if (OnUnexpectedError != null)
				OnUnexpectedError(null, exception);
		}

		public static void CreateLogFile()
		{
			if (!File.Exists(LogFilename))
			{
				File.Create(LogFilename).Dispose();
                _logToFile = true;
			}
			

		}

		public static void FinishLogFile()
		{
			if (File.Exists(LogFilename))
			{
				_logToFile = false;
				int count = 0;
				while (File.Exists(LogFilename + "." + count))
					count++;
                File.Move(LogFilename, LogFilename + "." + count);
				
			}
				
		}

		public static void SendLog(Context ctx)
		{
			if (!File.Exists(LogFilename))
				return;
			Intent sendIntent = new Intent();
			sendIntent.SetAction(Intent.ActionSend);
			sendIntent.PutExtra(Intent.ExtraText, File.ReadAllText(LogFilename));
			sendIntent.PutExtra(Intent.ExtraEmail, "crocoapps@gmail.com");
			sendIntent.PutExtra(Intent.ExtraSubject, "Keepass2Android log");
			sendIntent.SetType("text/plain");
			ctx.StartActivity(Intent.CreateChooser(sendIntent, "Send log to..."));
		}

        public static void LogTask(object task, string activityName)
        {
			Log($"Task in activity {activityName} changed to {task?.GetType()?.Name ?? "null"}");
        }
    }
}