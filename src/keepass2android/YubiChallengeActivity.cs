using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Nfc;
using Android.Nfc.Tech;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.IO;

namespace keepass2android
{
    [Activity(Label = "@string/yubichallenge_title_activity_nfc")]
    [IntentFilter(new string[] { "android.nfc.action.NDEF_DISCOVERED" }, Categories = new string[]{Android.Content.Intent.CategoryDefault})]
    public class YubiChallengeActivity : Activity
    {
        private byte[] challenge;
        private int slot;

        private const byte SLOT_CHAL_HMAC1 = 0x30;
        private const byte SLOT_CHAL_HMAC2 = 0x38;
        private const byte CHAL_BYTES = 0x40; // 64
        private const byte RESP_BYTES = 20;

        private static byte[] selectCommand = { 0x00, (byte) 0xA4, 0x04,
            0x00, 0x07, (byte) 0xA0, 0x00, 0x00, 0x05, 0x27, 0x20, 0x01, 0x00 };
        private static byte[] chalCommand = { 0x00, 0x01, SLOT_CHAL_HMAC2,
            0x00, CHAL_BYTES };

        private AlertDialog swipeDialog;

        private const int PERMISSIONS_REQUEST_NFC = 901234845;

        protected override void OnResume()
        {
            base.OnResume();

            
            challenge = Intent.GetByteArrayExtra("challenge");
            slot = PreferenceManager.GetDefaultSharedPreferences(this).GetInt("pref_Slot", 2);
            if (challenge == null) return;
            else if (challenge.Length != CHAL_BYTES) return;
            else ChallengeYubiKey();
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            Tag tag = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;
            SetResult(Result.Canceled, intent);
            if (tag != null)
            {
                IsoDep isoTag = IsoDep.Get(tag);
                try
                {
                    isoTag.Connect();
                    byte[] resp = isoTag.Transceive(selectCommand);
                    int length = resp.Length;
                    if (resp[length - 2] == (byte)0x90 && resp[length - 1] == 0x00)
                        DoChallengeYubiKey(isoTag, slot, challenge);
                    else
                    {
                        Toast.MakeText(this, Resource.String.yubichallenge_tag_error, ToastLength.Long)
                            .Show();
                        
                    }

                    isoTag.Close();
                }
                catch (TagLostException e)
                {
                    Toast.MakeText(this, Resource.String.yubichallenge_lost_tag, ToastLength.Long)
                        .Show();
                    
                }
                catch (IOException e)
                {
                    Toast.MakeText(this, GetString(Resource.String.yubichallenge_tag_error) + e.Message,
                    ToastLength.Long).Show();
                }
            }
            else SetResult(Result.Canceled, intent);
            Finish();
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (swipeDialog != null)
            {
                swipeDialog.Dismiss();
                swipeDialog = null;
            }
            DisableDispatch();
        }

        private void DisableDispatch()
        {
            NfcAdapter adapter = NfcAdapter.GetDefaultAdapter(this);
            if (adapter != null)
            {
                adapter.DisableForegroundDispatch(this);
            }
        }

        private void DoChallengeYubiKey(IsoDep isoTag, int slot, byte[] challenge)
        {
            if (challenge == null || challenge.Length != CHAL_BYTES)
                return;

            byte[] apdu = new byte[chalCommand.Length + CHAL_BYTES];
            chalCommand.CopyTo(apdu, 0);

            if (slot == 1)
                apdu[2] = SLOT_CHAL_HMAC1;
            challenge.CopyTo(apdu, chalCommand.Length);

            byte[] respApdu = isoTag.Transceive(apdu);
            if (respApdu.Length == 22 && respApdu[20] == (byte) 0x90
                && respApdu[21] == 0x00)
            {
                // Get the secret
                byte[] resp = new byte[RESP_BYTES];
                Array.Copy(respApdu, 0, resp, 0, RESP_BYTES);
                Intent data = Intent;
                data.PutExtra("response", resp);
                SetResult(Result.Ok, data);
            }
            else
            {
                Toast.MakeText(this, Resource.String.yubichallenge_challenge_failed, ToastLength.Long)
                    .Show();

            }
        }

        private void ChallengeYubiKey()
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this);
            builder.SetTitle(Resource.String.yubichallenge_challenging);
            builder.SetMessage(Resource.String.yubichallenge_swipe);
            builder.SetNegativeButton(Android.Resource.String.Cancel, (obj, args) => Finish());
            builder.SetCancelable(false);
            swipeDialog = builder.Show();
            
            EnableDispatch();
        }

        public override void OnBackPressed()
        {
            base.OnBackPressed();
            Finish();
        }

        private void EnableDispatch()
        {
            Intent intent = Intent;
            intent.AddFlags(ActivityFlags.SingleTop);

            PendingIntent tagIntent = PendingIntent.GetActivity(
                this, 0, intent, 0);

            IntentFilter iso = new IntentFilter(NfcAdapter.ActionNdefDiscovered);
            NfcAdapter adapter = NfcAdapter.GetDefaultAdapter(this);
            if (adapter == null)
            {
                Toast.MakeText(this, Resource.String.yubichallenge_no_nfc, ToastLength.Long).Show();
                return;
            }
            if (adapter.IsEnabled)
            {
                // register for foreground dispatch so we'll receive tags according to our intent filters
                adapter.EnableForegroundDispatch(
                    this, tagIntent, new IntentFilter[] { iso },
                    new String[][] { new String[] { Java.Lang.Class.FromType(typeof(IsoDep)).Name }
                    }
                );
            } else {
                AlertDialog.Builder dialog = new AlertDialog.Builder(this);
                dialog.SetTitle(Resource.String.yubichallenge_nfc_off);
                dialog.SetPositiveButton(Android.Resource.String.Yes, (sender, args) =>
                {
                    Intent settings = new Intent(Android.Provider.Settings.ActionNfcSettings);
                    StartActivity(settings);
                    ((Dialog)sender).Dismiss();
                });
                dialog.SetNegativeButton(Android.Resource.String.No, (sender, args) =>
                {
                    ((Dialog)sender).Dismiss();
                });
                dialog.Show();
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetResult(Result.Canceled);
            // Create your application here
        }
    }
}