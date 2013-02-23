/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 2 of the License, or
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
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	
	public class PasswordGenerator {
		private const String upperCaseChars	= "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		private const String lowerCaseChars 	= "abcdefghijklmnopqrstuvwxyz";
		private const String digitChars 		= "0123456789";
		private const String minusChars 		= "-";
		private const String underlineChars 	= "_";
		private const String spaceChars 		= " ";
		private const String specialChars 	= "!\"#$%&'*+,./:;=?@\\^`";
		private const String bracketChars 	= "[]{}()<>";
		
		private Context cxt;
		
		public PasswordGenerator(Context cxt) {
			this.cxt = cxt;
		}
		
		public String generatePassword(int length, bool upperCase, bool lowerCase, bool digits, bool minus, bool underline, bool space, bool specials, bool brackets) {
			if (length <= 0)
				throw new ArgumentException(cxt.GetString(Resource.String.error_wrong_length));
			
			if (!upperCase && !lowerCase && !digits && !minus && !underline && !space && !specials && !brackets)
				throw new ArgumentException(cxt.GetString(Resource.String.error_pass_gen_type));
			
			String characterSet = getCharacterSet(upperCase, lowerCase, digits, minus, underline, space, specials, brackets);
			
			int size = characterSet.Length;
			
			StringBuilder buffer = new StringBuilder();
			
			Random random = new Random();
			if (size > 0) {
				
				for (int i = 0; i < length; i++) {
					char c = characterSet[random.Next(size)];
					
					buffer.Append(c);
				}
			}
			
			return buffer.ToString();
		}
		
		public String getCharacterSet(bool upperCase, bool lowerCase, bool digits, bool minus, bool underline, bool space, bool specials, bool brackets) {
			StringBuilder charSet = new StringBuilder();
			
			if (upperCase)
				charSet.Append(upperCaseChars);
			
			if (lowerCase)
				charSet.Append(lowerCaseChars);
			
			if (digits)
				charSet.Append(digitChars);
			
			if (minus)
				charSet.Append(minusChars);

			if (underline)
				charSet.Append(underlineChars);
			
			if (space)
				charSet.Append(spaceChars);
			
			if (specials)
				charSet.Append(specialChars);
			
			if (brackets)
				charSet.Append(bracketChars);
			
			return charSet.ToString();
		}
	}

}

