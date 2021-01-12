using System;
using System.Collections.Generic;
using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Widget;
using keepass2android;

namespace PluginTOTP
{
	class TrayTotpPluginAdapter : ITotpPluginAdapter
	{
		public TotpData GetTotpData(IDictionary<string, string> entryFields, Context ctx, bool muteWarnings)
		{
			return new TrayTotpHandler(ctx, new Handler(Looper.MainLooper), muteWarnings).GetTotpData(entryFields);
		}

		private class TrayTotpHandler
		{
			private readonly Context _ctx;
			private readonly Handler _uiThreadHandler;
			private readonly bool _muteWarnings;

			private string SeedFieldName { get { return PreferenceManager.GetDefaultSharedPreferences(_ctx).GetString(_ctx.GetString(Resource.String.TrayTotp_SeedField_key), "TOTP Seed"); } }
			private string SettingsFieldName { get { return PreferenceManager.GetDefaultSharedPreferences(_ctx).GetString(_ctx.GetString(Resource.String.TrayTotp_SettingsField_key), "TOTP Settings"); } }

			public TrayTotpHandler(Context ctx, Handler uiThreadHandler, bool muteWarnings)
			{
				_ctx = ctx;
				_uiThreadHandler = uiThreadHandler;
				_muteWarnings = muteWarnings;
			}

			/// <summary>
			/// Check if specified Entry contains Settings that are not null.
			/// </summary>
			internal bool SettingsCheck(IDictionary<string, string> entryFields)
			{
				string settings;
				entryFields.TryGetValue(SettingsFieldName, out settings);
				return !String.IsNullOrEmpty(settings);
			}

			internal bool SeedCheck(IDictionary<string, string> entryFields)
			{
				string seed;
				entryFields.TryGetValue(SeedFieldName, out seed);
				return !String.IsNullOrEmpty(seed);
			}

			/// <summary>
			/// Check if specified Entry's Interval and Length are valid. All settings statuses are available as out booleans.
			/// </summary>
			/// <param name="pe">Password Entry.</param>
			/// <param name="IsIntervalValid">Interval Validity.</param>
			/// <param name="IsLengthValid">Length Validity.</param>
			/// <param name="IsUrlValid">Url Validity.</param>
			/// <returns>Error(s) while validating Interval or Length.</returns>
			internal bool SettingsValidate(IDictionary<string, string> entryFields, out bool IsIntervalValid, out bool IsLengthValid, out bool IsUrlValid)
			{
				bool SettingsValid = true;
				try
				{
					string[] Settings = SettingsGet(entryFields);
					try
					{
						IsIntervalValid = (Convert.ToInt16(Settings[0]) > 0) && (Convert.ToInt16(Settings[0]) < 61); //Interval
					}
					catch (Exception)
					{
						IsIntervalValid = false;
						SettingsValid = false;
					}
					try
					{
						IsLengthValid = (Settings[1] == "6") || (Settings[1] == "8") || (Settings[1] == "S"); //Length
					}
					catch (Exception)
					{
						IsLengthValid = false;
						SettingsValid = false;
					}
					try
					{
						IsUrlValid = (Settings[2].StartsWith("http://")) || (Settings[2].StartsWith("https://")); //Url
					}
					catch (Exception)
					{
						IsUrlValid = false;
					}
				}
				catch (Exception)
				{
					IsIntervalValid = false;
					IsLengthValid = false;
					IsUrlValid = false;
					SettingsValid = false;
				}
				return SettingsValid;
			}

			private string[] SettingsGet(IDictionary<string, string> entryFields)
			{
				return entryFields[SettingsFieldName].Split(';');
			}

			public TotpData GetTotpData(IDictionary<string, string> entryFields)
			{
				TotpData res = new TotpData();

				if (SettingsCheck(entryFields) && SeedCheck(entryFields))
				{
					bool ValidInterval; bool ValidLength; bool ValidUrl;
					if (SettingsValidate(entryFields, out ValidInterval, out ValidLength, out ValidUrl))
					{
						bool NoTimeCorrection = false;
						string[] Settings = SettingsGet(entryFields);
						res.Duration = Settings[0];
						res.Length = Settings[1];
						if (ValidUrl)
						{
							NoTimeCorrection = true;
							res.Url = Settings[2];
							/*var CurrentTimeCorrection = TimeCorrections[Settings[2]];
							if (CurrentTimeCorrection != null)
							{
								TotpGenerator.TimeCorrection = CurrentTimeCorrection.TimeCorrection;
							}
							else
							{
								TotpGenerator.TimeCorrection = TimeSpan.Zero;
								NoTimeCorrection = true;
							}*/
						}
						string InvalidCharacters;
						if (SeedValidate(entryFields, out InvalidCharacters))
						{
							res.IsTotpEntry = true;
							res.TotpSeed = SeedGet(entryFields).ExtWithoutSpaces();


						}
						else
						{
							ShowWarning("Bad seed!" + InvalidCharacters.ExtWithParenthesis().ExtWithSpaceBefore());
						}
						if (NoTimeCorrection)
							ShowWarning("Warning: TOTP Time correction not implemented!");
					}
					else
					{
						ShowWarning("Bad settings!");
					}
				}
				else
				{
					//no totp entry
				}
				return res;
			}

