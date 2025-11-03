using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.RecyclerView.Widget;
using System.Collections.Generic;
using System.Linq;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;

namespace keepass2android
{
    [Activity(Label = "@string/configure_ssids", Theme = "@style/MaterialDialogTheme")]
    public class SSIDManagerActivity : AppCompatActivity
    {
        const int RequestLocationId = 1001;

        Button btnAddCurrentSSID, btnAddSSID, btnSave;
        RecyclerView rvSSIDs;
        TextView tvNoSSIDs;

        List<string> ssidList = new List<string>();
        SSIDAdapter adapter;

        const string PrefsName = "SSIDPrefs";
        const string KeySSIDs = "SSIDs";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.edit_ssids);

            // Views
            btnAddCurrentSSID = FindViewById<Button>(Resource.Id.btnAddCurrentSSID);
            btnAddSSID = FindViewById<Button>(Resource.Id.btnAddSSID);
            btnSave = FindViewById<Button>(Resource.Id.btnSave);
            rvSSIDs = FindViewById<RecyclerView>(Resource.Id.rvSSIDs);
            tvNoSSIDs = FindViewById<TextView>(Resource.Id.tvNoSSIDs);

            // Preferences
            LoadSSIDs();

            adapter = new SSIDAdapter(ssidList);
            adapter.DeleteClicked += position =>
            {
                adapter.RemoveAt(position);
                SaveSSIDs();
                UpdateNoSSIDsLabel();
            };

            rvSSIDs.SetLayoutManager(new LinearLayoutManager(this));
            rvSSIDs.SetAdapter(adapter);

            btnAddSSID.Click += (s, e) => ShowAddSSIDDialog();
            btnAddCurrentSSID.Click += (s, e) => AddCurrentSSID();
            btnSave.Click += (s, e) => Finish();

            UpdateNoSSIDsLabel();

            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) != Permission.Granted)
            {
                ShowPermissionRationaleOrRequest();
            }
        }

        void UpdateNoSSIDsLabel()
        {
            tvNoSSIDs.Visibility = ssidList.Count == 0 ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Gone;
        }

        void LoadSSIDs()
        {
            ssidList = LoadSSIDS(this);
        }

        public static List<string> LoadSSIDS(Context context)
        {
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var ssids = prefs.GetString(KeySSIDs, "");
            if (!string.IsNullOrEmpty(ssids))
                return ssids.Split('|').ToList();
            return new List<string>();
        }

        void SaveSSIDs()
        {
            SaveSSIDs(ssidList, this);

        }

        public static void SaveSSIDs(List<string> ssidList, Context context)
        {
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var prefsEditor = prefs.Edit();
            prefsEditor.PutString(KeySSIDs, string.Join("|", ssidList));
            prefsEditor.Commit();

        }

        void ShowAddSSIDDialog()
        {
            EditText input = new EditText(this);
            input.Hint = GetString(Resource.String.enter_ssid_hint);

            new AlertDialog.Builder(this)
                .SetTitle(GetString(Resource.String.add_ssid))
                .SetView(input)
                .SetPositiveButton(Android.Resource.String.Ok, (sender, args) =>
                {
                    string ssid = input.Text.Trim();
                    if (!string.IsNullOrEmpty(ssid) && !ssidList.Contains(ssid))
                    {
                        ssidList.Add(ssid);
                        adapter.NotifyItemInserted(ssidList.Count - 1);
                        SaveSSIDs();
                        UpdateNoSSIDsLabel();
                    }
                    else
                    {
                        Toast.MakeText(this, GetString(Resource.String.ssid_empty_or_exists), ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton(Android.Resource.String.Cancel, (sender, args) => { })
                .Show();
        }

        void AddCurrentSSID()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) != Permission.Granted)
            {
                ShowPermissionRationaleOrRequest();
                return;
            }

            var wifiManager = (WifiManager)GetSystemService(WifiService);
            string ssid = wifiManager.ConnectionInfo.SSID?.Replace("\"", "");

            if (!string.IsNullOrEmpty(ssid) && !ssidList.Contains(ssid))
            {
                ssidList.Add(ssid);
                adapter.NotifyItemInserted(ssidList.Count - 1);
                SaveSSIDs();
                UpdateNoSSIDsLabel();
            }
            else
            {
                Toast.MakeText(this, GetString(Resource.String.ssid_empty_or_exists), ToastLength.Short).Show();
            }
        }

        void ShowPermissionRationaleOrRequest()
        {
            new AlertDialog.Builder(this)
                .SetTitle(GetString(Resource.String.permission_needed))
                .SetMessage(GetString(Resource.String.permission_location_rationale))
                .SetPositiveButton(Android.Resource.String.Ok, (s, e) =>
                {
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(this, new string[] { Manifest.Permission.AccessFineLocation }, RequestLocationId);
                })
                .SetNegativeButton(Android.Resource.String.Cancel, (s, e) => { Finish(); })
                .Show();

        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == RequestLocationId)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                {
                    Toast.MakeText(this, GetString(Resource.String.permission_granted), ToastLength.Short).Show();
                }
                else
                {
                    Toast.MakeText(this, GetString(Resource.String.permission_denied), ToastLength.Short).Show();
                    Finish();
                }
            }
        }
    }
}
