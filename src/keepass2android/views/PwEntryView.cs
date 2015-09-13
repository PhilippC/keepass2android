/*
This file is part of Keepass2Android, Copyright 2013 Philipp Crocoll. This file is based on Keepassdroid, Copyright Brian Pellin.

  Keepass2Android is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  Keepass2Android is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.
  */
using System;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using Android.Text;
using Android.Text.Style;
using Android.Preferences;


namespace keepass2android.view
{
	public sealed class PwEntryView : GroupListItemView
	{
		private readonly GroupBaseActivity _groupActivity;
		private PwEntry _entry;
		private readonly TextView _textView;
		private readonly TextView _textviewDetails;
		private readonly TextView _textgroupFullPath;

		private int _pos;

		private int? _defaultTextColor;

		readonly bool _showDetail;
		readonly bool _showGroupFullPath;
		readonly bool _isSearchResult;


		private const int MenuOpen = Menu.First;
		private const int MenuDelete = MenuOpen + 1;
		private const int MenuMove = MenuDelete + 1;
		private const int MenuNavigate = MenuMove + 1;
		
		public static PwEntryView GetInstance(GroupBaseActivity act, PwEntry pw, int pos)
		{
			return new PwEntryView(act, pw, pos);

		}

		public PwEntryView(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
			
		}

		private PwEntryView(GroupBaseActivity groupActivity, PwEntry pw, int pos):base(groupActivity)
		{
			_groupActivity = groupActivity;
			
			View ev = Inflate(groupActivity, Resource.Layout.entry_list_entry, null);
			_textView = (TextView)ev.FindViewById(Resource.Id.entry_text);
			_textView.TextSize = PrefsUtil.GetListTextSize(groupActivity);

			_textviewDetails = (TextView)ev.FindViewById(Resource.Id.entry_text_detail);
			_textviewDetails.TextSize = PrefsUtil.GetListDetailTextSize(groupActivity);

			_textgroupFullPath = (TextView)ev.FindViewById(Resource.Id.group_detail);
			_textgroupFullPath.TextSize = PrefsUtil.GetListDetailTextSize(groupActivity);

			_showDetail = PreferenceManager.GetDefaultSharedPreferences(groupActivity).GetBoolean(
				groupActivity.GetString(Resource.String.ShowUsernameInList_key), 
				Resources.GetBoolean(Resource.Boolean.ShowUsernameInList_default));

			_showGroupFullPath = PreferenceManager.GetDefaultSharedPreferences(groupActivity).GetBoolean(
				groupActivity.GetString(Resource.String.ShowGroupnameInSearchResult_key), 
				Resources.GetBoolean(Resource.Boolean.ShowGroupnameInSearchResult_default));

			_isSearchResult = _groupActivity is keepass2android.search.SearchResults;


			PopulateView(ev, pw, pos);
			
			LayoutParams lp = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
			
			AddView(ev, lp);
			
		}
		
		private void PopulateView(View ev, PwEntry pw, int pos)
		{
			_entry = pw;
			_pos = pos;
			
			ImageView iv = (ImageView)ev.FindViewById(Resource.Id.icon);
			bool isExpired = pw.Expires && pw.ExpiryTime < DateTime.Now;
			if (isExpired)
			{
				App.Kp2a.GetDb().DrawableFactory.AssignDrawableTo(iv, Resources, App.Kp2a.GetDb().KpDatabase, PwIcon.Expired, PwUuid.Zero, false);
			} else
			{
				App.Kp2a.GetDb().DrawableFactory.AssignDrawableTo(iv, Resources, App.Kp2a.GetDb().KpDatabase, pw.IconId, pw.CustomIconUuid, false);
			}

			String title = pw.Strings.ReadSafe(PwDefs.TitleField);
			var str = new SpannableString(title);

			if (isExpired)
			{
				str.SetSpan(new StrikethroughSpan(), 0, title.Length, SpanTypes.ExclusiveExclusive);
			}
			_textView.TextFormatted = str;

			if (_defaultTextColor == null)
				_defaultTextColor = _textView.TextColors.DefaultColor;

			if (_groupActivity.IsBeingMoved(_entry.Uuid))
			{
				int elementBeingMoved = Context.Resources.GetColor(Resource.Color.element_being_moved);
				_textView.SetTextColor(new Color(elementBeingMoved));
			}
			else
				_textView.SetTextColor(new Color((int)_defaultTextColor));

			String detail = pw.Strings.ReadSafe(PwDefs.UserNameField);

			if ((_showDetail == false) || (String.IsNullOrEmpty(detail)))
			{
				_textviewDetails.Visibility = ViewStates.Gone;
			}
			else
			{
				var strDetail = new SpannableString(detail);
				
				if (isExpired)
				{
					strDetail.SetSpan(new StrikethroughSpan(), 0, detail.Length, SpanTypes.ExclusiveExclusive);
				}
				_textviewDetails.TextFormatted = strDetail;

				_textviewDetails.Visibility = ViewStates.Visible;
			}
				
			if ( (!_showGroupFullPath) || (!_isSearchResult) ) {
				_textgroupFullPath.Visibility = ViewStates.Gone;
			}

			else {
				String groupDetail = pw.ParentGroup.GetFullPath();

				var strGroupDetail = new SpannableString (groupDetail);

				if (isExpired) {
					strGroupDetail.SetSpan (new StrikethroughSpan (), 0, groupDetail.Length, SpanTypes.ExclusiveExclusive);
				}
				_textgroupFullPath.TextFormatted = strGroupDetail;

				_textgroupFullPath.Visibility = ViewStates.Visible;
			}

		}
		
		public void ConvertView(PwEntry pw, int pos)
		{
			PopulateView(this, pw, pos);
		}

		
		private void LaunchEntry()
		{
			_groupActivity.LaunchActivityForEntry(_entry, _pos);
			_groupActivity.OverridePendingTransition(Resource.Animation.anim_enter, Resource.Animation.anim_leave);
		}
		/*
		public override void OnCreateMenu(IContextMenu menu, IContextMenuContextMenuInfo menuInfo)
		{
			menu.Add(0, MenuOpen, 0, Resource.String.menu_open);
			if (App.Kp2a.GetDb().CanWrite)
			{
				menu.Add(0, MenuDelete, 0, Resource.String.menu_delete);
				menu.Add(0, MenuMove, 0, Resource.String.menu_move);

				if (_isSearchResult) {
					menu.Add (0, MenuNavigate, 0, Resource.String.menu_navigate);
				}

			}
		}
		
		public override bool OnContextItemSelected(IMenuItem item)
		{
			switch (item.ItemId)
			{
				
				case MenuOpen:
					LaunchEntry();
					return true;
				case MenuDelete:
					Handler handler = new Handler();
					DeleteEntry task = new DeleteEntry(Context, App.Kp2a, _entry, new GroupBaseActivity.RefreshTask(handler, _groupActivity));
					task.Start();
					return true;
				case MenuMove:
					NavigateToFolderAndLaunchMoveElementTask navMove = 
						new NavigateToFolderAndLaunchMoveElementTask(_entry.ParentGroup, _entry.Uuid, _isSearchResult);
					_groupActivity.StartTask (navMove);
					return true;
				case MenuNavigate: 
					NavigateToFolder navNavigate = new NavigateToFolder(_entry.ParentGroup, true);
					_groupActivity.StartTask (navNavigate);
					return true;

				default:
					return false;
			}
		}

		*/
	    public override void OnClick()
	    {
	        LaunchEntry();
	    }
	}
}

