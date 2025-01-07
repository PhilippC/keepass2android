/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2021 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using KeePassLib.Native;

namespace KeePassLib.Utility
{
	/// <summary>
	/// A class containing various static path utility helper methods (like
	/// stripping extension from a file, etc.).
	/// </summary>
	public static class UrlUtil
	{
		private static readonly char[] g_vPathTrimCharsWs = new char[] {
			'\"', ' ', '\t', '\r', '\n' };

		public static char LocalDirSepChar
		{
			get { return Path.DirectorySeparatorChar; }
		}

		private static char[] g_vDirSepChars = null;
		private static char[] DirSepChars
		{
			get
			{
				if (g_vDirSepChars == null)
				{
					List<char> l = new List<char>();
					l.Add('/'); // For URLs, also on Windows

					// On Unix-like systems, '\\' is not a separator
					if (!NativeLib.IsUnix()) l.Add('\\');

					if (!l.Contains(UrlUtil.LocalDirSepChar))
					{
						Debug.Assert(false);
						l.Add(UrlUtil.LocalDirSepChar);
					}

					g_vDirSepChars = l.ToArray();
				}

				return g_vDirSepChars;
			}
		}

		/// <summary>
		/// Get the directory (path) of a file name. The returned string may be
		/// terminated by a directory separator character. Example:
		/// passing <c>C:\\My Documents\\My File.kdb</c> in <paramref name="strFile" />
		/// and <c>true</c> to <paramref name="bAppendTerminatingChar"/>
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
		/// <returns>Directory of the file.</returns>
		public static string GetFileDirectory(string strFile, bool bAppendTerminatingChar,
			bool bEnsureValidDirSpec)
		{
			Debug.Assert(strFile != null);
			if (strFile == null) throw new ArgumentNullException("strFile");

			int nLastSep = strFile.LastIndexOfAny(UrlUtil.DirSepChars);
			if (nLastSep < 0) return string.Empty; // No directory

			if (bEnsureValidDirSpec && (nLastSep == 2) && (strFile[1] == ':') &&
				(strFile[2] == '\\')) // Length >= 3 and Windows root directory
				bAppendTerminatingChar = true;

			if (!bAppendTerminatingChar) return strFile.Substring(0, nLastSep);
			return EnsureTerminatingSeparator(strFile.Substring(0, nLastSep),
				(strFile[nLastSep] == '/'));
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
			Debug.Assert(strPath != null); if (strPath == null) throw new ArgumentNullException("strPath");

			int nLastSep = strPath.LastIndexOfAny(UrlUtil.DirSepChars);

			if (nLastSep < 0) return strPath;
			if (nLastSep >= (strPath.Length - 1)) return string.Empty;

			return strPath.Substring(nLastSep + 1);
		}

		/// <summary>
		/// Strip the extension of a file.
		/// </summary>
		/// <param name="strPath">Full path of a file with extension.</param>
		/// <returns>File name without extension.</returns>
		public static string StripExtension(string strPath)
		{
			Debug.Assert(strPath != null); if (strPath == null) throw new ArgumentNullException("strPath");

			int nLastDirSep = strPath.LastIndexOfAny(UrlUtil.DirSepChars);
			int nLastExtDot = strPath.LastIndexOf('.');

			if (nLastExtDot <= nLastDirSep) return strPath;

			return strPath.Substring(0, nLastExtDot);
		}

