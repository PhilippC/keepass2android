using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Text.Method;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
	public static class ChangeLog
	{
		public static void ShowChangeLog(Context ctx, Action onDismiss)
		{
			AlertDialog.Builder builder = new AlertDialog.Builder(ctx);
			builder.SetTitle(ctx.GetString(Resource.String.ChangeLog_title));
			String[] changeLog = {
					ctx.GetString(Resource.String.ChangeLog_0_9_3_r5),
					ctx.GetString(Resource.String.ChangeLog_0_9_3),
					ctx.GetString(Resource.String.ChangeLog_0_9_2),
					ctx.GetString(Resource.String.ChangeLog_0_9_1),
					ctx.GetString(Resource.String.ChangeLog_0_9),
					ctx.GetString(Resource.String.ChangeLog_0_8_6),
					ctx.GetString(Resource.String.ChangeLog_0_8_5),
					ctx.GetString(Resource.String.ChangeLog_0_8_4),
					ctx.GetString(Resource.String.ChangeLog_0_8_3),
					ctx.GetString(Resource.String.ChangeLog_0_8_2),
					ctx.GetString(Resource.String.ChangeLog_0_8_1),
					ctx.GetString(Resource.String.ChangeLog_0_8),
					ctx.GetString(Resource.String.ChangeLog_0_7),
					ctx.GetString(Resource.String.ChangeLog)
					 };

			builder.SetPositiveButton(Android.Resource.String.Ok, (dlgSender, dlgEvt) => { });

			builder.SetMessage("temp");
			Dialog dialog = builder.Create();
			dialog.DismissEvent += (sender, e) =>
			{
				onDismiss();
			};
			dialog.Show();
			TextView message = (TextView)dialog.FindViewById(Android.Resource.Id.Message);

			message.MovementMethod = LinkMovementMethod.Instance;
			message.TextFormatted = Html.FromHtml(ConcatChangeLog(ctx, changeLog));
			message.LinksClickable = true;

		}


		static string ConcatChangeLog(Context ctx, string[] changeLog)
		{
			string res = "";
			bool isFirst = true;
			foreach (string c in changeLog)
			{
				res += c;
				if (isFirst)
				{
					if (res.EndsWith("\n") == false)
						res += "\n";
					string donateUrl = ctx.GetString(Resource.String.donate_url,
														 new Java.Lang.Object[]{ctx.Resources.Configuration.Locale.Language,
						ctx.PackageName
					});
					res += " * <a href=\"" + donateUrl
						+ "\">" +
						ctx.GetString(Resource.String.ChangeLog_keptDonate)
							+ "<a/>";
					isFirst = false;
				}

				while (res.EndsWith("\n\n") == false)
					res += "\n";
			}
			return res.Replace("\n", "<br>");

		}
	}
}