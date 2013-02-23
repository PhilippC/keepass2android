/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2012 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

using KeePassLib.Native;

namespace KeePassLib.Utility
{
	/// <summary>
	/// A class containing various static path utility helper methods (like
	/// stripping extension from a file, etc.).
	/// </summary>
	public static class UrlUtil
	{
		private static readonly char[] m_vDirSeps = new char[] { '\\', '/',
			Path.DirectorySeparatorChar };

		/// <summary>
		/// Get the directory (path) of a file name. The returned string is
		/// terminated by a directory separator character. Example:
		/// passing <c>C:\\My Documents\\My File.kdb</c> in <paramref name="strFile" />
		/// would produce this string: <c>C:\\My Documents\\</c>.
		/// </summary>
		/// <param name="strFile">Full path of a file.</param>
		/// <param name="bAppendTerminatingChar">Append a terminating directory separator
		/// character to the returned path.</param>
		/// <param name="bEnsureValidDirSpec">If <c>true</c>, the returned path
		/// is guaranteed to be a valid directory path (for example <c>X:\\</c> instead
		/// of <c>X:</c>, overriding <paramref name="bAppendTerminatingChar" />).
		/// This should only be set to <c>true</c>, if the returned path is directly
		/// passed to some directory API.</param>
		/// <returns>Directory of the file. The return value is an empty string
		/// (<c>""</c>) if the input parameter is <c>null</c>.</returns>
		public static string GetFileDirectory(string strFile, bool bAppendTerminatingChar,
			bool bEnsureValidDirSpec)
		{
			Debug.Assert(strFile != null);
			if(strFile == null) throw new ArgumentNullException("strFile");

			int nLastSep = strFile.LastIndexOfAny(m_vDirSeps);
			if(nLastSep < 0) return strFile; // None

			if(bEnsureValidDirSpec && (nLastSep == 2) && (strFile[1] == ':') &&
				(strFile[2] == '\\')) // Length >= 3 and Windows root directory
				bAppendTerminatingChar = true;

			if(!bAppendTerminatingChar) return strFile.Substring(0, nLastSep);
			return EnsureTerminatingSeparator(strFile.Substring(0, nLastSep), false);
		}

		/// <summary>
		/// Gets the file name of the specified file (full path). Example:
		/// if <paramref name="strPath" /> is <c>C:\\My Documents\\My File.kdb</c>
		/// the returned string is <c>My File.kdb</c>.
		/// </summary>
		/// <param name="strPath">Full path of a file.</param>
		/// <returns>File name of the specified file. The return value is
		/// an empty string (<c>""</c>) if the input parameter is <c>null</c>.</returns>
		public static string GetFileName(string strPath)
		{
			Debug.Assert(strPath != null); if(strPath == null) throw new ArgumentNullException("strPath");

			int nLastSep = strPath.LastIndexOfAny(m_vDirSeps);

			if(nLastSep < 0) return strPath;
			if(nLastSep >= (strPath.Length - 1)) return string.Empty;

			return strPath.Substring(nLastSep + 1);
		}

		/// <summary>
		/// Strip the extension of a file.
		/// </summary>
		/// <param name="strPath">Full path of a file with extension.</param>
		/// <returns>File name without extension.</returns>
		public static string StripExtension(string strPath)
		{
			Debug.Assert(strPath != null); if(strPath == null) throw new ArgumentNullException("strPath");

			int nLastDirSep = strPath.LastIndexOfAny(m_vDirSeps);
			int nLastExtDot = strPath.LastIndexOf('.');

			if(nLastExtDot <= nLastDirSep) return strPath;

			return strPath.Substring(0, nLastExtDot);
		}

