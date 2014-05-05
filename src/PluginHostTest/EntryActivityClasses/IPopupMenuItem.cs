using System;
using Android.Content;
using Android.Graphics.Drawables;
using KeePassLib;
using PluginHostTest;

namespace keepass2android
{
	internal interface IPopupMenuItem	
	{
		Drawable Icon { get; }
		String Text { get; }

		void HandleClick();
	}

	class GotoUrlMenuItem : IPopupMenuItem
	{
		private readonly EntryActivity _ctx;

		public GotoUrlMenuItem(EntryActivity ctx)
		{
			_ctx = ctx;
		}

		public Drawable Icon
		{
			get { return _ctx.Resources.GetDrawable(Android.Resource.Drawable.IcMenuUpload); }
		}

		public string Text
		{
			get { return _ctx.Resources.GetString(Resource.String.menu_url); }
		}

		public void HandleClick()
		{
			//TODO
			_ctx.GotoUrl();
		}
	}

	class ToggleVisibilityPopupMenuItem : IPopupMenuItem
	{
		private readonly EntryActivity _activity;
		

		public ToggleVisibilityPopupMenuItem(EntryActivity activity)
		{
			_activity = activity;
			
		}

		public Drawable Icon 
		{ 
			get
			{
				//return new TextDrawable("\uF06E", _activity);
				return _activity.Resources.GetDrawable(Resource.Drawable.ic_action_eye_open);
				
			}
		}
		public string Text
		{
			get
			{
				return _activity.Resources.GetString(
					_activity._showPassword ? 
						Resource.String.menu_hide_password 
						: Resource.String.show_password);
			}
		}

		
		public void HandleClick()
		{
			_activity.ToggleVisibility();
		}
	}

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