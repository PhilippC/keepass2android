// This file is part of Keepass2Android, Copyright 2025.
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Dialog;
using keepass2android.KeeShare;

namespace keepass2android
{
    /// <summary>
    /// Dialog for prompting user to trust an unknown KeeShare signer.
    /// Shows the signer name and SHA-256 fingerprint for verification.
    /// This is a KEY DIFFERENTIATOR from PR #3106 - clean security UX.
    /// </summary>
    public class TrustSignerDialog : IKeeShareUserInteraction
    {
        private readonly Activity _activity;
        private bool _autoImportEnabled = true;

        public TrustSignerDialog(Activity activity)
        {
            _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        }

        public bool IsAutoImportEnabled => _autoImportEnabled;

        /// <summary>
        /// Set auto-import preference
        /// </summary>
        public void SetAutoImportEnabled(bool enabled)
        {
            _autoImportEnabled = enabled;
        }

        /// <summary>
        /// Shows a Material Design dialog prompting the user to trust an unknown signer.
        /// Displays the signer name and SHA-256 fingerprint in a clear, readable format.
        /// </summary>
        public Task<TrustDecision> PromptTrustDecisionAsync(UntrustedSignerInfo signerInfo)
        {
            var tcs = new TaskCompletionSource<TrustDecision>();
            
            _activity.RunOnUiThread(() =>
            {
                try
                {
                    ShowTrustDialog(signerInfo, tcs);
                }
                catch (Exception ex)
                {
                    Kp2aLog.Log($"KeeShare: Error showing trust dialog: {ex.Message}");
                    tcs.TrySetResult(TrustDecision.Reject);
                }
            });
            
            return tcs.Task;
        }

        private void ShowTrustDialog(UntrustedSignerInfo signerInfo, TaskCompletionSource<TrustDecision> tcs)
        {
            // Inflate custom view for dialog
            var inflater = LayoutInflater.From(_activity);
            var dialogView = inflater.Inflate(Resource.Layout.dialog_trust_signer, null);
            
            // Populate views
            var signerNameText = dialogView.FindViewById<TextView>(Resource.Id.trust_signer_name);
            var fingerprintText = dialogView.FindViewById<TextView>(Resource.Id.trust_fingerprint);
            var sharePathText = dialogView.FindViewById<TextView>(Resource.Id.trust_share_path);
            var warningText = dialogView.FindViewById<TextView>(Resource.Id.trust_warning);
            
            if (signerNameText != null)
                signerNameText.Text = signerInfo?.SignerName ?? "Unknown";
            
            if (fingerprintText != null)
            {
                // Format fingerprint in groups for readability
                string formatted = FormatFingerprint(signerInfo?.KeyFingerprint);
                fingerprintText.Text = formatted;
                fingerprintText.SetTypeface(Android.Graphics.Typeface.Monospace, Android.Graphics.TypefaceStyle.Normal);
            }
            
            if (sharePathText != null)
                sharePathText.Text = signerInfo?.ShareLocation?.GetDisplayName() ?? "";
            
            if (warningText != null)
            {
                warningText.Text = _activity.GetString(Resource.String.keeshare_trust_warning);
            }
            
            // Build dialog
            var builder = new MaterialAlertDialogBuilder(_activity)
                .SetTitle(Resource.String.keeshare_trust_title)
                .SetView(dialogView)
                .SetCancelable(false);
            
            // Trust Permanently button
            builder.SetPositiveButton(Resource.String.keeshare_trust_permanently, (s, e) =>
            {
                tcs.TrySetResult(TrustDecision.TrustPermanently);
            });
            
            // Trust Once button
            builder.SetNeutralButton(Resource.String.keeshare_trust_once, (s, e) =>
            {
                tcs.TrySetResult(TrustDecision.TrustOnce);
            });
            
            // Reject button
            builder.SetNegativeButton(Resource.String.keeshare_reject, (s, e) =>
            {
                tcs.TrySetResult(TrustDecision.Reject);
            });
            
            var dialog = builder.Create();
            
            // Handle back button / dismiss
            dialog.SetOnCancelListener(new DialogCancelListener(() =>
            {
                tcs.TrySetResult(TrustDecision.Cancel);
            }));
            
            dialog.Show();
        }

        /// <summary>
        /// Formats a hex fingerprint for display (XX:XX:XX:XX format in groups of 4)
        /// </summary>
        private string FormatFingerprint(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return "No fingerprint available";
            
            fingerprint = fingerprint.ToUpperInvariant();
            
            // Format as XX:XX:XX:XX with line breaks every 16 characters
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < fingerprint.Length; i += 2)
            {
                if (i > 0)
                {
                    if (i % 16 == 0)
                        sb.Append('\n');
                    else
                        sb.Append(':');
                }
                
                int remaining = Math.Min(2, fingerprint.Length - i);
                sb.Append(fingerprint.Substring(i, remaining));
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Notifies the user about completed imports with a summary toast/snackbar.
        /// </summary>
        public void NotifyImportResults(List<KeeShareImportResult> results)
        {
            if (results == null || results.Count == 0)
                return;
            
            _activity.RunOnUiThread(() =>
            {
                int successCount = 0;
                int failCount = 0;
                int totalEntries = 0;
                
                foreach (var result in results)
                {
                    if (result.IsSuccess)
                    {
                        successCount++;
                        totalEntries += result.EntriesImported;
                    }
                    else
                    {
                        failCount++;
                    }
                }
                
                string message;
                if (failCount == 0 && successCount > 0)
                {
                    message = string.Format(
                        _activity.GetString(Resource.String.keeshare_import_success),
                        totalEntries, successCount);
                }
                else if (successCount == 0 && failCount > 0)
                {
                    message = string.Format(
                        _activity.GetString(Resource.String.keeshare_import_failed),
                        failCount);
                }
                else
                {
                    message = string.Format(
                        _activity.GetString(Resource.String.keeshare_import_partial),
                        successCount, failCount);
                }
                
                Toast.MakeText(_activity, message, ToastLength.Long).Show();
            });
        }

        /// <summary>
        /// Helper class for handling dialog cancellation
        /// </summary>
        private class DialogCancelListener : Java.Lang.Object, IDialogInterfaceOnCancelListener
        {
            private readonly Action _onCancel;
            
            public DialogCancelListener(Action onCancel)
            {
                _onCancel = onCancel;
            }
            
            public void OnCancel(IDialogInterface dialog)
            {
                _onCancel?.Invoke();
            }
        }
    }
}
