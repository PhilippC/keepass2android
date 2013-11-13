/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2013 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Collections.Specialized;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

using KeePassLib.Resources;
using KeePassLib.Serialization;

namespace KeePassLib.Utility
{
	public sealed class MessageServiceEventArgs : EventArgs
	{
		private string m_strTitle = string.Empty;
		private string m_strText = string.Empty;
		private MessageBoxButtons m_msgButtons = MessageBoxButtons.OK;
		private MessageBoxIcon m_msgIcon = MessageBoxIcon.None;

		public string Title { get { return m_strTitle; } }
		public string Text { get { return m_strText; } }
		public MessageBoxButtons Buttons { get { return m_msgButtons; } }
		public MessageBoxIcon Icon { get { return m_msgIcon; } }

		public MessageServiceEventArgs() { }

		public MessageServiceEventArgs(string strTitle, string strText,
			MessageBoxButtons msgButtons, MessageBoxIcon msgIcon)
		{
			m_strTitle = (strTitle ?? string.Empty);
			m_strText = (strText ?? string.Empty);
			m_msgButtons = msgButtons;
			m_msgIcon = msgIcon;
		}
	}

	public static class MessageService
	{
		private static volatile uint m_uCurrentMessageCount = 0;

#if !KeePassLibSD
		private const MessageBoxIcon m_mbiInfo = MessageBoxIcon.Information;
		private const MessageBoxIcon m_mbiWarning = MessageBoxIcon.Warning;
		private const MessageBoxIcon m_mbiFatal = MessageBoxIcon.Error;

		private const MessageBoxOptions m_mboRtl = (MessageBoxOptions.RtlReading |
			MessageBoxOptions.RightAlign);
#else
		private const MessageBoxIcon m_mbiInfo = MessageBoxIcon.Asterisk;
		private const MessageBoxIcon m_mbiWarning = MessageBoxIcon.Exclamation;
		private const MessageBoxIcon m_mbiFatal = MessageBoxIcon.Hand;
#endif
		private const MessageBoxIcon m_mbiQuestion = MessageBoxIcon.Question;

		public static string NewLine
		{
#if !KeePassLibSD
			get { return Environment.NewLine; }
#else
			get { return "\r\n"; }
#endif
		}

		public static string NewParagraph
		{
#if !KeePassLibSD
			get { return Environment.NewLine + Environment.NewLine; }
#else
			get { return "\r\n\r\n"; }
#endif
		}

		public static uint CurrentMessageCount
		{
			get { return m_uCurrentMessageCount; }
		}

		public static event EventHandler<MessageServiceEventArgs> MessageShowing;

		private static string ObjectsToMessage(object[] vLines)
		{
			return ObjectsToMessage(vLines, false);
		}

		private static string ObjectsToMessage(object[] vLines, bool bFullExceptions)
		{
			if(vLines == null) return string.Empty;

			string strNewPara = MessageService.NewParagraph;

			StringBuilder sbText = new StringBuilder();
			bool bSeparator = false;

			foreach(object obj in vLines)
			{
				if(obj == null) continue;

				string strAppend = null;

				Exception exObj = (obj as Exception);
				string strObj = (obj as string);
#if (!KeePassLibSD && !KeePassRT)
				StringCollection scObj = (obj as StringCollection);
#endif

				if(exObj != null)
				{
					if(bFullExceptions)
						strAppend = StrUtil.FormatException(exObj);
					else if((exObj.Message != null) && (exObj.Message.Length > 0))
						strAppend = exObj.Message;
				}
#if (!KeePassLibSD && !KeePassRT)
				else if(scObj != null)
				{
					StringBuilder sb = new StringBuilder();
					foreach(string strCollLine in scObj)
					{
						if(sb.Length > 0) sb.AppendLine();
						sb.Append(strCollLine.TrimEnd());
					}
					strAppend = sb.ToString();
				}
#endif
				else if(strObj != null)
					strAppend = strObj;
				else
					strAppend = obj.ToString();

				if(!string.IsNullOrEmpty(strAppend))
				{
					if(bSeparator) sbText.Append(strNewPara);
					else bSeparator = true;

					sbText.Append(strAppend);
				}
			}

			return sbText.ToString();
		}

#if (!KeePassLibSD && !KeePassRT)
		internal static Form GetTopForm()
		{
			FormCollection fc = Application.OpenForms;
			if((fc == null) || (fc.Count == 0)) return null;

			return fc[fc.Count - 1];
		}
#endif

