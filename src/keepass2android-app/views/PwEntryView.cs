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
using System.Linq;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using KeePassLib;
using Android.Text;
using Android.Text.Style;
using Android.Preferences;
using KeePass.Util.Spr;
using KeeTrayTOTP.Libraries;
using PluginTOTP;
using Android.Content;
using System.ComponentModel;
using keepass2android;


namespace keepass2android.view
{
    public sealed class PwEntryView : GroupListItemView
    {
        private readonly GroupBaseActivity _groupActivity;
        private PwEntry _entry;
        private readonly TextView _textView;
        private readonly TextView _textviewDetails;
        private readonly TextView _textgroupFullPath;
        private readonly ProgressBar _totpCountdown;
        private readonly TextView _totpText;
        private readonly LinearLayout _totpLayout;

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

        private PwEntryView(GroupBaseActivity groupActivity, PwEntry pw, int pos) : base(groupActivity)
        {
            _groupActivity = groupActivity;

            View ev = Inflate(groupActivity, Resource.Layout.entry_list_entry, null);
            _textView = (TextView)ev.FindViewById(Resource.Id.entry_text);
            _textView.TextSize = PrefsUtil.GetListTextSize(groupActivity);

            Database db = App.Kp2a.FindDatabaseForElement(pw);

            ev.FindViewById(Resource.Id.entry_icon_bkg).Visibility = db.DrawableFactory.IsWhiteIconSet ? ViewStates.Visible : ViewStates.Gone;

            _textviewDetails = (TextView)ev.FindViewById(Resource.Id.entry_text_detail);
            _textviewDetails.TextSize = PrefsUtil.GetListDetailTextSize(groupActivity);

            _textgroupFullPath = (TextView)ev.FindViewById(Resource.Id.group_detail);
            _textgroupFullPath.TextSize = PrefsUtil.GetListDetailTextSize(groupActivity);

            _totpCountdown = ev.FindViewById<ProgressBar>(Resource.Id.TotpCountdownProgressBar);
            _totpText = ev.FindViewById<TextView>(Resource.Id.totp_text);
            _totpLayout = ev.FindViewById<LinearLayout>(Resource.Id.totp_layout);

            _totpLayout.LongClick += (sender, args) =>
            {
                string totp = UpdateTotp();
                if (!String.IsNullOrEmpty(totp))
                    CopyToClipboardService.CopyValueToClipboardWithTimeout(_groupActivity, totp, true);
            };

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

            if (_groupBaseActivity.IsFinishing)
                return;
            _entry = pw;
            _pos = pos;
            try
            {


                ev.FindViewById(Resource.Id.icon).Visibility = ViewStates.Visible;
                ev.FindViewById(Resource.Id.check_mark).Visibility = ViewStates.Invisible;

                _db = App.Kp2a.TryFindDatabaseForElement(_entry);
                if (_db == null)
                {
                    ev.FindViewById(Resource.Id.icon).Visibility = ViewStates.Gone;
                    _textView.TextFormatted = new SpannableString("(no data)");
                    _textviewDetails.Visibility = ViewStates.Gone;
                    _textgroupFullPath.Visibility = ViewStates.Gone;
                    return;
                }

                ImageView iv = (ImageView)ev.FindViewById(Resource.Id.icon);
                bool isExpired = pw.Expires && pw.ExpiryTime < DateTime.Now;
                if (isExpired)
                {
                    _db.DrawableFactory.AssignDrawableTo(iv, Context, _db.KpDatabase, PwIcon.Expired, PwUuid.Zero, false);
                }
                else
                {
                    _db.DrawableFactory.AssignDrawableTo(iv, Context, _db.KpDatabase, pw.IconId, pw.CustomIconUuid, false);
                }

                String title = pw.Strings.ReadSafe(PwDefs.TitleField);
                title = SprEngine.Compile(title, new SprContext(_entry, _db.KpDatabase, SprCompileFlags.All));
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
                    int elementBeingMoved = Context.Resources.GetColor(Resource.Color.md_theme_inversePrimary);
                    _textView.SetTextColor(new Color(elementBeingMoved));
                }
                else
                    _textView.SetTextColor(new Color((int)_defaultTextColor));

                String detail = pw.Strings.ReadSafe(PwDefs.UserNameField);
                detail = SprEngine.Compile(detail, new SprContext(_entry, _db.KpDatabase, SprCompileFlags.All));

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

                if ((!_showGroupFullPath) || (!_isSearchResult))
                {
                    _textgroupFullPath.Visibility = ViewStates.Gone;
                }

                else
                {
                    String groupDetail = pw.ParentGroup.GetFullPath();
                    if (App.Kp2a.OpenDatabases.Count() > 1)
                    {
                        groupDetail += "(" + App.Kp2a.GetFileStorage(_db.Ioc).GetDisplayName(_db.Ioc) + ")";
                    }

                    var strGroupDetail = new SpannableString(groupDetail);

                    if (isExpired)
                    {
                        strGroupDetail.SetSpan(new StrikethroughSpan(), 0, groupDetail.Length, SpanTypes.ExclusiveExclusive);
                    }
                    _textgroupFullPath.TextFormatted = strGroupDetail;

                    _textgroupFullPath.Visibility = ViewStates.Visible;
                }

                //try to get totp data
                UpdateTotp();

            }
            catch (Exception e)
            {
                Kp2aLog.LogUnexpectedError(e);

            }


        }

        public void ConvertView(PwEntry pw, int pos)
        {
            PopulateView(this, pw, pos);
        }


        private void LaunchEntry()
        {
            _groupActivity.LaunchActivityForEntry(_entry, _pos);
            //_groupActivity.OverridePendingTransition(Resource.Animation.anim_enter, Resource.Animation.anim_leave);
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



        private TotpData _totpData;

        private Database _db;

        public string UpdateTotp()
        {
            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(_groupActivity);
            bool showTotpDefault = _groupActivity.MayPreviewTotp;


            if (showTotpDefault)
                _totpData = new Kp2aTotp().TryGetTotpData(new PwEntryOutput(_entry, _db));
            else
                _totpData = null;

            if (_totpData?.IsTotpEntry != true)
            {
                _totpLayout.Visibility = ViewStates.Gone;
                return null;
            }

            _totpLayout.Visibility = ViewStates.Visible;

            TOTPProvider prov = new TOTPProvider(_totpData);
            string totp = prov.GenerateByByte(_totpData.TotpSecret);

            _totpText.Text = totp;
            var progressBar = _totpCountdown;
            progressBar.Progress = prov.Timer;
            progressBar.Max = prov.Duration;

            return totp;
        }
    }
}

