/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2025 Dominik Reichl <dominik.reichl@t-online.de>

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

#if !KeePassUAP
using System.Security.Cryptography;
#endif

using KeePassLib.Resources;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeePassLib.Cryptography.PasswordGenerator
{
    public enum PwgError
    {
        Success = 0,
        Unknown = 1,
        TooFewCharacters = 2,
        UnknownAlgorithm = 3,
        InvalidCharSet = 4,
        InvalidPattern = 5
    }

    /// <summary>
    /// Password generator.
    /// </summary>
    public static class PwGenerator
    {

        private static CryptoRandomStream CreateRandomStream(byte[] pbAdditionalEntropy,
            out byte[] pbKey)
        {
            pbKey = CryptoRandom.Instance.GetRandomBytes(128);

            // Mix in additional entropy
            Debug.Assert(pbKey.Length >= 64);
            if ((pbAdditionalEntropy != null) && (pbAdditionalEntropy.Length != 0))
            {
                using (SHA512Managed h = new SHA512Managed())
                {
                    byte[] pbHash = h.ComputeHash(pbAdditionalEntropy);
                    MemUtil.XorArray(pbHash, 0, pbKey, 0, pbHash.Length);
                    MemUtil.ZeroByteArray(pbHash);
                }
            }

            return new CryptoRandomStream(CrsAlgorithm.ChaCha20, pbKey);
        }

        internal static char GenerateCharacter(PwCharSet pwCharSet,
            CryptoRandomStream crsRandomSource)
        {
            uint cc = pwCharSet.Size;
            if (cc == 0) return char.MinValue;

            uint i = (uint)crsRandomSource.GetRandomUInt64(cc);
            return pwCharSet[i];
        }

        internal static bool PrepareCharSet(PwCharSet pwCharSet, PwProfile pwProfile)
        {
            uint cc = pwCharSet.Size;
            for (uint i = 0; i < cc; ++i)
            {
                char ch = pwCharSet[i];
                if ((ch == char.MinValue) || (ch == '\t') || (ch == '\r') ||
                    (ch == '\n') || char.IsSurrogate(ch))
                    return false;
            }

            if (pwProfile.ExcludeLookAlike) pwCharSet.Remove(PwCharSet.LookAlike);

            if (!string.IsNullOrEmpty(pwProfile.ExcludeCharacters))
                pwCharSet.Remove(pwProfile.ExcludeCharacters);

            return true;
        }

        internal static void Shuffle(char[] v, CryptoRandomStream crsRandomSource)
        {
            if (v == null) { Debug.Assert(false); return; }
            if (crsRandomSource == null) { Debug.Assert(false); return; }

            for (int i = v.Length - 1; i >= 1; --i)
            {
                int j = (int)crsRandomSource.GetRandomUInt64((ulong)(i + 1));

                char t = v[i];
                v[i] = v[j];
                v[j] = t;
            }
        }

        private static PwgError GenerateCustom(out ProtectedString psOut,
            PwProfile pwProfile, CryptoRandomStream crs,
            CustomPwGeneratorPool pwAlgorithmPool)
        {
            psOut = ProtectedString.Empty;

            Debug.Assert(pwProfile.GeneratorType == PasswordGeneratorType.Custom);
            if (pwAlgorithmPool == null) return PwgError.UnknownAlgorithm;

            string strID = pwProfile.CustomAlgorithmUuid;
            if (string.IsNullOrEmpty(strID)) return PwgError.UnknownAlgorithm;

            byte[] pbUuid = Convert.FromBase64String(strID);
            PwUuid uuid = new PwUuid(pbUuid);
            CustomPwGenerator pwg = pwAlgorithmPool.Find(uuid);
            if (pwg == null) { Debug.Assert(false); return PwgError.UnknownAlgorithm; }

            ProtectedString pwd = pwg.Generate(pwProfile.CloneDeep(), crs);
            if (pwd == null) return PwgError.Unknown;

            psOut = pwd;
            return PwgError.Success;
        }

        internal static string ErrorToString(PwgError e, bool bHeader)
        {
            if (e == PwgError.Success) { Debug.Assert(false); return string.Empty; }
            if ((e == PwgError.Unknown) && bHeader) return KLRes.PwGenFailed;

            string str = KLRes.UnknownError;
            switch (e)
            {
                // case PwgError.Success:
                //	break;

                case PwgError.Unknown:
                    break;

                case PwgError.TooFewCharacters:
                    str = KLRes.CharSetTooFewChars;
                    break;

                case PwgError.UnknownAlgorithm:
                    str = KLRes.AlgorithmUnknown;
                    break;

                case PwgError.InvalidCharSet:
                    str = KLRes.CharSetInvalid;
                    break;

                case PwgError.InvalidPattern:
                    str = KLRes.PatternInvalid;
                    break;

                default:
                    Debug.Assert(false);
                    break;
            }

            if (bHeader)
                str = KLRes.PwGenFailed + MessageService.NewParagraph + str;

            return str;
        }

        internal static string ErrorToString(Exception ex, bool bHeader)
        {
            string str = ((ex == null) ? KLRes.UnknownError :
                StrUtil.FormatException(ex));

            if (bHeader)
                str = KLRes.PwGenFailed + MessageService.NewParagraph + str;

            return str;
        }
    }
}
