/*
  This file was modified my Philipp Crocoll, 2013. Based on: 
  
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
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Utility;
using keepass2android;
using keepass2android.Io;

namespace OtpKeyProv
{
	public sealed class OathHotpKeyProv
		/*removed base class KeyProvider because "synchronous" interface is not suitable on Android*/
	{
		private const string AuxFileExt = ".otp.xml";
		private const string ProvType = "OATH HOTP / RFC 4226";
		private const string ProvVersion = "2.0"; // File version, not OtpKeyProv version

		public static string Name
		{
			get { return "One-Time Passwords (OATH HOTP)"; }
		}


		public const string ShortProductName = "OtpKeyProv";
		public const string ProductName = "OtpKeyProv KeePass Plugin";


		private static IOConnectionInfo GetAuxFileIoc(KeyProviderQueryContext ctx)
		{
			IOConnectionInfo ioc = ctx.DatabaseIOInfo.CloneDeep();
			IFileStorage fileStorage = App.Kp2a.GetOtpAuxFileStorage(ioc);
			IOConnectionInfo iocAux = fileStorage.GetFilePath(fileStorage.GetParentPath(ioc),
			                                                  fileStorage.GetFilenameWithoutPathAndExt(ioc) + AuxFileExt);

			return iocAux;
		}

		public static OtpInfo LoadOtpInfo(KeyProviderQueryContext ctx)
		{
			return OtpInfo.Load(GetAuxFileIoc(ctx));
		}

		/*
		private static byte[] Open(KeyProviderQueryContext ctx, OtpInfo otpInfo)
		{
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
		 * */

		/// <summary>
		/// Sets the "Secret" field in otpInfo based on the list of entered OTPs (lOtps) or the entered secret itself which is in format fmt
		/// </summary>
		/// based on the code in OtpKeyPromptForm.cs
		public void SetSecret(OtpInfo otpInfo, List<string> lOtps, string secret, OtpDataFmt? fmt)
		{
			byte[] pbSecret = EncodingUtil.ParseKey(secret,
			                                        (fmt.HasValue ? fmt.Value : OtpDataFmt.Hex));
			if (pbSecret != null)
			{
				otpInfo.Secret = pbSecret;
				return;
			}

			if (!string.IsNullOrEmpty(otpInfo.EncryptedSecret)) // < v2.0
			{
				byte[] pbKey32 = OtpUtil.KeyFromOtps(lOtps.ToArray(), 0,
				                                     lOtps.Count, Convert.FromBase64String(
					                                     otpInfo.TransformationKey), otpInfo.TransformationRounds);
				if (pbKey32 == null) throw new InvalidOperationException();

				pbSecret = OtpUtil.DecryptData(otpInfo.EncryptedSecret,
				                               pbKey32, Convert.FromBase64String(otpInfo.EncryptionIV));
				if (pbSecret == null) throw new InvalidOperationException();

				otpInfo.Secret = pbSecret;
				otpInfo.Counter += (ulong) otpInfo.OtpsRequired;
			}
			else // >= v2.0, supporting look-ahead
			{
				bool bSuccess = false;
				for (int i = 0; i < otpInfo.EncryptedSecrets.Count; ++i)
				{
					OtpEncryptedData d = otpInfo.EncryptedSecrets[i];
					pbSecret = OtpUtil.DecryptSecret(d, lOtps.ToArray(), 0,
					                                 lOtps.Count);
					if (pbSecret != null)
					{
						otpInfo.Secret = pbSecret;
						otpInfo.Counter += ((ulong) otpInfo.OtpsRequired +
						                    (ulong) i);
						bSuccess = true;
						break;
					}
				}
				if (!bSuccess) throw new InvalidOperationException();
			}
		}


		private static bool CreateAuxFile(OtpInfo otpInfo,
			KeyProviderQueryContext ctx)
		{
			otpInfo.Type = ProvType;
			otpInfo.Version = ProvVersion;
			otpInfo.Generator = ProductName;

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
