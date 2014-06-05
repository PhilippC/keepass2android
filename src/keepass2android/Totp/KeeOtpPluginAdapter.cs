using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Android.Content;
using KeePassLib.Collections;

namespace PluginTOTP
{
	/// <summary>
	/// Adapter to read the TOTP data from a KeeOTP entry.
	/// </summary>
	/// /// This class uses some methods from the KeeOTP plugin (licensed under MIT license)
	class KeeOtpPluginAdapter : ITotpPluginAdapter
	{
		public const string StringDictionaryKey = "otp";

		const string KeyParameter = "key";
		const string StepParameter = "step";
		const string SizeParameter = "size";
        

		public TotpData GetTotpData(IDictionary<string, string> entryFields, Context ctx, bool muteWarnings)
		{
			return new KeeOtpHandler(entryFields, ctx).GetData();
		}

		internal class KeeOtpHandler
		{
			private readonly Context _ctx;
			private readonly IDictionary<string, string> _entryFields;

			public KeeOtpHandler(IDictionary<string, string> entryFields, Context ctx)
			{
				_entryFields = entryFields;
				_ctx = ctx;
			}

			public TotpData GetData()
			{
				TotpData res = new TotpData();
				string data;
				if (!_entryFields.TryGetValue("otp", out data))
				{
					return res;
				}
				NameValueCollection parameters = ParseQueryString(data);

				if (parameters[KeyParameter] == null)
				{
					return res;
				}
				res.TotpSeed = parameters[KeyParameter];

				
				res.Duration = GetIntOrDefault(parameters, StepParameter, 30);
				res.Length = GetIntOrDefault(parameters, SizeParameter, 6);

				res.IsTotpEnry = true;
				return res;

			}

			private static int GetIntOrDefault(NameValueCollection parameters, string parameterKey, int defaultValue)
			{
				if (parameters[parameterKey] != null)
				{
					int step;
					if (int.TryParse(parameters[parameterKey], out step))
						return step;
					else
						return defaultValue;
				}
				else
					return defaultValue;
			}

        

			/// <remarks>
			/// Hacky query string parsing.  This was done due to reports
			/// of people with just a 3.5 or 4.0 client profile getting errors
			/// as the System.Web assembly where .net's implementation of
			/// Url encoding and query string parsing is locate.
			/// 
			/// This should be fine since the only thing stored in the string
			/// that needs to be encoded or decoded is the '=' sign.
			/// </remarks>
			private static NameValueCollection ParseQueryString(string data)
			{
				var collection = new NameValueCollection();

				var parameters = data.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var parameter in parameters)
				{
					if (parameter.Contains("="))
					{
						var pieces = parameter.Split('=');
						if (pieces.Length != 2)
							continue;

						collection.Add(pieces[0], pieces[1].Replace("%3d", "="));
					}
				}

				return collection;
			}

		}
	}
}