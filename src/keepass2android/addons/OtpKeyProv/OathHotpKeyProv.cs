/*
  OtpKeyProv Plugin
  Copyright (C) 2011-2012 Dominik Reichl <dominik.reichl@t-online.de>

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
using System.Windows.Forms;
using System.Diagnostics;

using OtpKeyProv.Forms;

using KeePass.UI;

using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;

namespace OtpKeyProv
{
	public sealed class OathHotpKeyProv : KeyProvider
	{
		private const string AuxFileExt = ".otp.xml";
		private const string ProvType = "OATH HOTP / RFC 4226";
		private const string ProvVersion = "2.0"; // File version, not OtpKeyProv version

		public override string Name
		{
			get { return "One-Time Passwords (OATH HOTP)"; }
		}

		public override bool SecureDesktopCompatible
		{
			get { return true; }
		}

		public override byte[] GetKey(KeyProviderQueryContext ctx)
		{
			try
			{
				if(ctx.CreatingNewKey) return Create(ctx);
				return Open(ctx);
			}
			catch(Exception ex) { MessageService.ShowWarning(ex.Message); }

			return null;
		}

		private static IOConnectionInfo GetAuxFileIoc(KeyProviderQueryContext ctx)
		{
			IOConnectionInfo ioc = ctx.DatabaseIOInfo.CloneDeep();
			ioc.Path = UrlUtil.StripExtension(ioc.Path) + AuxFileExt;
			return ioc;
		}

		private static byte[] Create(KeyProviderQueryContext ctx)
		{
			IOConnectionInfo iocPrev = GetAuxFileIoc(ctx);
			OtpInfo otpInfo = OtpInfo.Load(iocPrev);
			if(otpInfo == null) otpInfo = new OtpInfo();

			OtpKeyCreationForm dlg = new OtpKeyCreationForm();
			dlg.InitEx(otpInfo, ctx);

			if(UIUtil.ShowDialogAndDestroy(dlg) != DialogResult.OK)
				return null;

			if(!CreateAuxFile(otpInfo, ctx)) return null;
			return otpInfo.Secret;
		}

		private static byte[] Open(KeyProviderQueryContext ctx)
		{
			IOConnectionInfo ioc = GetAuxFileIoc(ctx);
			OtpInfo otpInfo = OtpInfo.Load(ioc);
			if(otpInfo == null)
			{
				MessageService.ShowWarning("Failed to load auxiliary OTP info file:",
					ioc.GetDisplayName());
				return null;
			}
			if(otpInfo.Type != ProvType)
			{
				MessageService.ShowWarning("Unknown OTP generator type!");
				return null;
			}

			OtpKeyPromptForm dlg = new OtpKeyPromptForm();
			dlg.InitEx(otpInfo, ctx);
			if(UIUtil.ShowDialogAndDestroy(dlg) != DialogResult.OK)
				return null;

			if(!CreateAuxFile(otpInfo, ctx)) return null;
			return otpInfo.Secret;
		}

		private static bool CreateAuxFile(OtpInfo otpInfo,
			KeyProviderQueryContext ctx)
		{
			otpInfo.Type = ProvType;
			otpInfo.Version = ProvVersion;
			otpInfo.Generator = OtpKeyProvExt.ProductName;

			otpInfo.EncryptSecret();

			IOConnectionInfo ioc = GetAuxFileIoc(ctx);
			if(!OtpInfo.Save(ioc, otpInfo))
			{
				MessageService.ShowWarning("Failed to save auxiliary OTP info file:",
					ioc.GetDisplayName());
				return false;
			}

			return true;
		}
	}
}
