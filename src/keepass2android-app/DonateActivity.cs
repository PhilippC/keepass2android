// This file is part of Keepass2Android, Copyright 2026 Philipp Crocoll.
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

#if DONATION_BILLING
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.BillingClient.Api;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Dialog;

namespace keepass2android
{
  /// <summary>
  /// Lets the user donate through Google Play Billing (Google Play build only, see DonationMode in the csproj).
  /// </summary>
  [Activity(Label = "@string/donate_title", Theme = "@style/Kp2aTheme_ActionBar",
      ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
  public class DonateActivity : LifecycleAwareActivity, DonationBillingManager.IListener
  {
    private readonly ActivityDesign _design;
    private DonationBillingManager? _billingManager;
    private bool _productsShown;
    private readonly Dictionary<string, int> _buttonIdByProductId = new Dictionary<string, int>
    {
      { "oktfestbeer", Resource.Id.donate_oktfestbeer },
      { "smallbeer", Resource.Id.donate_smallbeer },
      { "coke", Resource.Id.donate_coke },
    };

    public DonateActivity()
    {
      _design = new ActivityDesign(this);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
      _design.ApplyTheme();
      base.OnCreate(savedInstanceState);
      SetContentView(Resource.Layout.donate_billing);
      new Util.InsetListener(FindViewById(Resource.Id.donate_container)!).Apply();
      SupportActionBar?.SetDisplayHomeAsUpEnabled(true);

      _billingManager = new DonationBillingManager(this, this);
      _billingManager.StartConnection();
    }

    protected override void OnResume()
    {
      base.OnResume();
      // pick up purchases completed while we were in background (e.g. pending payments)
      if (_billingManager?.IsReady == true)
        _billingManager.ProcessExistingPurchases(null);
    }

    protected override void OnDestroy()
    {
      _billingManager?.EndConnection();
      _billingManager = null;
      base.OnDestroy();
    }

    public override bool OnSupportNavigateUp()
    {
      Finish();
      return true;
    }

    private void SetBusy(bool busy)
    {
      FindViewById(Resource.Id.donate_progress)!.Visibility = busy ? ViewStates.Visible : ViewStates.Gone;
      foreach (var buttonId in _buttonIdByProductId.Values)
        FindViewById(buttonId)!.Enabled = !busy;
    }

    private void ShowStatus(string text)
    {
      var status = FindViewById<TextView>(Resource.Id.donate_status)!;
      status.Text = text;
      status.Visibility = ViewStates.Visible;
    }

    private void ShowDialog(string message)
    {
      if (IsFinishing || IsDestroyed)
        return;
      new MaterialAlertDialogBuilder(this)
          .SetMessage(message)!
          .SetPositiveButton(Android.Resource.String.Ok, (sender, args) => { })!
          .Show();
    }

    private static string DescribeError(BillingResult result)
    {
      return string.IsNullOrEmpty(result.DebugMessage)
          ? "error code " + result.ResponseCode
          : result.DebugMessage;
    }

    public void OnProductsLoaded(IList<ProductDetails> products)
    {
      RunOnUiThread(() =>
      {
        if (_billingManager == null || _productsShown)
          return;
        _productsShown = true;
        foreach (var product in products)
        {
          if (!_buttonIdByProductId.TryGetValue(product.ProductId, out int buttonId))
            continue;
          var button = FindViewById<Button>(buttonId)!;
          string? price = product.OneTimePurchaseOfferDetailsList?.FirstOrDefault()?.FormattedPrice;
          if (price != null)
            button.Text = GetString(Resource.String.donate_product_with_price, GetLabel(product.ProductId), price);
          button.Visibility = ViewStates.Visible;
          button.Click += (sender, args) =>
          {
            SetBusy(true);
            _billingManager?.LaunchDonation(this, product);
          };
        }
        SetBusy(false);
        if (!products.Any(p => _buttonIdByProductId.ContainsKey(p.ProductId)))
          ShowStatus(GetString(Resource.String.donate_billing_unavailable, "no products found"));
      });
    }

    private string GetLabel(string productId)
    {
      switch (productId)
      {
        case "oktfestbeer": return GetString(Resource.String.donate_oktoberfest_beer);
        case "smallbeer": return GetString(Resource.String.donate_small_beer);
        default: return GetString(Resource.String.donate_coke);
      }
    }

    public void OnDonationCompleted()
    {
      RunOnUiThread(() =>
      {
        SetBusy(false);
        ShowDialog(GetString(Resource.String.donate_thank_you));
      });
    }

    public void OnDonationPending()
    {
      RunOnUiThread(() =>
      {
        SetBusy(false);
        ShowDialog(GetString(Resource.String.donate_pending));
      });
    }

    public void OnDonationCanceled()
    {
      RunOnUiThread(() => SetBusy(false));
    }

    public void OnBillingUnavailable(BillingResult result)
    {
      RunOnUiThread(() =>
      {
        SetBusy(false);
        ShowStatus(GetString(Resource.String.donate_billing_unavailable, DescribeError(result)));
      });
    }

    public void OnDonationFailed(BillingResult result)
    {
      RunOnUiThread(() =>
      {
        SetBusy(false);
        ShowDialog(GetString(Resource.String.donate_failed, DescribeError(result)));
      });
    }
  }
}
#endif
