using Android.Widget;
using Android.Content;
using Android.Views;
using System.Collections.Generic;
using Android.App;
using PluginHostTest;

namespace keepass2android
{

	public class PluginItem
	{
		private readonly string _package;

		public PluginItem(string package, string _label, int _icon, string _version, string _enabledStatus)
		{
			_package = package;
			Label = _label;
			Icon = _icon;
			Version = _version;
			EnabledStatus = _enabledStatus;
		}

		public string Label
		{
			get;
			set;
		}

		public string Version
		{
			get;
			set;
		}

		public string EnabledStatus
		{
			get;
			set;
		}

		public int Icon
		{
			get;
			set;
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
			holder.imgIcon.SetImageResource(item.Icon);

			return row;
		}
	}

}
