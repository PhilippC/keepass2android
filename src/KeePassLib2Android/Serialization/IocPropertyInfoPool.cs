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
using System.Diagnostics;
using System.Text;

using KeePassLib.Resources;
using KeePassLib.Utility;

namespace KeePassLib.Serialization
{
	public static class IocKnownProtocols
	{
		public const string Http = "HTTP";
		public const string Https = "HTTPS";
		public const string WebDav = "WebDAV";
		public const string Ftp = "FTP";
	}

	public static class IocKnownProperties
	{
		public const string Timeout = "Timeout";
		public const string PreAuth = "PreAuth";

		public const string UserAgent = "UserAgent";
		public const string Expect100Continue = "Expect100Continue";

		public const string Passive = "Passive";
	}

	public static class IocPropertyInfoPool
	{
		private static List<IocPropertyInfo> m_l = null;
		public static IEnumerable<IocPropertyInfo> PropertyInfos
		{
			get { EnsureInitialized(); return m_l; }
		}

		private static void EnsureInitialized()
		{
			if(m_l != null) return;

			string strGen = KLRes.General;
			string strHttp = IocKnownProtocols.Http;
			string strHttps = IocKnownProtocols.Https;
			string strWebDav = IocKnownProtocols.WebDav;
			string strFtp = IocKnownProtocols.Ftp;

			string[] vGen = new string[] { strGen };
			string[] vHttp = new string[] { strHttp, strHttps, strWebDav };
			string[] vFtp = new string[] { strFtp };

			List<IocPropertyInfo> l = new List<IocPropertyInfo>();

			l.Add(new IocPropertyInfo(IocKnownProperties.Timeout,
				typeof(long), KLRes.Timeout + " [ms]", vGen));
			l.Add(new IocPropertyInfo(IocKnownProperties.PreAuth,
				typeof(bool), KLRes.PreAuth, vGen));

			l.Add(new IocPropertyInfo(IocKnownProperties.UserAgent,
				typeof(string), KLRes.UserAgent, vHttp));
			l.Add(new IocPropertyInfo(IocKnownProperties.Expect100Continue,
				typeof(bool), KLRes.Expect100Continue, vHttp));

			l.Add(new IocPropertyInfo(IocKnownProperties.Passive,
				typeof(bool), KLRes.Passive, vFtp));

			// l.Add(new IocPropertyInfo("Test", typeof(bool),
			//	"Long long long long long long long long long long long long long long long long long long long long",
			//	new string[] { "Proto 1/9", "Proto 2/9", "Proto 3/9", "Proto 4/9", "Proto 5/9",
			//	"Proto 6/9", "Proto 7/9", "Proto 8/9", "Proto 9/9" }));

			m_l = l;
		}

		public static IocPropertyInfo Get(string strName)
		{
			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return null; }

			EnsureInitialized();
			foreach(IocPropertyInfo pi in m_l)
			{
				if(pi.Name.Equals(strName, StrUtil.CaseIgnoreCmp))
					return pi;
			}

			return null;
		}

		public static bool Add(IocPropertyInfo pi)
		{
			if(pi == null) { Debug.Assert(false); return false; }

			// Name must be non-empty
			string strName = pi.Name;
			if(string.IsNullOrEmpty(strName)) { Debug.Assert(false); return false; }

			IocPropertyInfo piEx = Get(strName); // Ensures initialized
			if(piEx != null) { Debug.Assert(false); return false; } // Exists already

			m_l.Add(pi);
			return true;
		}
	}
}
