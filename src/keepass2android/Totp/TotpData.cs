using System.Collections.Generic;
using KeeTrayTOTP.Libraries;

namespace PluginTOTP
{
    public class TotpData
    {
        public TotpData()
        {
            HashAlgorithm = "HMAC-SHA-1";
        }

        public const string EncoderSteam = "steam";
        public const string EncoderRfc6238 = "rfc6238";


        public bool IsTotpEntry { get; set; }

        public byte[] TotpSecret { get; set; }
		public string TotpSeed
        {
            set { TotpSecret = Base32.Decode(value.Trim()); }
        }
		public string Duration { get; set; }
        public string Encoder { get; set; }
        public string Length { get; set; }
		public string Url { get; set; }

        public string HashAlgorithm { get; set; }
    }
}