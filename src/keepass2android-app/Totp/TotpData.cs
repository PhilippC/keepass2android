// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using KeeTrayTOTP.Libraries;

namespace PluginTOTP
{
    public class TotpData
    {
        public TotpData()
        {
            HashAlgorithm = HashSha1;
        }

        public const string EncoderSteam = "steam";
        public const string EncoderRfc6238 = "rfc6238";
        public const string HashSha1 = "HMAC-SHA-1";
        public const string HashSha256 = "HMAC-SHA-256";
        public const string HashSha512 = "HMAC-SHA-512";


        public bool IsTotpEntry { get; set; }

        public byte[] TotpSecret { get; set; }
        public string TotpSeed
        {
            set { TotpSecret = Base32.Decode(value.Trim()); }
            get { return Base32.Encode(TotpSecret); }
        }
        public string Duration { get; set; }
        public string Encoder { get; set; }
        public string Length { get; set; }
        public string TimeCorrectionUrl { get; set; }

        public string HashAlgorithm { get; set; }

        public bool IsDefaultRfc6238
        {
            get { return Length == "6" && Duration == "30" && (HashAlgorithm == null || HashAlgorithm == HashSha1); }
        }

        public List<string> InternalFields { get; set; } = new List<string>();

        public static TotpData MakeDefaultRfc6238()
        {
            return new TotpData()
            {
                Duration = "30",
                Length = "6",
                HashAlgorithm = HashSha1
            };
        }
    }
}