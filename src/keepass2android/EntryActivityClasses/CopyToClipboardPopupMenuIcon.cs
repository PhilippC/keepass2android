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

		public CopyToClipboardPopupMenuIcon(Context context, IStringView stringView)
		{
			_context = context;
			_stringView = stringView;
			
		}

		public Drawable Icon 
		{ 
			get
			{
				return _context.Resources.GetDrawable(Resource.Drawable.ic_menu_copy_holo_light);
			}
		}
		public string Text
		{
			//TODO localize
			get { return "Copy to clipboard"; }
		}

		
		public void HandleClick()
		{
			CopyToClipboardService.CopyValueToClipboardWithTimeout(_context, _stringView.Text);
		}
	}
}