		/// <summary>
		/// Get the extension of a file.
		/// </summary>
		/// <param name="strPath">Full path of a file with extension.</param>
		/// <returns>Extension without prepending dot.</returns>
		public static string GetExtension(string strPath)
		{
			Debug.Assert(strPath != null); if (strPath == null) throw new ArgumentNullException("strPath");

			int nLastDirSep = strPath.LastIndexOfAny(UrlUtil.DirSepChars);
			int nLastExtDot = strPath.LastIndexOf('.');

			if (nLastExtDot <= nLastDirSep) return string.Empty;
			if (nLastExtDot == (strPath.Length - 1)) return string.Empty;

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
			Debug.Assert(strPath != null); if (strPath == null) throw new ArgumentNullException("strPath");

			int nLength = strPath.Length;
			if (nLength <= 0) return string.Empty;

			char chLast = strPath[nLength - 1];
			if (Array.IndexOf<char>(UrlUtil.DirSepChars, chLast) >= 0)
				return strPath;

			if (bUrl) return (strPath + '/');
			return (strPath + UrlUtil.LocalDirSepChar);
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

		internal static int IndexOfSecondEnclQuote(string str)
		{
			if (str == null) { Debug.Assert(false); return -1; }
			if (str.Length <= 1) return -1;
			if (str[0] != '\"') { Debug.Assert(false); return -1; }

			if (NativeLib.IsUnix())
			{
				// Find non-escaped quote
				string strFlt = str.Replace("\\\\", new string(
					StrUtil.GetUnusedChar(str + "\\\""), 2)); // Same length
				Match m = Regex.Match(strFlt, "[^\\\\]\\u0022");
				int i = (((m != null) && m.Success) ? m.Index : -1);
				return ((i >= 0) ? (i + 1) : -1); // Index of quote
			}

			// Windows does not allow quotes in folder/file names
			return str.IndexOf('\"', 1);
		}

		public static string GetQuotedAppPath(string strPath)
		{
			if (strPath == null) { Debug.Assert(false); return string.Empty; }

			string str = strPath.Trim();
			if (str.Length <= 1) return str;
			if (str[0] != '\"') return str;

			int iSecond = IndexOfSecondEnclQuote(str);
			if (iSecond <= 0) return str;

			return str.Substring(1, iSecond - 1);
		}

		public static string FileUrlToPath(string strUrl)
		{
			if (strUrl == null) { Debug.Assert(false); throw new ArgumentNullException("strUrl"); }
			if (strUrl.Length == 0) { Debug.Assert(false); return string.Empty; }

			if (!strUrl.StartsWith(Uri.UriSchemeFile + ":", StrUtil.CaseIgnoreCmp))
			{
				Debug.Assert(false);
				return strUrl;
			}

			try
			{
				Uri uri = new Uri(strUrl);
				string str = uri.LocalPath;
				if (!string.IsNullOrEmpty(str)) return str;
			}
			catch (Exception) { Debug.Assert(false); }

			Debug.Assert(false);
			return strUrl;
		}

		public static bool UnhideFile(string strFile)
		{
#if KeePassLibSD
			return false;
#else
			if (strFile == null) throw new ArgumentNullException("strFile");

			try
			{
				FileAttributes fa = File.GetAttributes(strFile);
				if ((long)(fa & FileAttributes.Hidden) == 0) return false;

				return HideFile(strFile, false);
			}
			catch (Exception) { }

			return false;
#endif
		}

		public static bool HideFile(string strFile, bool bHide)
		{
#if KeePassLibSD
			return false;
#else
			if (strFile == null) throw new ArgumentNullException("strFile");

			try
			{
				FileAttributes fa = File.GetAttributes(strFile);

				if (bHide) fa = ((fa & ~FileAttributes.Normal) | FileAttributes.Hidden);
				else // Unhide
				{
					fa &= ~FileAttributes.Hidden;
					if ((long)fa == 0) fa = FileAttributes.Normal;
				}

				File.SetAttributes(strFile, fa);
				return true;
			}
			catch (Exception) { }

			return false;
#endif
		}

		public static string MakeRelativePath(string strBaseFile, string strTargetFile)
		{
			if (strBaseFile == null) throw new ArgumentNullException("strBasePath");
			if (strTargetFile == null) throw new ArgumentNullException("strTargetPath");
			if (strBaseFile.Length == 0) return strTargetFile;
			if (strTargetFile.Length == 0) return string.Empty;

			// Test whether on different Windows drives
			if ((strBaseFile.Length >= 3) && (strTargetFile.Length >= 3))
			{
				if ((strBaseFile[1] == ':') && (strTargetFile[1] == ':') &&
					(strBaseFile[2] == '\\') && (strTargetFile[2] == '\\') &&
					(strBaseFile[0] != strTargetFile[0]))
					return strTargetFile;
			}

#if (!KeePassLibSD && !KeePassUAP)
			if (NativeLib.IsUnix())
			{
#endif
				bool bBaseUnc = IsUncPath(strBaseFile);
				bool bTargetUnc = IsUncPath(strTargetFile);
				if ((!bBaseUnc && bTargetUnc) || (bBaseUnc && !bTargetUnc))
					return strTargetFile;

				string strBase = GetShortestAbsolutePath(strBaseFile);
				string strTarget = GetShortestAbsolutePath(strTargetFile);
				string[] vBase = strBase.Split(UrlUtil.DirSepChars);
				string[] vTarget = strTarget.Split(UrlUtil.DirSepChars);

				int i = 0;
				while ((i < (vBase.Length - 1)) && (i < (vTarget.Length - 1)) &&
					(vBase[i] == vTarget[i])) { ++i; }

				StringBuilder sbRel = new StringBuilder();
				for (int j = i; j < (vBase.Length - 1); ++j)
				{
					if (sbRel.Length > 0) sbRel.Append(UrlUtil.LocalDirSepChar);
					sbRel.Append("..");
				}
				for (int k = i; k < vTarget.Length; ++k)
				{
					if (sbRel.Length > 0) sbRel.Append(UrlUtil.LocalDirSepChar);
					sbRel.Append(vTarget[k]);
				}

				return sbRel.ToString();
#if (!KeePassLibSD && !KeePassUAP)
			}

			try // Windows
			{
				const int nMaxPath = NativeMethods.MAX_PATH * 2;
				StringBuilder sb = new StringBuilder(nMaxPath + 2);
				if (!NativeMethods.PathRelativePathTo(sb, strBaseFile, 0,
					strTargetFile, 0))
					return strTargetFile;

				string str = sb.ToString();
				while (str.StartsWith(".\\")) str = str.Substring(2, str.Length - 2);

				return str;
			}
			catch (Exception) { Debug.Assert(false); }
			return strTargetFile;
#endif
		}

		public static string MakeAbsolutePath(string strBaseFile, string strTargetFile)
		{
			if (strBaseFile == null) throw new ArgumentNullException("strBasePath");
			if (strTargetFile == null) throw new ArgumentNullException("strTargetPath");
			if (strBaseFile.Length == 0) return strTargetFile;
			if (strTargetFile.Length == 0) return string.Empty;

			if (IsAbsolutePath(strTargetFile)) return strTargetFile;

			string strBaseDir = GetFileDirectory(strBaseFile, true, false);
			return GetShortestAbsolutePath(strBaseDir + strTargetFile);
		}

		public static bool IsAbsolutePath(string strPath)
		{
			if (strPath == null) throw new ArgumentNullException("strPath");
			if (strPath.Length == 0) return false;

			if (IsUncPath(strPath)) return true;

			try { return Path.IsPathRooted(strPath); }
			catch (Exception) { Debug.Assert(false); }

			return true;
		}

		public static string GetShortestAbsolutePath(string strPath)
		{
			if (strPath == null) throw new ArgumentNullException("strPath");
			if (strPath.Length == 0) return string.Empty;

			// Path.GetFullPath is incompatible with UNC paths traversing over
			// different server shares (which are created by PathRelativePathTo);
			// we need to build the absolute path on our own...
			if (IsUncPath(strPath))
			{
				char chSep = strPath[0];
				char[] vSep = ((chSep == '/') ? (new char[] { '/' }) :
					(new char[] { '\\', '/' }));

				List<string> l = new List<string>();
#if !KeePassLibSD
				string[] v = strPath.Split(vSep, StringSplitOptions.None);
#else
				string[] v = strPath.Split(vSep);
#endif
				Debug.Assert((v.Length >= 3) && (v[0].Length == 0) &&
					(v[1].Length == 0));

				foreach (string strPart in v)
				{
					if (strPart.Equals(".")) continue;
					else if (strPart.Equals(".."))
					{
						if (l.Count > 0) l.RemoveAt(l.Count - 1);
						else { Debug.Assert(false); }
					}
					else l.Add(strPart); // Do not ignore zero length parts
				}

				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < l.Count; ++i)
				{
					// Don't test length of sb, might be 0 due to initial UNC seps
					if (i > 0) sb.Append(chSep);

					sb.Append(l[i]);
				}

				return sb.ToString();
			}

			string str;
			try { str = Path.GetFullPath(strPath); }
			catch (Exception) { Debug.Assert(false); return strPath; }

			Debug.Assert((str.IndexOf("\\..\\") < 0) || NativeLib.IsUnix());
			foreach (char ch in UrlUtil.DirSepChars)
			{
				string strSep = new string(ch, 1);
				str = str.Replace(strSep + "." + strSep, strSep);
			}

			return str;
		}

