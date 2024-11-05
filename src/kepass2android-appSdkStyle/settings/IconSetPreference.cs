using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Preferences;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace keepass2android
{
    public class IconSetPreference : ListPreference
    {
        private int selectedEntry;

        private class IconSet
        {
            public string PackageName { get; set; }
            public string DisplayName { get; set; }

            public Drawable GetIcon(Context context)
            {
                if (PackageName == context.PackageName)
                    return context.Resources.GetDrawable(Resource.Drawable.ic00);
                Resources res = context.PackageManager.GetResourcesForApplication(PackageName);

                return res.GetDrawable(res.GetIdentifier("ic00", "drawable", PackageName));
            }
        }

        private class IconListPreferenceScreenAdapter : BaseAdapter
        {
            private readonly IconSetPreference _pref;

            public IconListPreferenceScreenAdapter(IconSetPreference pref, Context context)
            {
                _pref = pref;
            }


            private class CustomHolder : Java.Lang.Object
            {
                private TextView text = null;
                private RadioButton rButton = null;

                public CustomHolder(View row, int position, IconSetPreference pref)
                {
                    text = (TextView)row.FindViewById(Resource.Id.image_list_view_row_text_view);
                    text.Text = pref.IconSets[position].DisplayName;

                    rButton = (RadioButton)row.FindViewById(Resource.Id.image_list_view_row_radio_button);
                    rButton.Id = position;
                    rButton.Clickable = false;
                    rButton.Checked = (pref.selectedEntry == position);

                    try
                    {
                        Drawable dr = pref.IconSets[position].GetIcon(row.Context);
                        var bitmap = ((BitmapDrawable)dr).Bitmap;
                        Drawable d = new BitmapDrawable(row.Resources, Bitmap.CreateScaledBitmap(bitmap, 64, 64, true));
                        text.SetCompoundDrawablesWithIntrinsicBounds(d, null, null, null);
                        text.Text = (" " + text.Text);
                    }
                    catch (Exception)
                    {
                    }


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
                row = LayoutInflater.From(_pref.Context).Inflate(Resource.Layout.image_list_preference_row, parent, false);
                holder = new CustomHolder(row, position, _pref);

                row.Tag = holder;

                // row.setClickable(true);
                // row.setFocusable(true);
                // row.setFocusableInTouchMode(true);
                row.Click += (sender, args) =>
                {

                    ((View)sender).RequestFocus();

                    Dialog mDialog = _pref.Dialog;
                    mDialog.Dismiss();

                    _pref.CallChangeListener(_pref.IconSets[p].PackageName);
                    ISharedPreferences pref = PreferenceManager.GetDefaultSharedPreferences(_pref.Context);
                    var edit = pref.Edit();
                    edit.PutString(_pref.Key, _pref.IconSets[p].PackageName);
                    edit.Commit();
                    _pref.selectedEntry = p;
					
                };

                return row;
            }

            public override int Count
            {
                get { return _pref.IconSets.Count; }
            }
        }


        List<IconSet> _iconSets = null;
        List<IconSet> IconSets
        {
            get
            {
                if (_iconSets != null)
                    return _iconSets;
                _iconSets = new List<IconSet>();

                _iconSets.Add(new IconSet()
                {
                    DisplayName = Context.GetString(AppNames.AppNameResource),
                    PackageName = Context.PackageName
                });

                foreach (var p in Context.PackageManager.GetInstalledPackages(0))
                {
                    try
                    {

                        string packageName = p.PackageName;
                        Resources res = Context.PackageManager.GetResourcesForApplication(packageName);
                        int nameId = res.GetIdentifier("kp2a_iconset_name", "string", packageName);
                        _iconSets.Add(new IconSet()
                        {
                            DisplayName = res.GetString(nameId),
                            PackageName = packageName
                        });
                    }
                    catch (Exception)
                    {

                    }
                }
                return _iconSets;
            }
        }
        protected IconSetPreference(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        private readonly Task _populatorTask;

        public IconSetPreference(Context context, IAttributeSet attrs)
            : base(context, attrs)
        {
            _populatorTask = Task.Factory.StartNew(() =>
            {
                SetEntries(IconSets.Select(s => s.DisplayName).ToArray());
                SetEntryValues(IconSets.Select(s => s.PackageName).ToArray());
            });
			
        }


        protected override void OnPrepareDialogBuilder(AlertDialog.Builder builder)
        {
            _populatorTask.Wait();
            base.OnPrepareDialogBuilder(builder);


            var iconListPreferenceAdapter = new IconListPreferenceScreenAdapter(this, Context);

            String selectedValue = PreferenceManager.GetDefaultSharedPreferences(Context).GetString(Key, "");
            for (int i = 0; i < IconSets.Count; i++)
            {
                if (selectedValue == IconSets[i].PackageName)
                {
                    selectedEntry = i;
                    break;
                }
            }

            builder.SetAdapter(iconListPreferenceAdapter, (sender, args) => { });
            builder.SetNeutralButton(Resource.String.IconSet_install, (sender, args) =>
            {
                Util.GotoUrl(Context, "market://search?q=keepass2android icon set");
            });


        }
    }
}