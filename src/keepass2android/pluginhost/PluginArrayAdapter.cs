using System;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Widget;
using Android.Content;
using Android.Views;
using System.Collections.Generic;
using Android.App;
using Android.Runtime;

namespace keepass2android
{
	/// <summary>
	/// Represents information about a plugin for display in the plugin list activity
	/// </summary>
	public class PluginItem
	{
		private readonly string _package;
		private readonly Context _ctx;
		private readonly Resources _pluginRes;

		public PluginItem(string package, string enabledStatus, Context ctx)
		{
			_package = package;
			_ctx = ctx;
			EnabledStatus = enabledStatus;
			_pluginRes = _ctx.PackageManager.GetResourcesForApplication(_package);
		}

		public string Label
		{
			get
			{
				return PluginDetailsActivity.GetStringFromPlugin(_pluginRes, _package, "kp2aplugin_title");
			}
			
		}

		public string Version
		{
			get
			{
				return _ctx.PackageManager.GetPackageInfo(_package, 0).VersionName;
			}
		}

		public string EnabledStatus
		{
			get;
			set;
		}

		public Drawable Icon
		{
			get
			{
				return _ctx.PackageManager.GetApplicationIcon(_package);
			}
		}

		public string Package
		{
			get { return _package; }
		}
	}


	public class PluginArrayAdapter : ArrayAdapter<PluginItem>
	{

		class PluginViewHolder : Java.Lang.Object
		{
			public ImageView imgIcon;
			public TextView txtTitle;
			public TextView txtVersion;

			public TextView txtEnabledStatus;
		}

		Context context;
		int layoutResourceId;
		IList<PluginItem> data = null;

		public PluginArrayAdapter(IntPtr javaReference, JniHandleOwnership transfer)
			: base(javaReference, transfer)
		{
		}

		public PluginArrayAdapter(Context context, int layoutResourceId, IList<PluginItem> data) :
			base(context, layoutResourceId, data)
		{

			this.layoutResourceId = layoutResourceId;
			this.context = context;
			this.data = data;
		}

		public override View GetView(int position, View convertView, ViewGroup parent)
		{
			View row = convertView;
			PluginViewHolder holder = null;

			if (row == null)
			{
				LayoutInflater inflater = ((Activity)context).LayoutInflater;
				row = inflater.Inflate(layoutResourceId, parent, false);

				holder = new PluginViewHolder();
				holder.imgIcon = (ImageView)row.FindViewById(Resource.Id.imgIcon);
				holder.txtTitle = (TextView)row.FindViewById(Resource.Id.txtLabel);
				holder.txtVersion = (TextView)row.FindViewById(Resource.Id.txtVersion);
				holder.txtEnabledStatus = (TextView)row.FindViewById(Resource.Id.txtStatus);

				row.Tag = holder;
			}
			else
			{
				holder = (PluginViewHolder)row.Tag;
			}

			var item = data[position];
			holder.txtTitle.Text = item.Label;
			holder.txtVersion.Text = item.Version;
			holder.txtEnabledStatus.Text = item.EnabledStatus;
			holder.imgIcon.SetImageDrawable(item.Icon);

			return row;
		}
	}

}