		public static int GetUrlLength(string strText, int nOffset)
		{
			if (strText == null) throw new ArgumentNullException("strText");
			if (nOffset > strText.Length) throw new ArgumentException(); // Not >= (0 len)

			int iPosition = nOffset, nLength = 0, nStrLen = strText.Length;

			while (iPosition < nStrLen)
			{
				char ch = strText[iPosition];
				++iPosition;

				if ((ch == ' ') || (ch == '\t') || (ch == '\r') || (ch == '\n'))
					break;

				++nLength;
			}

			return nLength;
		}

		internal static string GetScheme(string strUrl)
		{
			if (string.IsNullOrEmpty(strUrl)) return string.Empty;

			int i = strUrl.IndexOf(':');
			if (i > 0) return strUrl.Substring(0, i);

			return string.Empty;
		}

		public static string RemoveScheme(string strUrl)
		{
			if (string.IsNullOrEmpty(strUrl)) return string.Empty;

			int i = strUrl.IndexOf(':');
			if (i < 0) return strUrl; // No scheme to remove
			++i;

			// A single '/' indicates a path (absolute) and should not be removed
			if (((i + 1) < strUrl.Length) && (strUrl[i] == '/') &&
				(strUrl[i + 1] == '/'))
				i += 2; // Skip authority prefix

			return strUrl.Substring(i);
		}