			private void ShowWarning(string warning)
			{
				if (_muteWarnings)
					return;
				try
				{
					_uiThreadHandler.Post(() => Toast.MakeText(_ctx, warning, ToastLength.Short).Show());
				}
				catch (Exception e)
				{
					Kp2aLog.LogUnexpectedError(e);
					//ignore, it's only a warning
				}
				
			}

			private bool SeedValidate(IDictionary<string, string> entryFields, out string invalidCharacters)
			{
				return SeedGet(entryFields).ExtWithoutSpaces().ExtIsBase32(out invalidCharacters);
			}
			internal string SeedGet(IDictionary<string, string> entryFields)
			{
				return entryFields[SeedFieldName];
			}
		}


	}

	/// <summary>
	/// Class to support custom extensions.
	/// </summary>
	internal static class Extensions
	{
		/// <summary>
		/// Concatenates a space in front of the current string.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <returns></returns>
		internal static string ExtWithSpaceBefore(this string Extension)
		{
			return " " + Extension;
		}

		/// <summary>
		/// Concatenates the current string with space to the end.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <returns></returns>
		internal static string ExtWithSpaceAfter(this string Extension)
		{
			return Extension + " ";
		}

		/// <summary>
		/// Concatenates the current string with a bracket in front and to the end.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <returns></returns>
		internal static string ExtWithBrackets(this string Extension)
		{
			return ExtWith(Extension, '{', '}');
		}

		/// <summary>
		/// Concatenates the current string with a parenthesis in front and to the end.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <returns></returns>
		internal static string ExtWithParenthesis(this string Extension)
		{
			return ExtWith(Extension, '(', ')');
		}

		/// <summary>
		/// Concatenates the current string with a charater in front and another character to the end.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <param name="Left">Front character.</param>
		/// <param name="Right">End charater.</param>
		/// <returns></returns>
		internal static string ExtWith(this string Extension, char Left, char Right)
		{
			return Left + Extension + Right;
		}

		/// <summary>
		/// Remove all spaces from the current string.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <returns></returns>
		internal static string ExtWithoutSpaces(this string Extension)
		{
			return Extension.ExtWithout(" ");
		}

		/// <summary>
		/// Remove all specified characters from the current string.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <param name="Chars">Characters to remove.</param>
		/// <returns></returns>
		internal static string ExtWithout(this string Extension, string Chars)
		{
			foreach (var Char in Chars)
			{
				Extension = Extension.Replace(Char.ToString(), "");
			}
			return Extension;
		}

		

		/// <summary>
		/// Splits the string and returns specified substring.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <param name="Index">Split index.</param>
		/// <param name="Seperator">Split seperators.</param>
		/// <returns></returns>
		internal static string ExtSplit(this string Extension, int Index, char Seperator = ';')
		{
			if (Extension != string.Empty)
			{
				try
				{
					var Text = Extension;
					if (Text.Contains(Seperator.ToString()))
					{
						return Text.Split(Seperator)[Index];
					}
					return Text;
				}
				catch (Exception)
				{
					return string.Empty;
				}
			}
			return string.Empty;
		}

		/// <summary>
		/// Makes sure the string provided as a Seed is Base32. Invalid characters are available as out string.
		/// </summary>
		/// <param name="Extension">Current string.</param>
		/// <param name="InvalidChars">Invalid characters.</param>
		/// <returns>Validity of the string's characters for Base32 format.</returns>
		internal static bool ExtIsBase32(this string Extension, out string InvalidChars)
		{
			InvalidChars = null;
			try
			{
				foreach (var CurrentChar in Extension)
				{
					var CurrentCharValue = Char.GetNumericValue(CurrentChar);
					if (Char.IsLetter(CurrentChar))
					{
						continue;
					}
					if (Char.IsDigit(CurrentChar))
					{
						if ((CurrentCharValue > 1) && (CurrentCharValue < 8))
						{
							continue;
						}
					}
					InvalidChars = (InvalidChars + CurrentCharValue.ToString().ExtWithSpaceBefore()).Trim();
				}
			}
			catch (Exception)
			{
				InvalidChars = "(error)";
			}
			return InvalidChars == null;
		}
	}
}