using Android.Content;
using Android.Widget;
using Android.Content.Res;
using KeePassLib;
using Android.Graphics.Drawables;

namespace keepass2android
{
    public interface IDrawableFactory
    {
		void AssignDrawableTo(ImageView iv, Context context, PwDatabase db, PwIcon icon, PwUuid customIconId, bool forGroup);

		Drawable GetIconDrawable(Context context, PwDatabase db, PwIcon icon, PwUuid customIconId, bool forGroup);

		bool IsWhiteIconSet { get; }

        void Clear();
    }
}