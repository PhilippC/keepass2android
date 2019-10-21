using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Preferences;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using keepass2android.services.AutofillBase;

namespace keepass2android
{
    public class AutofillDisabledQueriesPreference : ListPreference
    {
        

        private class DisabledQuery
        {
            public string Query { get; set; }
            public string DisplayName { get; set; }
        }

        private class DisabledQueryPreferenceScreenAdapter : BaseAdapter
        {
            public Dictionary<string, bool> DisabledQueriesValues = new Dictionary<string, bool>();

            private readonly AutofillDisabledQueriesPreference _pref;

            public DisabledQueryPreferenceScreenAdapter(AutofillDisabledQueriesPreference pref, Context context)
            {
                _pref = pref;
            }


            private class CustomHolder : Java.Lang.Object
            {
                private TextView text = null;
                private CheckBox checkbox = null;

                public CustomHolder(View row, int position, AutofillDisabledQueriesPreference pref)
                {
                    text = (TextView) row.FindViewById(Resource.Id.disabled_query_text);
                    text.Text = pref.DisabledQueries[position].DisplayName;

                    checkbox = (CheckBox) row.FindViewById(Resource.Id.disabled_query_checkbox);
                    checkbox.Id = position;
                    checkbox.Clickable = true;
                    checkbox.Checked = true;

                }

                public CheckBox Checkbox
                {
                    get { return checkbox; }
                }
            }

            public override Java.Lang.Object GetItem(int position)
            {
                return null;
            }

            public override long GetItemId(int position)
            {
                return position;
            }



            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                View row = convertView;
                CustomHolder holder = null;
                int p = position;
                row = LayoutInflater.From(_pref.Context)
                    .Inflate(Resource.Layout.disabled_queries_preference_row, parent, false);
                holder = new CustomHolder(row, position, _pref);

                row.Tag = holder;

                /*row.Clickable = true;
                row.Focusable = true;
                row.FocusableInTouchMode = true;
                */
                ((CustomHolder) row.Tag).Checkbox.CheckedChange += (sender, args) =>
                {
                    DisabledQueriesValues[_pref.DisabledQueries[p].Query] = args.IsChecked;
                };
                

                return row;
            }

            public override int Count
            {
                get { return _pref.DisabledQueries.Count; }
            }
        }


        List<DisabledQuery> _disabledQueries = null;

        private List<DisabledQuery> DisabledQueries
        {
            get
            {               
                var prefs = PreferenceManager.GetDefaultSharedPreferences(this.Context);
                _disabledQueries = prefs.GetStringSet("AutoFillDisabledQueries", new List<string>()).Select(str =>
                    new DisabledQuery() {Query = str, DisplayName = AutofillServiceBase.GetDisplayNameForQuery(str, Context)}).ToList();

                return _disabledQueries;
            }
        }

        

        protected AutofillDisabledQueriesPreference(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        private readonly Task _populatorTask;

        public AutofillDisabledQueriesPreference(Context context, IAttributeSet attrs)
            : base(context, attrs)
        {
            _populatorTask = Task.Factory.StartNew(() =>
            {
                SetEntries(DisabledQueries.Select(s => s.DisplayName).ToArray());
                SetEntryValues(DisabledQueries.Select(s => s.Query).ToArray());
            });

        }


        protected override void OnPrepareDialogBuilder(AlertDialog.Builder builder)
        {
            _populatorTask.Wait();
            base.OnPrepareDialogBuilder(builder);


            var adapter = new DisabledQueryPreferenceScreenAdapter(this, Context);
            
            builder.SetAdapter(adapter, (sender, args) => { });
            builder.SetPositiveButton(Android.Resource.String.Ok, (sender, args) =>
            {
                List<string> newList = adapter.DisabledQueriesValues.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                var prefs = PreferenceManager.GetDefaultSharedPreferences(this.Context);
                prefs.Edit().PutStringSet("AutoFillDisabledQueries", newList).Commit();

            });


        }
    }
}