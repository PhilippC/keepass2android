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

namespace keepass2android.AutoFillPlugin
{
    public class Credentials
    {
        public string User;
        public string Password;
        public string Url;
    }
}