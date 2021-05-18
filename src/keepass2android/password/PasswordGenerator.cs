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
using Android.App;
using Android.Content;

namespace keepass2android
{
    public static class StringExtension
    {
        public static string ToUpperFirstLetter(this string source)
        {
            if (string.IsNullOrEmpty(source))
                return string.Empty;
            // convert to char array of the string
            char[] letters = source.ToCharArray();
            // upper case the first char
            letters[0] = char.ToUpper(letters[0]);
            // return the array made of the new char array
            return new string(letters);
        }
    }

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

        public class CombinedKeyOptions
        {
            protected bool Equals(CombinedKeyOptions other)
            {
                return Equals(PassphraseGenerationOptions, other.PassphraseGenerationOptions) && Equals(PasswordGenerationOptions, other.PasswordGenerationOptions);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((CombinedKeyOptions) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(PassphraseGenerationOptions, PasswordGenerationOptions);
            }

            public PassphraseGenerationOptions PassphraseGenerationOptions { get; set; }
            public PasswordGenerationOptions PasswordGenerationOptions { get; set; }
            
        }

        public class PasswordGenerationOptions
        {
            protected bool Equals(PasswordGenerationOptions other)
            {
                return Length == other.Length && UpperCase == other.UpperCase && LowerCase == other.LowerCase && Digits == other.Digits && Minus == other.Minus && Underline == other.Underline && Space == other.Space && Specials == other.Specials && SpecialsExtended == other.SpecialsExtended && Brackets == other.Brackets && ExcludeLookAlike == other.ExcludeLookAlike && AtLeastOneFromEachGroup == other.AtLeastOneFromEachGroup;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((PasswordGenerationOptions) obj);
            }

            public override int GetHashCode()
            {
                var hashCode = new HashCode();
                hashCode.Add(Length);
                hashCode.Add(UpperCase);
                hashCode.Add(LowerCase);
                hashCode.Add(Digits);
                hashCode.Add(Minus);
                hashCode.Add(Underline);
                hashCode.Add(Space);
                hashCode.Add(Specials);
                hashCode.Add(SpecialsExtended);
                hashCode.Add(Brackets);
                hashCode.Add(ExcludeLookAlike);
                hashCode.Add(AtLeastOneFromEachGroup);
                return hashCode.ToHashCode();
            }

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

        public class PassphraseGenerationOptions
        {
            protected bool Equals(PassphraseGenerationOptions other)
            {
                return CaseMode == other.CaseMode && Separator == other.Separator && WordCount == other.WordCount;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((PassphraseGenerationOptions) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine((int) CaseMode, Separator, WordCount);
            }

            public enum PassphraseCaseMode
            {
                Lowercase,
                Uppercase,
                PascalCase
            };
            public PassphraseCaseMode CaseMode { get; set; }
            public string Separator { get; set; }
            public int WordCount { get; set; }

        }



        public String GeneratePassword(CombinedKeyOptions options) 
        {
            if ((options.PassphraseGenerationOptions== null || options.PassphraseGenerationOptions.WordCount == 0)
                && (options.PasswordGenerationOptions == null || options.PasswordGenerationOptions.Length == 0))
            {
                throw new Exception("Bad options");
            }

            string key = "";

            Random random = new SecureRandom();

            var passwordOptions = options.PasswordGenerationOptions;
            var passphraseOptions = options.PassphraseGenerationOptions;
            if (passphraseOptions != null && passphraseOptions.WordCount > 0)
            {
                var wl = new Wordlist();
                string passphrase = "";
                for (int i = 0; i < passphraseOptions.WordCount; i++)
                {
                    
                    string word = wl.GetWord(random);

                    if (passphraseOptions.CaseMode == PassphraseGenerationOptions.PassphraseCaseMode.Uppercase)
                    {
                        word = word.ToUpper();
                    }
                    else if (passphraseOptions.CaseMode == PassphraseGenerationOptions.PassphraseCaseMode.Lowercase)
                    {
                        word = word.ToLower();
                    }
                    else if (passphraseOptions.CaseMode == PassphraseGenerationOptions.PassphraseCaseMode.PascalCase)
                    {
                        word = word.ToUpperFirstLetter();
                    }

                    passphrase += word;

                    if (i < passphraseOptions.WordCount - 1 || passwordOptions != null)
                        passphrase += passphraseOptions.Separator;

                }

                key += passphrase;
            }

            
            if (passwordOptions != null)
            {
                var groups = GetCharacterGroups(passwordOptions);
                String characterSet = GetCharacterSet(passwordOptions, groups);

                if (characterSet.Length == 0)
                    throw new Exception("Bad options");

                int size = characterSet.Length;

                StringBuilder buffer = new StringBuilder();

                if (passwordOptions.AtLeastOneFromEachGroup)
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
                    while (buffer.Length < passwordOptions.Length)
                    {
                        buffer.Append(characterSet[random.Next(size)]);
                    }
                }

                var password = buffer.ToString();

                if (passwordOptions.AtLeastOneFromEachGroup)
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

                key += password;
            }


            return key;
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

