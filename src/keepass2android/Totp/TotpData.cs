namespace PluginTOTP
{
	struct TotpData
	{
		public bool IsTotpEnry { get; set; }
		public string TotpSeed { get; set; }
		public int Duration { get; set; }
		public int Length { get; set; }

	}
}