		private static DialogResult SafeShowMessageBox(string strText, string strTitle,
			MessageBoxButtons mb, MessageBoxIcon mi, MessageBoxDefaultButton mdb)
		{
#if (KeePassLibSD || KeePassRT)
			return MessageBox.Show(strText, strTitle, mb, mi, mdb);
#else
			IWin32Window wnd = null;
			try
			{
				Form f = GetTopForm();
				if((f != null) && f.InvokeRequired)
					return (DialogResult)f.Invoke(new SafeShowMessageBoxInternalDelegate(
						SafeShowMessageBoxInternal), f, strText, strTitle, mb, mi, mdb);
				else wnd = f;
			}
			catch(Exception) { Debug.Assert(false); }

			if(wnd == null)
			{
				if(StrUtil.RightToLeft)
					return MessageBox.Show(strText, strTitle, mb, mi, mdb, m_mboRtl);
				return MessageBox.Show(strText, strTitle, mb, mi, mdb);
			}

			try
			{
				if(StrUtil.RightToLeft)
					return MessageBox.Show(wnd, strText, strTitle, mb, mi, mdb, m_mboRtl);
				return MessageBox.Show(wnd, strText, strTitle, mb, mi, mdb);
			}
			catch(Exception) { Debug.Assert(false); }

			if(StrUtil.RightToLeft)
				return MessageBox.Show(strText, strTitle, mb, mi, mdb, m_mboRtl);
			return MessageBox.Show(strText, strTitle, mb, mi, mdb);
#endif
		}

#if (!KeePassLibSD && !KeePassRT)
		internal delegate DialogResult SafeShowMessageBoxInternalDelegate(IWin32Window iParent,
			string strText, string strTitle, MessageBoxButtons mb, MessageBoxIcon mi,
			MessageBoxDefaultButton mdb);

		internal static DialogResult SafeShowMessageBoxInternal(IWin32Window iParent,
			string strText, string strTitle, MessageBoxButtons mb, MessageBoxIcon mi,
			MessageBoxDefaultButton mdb)
		{
			if(StrUtil.RightToLeft)
				return MessageBox.Show(iParent, strText, strTitle, mb, mi, mdb, m_mboRtl);
			return MessageBox.Show(iParent, strText, strTitle, mb, mi, mdb);
		}
#endif

		public static void ShowInfo(params object[] vLines)
		{
			ShowInfoEx(null, vLines);
		}

		public static void ShowInfoEx(string strTitle, params object[] vLines)
		{
			++m_uCurrentMessageCount;

			strTitle = (strTitle ?? PwDefs.ShortProductName);
			string strText = ObjectsToMessage(vLines);

			if(MessageService.MessageShowing != null)
				MessageService.MessageShowing(null, new MessageServiceEventArgs(
					strTitle, strText, MessageBoxButtons.OK, m_mbiInfo));

			SafeShowMessageBox(strText, strTitle, MessageBoxButtons.OK, m_mbiInfo,
				MessageBoxDefaultButton.Button1);

			--m_uCurrentMessageCount;
		}

		public static void ShowWarning(params object[] vLines)
		{
			ShowWarningPriv(vLines, false);
		}

		internal static void ShowWarningExcp(params object[] vLines)
		{
			ShowWarningPriv(vLines, true);
		}

		private static void ShowWarningPriv(object[] vLines, bool bFullExceptions)
		{
			++m_uCurrentMessageCount;

			string strTitle = PwDefs.ShortProductName;
			string strText = ObjectsToMessage(vLines, bFullExceptions);

			if(MessageService.MessageShowing != null)
				MessageService.MessageShowing(null, new MessageServiceEventArgs(
					strTitle, strText, MessageBoxButtons.OK, m_mbiWarning));

			SafeShowMessageBox(strText, strTitle, MessageBoxButtons.OK, m_mbiWarning,
				MessageBoxDefaultButton.Button1);

			--m_uCurrentMessageCount;
		}

		public static void ShowFatal(params object[] vLines)
		{
			++m_uCurrentMessageCount;

			string strTitle = PwDefs.ShortProductName + @" - " + KLRes.FatalError;
			string strText = KLRes.FatalErrorText + MessageService.NewParagraph +
				KLRes.ErrorInClipboard + MessageService.NewParagraph +
				// Please send it to the KeePass developers.
				// KLRes.ErrorFeedbackRequest + MessageService.NewParagraph +
				ObjectsToMessage(vLines);

			try
			{
#if !KeePassLibSD
				Clipboard.Clear();
				Clipboard.SetText(ObjectsToMessage(vLines, true));
#else
				Clipboard.SetDataObject(ObjectsToMessage(vLines, true));
#endif
			}
			catch(Exception) { Debug.Assert(false); }

			if(MessageService.MessageShowing != null)
				MessageService.MessageShowing(null, new MessageServiceEventArgs(
					strTitle, strText, MessageBoxButtons.OK, m_mbiFatal));

			SafeShowMessageBox(strText, strTitle, MessageBoxButtons.OK, m_mbiFatal,
				MessageBoxDefaultButton.Button1);

			--m_uCurrentMessageCount;
		}