		/// <summary>
		/// Get the extension of a file.
		/// </summary>
		/// <param name="strPath">Full path of a file with extension.</param>
		/// <returns>Extension without prepending dot.</returns>
		public static string GetExtension(string strPath)
		{
			Debug.Assert(strPath != null); if(strPath == null) throw new ArgumentNullException("strPath");

			int nLastDirSep = strPath.LastIndexOfAny(m_vDirSeps);
			int nLastExtDot = strPath.LastIndexOf('.');

			if(nLastExtDot <= nLastDirSep) return string.Empty;
			if(nLastExtDot == (strPath.Length - 1)) return string.Empty;

			return strPath.Substring(nLastExtDot + 1);
		}

		/// <summary>
		/// Ensure that a path is terminated with a directory separator character.
		/// </summary>
		/// <param name="strPath">Input path.</param>
		/// <param name="bUrl">If <c>true</c>, a slash (<c>/</c>) is appended to
		/// the string if it's not terminated already. If <c>false</c>, the
		/// default system directory separator character is used.</param>
		/// <returns>Path having a directory separator as last character.</returns>
		public static string EnsureTerminatingSeparator(string strPath, bool bUrl)
		{
			Debug.Assert(strPath != null); if(strPath == null) throw new ArgumentNullException("strPath");

			int nLength = strPath.Length;
			if(nLength <= 0) return string.Empty;

			char chLast = strPath[nLength - 1];

			for(int i = 0; i < m_vDirSeps.Length; ++i)
			{
				if(chLast == m_vDirSeps[i]) return strPath;
			}

			if(bUrl) return (strPath + '/');
			return (strPath + Path.DirectorySeparatorChar);
		}

		/* /// <summary>
		/// File access mode enumeration. Used by the <c>FileAccessible</c>
		/// method.
		/// </summary>
		public enum FileAccessMode
		{
			/// <summary>
			/// Opening a file in read mode. The specified file must exist.
			/// </summary>
			Read = 0,

			/// <summary>
			/// Opening a file in create mode. If the file exists already, it
			/// will be overwritten. If it doesn't exist, it will be created.
			/// The return value is <c>true</c>, if data can be written to the
			/// file.
			/// </summary>
			Create
		} */

		/* /// <summary>
		/// Test if a specified path is accessible, either in read or write mode.
		/// </summary>
		/// <param name="strFilePath">Path to test.</param>
		/// <param name="fMode">Requested file access mode.</param>
		/// <returns>Returns <c>true</c> if the specified path is accessible in
		/// the requested mode, otherwise the return value is <c>false</c>.</returns>
		public static bool FileAccessible(string strFilePath, FileAccessMode fMode)
		{
			Debug.Assert(strFilePath != null);
			if(strFilePath == null) throw new ArgumentNullException("strFilePath");

			if(fMode == FileAccessMode.Read)
			{
				FileStream fs;

				try { fs = File.OpenRead(strFilePath); }
				catch(Exception) { return false; }
				if(fs == null) return false;

				fs.Close();
				return true;
			}
			else if(fMode == FileAccessMode.Create)
			{
				FileStream fs;

				try { fs = File.Create(strFilePath); }
				catch(Exception) { return false; }
				if(fs == null) return false;

				fs.Close();
				return true;
			}

			return false;
		} */

		public static string GetQuotedAppPath(string strPath)
		{
			int nFirst = strPath.IndexOf('\"');
			int nSecond = strPath.IndexOf('\"', nFirst + 1);

			if((nFirst >= 0) && (nSecond >= 0))
				return strPath.Substring(nFirst + 1, nSecond - nFirst - 1);

			return strPath;
		}

		public static string FileUrlToPath(string strUrl)
		{
			Debug.Assert(strUrl != null);
			if(strUrl == null) throw new ArgumentNullException("strUrl");

			string str = strUrl;
			if(str.StartsWith(@"file:///", StrUtil.CaseIgnoreCmp))
				str = str.Substring(8, str.Length - 8);

			str = str.Replace('/', Path.DirectorySeparatorChar);

			return str;
		}

		public static bool UnhideFile(string strFile)
		{
#if KeePassLibSD
			return false;
#else
			if(strFile == null) throw new ArgumentNullException("strFile");

			try
			{
				FileAttributes fa = File.GetAttributes(strFile);
				if((long)(fa & FileAttributes.Hidden) == 0) return false;

				return HideFile(strFile, false);
			}
			catch(Exception) { }

			return false;
#endif
		}

