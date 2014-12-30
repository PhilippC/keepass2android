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
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Text.Method;
using Android.Widget;
using Android.Content.PM;

namespace keepass2android
{
	
	public class AboutDialog : Dialog {
		
		public AboutDialog(Context context):base (context) {
		}
		public AboutDialog(Context context, int theme)
			: base(context, theme)
		{
		}
		
		public AboutDialog(IntPtr javaRef, JniHandleOwnership transfer) : base(javaRef, transfer)
		{
			
		}
		
		
		protected override void OnCreate(Bundle savedInstanceState) {
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.about);
			SetTitle(Resource.String.app_name);
			
			SetVersion();
			SetContributors();
			
			FindViewById(Resource.Id.suggest).Click += delegate
				{
					try
					{
						Util.GotoUrl(Context, Resource.String.SuggestionsURL);
					}
					catch (ActivityNotFoundException)
					{
						Toast.MakeText(Context, Resource.String.no_url_handler, ToastLength.Long).Show();
					}

				};
			FindViewById(Resource.Id.rate).Click += delegate
			{
				try
				{
					Util.GotoMarket(Context);
				}
				catch (ActivityNotFoundException)
				{
					Toast.MakeText(Context, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
			};
			FindViewById(Resource.Id.translate).Click += delegate
			{
				try
				{
					Util.GotoUrl(Context, Resource.String.TranslationURL);
				}
				catch (ActivityNotFoundException)
				{
					Toast.MakeText(Context, Resource.String.no_url_handler, ToastLength.Long).Show();
				}
			}; FindViewById(Resource.Id.donate).Click += delegate
			{
				Util.GotoDonateUrl(Context);
			};
		}

		private void SetContributors()
		{
			TextView tv = (TextView)FindViewById(Resource.Id.further_authors);
			tv.Text = Context.GetString(Resource.String.further_authors, new Java.Lang.Object[] { Context.GetString(Resource.String.further_author_names) });

			TextView tvdesigners = (TextView)FindViewById(Resource.Id.designers);
			tvdesigners.Text = Context.GetString(Resource.String.designers, new Java.Lang.Object[] { Context.GetString(Resource.String.designer_names) });
		}

		private void SetVersion() {
			Context ctx = Context;
			
			String version;
			try {
				PackageInfo packageInfo = ctx.PackageManager.GetPackageInfo(ctx.PackageName, 0);
				version = packageInfo.VersionName;
				
			} catch (PackageManager.NameNotFoundException) {
				version = "";
			}
			
			TextView tv = (TextView) FindViewById(Resource.Id.versionX);
			tv.Text = version;

			FindViewById(Resource.Id.versionB).Click += (sender, args) => ChangeLog.ShowChangeLog(ctx, () => { });
		}
		
	}

}

