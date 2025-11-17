// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

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
