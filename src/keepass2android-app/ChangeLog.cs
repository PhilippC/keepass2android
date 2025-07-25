﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Text;
using Android.Text.Method;
using Android.Text.Util;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.Core.Content;
using Google.Android.Material.Dialog;
using keepass2android;

namespace keepass2android
{
	public static class ChangeLog
	{
		public static void ShowChangeLog(Context ctx, Action onDismiss)
		{
			MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(ctx);
			builder.SetTitle(ctx.GetString(Resource.String.ChangeLog_title));
			List<string> changeLog = new List<string>{
				#if !NoNet
				BuildChangelogString(ctx, new List<int>{Resource.Array.ChangeLog_1_14_net}, "1.14"),
				#endif

                BuildChangelogString(ctx, new List<int>{Resource.Array.ChangeLog_1_13}, "1.13"),

                BuildChangelogString(ctx, new List<int>{Resource.Array.ChangeLog_1_12
#if !NoNet
					,Resource.Array.ChangeLog_1_12_net
#endif
                }, "1.12"),
                BuildChangelogString(ctx, new List<int>{Resource.Array.ChangeLog_1_11
#if !NoNet
                    ,Resource.Array.ChangeLog_1_11_net
#endif
                }, "1.11"),
                BuildChangelogString(ctx, Resource.Array.ChangeLog_1_10, "1.10"),
                BuildChangelogString(ctx, Resource.Array.ChangeLog_1_09e, "1.09e"),
				BuildChangelogString(ctx, Resource.Array.ChangeLog_1_09d, "1.09d"),
				BuildChangelogString(ctx, Resource.Array.ChangeLog_1_09c, "1.09c"),
				BuildChangelogString(ctx, Resource.Array.ChangeLog_1_09b, "1.09b"),
				BuildChangelogString(ctx, Resource.Array.ChangeLog_1_09a, "1.09a"),
				BuildChangelogString(ctx, Resource.Array.ChangeLog_1_08d, "1.08d"),
				BuildChangelogString(ctx, Resource.Array.ChangeLog_1_08c, "1.08c"),
				BuildChangelogString(ctx, Resource.Array.ChangeLog_1_08b, "1.08b"),
				BuildChangelogString(ctx, Resource.Array.ChangeLog_1_08, "1.08"),
			    ctx.GetString(Resource.String.ChangeLog_1_07b),
                ctx.GetString(Resource.String.ChangeLog_1_07),
                ctx.GetString(Resource.String.ChangeLog_1_06),
                ctx.GetString(Resource.String.ChangeLog_1_05),
				ctx.GetString(Resource.String.ChangeLog_1_04b),
                ctx.GetString(Resource.String.ChangeLog_1_04),
                ctx.GetString(Resource.String.ChangeLog_1_03),
				ctx.GetString(Resource.String.ChangeLog_1_02),
#if !NoNet
				ctx.GetString(Resource.String.ChangeLog_1_01g),
				ctx.GetString(Resource.String.ChangeLog_1_01d),
#endif
				ctx.GetString(Resource.String.ChangeLog_1_01),
				ctx.GetString(Resource.String.ChangeLog_1_0_0e),
				ctx.GetString(Resource.String.ChangeLog_1_0_0),
				ctx.GetString(Resource.String.ChangeLog_0_9_9c),				
				ctx.GetString(Resource.String.ChangeLog_0_9_9),				
				ctx.GetString(Resource.String.ChangeLog_0_9_8c),				
					ctx.GetString(Resource.String.ChangeLog_0_9_8b),				
					ctx.GetString(Resource.String.ChangeLog_0_9_8),
#if !NoNet
					//0.9.7b fixes were already included in 0.9.7 offline
					ctx.GetString(Resource.String.ChangeLog_0_9_7b),
#endif
					ctx.GetString(Resource.String.ChangeLog_0_9_7),
					ctx.GetString(Resource.String.ChangeLog_0_9_6),
					ctx.GetString(Resource.String.ChangeLog_0_9_5),
					ctx.GetString(Resource.String.ChangeLog_0_9_4),
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

			String version;
			try {
				PackageInfo packageInfo = ctx.PackageManager.GetPackageInfo(ctx.PackageName, 0);
				version = packageInfo.VersionName;
				
			} catch (PackageManager.NameNotFoundException) {
				version = "";
			}

			string warning = "";
			if (version.Contains("pre"))
			{
				warning = ctx.GetString(Resource.String.PreviewWarning);
			}

			builder.SetPositiveButton(Android.Resource.String.Ok, (dlgSender, dlgEvt) => {((AndroidX.AppCompat.App.AlertDialog)dlgSender).Dismiss(); });
			builder.SetCancelable(false);

			WebView wv = new WebView(ctx);

			
            
            

			builder.SetView(wv);
			Dialog dialog = builder.Create();
            dialog.DismissEvent += (sender, e) =>
			{
				onDismiss();
			};
            wv.SetBackgroundColor(Color.Transparent);
            wv.LoadDataWithBaseURL(null, GetLog(changeLog, warning, dialog.Context), "text/html", "UTF-8", null);

            dialog.Show();
		}

        private static string BuildChangelogString(Context ctx, int changeLogResId, string version)
        {
            return BuildChangelogString(ctx, new List<int>() { changeLogResId }, version);

        }


        private static string BuildChangelogString(Context ctx, List<int> changeLogResIds, string version)
        {
            string result = "Version " + version + "\n";
            string previous = "";
            foreach (var changeLogResId in changeLogResIds)
            {
                foreach (var item in ctx.Resources.GetStringArray(changeLogResId))
                {
                    if (item == previous) //there was some trouble with crowdin translations, remove duplicates
                        continue;
                    result += " * " + item + "\n";
                    previous = item;
                }
            }
            
            return result;

        }

		private const string HtmlEnd = @"</body>
</html>";
    
		private static string GetLog(List<string> changeLog, string warning, Context ctx)
        {
            string secondaryColor = "31628D";
            string onSurfaceColor = "171D1E";
            if (((int)ctx.Resources.Configuration.UiMode & (int)UiMode.NightMask) == (int)UiMode.NightYes)
            {
                secondaryColor = "99CBFF";
                onSurfaceColor = "E1E4D6";
            }
                

            string HtmlStart = @"<html>
  <head>
    <style type='text/css'>
      a            { color:#"+ onSurfaceColor + @" }
      div.title    { 
          color:"+ secondaryColor+@"; 
          font-size:1.2em; 
          font-weight:bold; 
          margin-top:1em; 
          margin-bottom:0.5em; 
          text-align:center }
      div.subtitle { 
          color:"+ secondaryColor+@"; 
          font-size:0.8em; 
          margin-bottom:1em; 
          text-align:center }
      div.freetext { color:#"+ onSurfaceColor + @" }
      div.list     { color:#"+ onSurfaceColor + @" }
    </style>
  </head>
  <body>";


            StringBuilder sb = new StringBuilder(HtmlStart);
			if (!string.IsNullOrEmpty(warning))
			{
				sb.Append(warning);
			}
			bool inList = false;
			bool isFirst = true;
			foreach (string versionLog in changeLog)
			{
				string versionLog2 = versionLog; 
				bool title = true;
			    if (isFirst)
			    {

			        bool showDonateOption = true;
			        ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(ctx);
			        if (prefs.GetBoolean(ctx.GetString(Resource.String.NoDonationReminder_key), false))
			            showDonateOption = false;

			        long usageCount = prefs.GetLong(ctx.GetString(Resource.String.UsageCount_key), 0);

			        if (usageCount <= 5)
			            showDonateOption = false;

			        if (showDonateOption)
			        {
			            if (versionLog2.EndsWith("\n") == false)
			                versionLog2 += "\n";
			            string donateUrl = ctx.GetString(Resource.String.donate_url,
			                new Java.Lang.Object[]{ctx.Resources.Configuration.Locale.Language,
			                    ctx.PackageName
			                });

			            versionLog2 += " * <a href=\"" + donateUrl
			                           + "\">" +
			                           ctx.GetString(Resource.String.ChangeLog_keptDonate)
			                           + "<a/>";
			        }
			        isFirst = false;
			    }
                foreach (string line in versionLog2.Split('\n'))
				{
					string w = line.Trim();
					if (title)
					{
						if (inList)
						{
							sb.Append("</ul></div>\n");
							inList = false;
						}
						w = w.Replace("<b>","");
						w = w.Replace("</b>", "");
						w = w.Replace("\\n", "");
						sb.Append("<div class='title'>"
								+ w.Trim() + "</div>\n");
						title = false;
					}
					else
					{
						w = w.Replace("\\n", "<br />");
						if ((w.StartsWith("*") || (w.StartsWith("•"))))
						{
							if (!inList)
							{
								sb.Append("<div class='list'><ul>\n");
								inList = true;
							}
							sb.Append("<li>");
							sb.Append(w.Substring(1).Trim());
							sb.Append("</li>\n");
						}
						else
						{
							if (inList)
							{
								sb.Append("</ul></div>\n");
								inList = false;
							}
							sb.Append(w);
						}
					}
				}
			}
			sb.Append(HtmlEnd);
			return sb.ToString();
		}
	}
}