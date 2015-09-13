using System;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Widget;
using KeePassLib;
using keepass2android;

namespace Kp2aUnitTests
{
	internal class TestDrawableFactory : IDrawableFactory
	{
		public void AssignDrawableTo(ImageView iv, Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId, bool forGroup)
		{
			
		}

		public Drawable GetIconDrawable(Resources res, PwDatabase db, PwIcon icon, PwUuid customIconId, bool forGroup)
		{
			return res.GetDrawable(Resource.Drawable.Icon);
		}

		public void Clear()
		{
			
		}
	}
}