		public static DialogResult Ask(string strText, string strTitle,
			MessageBoxButtons mbb)
		{
			++m_uCurrentMessageCount;

			string strTextEx = (strText ?? string.Empty);
			string strTitleEx = (strTitle ?? PwDefs.ShortProductName);

			if(MessageService.MessageShowing != null)
				MessageService.MessageShowing(null, new MessageServiceEventArgs(
					strTitleEx, strTextEx, mbb, m_mbiQuestion));

			DialogResult dr = SafeShowMessageBox(strTextEx, strTitleEx, mbb,
				m_mbiQuestion, MessageBoxDefaultButton.Button1);

			--m_uCurrentMessageCount;
			return dr;
		}

		public static bool AskYesNo(string strText, string strTitle, bool bDefaultToYes,
			MessageBoxIcon mbi)
		{
			++m_uCurrentMessageCount;

			string strTextEx = (strText ?? string.Empty);
			string strTitleEx = (strTitle ?? PwDefs.ShortProductName);

			if(MessageService.MessageShowing != null)
				MessageService.MessageShowing(null, new MessageServiceEventArgs(
					strTitleEx, strTextEx, MessageBoxButtons.YesNo, mbi));

			DialogResult dr = SafeShowMessageBox(strTextEx, strTitleEx,
				MessageBoxButtons.YesNo, mbi, bDefaultToYes ?
				MessageBoxDefaultButton.Button1 : MessageBoxDefaultButton.Button2);

			--m_uCurrentMessageCount;
			return (dr == DialogResult.Yes);
		}

		public static bool AskYesNo(string strText, string strTitle, bool bDefaultToYes)
		{
			return AskYesNo(strText, strTitle, bDefaultToYes, m_mbiQuestion);
		}

		public static bool AskYesNo(string strText, string strTitle)
		{
			return AskYesNo(strText, strTitle, true, m_mbiQuestion);
		}

		public static bool AskYesNo(string strText)
		{
			return AskYesNo(strText, null, true, m_mbiQuestion);
		}

		public static void ShowLoadWarning(string strFilePath, Exception ex)
		{
			ShowLoadWarning(strFilePath, ex, false);
		}

		public static void ShowLoadWarning(string strFilePath, Exception ex,
			bool bFullException)
		{
			string str = string.Empty;

			if((strFilePath != null) && (strFilePath.Length > 0))
				str += strFilePath + MessageService.NewParagraph;

			str += KLRes.FileLoadFailed;

			if((ex != null) && (ex.Message != null) && (ex.Message.Length > 0))
			{
				str += MessageService.NewParagraph;
				if(!bFullException) str += ex.Message;
				else str += ObjectsToMessage(new object[] { ex }, true);
			}

			ShowWarning(str);
		}

		public static void ShowLoadWarning(IOConnectionInfo ioConnection, Exception ex)
		{
			if(ioConnection != null)
				ShowLoadWarning(ioConnection.GetDisplayName(), ex, false);
			else ShowWarning(ex);
		}

		public static void ShowSaveWarning(string strFilePath, Exception ex,
			bool bCorruptionWarning)
		{
			FileLockException fl = (ex as FileLockException);
			if(fl != null)
			{
				ShowWarning(fl.Message);
				return;
			}

			string str = string.Empty;
			if((strFilePath != null) && (strFilePath.Length > 0))
				str += strFilePath + MessageService.NewParagraph;

			str += KLRes.FileSaveFailed;

			if((ex != null) && (ex.Message != null) && (ex.Message.Length > 0))
				str += MessageService.NewParagraph + ex.Message;

			if(bCorruptionWarning)
				str += MessageService.NewParagraph + KLRes.FileSaveCorruptionWarning;

			ShowWarning(str);
		}

		public static void ShowSaveWarning(IOConnectionInfo ioConnection, Exception ex,
			bool bCorruptionWarning)
		{
			if(ioConnection != null)
				ShowSaveWarning(ioConnection.GetDisplayName(), ex, bCorruptionWarning);
			else ShowWarning(ex);
		}

		public static void ExternalIncrementMessageCount()
		{
			++m_uCurrentMessageCount;
		}

		public static void ExternalDecrementMessageCount()
		{
			--m_uCurrentMessageCount;
		}
	}
}