		public static bool HideFile(string strFile, bool bHide)
		{
#if KeePassLibSD
			return false;
#else
			if(strFile == null) throw new ArgumentNullException("strFile");

			try
			{
				FileAttributes fa = File.GetAttributes(strFile);

				if(bHide) fa = ((fa & ~FileAttributes.Normal) | FileAttributes.Hidden);
				else // Unhide
				{
					fa &= ~FileAttributes.Hidden;
					if((long)fa == 0) fa |= FileAttributes.Normal;
				}

				File.SetAttributes(strFile, fa);
				return true;
			}
			catch(Exception) { }

			return false;
#endif
		}

		public static string MakeRelativePath(string strBaseFile, string strTargetFile)
		{
			if(strBaseFile == null) throw new ArgumentNullException("strBasePath");
			if(strTargetFile == null) throw new ArgumentNullException("strTargetPath");
			if(strBaseFile.Length == 0) return strTargetFile;
			if(strTargetFile.Length == 0) return string.Empty;

			// Test whether on different Windows drives
			if((strBaseFile.Length >= 3) && (strTargetFile.Length >= 3))
			{
				if((strBaseFile[1] == ':') && (strTargetFile[1] == ':') &&
					(strBaseFile[2] == '\\') && (strTargetFile[2] == '\\') &&
					(strBaseFile[0] != strTargetFile[0]))
					return strTargetFile;
			}

			if(NativeLib.IsUnix())
			{
				bool bBaseUnc = IsUncPath(strBaseFile);
				bool bTargetUnc = IsUncPath(strTargetFile);
				if((!bBaseUnc && bTargetUnc) || (bBaseUnc && !bTargetUnc))
					return strTargetFile;

				string strBase = GetShortestAbsolutePath(strBaseFile);
				string strTarget = GetShortestAbsolutePath(strTargetFile);
				string[] vBase = strBase.Split(m_vDirSeps);
				string[] vTarget = strTarget.Split(m_vDirSeps);

				int i = 0;
				while((i < (vBase.Length - 1)) && (i < (vTarget.Length - 1)) &&
					(vBase[i] == vTarget[i])) { ++i; }

				StringBuilder sbRel = new StringBuilder();
				for(int j = i; j < (vBase.Length - 1); ++j)
				{
					if(sbRel.Length > 0) sbRel.Append(Path.DirectorySeparatorChar);
					sbRel.Append("..");
				}
				for(int k = i; k < vTarget.Length; ++k)
				{
					if(sbRel.Length > 0) sbRel.Append(Path.DirectorySeparatorChar);
					sbRel.Append(vTarget[k]);
				}

				return sbRel.ToString();
			}

#if KeePassLibSD
			return strTargetFile;
#else
			try // Windows
			{
				const int nMaxPath = NativeMethods.MAX_PATH * 2;
				StringBuilder sb = new StringBuilder(nMaxPath + 2);
				if(NativeMethods.PathRelativePathTo(sb, strBaseFile, 0,
					strTargetFile, 0) == false)
					return strTargetFile;

				string str = sb.ToString();
				while(str.StartsWith(".\\")) str = str.Substring(2, str.Length - 2);

				return str;
			}
			catch(Exception) { Debug.Assert(false); return strTargetFile; }
#endif
		}

		public static string MakeAbsolutePath(string strBaseFile, string strTargetFile)
		{
			if(strBaseFile == null) throw new ArgumentNullException("strBasePath");
			if(strTargetFile == null) throw new ArgumentNullException("strTargetPath");
			if(strBaseFile.Length == 0) return strTargetFile;
			if(strTargetFile.Length == 0) return string.Empty;

			if(IsAbsolutePath(strTargetFile)) return strTargetFile;

			string strBaseDir = GetFileDirectory(strBaseFile, true, false);
			return GetShortestAbsolutePath(strBaseDir + strTargetFile);
		}

