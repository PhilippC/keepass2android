using System.Collections.Generic;

namespace PluginTOTP
{
	struct TotpData
    {
        public const string EncoderSteam = "steam";
        public const string EncoderRfc6238 = "rfc6238";

        public bool IsTotpEnry { get; set; }
		public string TotpSeed { get; set; }
		public string Duration { get; set; }
        public string Encoder { get; set; }
        public string Length { get; set; }
		public string Url { get; set; }

		public string[] Settings
		{
			get
			{
				List<string> settings = new List<string>() { Duration, Encoder == EncoderSteam ? "S" : Length};
				if (Url != null)
					settings.Add(Url);
				return settings.ToArray();
			}
		}
	}
}