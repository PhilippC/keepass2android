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
using System.Collections.Generic;
using System.Linq;
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
        private const String ExtendedChars = "§©®¢°±¹²³¼½×÷«âéïñù¡¿»¦Ø";
		private const String BracketChars 	= "[]{}()<>";
		
		private readonly Context _cxt;

		public sealed class SecureRandom : Random
		{

			private readonly RandomNumberGenerator _rng = new RNGCryptoServiceProvider();


			public override int Next()
			{
				var data = new byte[sizeof(int)];
				_rng.GetBytes(data);
				return BitConverter.ToInt32(data, 0) & (Int32.MaxValue - 1);
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
				return randUint / (UInt32.MaxValue + 1.0);
			}

			public override void NextBytes(byte[] data)
			{
				_rng.GetBytes(data);
			}
		}
		
		public PasswordGenerator(Context cxt) {
			_cxt = cxt;
		}


        public class PasswordGenerationOptions
        {
            public int Length { get; set; }
            public bool UpperCase { get; set; }
            public bool LowerCase { get; set; }
            public bool Digits { get; set; }
            public bool Minus { get; set; }
            public bool Underline { get; set; }
            public bool Space { get; set; }
            public bool Specials { get; set; }
            public bool SpecialsExtended { get; set; }
            public bool Brackets { get; set; }

			public bool ExcludeLookAlike { get; set; }
			public bool AtLeastOneFromEachGroup { get; set; }

        }


        public String GeneratePassword(PasswordGenerationOptions options) {
			if (options.Length <= 0)
				throw new ArgumentException(_cxt.GetString(Resource.String.error_wrong_length));


            var groups = GetCharacterGroups(options);
			String characterSet = GetCharacterSet(options, groups);

			if (characterSet.Length == 0)
                throw new ArgumentException(_cxt.GetString(Resource.String.error_pass_gen_type));

			int size = characterSet.Length;
			
			StringBuilder buffer = new StringBuilder();

			Random random = new SecureRandom();

            if (options.AtLeastOneFromEachGroup)
            {
                foreach (var g in groups)
                {
                    if (g.Length > 0)
                    {
                        buffer.Append(g[random.Next(g.Length)]);
                    }
                }
            }

			if (size > 0) 
			{
				while (buffer.Length < options.Length)
				{
					buffer.Append(characterSet[random.Next(size)]);
				}
			}

			var password = buffer.ToString();

            if (options.AtLeastOneFromEachGroup)
            {
                //shuffle
                StringBuilder sb = new StringBuilder(password);
                for (int i = (password.Length - 1); i >= 1; i--)
                {
                    int j = random.Next(i + 1);

                    var tmp = sb[i];
                    sb[i] = sb[j];
                    sb[j] = tmp;
                }

                password = sb.ToString();
            }

            
			return password;
        }
		
		public string GetCharacterSet(PasswordGenerationOptions options, List<string> groups)
        {
            var characterSet =  String.Join("", groups);

            return characterSet;

        }

        private static List<string> GetCharacterGroups(PasswordGenerationOptions options)
        {
            List<string> groups = new List<string>();

            if (options.UpperCase)
                groups.Add(UpperCaseChars);

            if (options.LowerCase)
                groups.Add(LowerCaseChars);

            if (options.Digits)
                groups.Add(DigitChars);

            if (options.Minus)
                groups.Add(MinusChars);

            if (options.Underline)
                groups.Add(UnderlineChars);

            if (options.Space)
                groups.Add(SpaceChars);

            if (options.Specials)
                groups.Add(SpecialChars);

            if (options.SpecialsExtended)
                groups.Add(ExtendedChars);

            if (options.Brackets)
                groups.Add(BracketChars);

            
            if (options.ExcludeLookAlike)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    groups[i] = String.Join("", groups[i].Except("Il1|8B6GO0"));
                }
            }

            return groups;
        }
    }

}