		public static bool IsAbsolutePath(string strPath)
		{
			if(strPath == null) throw new ArgumentNullException("strPath");
			if(strPath.Length == 0) return false;

			if(IsUncPath(strPath)) return true;

			try { return Path.IsPathRooted(strPath); }
			catch(Exception) { Debug.Assert(false); }

			return true;
		}

		public static string GetShortestAbsolutePath(string strPath)
		{
			if(strPath == null) throw new ArgumentNullException("strPath");
			if(strPath.Length == 0) return string.Empty;

			// Path.GetFullPath is incompatible with UNC paths traversing over
			// different server shares (which are created by PathRelativePathTo);
			// we need to build the absolute path on our own...
			if(IsUncPath(strPath))
			{
				char chSep = strPath[0];
				Debug.Assert(Array.IndexOf<char>(m_vDirSeps, chSep) >= 0);

				List<string> l = new List<string>();
#if !KeePassLibSD
				string[] v = strPath.Split(m_vDirSeps, StringSplitOptions.None);
#else
				string[] v = strPath.Split(m_vDirSeps);
#endif
				Debug.Assert((v.Length >= 3) && (v[0].Length == 0) &&
					(v[1].Length == 0));

				foreach(string strPart in v)
				{
					if(strPart.Equals(".")) continue;
					else if(strPart.Equals(".."))
					{
						if(l.Count > 0) l.RemoveAt(l.Count - 1);
						else { Debug.Assert(false); }
					}
					else l.Add(strPart); // Do not ignore zero length parts
				}

				StringBuilder sb = new StringBuilder();
				for(int i = 0; i < l.Count; ++i)
				{
					// Don't test length of sb, might be 0 due to initial UNC seps
					if(i > 0) sb.Append(chSep);

					sb.Append(l[i]);
				}

				return sb.ToString();
			}

			string str;
			try { str = Path.GetFullPath(strPath); }
			catch(Exception) { Debug.Assert(false); return strPath; }

			Debug.Assert(str.IndexOf("\\..\\") < 0);
			foreach(char ch in m_vDirSeps)
			{
				string strSep = new string(ch, 1);
				str = str.Replace(strSep + "." + strSep, strSep);
			}

			return str;
		}

		public static int GetUrlLength(string strText, int nOffset)
		{
			if(strText == null) throw new ArgumentNullException("strText");
			if(nOffset > strText.Length) throw new ArgumentException(); // Not >= (0 len)

			int iPosition = nOffset, nLength = 0, nStrLen = strText.Length;

			while(iPosition < nStrLen)
			{
				char ch = strText[iPosition];
				++iPosition;

				if((ch == ' ') || (ch == '\t') || (ch == '\r') || (ch == '\n'))
					break;

				++nLength;
			}

			return nLength;
		}

		public static string RemoveScheme(string strUrl)
		{
			if(string.IsNullOrEmpty(strUrl)) return string.Empty;

			int nNetScheme = strUrl.IndexOf(@"://", StrUtil.CaseIgnoreCmp);
			int nShScheme = strUrl.IndexOf(@":/", StrUtil.CaseIgnoreCmp);
			int nSmpScheme = strUrl.IndexOf(@":", StrUtil.CaseIgnoreCmp);

			if((nNetScheme < 0) && (nShScheme < 0) && (nSmpScheme < 0))
				return strUrl; // No scheme

			int nMin = Math.Min(Math.Min((nNetScheme >= 0) ? nNetScheme : int.MaxValue,
				(nShScheme >= 0) ? nShScheme : int.MaxValue),
				(nSmpScheme >= 0) ? nSmpScheme : int.MaxValue);

			if(nMin == nNetScheme) return strUrl.Substring(nMin + 3);
			if(nMin == nShScheme) return strUrl.Substring(nMin + 2);
			return strUrl.Substring(nMin + 1);
		}

