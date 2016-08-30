/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2016 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;

namespace KeePassLib.Interfaces
{
	/// <summary>
	/// Status message types.
	/// </summary>
	public enum LogStatusType
	{
		/// <summary>
		/// Default type: simple information type.
		/// </summary>
		Info = 0,

		/// <summary>
		/// Warning message.
		/// </summary>
		Warning,

		/// <summary>
		/// Error message.
		/// </summary>
		Error,

		/// <summary>
		/// Additional information. Depends on lines above.
		/// </summary>
		AdditionalInfo
	}

	/// <summary>
	/// Status logging interface.
	/// </summary>
	public interface IStatusLogger
	{
		/// <summary>
		/// Function which needs to be called when logging is started.
		/// </summary>
		/// <param name="strOperation">This string should roughly describe
		/// the operation, of which the status is logged.</param>
		/// <param name="bWriteOperationToLog">Specifies whether the
		/// operation is written to the log or not.</param>
		void StartLogging(string strOperation, bool bWriteOperationToLog);

		/// <summary>
		/// Function which needs to be called when logging is ended
		/// (i.e. when no more messages will be logged and when the
		/// percent value won't change any more).
		/// </summary>
		void EndLogging();

		/// <summary>
		/// Set the current progress in percent.
		/// </summary>
		/// <param name="uPercent">Percent of work finished.</param>
		/// <returns>Returns <c>true</c> if the caller should continue
		/// the current work.</returns>
		bool SetProgress(uint uPercent);

		/// <summary>
		/// Set the current status text.
		/// </summary>
		/// <param name="strNewText">Status text.</param>
		/// <param name="lsType">Type of the message.</param>
		/// <returns>Returns <c>true</c> if the caller should continue
		/// the current work.</returns>
		bool SetText(string strNewText, LogStatusType lsType);

		/// <summary>
		/// Check if the user cancelled the current work.
		/// </summary>
		/// <returns>Returns <c>true</c> if the caller should continue
		/// the current work.</returns>
		bool ContinueWork();
	}

	public sealed class NullStatusLogger : IStatusLogger
	{
		public void StartLogging(string strOperation, bool bWriteOperationToLog) { }
		public void EndLogging() { }
		public bool SetProgress(uint uPercent) { return true; }
		public bool SetText(string strNewText, LogStatusType lsType) { return true; }
		public bool ContinueWork() { return true; }
	}
}
