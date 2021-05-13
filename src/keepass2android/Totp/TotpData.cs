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