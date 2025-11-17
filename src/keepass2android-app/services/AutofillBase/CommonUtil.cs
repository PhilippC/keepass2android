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
using System.Text;
using Android.OS;
using Android.Util;
using Java.Util;

namespace keepass2android.services.AutofillBase
{
    public class CommonUtil
    {
        public const string Tag = "Kp2aAutofill";
        public const bool Debug = true;

        static void BundleToString(StringBuilder builder, Bundle data)
        {
            var keySet = data.KeySet();
            builder.Append("[Bundle with ").Append(keySet.Count).Append(" keys:");
            foreach (var key in keySet)
            {
                builder.Append(' ').Append(key).Append('=');
                Object value = data.Get(key);
                if (value is Bundle)
                {
                    BundleToString(builder, (Bundle)value);
                }
                else
                {
                    builder.Append((value is Object[])
                        ? Arrays.ToString((bool[])value) : value);
                }
            }
            builder.Append(']');
        }

        public static string BundleToString(Bundle data)
        {
            if (data == null)
            {
                return "N/A";
            }
            StringBuilder builder = new StringBuilder();
            BundleToString(builder, data);
            return builder.ToString();
        }

        public static void logd(string s)
        {
#if DEBUG
            Log.Debug(Tag, s);
#endif
        }

        public static void loge(string s)
        {
            Kp2aLog.Log(s);
        }
    }
}