// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeeTrayTOTP.Libraries
{
  class TOTPEncoder
  {
    /// <summary>
    /// Character set for authenticator code
    /// </summary>
    private static readonly char[] STEAMCHARS = new char[] {
                '2', '3', '4', '5', '6', '7', '8', '9', 'B', 'C',
                'D', 'F', 'G', 'H', 'J', 'K', 'M', 'N', 'P', 'Q',
                'R', 'T', 'V', 'W', 'X', 'Y'};


    private static uint OTP2UInt(byte[] totp)
    {
      uint fullcode = BitConverter.ToUInt32(totp, 0) & 0x7fffffff;

      return fullcode;
    }

    public static readonly Func<byte[], int, string> rfc6238 = (byte[] bytes, int length) =>
    {
      uint fullcode = TOTPEncoder.OTP2UInt(bytes);
      uint mask = (uint)Math.Pow(10, length);
      return (fullcode % mask).ToString(new string('0', length));

    };

    public static readonly Func<byte[], int, string> steam = (byte[] bytes, int length) =>
    {
      uint fullcode = TOTPEncoder.OTP2UInt(bytes);

      StringBuilder code = new StringBuilder();
      for (var i = 0; i < length; i++)
      {
        code.Append(STEAMCHARS[fullcode % STEAMCHARS.Length]);
        fullcode /= (uint)STEAMCHARS.Length;
      }

      return code.ToString();
    };


  }
}