		public static string ConvertSeparators(string strPath)
		{
			return ConvertSeparators(strPath, Path.DirectorySeparatorChar);
		}

		public static string ConvertSeparators(string strPath, char chSeparator)
		{
			if(string.IsNullOrEmpty(strPath)) return string.Empty;

			strPath = strPath.Replace('/', chSeparator);
			strPath = strPath.Replace('\\', chSeparator);

			return strPath;
		}

		public static bool IsUncPath(string strPath)
		{
			if(strPath == null) throw new ArgumentNullException("strPath");

			return (strPath.StartsWith("\\\\") || strPath.StartsWith("//"));
		}

		public static string FilterFileName(string strName)
		{
			if(strName == null) { Debug.Assert(false); return string.Empty; }

			string str = strName;

			str = str.Replace('/', '-');
			str = str.Replace('\\', '-');
			str = str.Replace(":", string.Empty);
			str = str.Replace("*", string.Empty);
			str = str.Replace("?", string.Empty);
			str = str.Replace("\"", string.Empty);
			str = str.Replace(@"'", string.Empty);
			str = str.Replace('<', '(');
			str = str.Replace('>', ')');
			str = str.Replace('|', '-');

			return str;
		}

		/// <summary>
		/// Get the host component of an URL.
		/// This method is faster and more fault-tolerant than creating
		/// an <code>Uri</code> object and querying its <code>Host</code>
		/// property.
		/// </summary>
		/// <example>
		/// For the input <code>s://u:p@d.tld:p/p?q#f</code> the return
		/// value is <code>d.tld</code>.
		/// </example>
		public static string GetHost(string strUrl)
		{
			if(strUrl == null) { Debug.Assert(false); return string.Empty; }

			StringBuilder sb = new StringBuilder();
			bool bInExtHost = false;
			for(int i = 0; i < strUrl.Length; ++i)
			{
				char ch = strUrl[i];
				if(bInExtHost)
				{
					if(ch == '/')
					{
						if(sb.Length == 0) { } // Ignore leading '/'s
						else break;
					}
					else sb.Append(ch);
				}
				else // !bInExtHost
				{
					if(ch == ':') bInExtHost = true;
				}
			}

			string str = sb.ToString();
			if(str.Length == 0) str = strUrl;

			// Remove the login part
			int nLoginLen = str.IndexOf('@');
			if(nLoginLen >= 0) str = str.Substring(nLoginLen + 1);

			// Remove the port
			int iPort = str.LastIndexOf(':');
			if(iPort >= 0) str = str.Substring(0, iPort);

			return str;
		}

		public static bool AssemblyEquals(string strExt, string strShort)
		{
			if((strExt == null) || (strShort == null)) { Debug.Assert(false); return false; }

			if(strExt.Equals(strShort, StrUtil.CaseIgnoreCmp) ||
				strExt.StartsWith(strShort + ",", StrUtil.CaseIgnoreCmp))
				return true;

			if(!strShort.EndsWith(".dll", StrUtil.CaseIgnoreCmp))
			{
				if(strExt.Equals(strShort + ".dll", StrUtil.CaseIgnoreCmp) ||
					strExt.StartsWith(strShort + ".dll,", StrUtil.CaseIgnoreCmp))
					return true;
			}

			if(!strShort.EndsWith(".exe", StrUtil.CaseIgnoreCmp))
			{
				if(strExt.Equals(strShort + ".exe", StrUtil.CaseIgnoreCmp) ||
					strExt.StartsWith(strShort + ".exe,", StrUtil.CaseIgnoreCmp))
					return true;
			}

			return false;
		}

		public static string GetTempPath()
		{
			string strDir;
			if(NativeLib.IsUnix())
				strDir = NativeMethods.GetUserRuntimeDir();
			else strDir = Path.GetTempPath();

			try
			{
				if(Directory.Exists(strDir) == false)
					Directory.CreateDirectory(strDir);
			}
			catch(Exception) { Debug.Assert(false); }

			return strDir;
		}
	}
}
