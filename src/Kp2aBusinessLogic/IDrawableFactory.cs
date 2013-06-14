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
using Android.Content.Res;
using KeePassLib;
using Android.Graphics.Drawables;

namespace keepass2android
{
    public interface IDrawableFactory
    {
        void assignDrawableTo (ImageView iv, Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId);

        Drawable getIconDrawable(Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId);


        void Clear();
    }
}