		public static string ConvertSeparators(string strPath)
		{
			return ConvertSeparators(strPath, UrlUtil.LocalDirSepChar);
		}

		public static string ConvertSeparators(string strPath, char chSeparator)
		{
			if (string.IsNullOrEmpty(strPath)) return string.Empty;

			strPath = strPath.Replace('/', chSeparator);
			strPath = strPath.Replace('\\', chSeparator);

			return strPath;
		}

		public static bool IsUncPath(string strPath)
		{
			if (strPath == null) throw new ArgumentNullException("strPath");

			return (strPath.StartsWith("\\\\") || strPath.StartsWith("//"));
		}

		public static string FilterFileName(string strName)
		{
			if (string.IsNullOrEmpty(strName)) { Debug.Assert(false); return string.Empty; }

			// https://docs.microsoft.com/en-us/windows/desktop/fileio/naming-a-file

			StringBuilder sb = new StringBuilder(strName.Length);
			foreach (char ch in strName)
			{
				if (ch < '\u0020') continue;

				switch (ch)
				{
					case '\"':
					case '*':
					case ':':
					case '?':
						break;

					case '/':
					case '\\':
					case '|':
						sb.Append('-');
						break;

					case '<':
						sb.Append('(');
						break;

					case '>':
						sb.Append(')');
						break;

					default: sb.Append(ch); break;
				}
			}

			// Trim trailing spaces and periods
			for (int i = sb.Length - 1; i >= 0; --i)
			{
				char ch = sb[i];
				if ((ch == ' ') || (ch == '.')) sb.Remove(i, 1);
				else break;
			}

			return sb.ToString();
		}

