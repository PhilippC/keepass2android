/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2017 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

#if (!KeePassLibSD && !KeePassUAP)
using System.Net.Cache;
using System.Net.Security;
#endif

#if !KeePassUAP
using System.Security.Cryptography.X509Certificates;
#endif

using KeePassLib.Native;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
#if !KeePassLibSD
	internal sealed class IOWebClient : WebClient
	{
		private IOConnectionInfo m_ioc;

		public IOWebClient(IOConnectionInfo ioc) : base()
		{
			m_ioc = ioc;
		}

		protected override WebRequest GetWebRequest(Uri address)
		{
			WebRequest request = base.GetWebRequest(address);
			IOConnection.ConfigureWebRequest(request, m_ioc);
			return request;
		}
	}
#endif

	internal abstract class WrapperStream : Stream
	{
		private readonly Stream m_s;
		protected Stream BaseStream
		{
			get { return m_s; }
		}

		public override bool CanRead
		{
			get { return m_s.CanRead; }
		}

		public override bool CanSeek
		{
			get { return m_s.CanSeek; }
		}

		public override bool CanTimeout
		{
			get { return m_s.CanTimeout; }
		}

		public override bool CanWrite
		{
			get { return m_s.CanWrite; }
		}

		public override long Length
		{
			get { return m_s.Length; }
		}

		public override long Position
		{
			get { return m_s.Position; }
			set { m_s.Position = value; }
		}

		public override int ReadTimeout
		{
			get { return m_s.ReadTimeout; }
			set { m_s.ReadTimeout = value; }
		}

		public override int WriteTimeout
		{
			get { return m_s.WriteTimeout; }
			set { m_s.WriteTimeout = value; }
		}

		public WrapperStream(Stream sBase) : base()
		{
			if(sBase == null) throw new ArgumentNullException("sBase");

			m_s = sBase;
		}

#if !KeePassUAP
		public override IAsyncResult BeginRead(byte[] buffer, int offset,
			int count, AsyncCallback callback, object state)
		{
			return m_s.BeginRead(buffer, offset, count, callback, state);
		}

		public override IAsyncResult BeginWrite(byte[] buffer, int offset,
			int count, AsyncCallback callback, object state)
		{
			return BeginWrite(buffer, offset, count, callback, state);
		}
#endif

		protected override void Dispose(bool disposing)
		{
			if(disposing) m_s.Dispose();

			base.Dispose(disposing);
		}

#if !KeePassUAP
		public override int EndRead(IAsyncResult asyncResult)
		{
			return m_s.EndRead(asyncResult);
		}

		public override void EndWrite(IAsyncResult asyncResult)
		{
			m_s.EndWrite(asyncResult);
		}
#endif

		public override void Flush()
		{
			m_s.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return m_s.Read(buffer, offset, count);
		}

		public override int ReadByte()
		{
			return m_s.ReadByte();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return m_s.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			m_s.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			m_s.Write(buffer, offset, count);
		}

		public override void WriteByte(byte value)
		{
			m_s.WriteByte(value);
		}
	}

	internal sealed class IocStream : WrapperStream
	{
		private readonly bool m_bWrite; // Initially opened for writing
		private bool m_bDisposed = false;

		public IocStream(Stream sBase) : base(sBase)
		{
			m_bWrite = sBase.CanWrite;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if(disposing && MonoWorkarounds.IsRequired(10163) && m_bWrite &&
				!m_bDisposed)
			{
				try
				{
					Stream s = this.BaseStream;
					Type t = s.GetType();
					if(t.Name == "WebConnectionStream")
					{
						PropertyInfo pi = t.GetProperty("Request",
							BindingFlags.Instance | BindingFlags.NonPublic);
						if(pi != null)
						{
							WebRequest wr = (pi.GetValue(s, null) as WebRequest);
							if(wr != null)
								IOConnection.DisposeResponse(wr.GetResponse(), false);
							else { Debug.Assert(false); }
						}
						else { Debug.Assert(false); }
					}
				}
				catch(Exception) { Debug.Assert(false); }
			}

			m_bDisposed = true;
		}

		public static Stream WrapIfRequired(Stream s)
		{
			if(s == null) { Debug.Assert(false); return null; }

			if(MonoWorkarounds.IsRequired(10163) && s.CanWrite)
				return new IocStream(s);

			return s;
		}
	}

	public static class IOConnection
	{
#if !KeePassLibSD
		private static ProxyServerType m_pstProxyType = ProxyServerType.System;
		private static string m_strProxyAddr = string.Empty;
		private static string m_strProxyPort = string.Empty;
		private static ProxyAuthType m_patProxyAuthType = ProxyAuthType.Auto;
		private static string m_strProxyUserName = string.Empty;
		private static string m_strProxyPassword = string.Empty;

#if !KeePassUAP
		private static bool? m_obDefaultExpect100Continue = null;

		private static bool m_bSslCertsAcceptInvalid = false;
		internal static bool SslCertsAcceptInvalid
		{
			// get { return m_bSslCertsAcceptInvalid; }
			set { m_bSslCertsAcceptInvalid = value; }
		}
#endif
#endif

		// Web request methods
		public const string WrmDeleteFile = "DELETEFILE";
		public const string WrmMoveFile = "MOVEFILE";

		// Web request headers
		public const string WrhMoveFileTo = "MoveFileTo";

		public static event EventHandler<IOAccessEventArgs> IOAccessPre;

#if !KeePassLibSD
#if !KeePassUAP
		// Allow self-signed certificates, expired certificates, etc.
		private static bool AcceptCertificate(object sender,
			X509Certificate certificate, X509Chain chain,
			SslPolicyErrors sslPolicyErrors)
		{
			return true;
		}
#endif

		internal static void SetProxy(ProxyServerType pst, string strAddr,
			string strPort, ProxyAuthType pat, string strUserName,
			string strPassword)
		{
			m_pstProxyType = pst;
			m_strProxyAddr = (strAddr ?? string.Empty);
			m_strProxyPort = (strPort ?? string.Empty);
			m_patProxyAuthType = pat;
			m_strProxyUserName = (strUserName ?? string.Empty);
			m_strProxyPassword = (strPassword ?? string.Empty);
		}

		internal static void ConfigureWebRequest(WebRequest request,
			IOConnectionInfo ioc)
		{
			if(request == null) { Debug.Assert(false); return; } // No throw

			IocProperties p = ((ioc != null) ? ioc.Properties : null);
			if(p == null) { Debug.Assert(false); p = new IocProperties(); }

			IHasIocProperties ihpReq = (request as IHasIocProperties);
			if(ihpReq != null)
			{
				IocProperties pEx = ihpReq.IOConnectionProperties;
				if(pEx != null) p.CopyTo(pEx);
				else ihpReq.IOConnectionProperties = p.CloneDeep();
			}

			if(IsHttpWebRequest(request))
			{
				// WebDAV support
#if !KeePassUAP
				request.PreAuthenticate = true; // Also auth GET
#endif
				if(string.Equals(request.Method, WebRequestMethods.Http.Post,
					StrUtil.CaseIgnoreCmp))
					request.Method = WebRequestMethods.Http.Put;

#if !KeePassUAP
				HttpWebRequest hwr = (request as HttpWebRequest);
				if(hwr != null)
				{
					string strUA = p.Get(IocKnownProperties.UserAgent);
					if(!string.IsNullOrEmpty(strUA)) hwr.UserAgent = strUA;
				}
				else { Debug.Assert(false); }
#endif
			}
#if !KeePassUAP
			else if(IsFtpWebRequest(request))
			{
				FtpWebRequest fwr = (request as FtpWebRequest);
				if(fwr != null)
				{
					bool? obPassive = p.GetBool(IocKnownProperties.Passive);
					if(obPassive.HasValue) fwr.UsePassive = obPassive.Value;
				}
				else { Debug.Assert(false); }
			}
#endif

#if !KeePassUAP
			// Not implemented and ignored in Mono < 2.10
			try
			{
				request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
			}
			catch(NotImplementedException) { }
			catch(Exception) { Debug.Assert(false); }
#endif

			try
			{
				IWebProxy prx;
				if(GetWebProxy(out prx)) request.Proxy = prx;
			}
			catch(Exception) { Debug.Assert(false); }

#if !KeePassUAP
			long? olTimeout = p.GetLong(IocKnownProperties.Timeout);
			if(olTimeout.HasValue && (olTimeout.Value >= 0))
				request.Timeout = (int)Math.Min(olTimeout.Value, (long)int.MaxValue);

			bool? ob = p.GetBool(IocKnownProperties.PreAuth);
			if(ob.HasValue) request.PreAuthenticate = ob.Value;
#endif
		}

		internal static void ConfigureWebClient(WebClient wc)
		{
#if !KeePassUAP
			// Not implemented and ignored in Mono < 2.10
			try
			{
				wc.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
			}
			catch(NotImplementedException) { }
			catch(Exception) { Debug.Assert(false); }
#endif

			try
			{
				IWebProxy prx;
				if(GetWebProxy(out prx)) wc.Proxy = prx;
			}
			catch(Exception) { Debug.Assert(false); }
		}

		private static bool GetWebProxy(out IWebProxy prx)
		{
			bool b = GetWebProxyServer(out prx);
			if(b) AssignCredentials(prx);
			return b;
		}

		private static bool GetWebProxyServer(out IWebProxy prx)
		{
			prx = null;

			if(m_pstProxyType == ProxyServerType.None)
				return true; // Use null proxy

			if(m_pstProxyType == ProxyServerType.Manual)
			{
				try
				{
					if(m_strProxyAddr.Length == 0)
					{
						// First try default (from config), then system
						prx = WebRequest.DefaultWebProxy;
#if !KeePassUAP
						if(prx == null) prx = WebRequest.GetSystemWebProxy();
#endif
					}
					else if(m_strProxyPort.Length > 0)
						prx = new WebProxy(m_strProxyAddr, int.Parse(m_strProxyPort));
					else prx = new WebProxy(m_strProxyAddr);

					return (prx != null);
				}
#if KeePassUAP
				catch(Exception) { Debug.Assert(false); }
#else
				catch(Exception ex)
				{
					string strInfo = m_strProxyAddr;
					if(m_strProxyPort.Length > 0)
						strInfo += ":" + m_strProxyPort;
					MessageService.ShowWarning(strInfo, ex.Message);
				}
#endif

				return false; // Use default
			}

			Debug.Assert(m_pstProxyType == ProxyServerType.System);
			try
			{
				// First try system, then default (from config)
#if !KeePassUAP
				prx = WebRequest.GetSystemWebProxy();
#endif
				if(prx == null) prx = WebRequest.DefaultWebProxy;

				return (prx != null);
			}
			catch(Exception) { Debug.Assert(false); }

			return false;
		}

		private static void AssignCredentials(IWebProxy prx)
		{
			if(prx == null) return; // No assert

			string strUserName = m_strProxyUserName;
			string strPassword = m_strProxyPassword;

			ProxyAuthType pat = m_patProxyAuthType;
			if(pat == ProxyAuthType.Auto)
			{
				if((strUserName.Length > 0) || (strPassword.Length > 0))
					pat = ProxyAuthType.Manual;
				else pat = ProxyAuthType.Default;
			}

			try
			{
				if(pat == ProxyAuthType.None)
					prx.Credentials = null;
				else if(pat == ProxyAuthType.Default)
					prx.Credentials = CredentialCache.DefaultCredentials;
				else if(pat == ProxyAuthType.Manual)
				{
					if((strUserName.Length > 0) || (strPassword.Length > 0))
						prx.Credentials = new NetworkCredential(
							strUserName, strPassword);
				}
				else { Debug.Assert(false); }
			}
			catch(Exception) { Debug.Assert(false); }
		}

		private static void PrepareWebAccess(IOConnectionInfo ioc)
		{
#if !KeePassUAP
			IocProperties p = ((ioc != null) ? ioc.Properties : null);
			if(p == null) { Debug.Assert(false); p = new IocProperties(); }

			try
			{
				if(m_bSslCertsAcceptInvalid)
					ServicePointManager.ServerCertificateValidationCallback =
						IOConnection.AcceptCertificate;
				else
					ServicePointManager.ServerCertificateValidationCallback = null;
			}
			catch(Exception) { Debug.Assert(false); }

			try
			{
				SecurityProtocolType spt = (SecurityProtocolType.Ssl3 |
					SecurityProtocolType.Tls);

				// The flags Tls11 and Tls12 in SecurityProtocolType have been
				// introduced in .NET 4.5 and must not be set when running under
				// older .NET versions (otherwise an exception is thrown)
				Type tSpt = typeof(SecurityProtocolType);
				string[] vSpt = Enum.GetNames(tSpt);
				foreach(string strSpt in vSpt)
				{
					if(strSpt.Equals("Tls11", StrUtil.CaseIgnoreCmp))
						spt |= (SecurityProtocolType)Enum.Parse(tSpt, "Tls11", true);
					else if(strSpt.Equals("Tls12", StrUtil.CaseIgnoreCmp))
						spt |= (SecurityProtocolType)Enum.Parse(tSpt, "Tls12", true);
				}

				ServicePointManager.SecurityProtocol = spt;
			}
			catch(Exception) { Debug.Assert(false); }

			try
			{
				bool bCurCont = ServicePointManager.Expect100Continue;
				if(!m_obDefaultExpect100Continue.HasValue)
				{
					Debug.Assert(bCurCont); // Default should be true
					m_obDefaultExpect100Continue = bCurCont;
				}

				bool bNewCont = m_obDefaultExpect100Continue.Value;
				bool? ob = p.GetBool(IocKnownProperties.Expect100Continue);
				if(ob.HasValue) bNewCont = ob.Value;

				if(bNewCont != bCurCont)
					ServicePointManager.Expect100Continue = bNewCont;
			}
			catch(Exception) { Debug.Assert(false); }
#endif
		}

		private static IOWebClient CreateWebClient(IOConnectionInfo ioc)
		{
			PrepareWebAccess(ioc);

			IOWebClient wc = new IOWebClient(ioc);
			ConfigureWebClient(wc);

			if((ioc.UserName.Length > 0) || (ioc.Password.Length > 0))
				wc.Credentials = new NetworkCredential(ioc.UserName, ioc.Password);
			else if(NativeLib.IsUnix()) // Mono requires credentials
				wc.Credentials = new NetworkCredential("anonymous", string.Empty);

			return wc;
		}

		private static WebRequest CreateWebRequest(IOConnectionInfo ioc)
		{
			PrepareWebAccess(ioc);

			WebRequest req = WebRequest.Create(ioc.Path);
			ConfigureWebRequest(req, ioc);

			if((ioc.UserName.Length > 0) || (ioc.Password.Length > 0))
				req.Credentials = new NetworkCredential(ioc.UserName, ioc.Password);
			else if(NativeLib.IsUnix()) // Mono requires credentials
				req.Credentials = new NetworkCredential("anonymous", string.Empty);

			return req;
		}

		public static Stream OpenRead(IOConnectionInfo ioc)
		{
			RaiseIOAccessPreEvent(ioc, IOAccessType.Read);

			if(StrUtil.IsDataUri(ioc.Path))
			{
				byte[] pbData = StrUtil.DataUriToData(ioc.Path);
				if(pbData != null) return new MemoryStream(pbData, false);
			}

			if(ioc.IsLocalFile()) return OpenReadLocal(ioc);

			return IocStream.WrapIfRequired(CreateWebClient(ioc).OpenRead(
				new Uri(ioc.Path)));
		}
#else
		public static Stream OpenRead(IOConnectionInfo ioc)
		{
			RaiseIOAccessPreEvent(ioc, IOAccessType.Read);

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

			RaiseIOAccessPreEvent(ioc, IOAccessType.Write);

			if(ioc.IsLocalFile()) return OpenWriteLocal(ioc);

			Uri uri = new Uri(ioc.Path);
			Stream s;

			// Mono does not set HttpWebRequest.Method to POST for writes,
			// so one needs to set the method to PUT explicitly
			if(NativeLib.IsUnix() && IsHttpWebRequest(uri))
				s = CreateWebClient(ioc).OpenWrite(uri, WebRequestMethods.Http.Put);
			else s = CreateWebClient(ioc).OpenWrite(uri);

			return IocStream.WrapIfRequired(s);
		}
#else
		public static Stream OpenWrite(IOConnectionInfo ioc)
		{
			RaiseIOAccessPreEvent(ioc, IOAccessType.Write);

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

			RaiseIOAccessPreEvent(ioc, IOAccessType.Exists);

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
			RaiseIOAccessPreEvent(ioc, IOAccessType.Delete);

			if(ioc.IsLocalFile()) { File.Delete(ioc.Path); return; }

#if !KeePassLibSD
			WebRequest req = CreateWebRequest(ioc);
			if(req != null)
			{
				if(IsHttpWebRequest(req)) req.Method = "DELETE";
				else if(IsFtpWebRequest(req))
					req.Method = WebRequestMethods.Ftp.DeleteFile;
				else if(IsFileWebRequest(req))
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
			RaiseIOAccessPreEvent(iocFrom, iocTo, IOAccessType.Move);

			if(iocFrom.IsLocalFile()) { File.Move(iocFrom.Path, iocTo.Path); return; }

#if !KeePassLibSD
			WebRequest req = CreateWebRequest(iocFrom);
			if(req != null)
			{
				if(IsHttpWebRequest(req))
				{
#if KeePassUAP
					throw new NotSupportedException();
#else
					req.Method = "MOVE";
					req.Headers.Set("Destination", iocTo.Path); // Full URL supported
#endif
				}
				else if(IsFtpWebRequest(req))
				{
#if KeePassUAP
					throw new NotSupportedException();
#else
					req.Method = WebRequestMethods.Ftp.Rename;
					string strTo = UrlUtil.GetFileName(iocTo.Path);

					// We're affected by .NET bug 621450:
					// https://connect.microsoft.com/VisualStudio/feedback/details/621450/problem-renaming-file-on-ftp-server-using-ftpwebrequest-in-net-framework-4-0-vs2010-only
					// Prepending "./", "%2E/" or "Dummy/../" doesn't work.

					((FtpWebRequest)req).RenameTo = strTo;
#endif
				}
				else if(IsFileWebRequest(req))
				{
					File.Move(UrlUtil.FileUrlToPath(iocFrom.Path),
						UrlUtil.FileUrlToPath(iocTo.Path));
					return;
				}
				else
				{
#if KeePassUAP
					throw new NotSupportedException();
#else
					req.Method = WrmMoveFile;
					req.Headers.Set(WrhMoveFileTo, iocTo.Path);
#endif
				}

#if !KeePassUAP // Unreachable code
				DisposeResponse(req.GetResponse(), true);
#endif
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

		internal static void DisposeResponse(WebResponse wr, bool bGetStream)
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

		private static void RaiseIOAccessPreEvent(IOConnectionInfo ioc, IOAccessType t)
		{
			RaiseIOAccessPreEvent(ioc, null, t);
		}

		private static void RaiseIOAccessPreEvent(IOConnectionInfo ioc,
			IOConnectionInfo ioc2, IOAccessType t)
		{
			if(ioc == null) { Debug.Assert(false); return; }
			// ioc2 may be null

			if(IOConnection.IOAccessPre != null)
			{
				IOConnectionInfo ioc2Lcl = ((ioc2 != null) ? ioc2.CloneDeep() : null);
				IOAccessEventArgs e = new IOAccessEventArgs(ioc.CloneDeep(), ioc2Lcl, t);
				IOConnection.IOAccessPre(null, e);
			}
		}

		private static bool IsHttpWebRequest(Uri uri)
		{
			if(uri == null) { Debug.Assert(false); return false; }

			string sch = uri.Scheme;
			if(sch == null) { Debug.Assert(false); return false; }
			return (sch.Equals("http", StrUtil.CaseIgnoreCmp) || // Uri.UriSchemeHttp
				sch.Equals("https", StrUtil.CaseIgnoreCmp)); // Uri.UriSchemeHttps
		}

		internal static bool IsHttpWebRequest(WebRequest wr)
		{
			if(wr == null) { Debug.Assert(false); return false; }

#if KeePassUAP
			return IsHttpWebRequest(wr.RequestUri);
#else
			return (wr is HttpWebRequest);
#endif
		}

		internal static bool IsFtpWebRequest(WebRequest wr)
		{
			if(wr == null) { Debug.Assert(false); return false; }

#if KeePassUAP
			return string.Equals(wr.RequestUri.Scheme, "ftp", StrUtil.CaseIgnoreCmp);
#else
			return (wr is FtpWebRequest);
#endif
		}

		private static bool IsFileWebRequest(WebRequest wr)
		{
			if(wr == null) { Debug.Assert(false); return false; }

#if KeePassUAP
			return string.Equals(wr.RequestUri.Scheme, "file", StrUtil.CaseIgnoreCmp);
#else
			return (wr is FileWebRequest);
#endif
		}
	}
}
