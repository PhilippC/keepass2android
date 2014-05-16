/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */

using System;
using System.Security.Cryptography;
using System.Text;
using Android.Content;

namespace keepass2android
{
	/// <summary>
	/// Password generator
	/// </summary>
	public class PasswordGenerator {
		private const String UpperCaseChars	= "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		private const String LowerCaseChars 	= "abcdefghijklmnopqrstuvwxyz";
		private const String DigitChars 		= "0123456789";
		private const String MinusChars 		= "-";
		private const String UnderlineChars 	= "_";
		private const String SpaceChars 		= " ";
		private const String SpecialChars 	= "!\"#$%&'*+,./:;=?@\\^`";
		private const String BracketChars 	= "[]{}()<>";
		
		private readonly Context _cxt;

		public sealed class SecureRandom : Random
		{

			private readonly RandomNumberGenerator _rng = new RNGCryptoServiceProvider();


			public override int Next()
			{
				var data = new byte[sizeof(int)];
				_rng.GetBytes(data);
				return BitConverter.ToInt32(data, 0) & (int.MaxValue - 1);
			}

			public override int Next(int maxValue)
			{
				return Next(0, maxValue);
			}

			public override int Next(int minValue, int maxValue)
			{
				if (minValue > maxValue)
				{
					throw new ArgumentOutOfRangeException();
				}
				return (int)Math.Floor((minValue + (maxValue - minValue) * NextDouble()));
			}

			public override double NextDouble()
			{
				var data = new byte[sizeof(uint)];
				_rng.GetBytes(data);
				var randUint = BitConverter.ToUInt32(data, 0);
				return randUint / (uint.MaxValue + 1.0);
			}

			public override void NextBytes(byte[] data)
			{
				_rng.GetBytes(data);
			}
		}
		
		public PasswordGenerator(Context cxt) {
			_cxt = cxt;
		}
		
		public String GeneratePassword(int length, bool upperCase, bool lowerCase, bool digits, bool minus, bool underline, bool space, bool specials, bool brackets) {
			if (length <= 0)
				throw new ArgumentException(_cxt.GetString(Resource.String.error_wrong_length));
			
			if (!upperCase && !lowerCase && !digits && !minus && !underline && !space && !specials && !brackets)
				throw new ArgumentException(_cxt.GetString(Resource.String.error_pass_gen_type));
			
			String characterSet = GetCharacterSet(upperCase, lowerCase, digits, minus, underline, space, specials, brackets);
			
			int size = characterSet.Length;
			
			StringBuilder buffer = new StringBuilder();

			Random random = new SecureRandom();
			if (size > 0) 
			{
				for (int i = 0; i < length; i++) 
				{
					char c = characterSet[random.Next(size)];
					
					buffer.Append(c);
				}
			}
			
			return buffer.ToString();
		}
		
		public String GetCharacterSet(bool upperCase, bool lowerCase, bool digits, bool minus, bool underline, bool space, bool specials, bool brackets) {
			StringBuilder charSet = new StringBuilder();
			
			if (upperCase)
				charSet.Append(UpperCaseChars);
			
			if (lowerCase)
				charSet.Append(LowerCaseChars);
			
			if (digits)
				charSet.Append(DigitChars);
			
			if (minus)
				charSet.Append(MinusChars);

			if (underline)
				charSet.Append(UnderlineChars);
			
			if (space)
				charSet.Append(SpaceChars);
			
			if (specials)
				charSet.Append(SpecialChars);
			
			if (brackets)
				charSet.Append(BracketChars);
			
			return charSet.ToString();
		}
	}

}

