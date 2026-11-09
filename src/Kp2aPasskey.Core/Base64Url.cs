// This file is part of Keepass2Android, Copyright 2026 Philipp Crocoll.
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

using Android.Util;

namespace Kp2aPasskey.Core
{
  /// <summary>
  /// base64url (RFC 4648, section 5) as used by WebAuthn.
  /// </summary>
  public static class Base64Url
  {
    /// <summary>Encodes without padding and without line breaks.</summary>
    public static string Encode(byte[] data) =>
      Base64.EncodeToString(data, Base64Flags.UrlSafe | Base64Flags.NoPadding | Base64Flags.NoWrap)!;

    /// <summary>Decodes with or without padding. Throws <see cref="Java.Lang.IllegalArgumentException"/> for invalid input.</summary>
    public static byte[] Decode(string value) => Base64.Decode(value, Base64Flags.UrlSafe)!;
  }
}
