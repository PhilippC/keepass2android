using Android.Content;
using Android.Graphics.Drawables;

namespace keepass2android
{
	/// <summary>
	/// Reperesents the popup menu item in EntryActivity to copy a string to clipboard
	/// </summary>
	class CopyToClipboardPopupMenuIcon : IPopupMenuItem
	{
		private readonly Context _context;
		private readonly IStringView _stringView;
		private readonly bool _isProtected;

		public CopyToClipboardPopupMenuIcon(Context context, IStringView stringView, bool isProtected)
		{
			_context = context;
			_stringView = stringView;
			_isProtected = isProtected;
		}

		public Drawable Icon 
		{ 
			get
			{
				return _context.Resources.GetDrawable(Resource.Drawable.ic_menu_copy_holo_dark);
			}
		}
		public string Text
		{
			get { return _context.Resources.GetString(Resource.String.copy_to_clipboard); }
		}

		
		public void HandleClick()
		{
			CopyToClipboardService.CopyValueToClipboardWithTimeout(_context, _stringView.Text, _isProtected);
		}
	}
}