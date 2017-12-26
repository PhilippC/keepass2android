using System;
using System.Text;
using Android.OS;
using Java.Util;

namespace keepass2android.services.AutofillBase
{
    public class CommonUtil
    {
        public const string Tag = "Kp2aAutofill";
        public const bool Debug = true;
        public const string EXTRA_DATASET_NAME = "dataset_name";
        public const string EXTRA_FOR_RESPONSE = "for_response";

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
    }
}