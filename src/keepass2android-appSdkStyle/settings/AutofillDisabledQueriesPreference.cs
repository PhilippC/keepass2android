using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using AndroidX.Preference;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using keepass2android.services.AutofillBase;
using keepass2android_appSdkStyle;
using Google.Android.Material.Dialog;

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
            private readonly Context _context;

            public DisabledQueryPreferenceScreenAdapter(AutofillDisabledQueriesPreference pref, Context context)
            {
                _pref = pref;
                _context = context;
            }


            private class CustomHolder : Java.Lang.Object
            {
                private TextView text = null;
                private CheckBox checkbox = null;
                
                public CustomHolder(View row, int position, AutofillDisabledQueriesPreference pref, Context context)
                {
                    text = (TextView) row.FindViewById(Resource.Id.disabled_query_text);
                    text.Text = pref.DisabledQueries[position].DisplayName;
                    TypedValue typedValue = new TypedValue();

                    Resources.Theme theme = context.Theme;
                    if (theme != null)
                    {
                        theme.ResolveAttribute(Android.Resource.Attribute.TextColorPrimary, typedValue, true);
                        using (TypedArray arr = context.ObtainStyledAttributes(typedValue.Data, new int[] { Android.Resource.Attribute.TextColorPrimary }))
                        {
                            var primaryColor = arr.GetColorStateList(0);
                            text.SetTextColor(primaryColor);
                        }
                    }
                    
                    

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
                holder = new CustomHolder(row, position, _pref, _context);

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

        protected override void OnClick()
        {
            _populatorTask.Wait();

            var builder = new MaterialAlertDialogBuilder(Context);



            var adapter = new DisabledQueryPreferenceScreenAdapter(this, Context);

            builder.SetAdapter(adapter, (sender, args) => { });
            builder.SetPositiveButton(Android.Resource.String.Ok, (sender, args) =>
            {
                List<string> newList = adapter.DisabledQueriesValues.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                var prefs = PreferenceManager.GetDefaultSharedPreferences(this.Context);
                prefs.Edit().PutStringSet("AutoFillDisabledQueries", newList).Commit();

            });
            var dialog = builder.Create();
            dialog.Show();

        }

    }
}