		/// <summary>
		/// Get the host component of a URL.
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
			if (strUrl == null) { Debug.Assert(false); return string.Empty; }

			StringBuilder sb = new StringBuilder();
			bool bInExtHost = false;
			for (int i = 0; i < strUrl.Length; ++i)
			{
				char ch = strUrl[i];
				if (bInExtHost)
				{
					if (ch == '/')
					{
						if (sb.Length == 0) { } // Ignore leading '/'s
						else break;
					}
					else sb.Append(ch);
				}
				else // !bInExtHost
				{
					if (ch == ':') bInExtHost = true;
				}
			}

			string str = sb.ToString();
			if (str.Length == 0) str = strUrl;

			// Remove the login part
			int nLoginLen = str.IndexOf('@');
			if (nLoginLen >= 0) str = str.Substring(nLoginLen + 1);

			// Remove the port
			int iPort = str.LastIndexOf(':');
			if (iPort >= 0) str = str.Substring(0, iPort);

			return str;
		}

		public static bool AssemblyEquals(string strExt, string strShort)
		{
			if ((strExt == null) || (strShort == null)) { Debug.Assert(false); return false; }

			if (strExt.Equals(strShort, StrUtil.CaseIgnoreCmp) ||
				strExt.StartsWith(strShort + ",", StrUtil.CaseIgnoreCmp))
				return true;

			if (!strShort.EndsWith(".dll", StrUtil.CaseIgnoreCmp))
			{
				if (strExt.Equals(strShort + ".dll", StrUtil.CaseIgnoreCmp) ||
					strExt.StartsWith(strShort + ".dll,", StrUtil.CaseIgnoreCmp))
					return true;
			}

			if (!strShort.EndsWith(".exe", StrUtil.CaseIgnoreCmp))
			{
				if (strExt.Equals(strShort + ".exe", StrUtil.CaseIgnoreCmp) ||
					strExt.StartsWith(strShort + ".exe,", StrUtil.CaseIgnoreCmp))
					return true;
			}

			return false;
		}

		public static string GetTempPath()
		{
			string strDir;
			if (NativeLib.IsUnix())
				strDir = NativeMethods.GetUserRuntimeDir();
#if KeePassUAP
			else strDir = Windows.Storage.ApplicationData.Current.TemporaryFolder.Path;
#else
			else strDir = Path.GetTempPath();
#endif

			try
			{
				if (!Directory.Exists(strDir)) Directory.CreateDirectory(strDir);
			}
			catch (Exception) { Debug.Assert(false); }

			return strDir;
		}

#if !KeePassLibSD
		// Structurally mostly equivalent to UrlUtil.GetFileInfos
		public static List<string> GetFilePaths(string strDir, string strPattern,
			SearchOption opt)
		{
			List<string> l = new List<string>();
			if (strDir == null) { Debug.Assert(false); return l; }
			if (strPattern == null) { Debug.Assert(false); return l; }

			string[] v = Directory.GetFiles(strDir, strPattern, opt);
			if (v == null) { Debug.Assert(false); return l; }

			// Only accept files with the correct extension; GetFiles may
			// return additional files, see GetFiles documentation
			string strExt = GetExtension(strPattern);
			if (!string.IsNullOrEmpty(strExt) && (strExt.IndexOf('*') < 0) &&
				(strExt.IndexOf('?') < 0))
			{
				strExt = "." + strExt;

				foreach (string strPathRaw in v)
				{
					if (strPathRaw == null) { Debug.Assert(false); continue; }
					string strPath = strPathRaw.Trim(g_vPathTrimCharsWs);
					if (strPath.Length == 0) { Debug.Assert(false); continue; }
					Debug.Assert(strPath == strPathRaw);

					if (strPath.EndsWith(strExt, StrUtil.CaseIgnoreCmp))
						l.Add(strPathRaw);
				}
			}
			else l.AddRange(v);

			return l;
		}

