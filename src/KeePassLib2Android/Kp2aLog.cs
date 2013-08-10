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
using System.IO;
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

		private static string LogFilename
		{
			get { return "/mnt/sdcard/keepass2android.log"; }
		}

		private static bool LogToFile
		{
			get
			{
				if (_logToFile == null)
					_logToFile = File.Exists(LogFilename);
				return (bool) _logToFile;
			}
		}
	}
}