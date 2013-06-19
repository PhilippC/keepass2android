using Android.Widget;
using Android.Content.Res;
using KeePassLib;
using Android.Graphics.Drawables;

namespace keepass2android
{
    public interface IDrawableFactory
    {
        void AssignDrawableTo (ImageView iv, Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId);

        Drawable GetIconDrawable(Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId);


        void Clear();
    }
}