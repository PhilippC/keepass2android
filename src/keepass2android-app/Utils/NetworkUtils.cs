using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using AndroidX.Core.Content;
using System.Collections.Generic;
using Android;

namespace keepass2android
{
    public static class NetworkUtils
    {
        // returns true if the device is connected to one of the allowed SSIDs or - if the list is empty - if the device is connected to any Wi-Fi network.
        public static bool IsAllowedNetwork(Context context, List<string> allowedSSIDs)
        {

            var connectivityManager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService);
            Network network = connectivityManager.ActiveNetwork;
            if (network == null)
                return false;

            NetworkCapabilities capabilities = connectivityManager.GetNetworkCapabilities(network);
            if (capabilities == null)
                return false;

            if (capabilities.HasTransport(TransportType.Wifi))
            {
                if (ContextCompat.CheckSelfPermission(context, Manifest.Permission.AccessFineLocation) !=
                    Android.Content.PM.Permission.Granted)
                {
                    // Permission not granted
                    return false;
                }

                var wifiManager = (WifiManager)context.ApplicationContext.GetSystemService(Context.WifiService);
                string ssid = wifiManager.ConnectionInfo?.SSID?.Trim('"');
                return allowedSSIDs.Count == 0 || allowedSSIDs.Contains(ssid);
            }


            return false;
        }
    }
}