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
using System.Text;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;

#if !KeePassLibSD
using System.Net.Cache;
using System.Net.Security;
#endif

using KeePassLib.Native;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
#if !KeePassLibSD
	public sealed class IOWebClient : WebClient
	{
		protected override WebRequest GetWebRequest(Uri address)
		{
			WebRequest request = base.GetWebRequest(address);
			IOConnection.ConfigureWebRequest(request);
			return request;
		}
	}
#endif

	public static class IOConnection
	{
#if !KeePassLibSD
		private static ProxyServerType m_pstProxyType = ProxyServerType.System;
		private static string m_strProxyAddr = string.Empty;
		private static string m_strProxyPort = string.Empty;
		private static string m_strProxyUserName = string.Empty;
		private static string m_strProxyPassword = string.Empty;
#endif

		// Web request methods
		public const string WrmDeleteFile = "DELETEFILE";
		public const string WrmMoveFile = "MOVEFILE";

		// Web request headers
		public const string WrhMoveFileTo = "MoveFileTo";

#if !KeePassLibSD
		// Allow self-signed certificates, expired certificates, etc.
		private static bool ValidateServerCertificate(object sender,
			X509Certificate certificate, X509Chain chain,
			SslPolicyErrors sslPolicyErrors)
		{
			return true;
		}

		public static void SetProxy(ProxyServerType pst, string strAddr,
			string strPort, string strUserName, string strPassword)
		{
			m_pstProxyType = pst;
			m_strProxyAddr = (strAddr ?? string.Empty);
			m_strProxyPort = (strPort ?? string.Empty);
			m_strProxyUserName = (strUserName ?? string.Empty);
			m_strProxyPassword = (strPassword ?? string.Empty);
		}

		internal static void ConfigureWebRequest(WebRequest request)
		{
			if(request == null) { Debug.Assert(false); return; } // No throw

			// WebDAV support
			if(request is HttpWebRequest)
			{
				request.PreAuthenticate = true; // Also auth GET
				if(request.Method == WebRequestMethods.Http.Post)
					request.Method = WebRequestMethods.Http.Put;
			}
			// else if(request is FtpWebRequest)
			// {
			//	Debug.Assert(((FtpWebRequest)request).UsePassive);
			// }

			// Not implemented and ignored in Mono < 2.10
			try
			{
				request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
			}
			catch(NotImplementedException) { }
			catch(Exception) { Debug.Assert(false); }

			try
			{
				IWebProxy prx;
				if(GetWebProxy(out prx)) request.Proxy = prx;
			}
			catch(Exception) { Debug.Assert(false); }
		}

		internal static void ConfigureWebClient(WebClient wc)
		{
			// Not implemented and ignored in Mono < 2.10
			try
			{
				wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
			}
			catch(NotImplementedException) { }
			catch(Exception) { Debug.Assert(false); }

			try
			{
				IWebProxy prx;
				if(GetWebProxy(out prx)) wc.Proxy = prx;
			}
			catch(Exception) { Debug.Assert(false); }
		}

		private static bool GetWebProxy(out IWebProxy prx)
		{
			prx = null;

			if(m_pstProxyType == ProxyServerType.None)
				return true; // Use null proxy
			if(m_pstProxyType == ProxyServerType.Manual)
			{
				try
				{
					if(m_strProxyPort.Length > 0)
						prx = new WebProxy(m_strProxyAddr, int.Parse(m_strProxyPort));
					else prx = new WebProxy(m_strProxyAddr);

					if((m_strProxyUserName.Length > 0) || (m_strProxyPassword.Length > 0))
						prx.Credentials = new NetworkCredential(m_strProxyUserName,
							m_strProxyPassword);

					return true; // Use manual proxy
				}
				catch(Exception exProxy)
				{
					string strInfo = m_strProxyAddr;
					if(m_strProxyPort.Length > 0) strInfo += ":" + m_strProxyPort;
					MessageService.ShowWarning(strInfo, exProxy.Message);
				}

				return false; // Use default
			}

			if((m_strProxyUserName.Length == 0) && (m_strProxyPassword.Length == 0))
				return false; // Use default proxy, no auth

			try
			{
				prx = WebRequest.DefaultWebProxy;
				if(prx == null) prx = WebRequest.GetSystemWebProxy();
				if(prx == null) throw new InvalidOperationException();

				prx.Credentials = new NetworkCredential(m_strProxyUserName,
					m_strProxyPassword);
				return true;
			}
			catch(Exception) { Debug.Assert(false); }

			return false;
		}

		private static void PrepareWebAccess()
		{
			ServicePointManager.ServerCertificateValidationCallback =
				ValidateServerCertificate;
		}

		private static IOWebClient CreateWebClient(IOConnectionInfo ioc)
		{
			PrepareWebAccess();

			IOWebClient wc = new IOWebClient();
			ConfigureWebClient(wc);

			if((ioc.UserName.Length > 0) || (ioc.Password.Length > 0))
				wc.Credentials = new NetworkCredential(ioc.UserName, ioc.Password);
			else if(NativeLib.IsUnix()) // Mono requires credentials
				wc.Credentials = new NetworkCredential("anonymous", string.Empty);

			return wc;
		}

		private static WebRequest CreateWebRequest(IOConnectionInfo ioc)
		{
			PrepareWebAccess();

			WebRequest req = WebRequest.Create(ioc.Path);
			ConfigureWebRequest(req);

			if((ioc.UserName.Length > 0) || (ioc.Password.Length > 0))
				req.Credentials = new NetworkCredential(ioc.UserName, ioc.Password);
			else if(NativeLib.IsUnix()) // Mono requires credentials
				req.Credentials = new NetworkCredential("anonymous", string.Empty);

			return req;
		}

		public static Stream OpenRead(IOConnectionInfo ioc)
		{
			if(StrUtil.IsDataUri(ioc.Path))
			{
				byte[] pbData = StrUtil.DataUriToData(ioc.Path);
				if(pbData != null) return new MemoryStream(pbData, false);
			}

			if(ioc.IsLocalFile()) return OpenReadLocal(ioc);

			return CreateWebClient(ioc).OpenRead(new Uri(ioc.Path));
		}
#else
		public static Stream OpenRead(IOConnectionInfo ioc)
		{
			return OpenReadLocal(ioc);
		}
#endif

		private static Stream OpenReadLocal(IOConnectionInfo ioc)
		{
			return new FileStream(ioc.Path, FileMode.Open, FileAccess.Read,
				FileShare.Read);
		}

#if !KeePassLibSD
		public static Stream OpenWrite(IOConnectionInfo ioc)
		{
			if(ioc == null) { Debug.Assert(false); return null; }

			if(ioc.IsLocalFile()) return OpenWriteLocal(ioc);

			Uri uri = new Uri(ioc.Path);

			// Mono does not set HttpWebRequest.Method to POST for writes,
			// so one needs to set the method to PUT explicitly
			if(NativeLib.IsUnix() && (uri.Scheme.Equals(Uri.UriSchemeHttp,
				StrUtil.CaseIgnoreCmp) || uri.Scheme.Equals(Uri.UriSchemeHttps,
				StrUtil.CaseIgnoreCmp)))
				return CreateWebClient(ioc).OpenWrite(uri, WebRequestMethods.Http.Put);

			return CreateWebClient(ioc).OpenWrite(uri);
		}
#else
		public static Stream OpenWrite(IOConnectionInfo ioc)
		{
			return OpenWriteLocal(ioc);
		}
#endif

		private static Stream OpenWriteLocal(IOConnectionInfo ioc)
		{
			return new FileStream(ioc.Path, FileMode.Create, FileAccess.Write,
				FileShare.None);
		}

		public static bool FileExists(IOConnectionInfo ioc)
		{
			return FileExists(ioc, false);
		}

		public static bool FileExists(IOConnectionInfo ioc, bool bThrowErrors)
		{
			if(ioc == null) { Debug.Assert(false); return false; }

			if(ioc.IsLocalFile()) return File.Exists(ioc.Path);

#if !KeePassLibSD
			if(ioc.Path.StartsWith("ftp://", StrUtil.CaseIgnoreCmp))
			{
				bool b = SendCommand(ioc, WebRequestMethods.Ftp.GetDateTimestamp);
				if(!b && bThrowErrors) throw new InvalidOperationException();
				return b;
			}
#endif

			try
			{
				Stream s = OpenRead(ioc);
				if(s == null) throw new FileNotFoundException();

				try { s.ReadByte(); }
				catch(Exception) { }

				// We didn't download the file completely; close may throw
				// an exception -- that's okay
				try { s.Close(); }
				catch(Exception) { }
			}
			catch(Exception)
			{
				if(bThrowErrors) throw;
				return false;
			}

			return true;
		}

		public static void DeleteFile(IOConnectionInfo ioc)
		{
			if(ioc.IsLocalFile()) { File.Delete(ioc.Path); return; }

#if !KeePassLibSD
			WebRequest req = CreateWebRequest(ioc);
			if(req != null)
			{
				if(req is HttpWebRequest) req.Method = "DELETE";
				else if(req is FtpWebRequest) req.Method = WebRequestMethods.Ftp.DeleteFile;
				else if(req is FileWebRequest)
				{
					File.Delete(UrlUtil.FileUrlToPath(ioc.Path));
					return;
				}
				else req.Method = WrmDeleteFile;

				DisposeResponse(req.GetResponse(), true);
			}
#endif
		}

		/// <summary>
		/// Rename/move a file. For local file system and WebDAV, the
		/// specified file is moved, i.e. the file destination can be
		/// in a different directory/path. In contrast, for FTP the
		/// file is renamed, i.e. its destination must be in the same
		/// directory/path.
		/// </summary>
		/// <param name="iocFrom">Source file path.</param>
		/// <param name="iocTo">Target file path.</param>
		public static void RenameFile(IOConnectionInfo iocFrom, IOConnectionInfo iocTo)
		{
			if(iocFrom.IsLocalFile()) { File.Move(iocFrom.Path, iocTo.Path); return; }

#if !KeePassLibSD
			WebRequest req = CreateWebRequest(iocFrom);
			if(req != null)
			{
				if(req is HttpWebRequest)
				{
					req.Method = "MOVE";
					req.Headers.Set("Destination", iocTo.Path); // Full URL supported
				}
				else if(req is FtpWebRequest)
				{
					req.Method = WebRequestMethods.Ftp.Rename;
					((FtpWebRequest)req).RenameTo = UrlUtil.GetFileName(iocTo.Path);
				}
				else if(req is FileWebRequest)
				{
					File.Move(UrlUtil.FileUrlToPath(iocFrom.Path),
						UrlUtil.FileUrlToPath(iocTo.Path));
					return;
				}
				else
				{
					req.Method = WrmMoveFile;
					req.Headers.Set(WrhMoveFileTo, iocTo.Path);
				}

				DisposeResponse(req.GetResponse(), true);
			}
#endif

			// using(Stream sIn = IOConnection.OpenRead(iocFrom))
			// {
			//	using(Stream sOut = IOConnection.OpenWrite(iocTo))
			//	{
			//		MemUtil.CopyStream(sIn, sOut);
			//		sOut.Close();
			//	}
			//
			//	sIn.Close();
			// }
			// DeleteFile(iocFrom);
		}

#if !KeePassLibSD
		private static bool SendCommand(IOConnectionInfo ioc, string strMethod)
		{
			try
			{
				WebRequest req = CreateWebRequest(ioc);
				req.Method = strMethod;
				DisposeResponse(req.GetResponse(), true);
			}
			catch(Exception) { return false; }

			return true;
		}
#endif

		private static void DisposeResponse(WebResponse wr, bool bGetStream)
		{
			if(wr == null) return;

			try
			{
				if(bGetStream)
				{
					Stream s = wr.GetResponseStream();
					if(s != null) s.Close();
				}
			}
			catch(Exception) { Debug.Assert(false); }

			try { wr.Close(); }
			catch(Exception) { Debug.Assert(false); }
		}

		public static byte[] ReadFile(IOConnectionInfo ioc)
		{
			Stream sIn = null;
			MemoryStream ms = null;
			try
			{
				sIn = IOConnection.OpenRead(ioc);
				if(sIn == null) return null;

				ms = new MemoryStream();
				MemUtil.CopyStream(sIn, ms);

				return ms.ToArray();
			}
			catch(Exception) { }
			finally
			{
				if(sIn != null) sIn.Close();
				if(ms != null) ms.Close();
			}

			return null;
		}
	}
}
