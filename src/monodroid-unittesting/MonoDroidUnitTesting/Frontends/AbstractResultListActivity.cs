//
// Copyright (C) 2012 Maya Studios (http://mayastudios.com)
//
// This file is part of MonoDroidUnitTesting.
//
// MonoDroidUnitTesting is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MonoDroidUnitTesting is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with MonoDroidUnitTesting. If not, see <http://www.gnu.org/licenses/>.
//
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using MonoDroidUnitTesting.Utils;


namespace MonoDroidUnitTesting {
  public abstract class AbstractResultListActivity<T> : AbstractResultActivity {
    protected ArrayAdapter<T> ListAdapter { get; private set; }

    protected AbstractResultListActivity(string iconFileNamePrefix) : base(iconFileNamePrefix) { }

    protected override View CreateMainView() {
      LinearLayout mainLayout = new LinearLayout(this);
      mainLayout.Orientation = Orientation.Vertical;

      View headerView = CreateHeaderView();
      if (headerView != null) {
        mainLayout.AddView(headerView);
      }

      ListView listView = new ListView(this);
      this.ListAdapter = CreateListAdapter();
      listView.Adapter = this.ListAdapter;
      listView.CacheColorHint = Color.Transparent; // IMPORTANT! Otherwise there is flickering!
      listView.ItemClick += (s, e) => OnItemClicked(e);
      mainLayout.AddView(listView);

      return mainLayout;
    }

    protected virtual View CreateHeaderView() {
      return null;
    }

    protected abstract ArrayAdapter<T> CreateListAdapter();

    protected abstract void OnItemClicked(AdapterView.ItemClickEventArgs e);

    public abstract class TestResultAdapter : ArrayAdapter<T> {
      protected AbstractResultListActivity<T> Activity { get; private set; }

      public TestResultAdapter(AbstractResultListActivity<T> activity) : base(activity, 0) {
        this.Activity = activity;
      }

      protected abstract TestState GetStateFor(T item);

      protected abstract string GetHTMLDescriptionFor(T item);

      public override View GetView(int position, View convertView, ViewGroup parent) {
        ResultListItemView view = convertView as ResultListItemView;
        if (view == null) {
          view = new ResultListItemView(this.Context);
        }

        T item = GetItem(position);

        view.SetIcon(this.Activity.GetIconForState(GetStateFor(item)));
        view.SetHtml(GetHTMLDescriptionFor(item));

        return view;
      }
    }
  }
}