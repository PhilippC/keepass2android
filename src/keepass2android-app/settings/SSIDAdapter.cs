using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;

namespace keepass2android
{
    public class SSIDAdapter : RecyclerView.Adapter
    {
        public List<string> SSIDs { get; private set; }
        public event Action<int> DeleteClicked;

        public SSIDAdapter(List<string> ssids)
        {
            SSIDs = ssids;
        }

        public override int ItemCount => SSIDs.Count;

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            if (holder is SSIDViewHolder vh)
            {
                vh.TextView.Text = SSIDs[position];
                vh.DeleteButton.Click -= vh.OnDeleteClick;
                vh.OnDeleteClick = (s, e) =>
                {
                    int currentPosition = vh.AdapterPosition;
                    if (currentPosition != RecyclerView.NoPosition && currentPosition < SSIDs.Count)
                    {
                        DeleteClicked?.Invoke(currentPosition);
                    }
                };
                vh.DeleteButton.Click += vh.OnDeleteClick;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.item_ssid, parent, false);
            return new SSIDViewHolder(itemView);
        }

        public void RemoveAt(int position)
        {
            if (position >= 0 && position < SSIDs.Count)
            {
                SSIDs.RemoveAt(position);
                NotifyItemRemoved(position);
            }
        }
    }
    public class SSIDViewHolder : RecyclerView.ViewHolder
    {
        public TextView TextView { get; private set; }
        public ImageButton DeleteButton { get; private set; }
        public EventHandler OnDeleteClick { get; set; }

        public SSIDViewHolder(View itemView) : base(itemView)
        {
            TextView = itemView.FindViewById<TextView>(Resource.Id.tvSSIDName);
            DeleteButton = itemView.FindViewById<ImageButton>(Resource.Id.btnDelete);
        }
    }
}
