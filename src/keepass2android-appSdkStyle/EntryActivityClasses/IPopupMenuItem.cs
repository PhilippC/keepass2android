using System;
using Android.Graphics.Drawables;
using KeePassLib;

namespace keepass2android
{
	/// <summary>
	/// Interface for popup menu items in EntryActivity
	/// </summary>
	internal interface IPopupMenuItem	
	{
		Drawable Icon { get; }
		String Text { get; }

		void HandleClick();
	}
}