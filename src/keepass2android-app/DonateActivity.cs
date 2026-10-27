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
using Android.Content;
using Android.BillingClient.Api;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Button;
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
    private bool _yearly = true;
    private readonly Dictionary<string, ProductDetails> _products = new Dictionary<string, ProductDetails>();
    private readonly HashSet<string> _activeSubscriptions = new HashSet<string>();

    // each button donates either the one-time or the yearly product, depending on the selected frequency
    private class DonationSlot
    {
      public int ButtonId;
      public string OneTimeProductId = "";
      // empty: the slot always donates the one-time product, independent of the selected frequency
      public string YearlyProductId = "";
      public int LabelId;
      // format for the one-time button text, with the label as %1$s and the price as %2$s
      public int OneTimeFormatId = Resource.String.donate_product_with_price;
    }

    private static readonly DonationSlot[] Slots =
    {
      new DonationSlot { ButtonId = Resource.Id.donate_oktfestbeer, OneTimeProductId = "oktfestbeer", YearlyProductId = "oktfestbeer_yearly", LabelId = Resource.String.donate_oktoberfest_beer },
      new DonationSlot { ButtonId = Resource.Id.donate_smallbeer, OneTimeProductId = "smallbeer", YearlyProductId = "small_beer_yearly", LabelId = Resource.String.donate_small_beer },
      new DonationSlot { ButtonId = Resource.Id.donate_coke, OneTimeProductId = "coke", YearlyProductId = "coke_yearly", LabelId = Resource.String.donate_coke },
      // amount buttons: one-time only (no yearly product), shown in both modes. They only show the price.
      new DonationSlot { ButtonId = Resource.Id.donate_amount_10, OneTimeProductId = "donate_10eur", LabelId = Resource.String.donate_other_amount, OneTimeFormatId = Resource.String.donate_amount_price },
      new DonationSlot { ButtonId = Resource.Id.donate_amount_20, OneTimeProductId = "donate_20eur", LabelId = Resource.String.donate_other_amount, OneTimeFormatId = Resource.String.donate_amount_price },
      new DonationSlot { ButtonId = Resource.Id.donate_amount_50, OneTimeProductId = "donate_50eur", LabelId = Resource.String.donate_other_amount, OneTimeFormatId = Resource.String.donate_amount_price },
      new DonationSlot { ButtonId = Resource.Id.donate_amount_100, OneTimeProductId = "donate_100eur", LabelId = Resource.String.donate_other_amount, OneTimeFormatId = Resource.String.donate_amount_price },
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

      FindViewById<MaterialButtonToggleGroup>(Resource.Id.donate_frequency)!.ButtonChecked += (sender, args) =>
      {
        if (!args.P2) // P2: isChecked
          return;
        _yearly = args.P1 == Resource.Id.donate_frequency_yearly;
        UpdateButtons();
      };
      foreach (var slot in Slots)
        FindViewById<Button>(slot.ButtonId)!.Click += (sender, args) => OnSlotClicked(slot);

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
      foreach (var slot in Slots)
        FindViewById(slot.ButtonId)!.Enabled = !busy;
      FindViewById(Resource.Id.donate_frequency)!.Enabled = !busy;
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
          _products[product.ProductId] = product;

        bool hasYearly = Slots.Any(slot => _products.ContainsKey(slot.YearlyProductId));
        bool hasOneTime = Slots.Any(slot => _products.ContainsKey(slot.OneTimeProductId));
        // only offer the choice if both kinds of products are available
        FindViewById(Resource.Id.donate_frequency)!.Visibility = hasYearly && hasOneTime ? ViewStates.Visible : ViewStates.Gone;
        if (!hasYearly)
          _yearly = false;
        else if (!hasOneTime)
          _yearly = true;
        UpdateButtons();

        SetBusy(false);
        if (!hasYearly && !hasOneTime)
          ShowStatus(GetString(Resource.String.donate_billing_unavailable, "no products found"));
        else // an earlier, transient connection error is obsolete now
          FindViewById(Resource.Id.donate_status)!.Visibility = ViewStates.Gone;
      });
    }

    public void OnActiveSubscriptionsChanged(ICollection<string> activeProductIds)
    {
      RunOnUiThread(() =>
      {
        _activeSubscriptions.Clear();
        _activeSubscriptions.UnionWith(activeProductIds);
        UpdateButtons();
      });
    }

    private bool IsYearly(DonationSlot slot)
    {
      return _yearly && slot.YearlyProductId != "";
    }

    private void UpdateButtons()
    {
      foreach (var slot in Slots)
      {
        var button = FindViewById<Button>(slot.ButtonId)!;
        bool yearly = IsYearly(slot);
        string productId = yearly ? slot.YearlyProductId : slot.OneTimeProductId;
        if (!_products.TryGetValue(productId, out var product))
        {
          button.Visibility = ViewStates.Gone;
          continue;
        }
        string label = GetString(slot.LabelId);
        string? price = DonationBillingManager.GetFormattedPrice(product);
        if (yearly && _activeSubscriptions.Contains(productId))
          button.Text = GetString(Resource.String.donate_subscription_active, label);
        else if (price == null)
          button.Text = label;
        else
          button.Text = GetString(yearly ? Resource.String.donate_product_yearly_with_price : slot.OneTimeFormatId, label, price);
        button.Visibility = ViewStates.Visible;
      }

      bool anyAmountShown = new[] { Resource.Id.donate_amount_10, Resource.Id.donate_amount_20,
              Resource.Id.donate_amount_50, Resource.Id.donate_amount_100 }
          .Any(id => FindViewById(id)!.Visibility == ViewStates.Visible);
      FindViewById(Resource.Id.donate_other_amount_title)!.Visibility = anyAmountShown ? ViewStates.Visible : ViewStates.Gone;
    }

    private void OnSlotClicked(DonationSlot slot)
    {
      bool yearly = IsYearly(slot);
      string productId = yearly ? slot.YearlyProductId : slot.OneTimeProductId;
      if (!_products.TryGetValue(productId, out var product))
        return;
      if (yearly && _activeSubscriptions.Contains(productId))
      {
        // subscriptions can only be managed (e.g. canceled) in Google Play
        var uri = Android.Net.Uri.Parse("https://play.google.com/store/account/subscriptions?sku=" + productId + "&package=" + PackageName);
        try
        {
          StartActivity(new Intent(Intent.ActionView, uri));
        }
        catch (ActivityNotFoundException)
        {
          Toast.MakeText(this, uri.ToString(), ToastLength.Long)!.Show();
        }
        return;
      }
      SetBusy(true);
      _billingManager?.LaunchDonation(this, product);
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