		// Structurally mostly equivalent to UrlUtil.GetFilePaths
		public static List<FileInfo> GetFileInfos(DirectoryInfo di, string strPattern,
			SearchOption opt)
		{
			List<FileInfo> l = new List<FileInfo>();
			if (di == null) { Debug.Assert(false); return l; }
			if (strPattern == null) { Debug.Assert(false); return l; }

			FileInfo[] v = di.GetFiles(strPattern, opt);
			if (v == null) { Debug.Assert(false); return l; }

			// Only accept files with the correct extension; GetFiles may
			// return additional files, see GetFiles documentation
			string strExt = GetExtension(strPattern);
			if (!string.IsNullOrEmpty(strExt) && (strExt.IndexOf('*') < 0) &&
				(strExt.IndexOf('?') < 0))
			{
				strExt = "." + strExt;

				foreach (FileInfo fi in v)
				{
					if (fi == null) { Debug.Assert(false); continue; }
					string strPathRaw = fi.FullName;
					if (strPathRaw == null) { Debug.Assert(false); continue; }
					string strPath = strPathRaw.Trim(g_vPathTrimCharsWs);
					if (strPath.Length == 0) { Debug.Assert(false); continue; }
					Debug.Assert(strPath == strPathRaw);

					if (strPath.EndsWith(strExt, StrUtil.CaseIgnoreCmp))
						l.Add(fi);
				}
			}
			else l.AddRange(v);

			return l;
		}
#endif

		
		public static char GetDriveLetter(string strPath)
		{
			if (strPath == null) throw new ArgumentNullException("strPath");

			Debug.Assert(default(char) == '\0');
			if (strPath.Length < 3) return '\0';
			if ((strPath[1] != ':') || (strPath[2] != '\\')) return '\0';

			char ch = char.ToUpperInvariant(strPath[0]);
			return (((ch >= 'A') && (ch <= 'Z')) ? ch : '\0');
		}

		internal static string GetSafeFileName(string strName)
		{
			Debug.Assert(!string.IsNullOrEmpty(strName));

			string str = FilterFileName(GetFileName(strName ?? string.Empty));

			if (string.IsNullOrEmpty(str))
			{
				Debug.Assert(false);
				return "File.dat";
			}
			return str;
		}

		internal static string GetCanonicalUri(string strUri)
		{
			if (string.IsNullOrEmpty(strUri)) { Debug.Assert(false); return strUri; }

			try
			{
				Uri uri = new Uri(strUri);

				if (uri.IsAbsoluteUri) return uri.AbsoluteUri;
				else { Debug.Assert(false); }
			}
			catch (Exception) { Debug.Assert(false); }

			return strUri;
		}

		/* internal static Dictionary<string, string> ParseQuery(string strQuery)
		{
			Dictionary<string, string> d = new Dictionary<string, string>();
			if(string.IsNullOrEmpty(strQuery)) return d;

			string[] vKvp = strQuery.Split(new char[] { '?', '&' });
			if(vKvp == null) { Debug.Assert(false); return d; }

			foreach(string strKvp in vKvp)
			{
				if(string.IsNullOrEmpty(strKvp)) continue;

				string strKey, strValue;
				int iSep = strKvp.IndexOf('=');
				if(iSep < 0)
				{
					strKey = strKvp;
					strValue = string.Empty;
				}
				else
				{
					strKey = strKvp.Substring(0, iSep);
					strValue = strKvp.Substring(iSep + 1);
				}

				strKey = Uri.UnescapeDataString(strKey);
				strValue = Uri.UnescapeDataString(strValue);

				d[strKey] = strValue;
			}

			return d;
		} */
	}
}
