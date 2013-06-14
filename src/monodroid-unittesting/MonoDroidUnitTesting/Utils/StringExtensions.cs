//
// Copyright (C) 2012 Maya Studios (http://mayastudios.com)
//
// This file is part of MonoDroidUnitTesting.
//
// MonoDroidUnitTesting is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MonoDroidUnitTesting is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with MonoDroidUnitTesting. If not, see <http://www.gnu.org/licenses/>.
//


namespace System {
  internal static class StringExtensions {

    public static string Format(this string format, params object[] parameters) {
      return string.Format(format, parameters);
    }

    public static string FormatValues(this string format, params object[] values) {
      string[] valueStrings = new string[values.Length];

      for (int x = 0; x < values.Length; x++) {
        object value = values[x];
        string valueString;

        if (value == null) {
          valueString = "(null)";
        }
        else if (value is string) {
          valueString = "\"" + value + '"';
        }
        else if (value is Int32) {
          valueString = value.ToString();
        }
        else {
          if (value.GetType().IsArray) {
            valueString = string.Format("{0}[{1}] {...}", value.GetType().GetElementType().Name, ((Array)value).Length);
          }
          else {
            valueString = value.ToString();
          }

          valueString = "<" + valueString + ">";
        }

        valueStrings[x] = valueString;
      }

      return string.Format(format, valueStrings);
    }
  }
}