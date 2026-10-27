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
using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.BillingClient.Api;
using Android.Content;

namespace keepass2android
{
  /// <summary>
  /// Donations through Google Play Billing.
  /// One-time donations are in-app products which are consumed right after the purchase, so they can be donated
  /// any number of times. Consuming also acknowledges the purchase (otherwise Google refunds it after 3 days).
  /// Yearly donations are subscriptions. They are acknowledged (not consumed) and stay active until the user
  /// cancels them in Google Play.
  /// Purchases which could not be consumed/acknowledged immediately (e.g. app killed during the purchase, or a
  /// pending payment which completed later) are handled by <see cref="ProcessExistingPurchases"/>.
  /// </summary>
  public class DonationBillingManager : Java.Lang.Object, IPurchasesUpdatedListener, IBillingClientStateListener
  {
    public interface IListener
    {
      void OnProductsLoaded(IList<ProductDetails> products);
      void OnActiveSubscriptionsChanged(ICollection<string> activeProductIds);
      void OnDonationCompleted();
      void OnDonationPending();
      void OnDonationCanceled();
      void OnBillingUnavailable(BillingResult result);
      void OnDonationFailed(BillingResult result);
    }

    // must match the products configured in the Google Play Console
    // all one-time products allow multi-quantity purchases (configured in the Play Console): the user can change the
    // quantity in the Play purchase dialog. Such a purchase is a single purchase (with Purchase.Quantity > 1) which is
    // consumed once, so no special handling is needed.
    public static readonly string[] OneTimeProductIds =
    {
      "oktfestbeer", "smallbeer", "coke",
      "donate_1eur", "donate_10eur", "donate_20eur", "donate_50eur", "donate_100eur"
    };
    public static readonly string[] YearlyProductIds = { "oktfestbeer_yearly", "small_beer_yearly", "coke_yearly" };

    public static bool IsSubscription(string productId) => YearlyProductIds.Contains(productId);

    private readonly BillingClient _billingClient;
    private readonly IListener _listener;

    // the currently running purchase flow. If it fails with ITEM_ALREADY_OWNED (a previous one-time donation of the
    // same product was not consumed yet), the owned purchases are consumed and the purchase flow is retried once.
    private Activity? _launchActivity;
    private ProductDetails? _launchProduct;
    private bool _launchCanRetry;

    // purchase tokens which are being or have been consumed/acknowledged. Avoids handling (and thanking) twice when
    // OnPurchasesUpdated and ProcessExistingPurchases (e.g. from OnResume after the purchase dialog) overlap.
    private readonly HashSet<string> _handledTokens = new HashSet<string>();

    private readonly HashSet<string> _activeSubscriptions = new HashSet<string>();

    public DonationBillingManager(Context context, IListener listener)
    {
      _listener = listener;
      _billingClient = BillingClient.NewBuilder(context.ApplicationContext!)
          .SetListener(this)
          .EnablePendingPurchases(PendingPurchasesParams.NewBuilder().EnableOneTimeProducts()!.Build())
          .EnableAutoServiceReconnection()
          .Build();
    }

    public bool IsReady => _billingClient.IsReady;

    public void StartConnection()
    {
      _billingClient.StartConnection(this);
    }

    public void EndConnection()
    {
      _billingClient.EndConnection();
    }

    public void OnBillingSetupFinished(BillingResult result)
    {
      if (result.ResponseCode != BillingResponseCode.Ok)
      {
        Kp2aLog.Log("Donation billing setup failed: " + result.ResponseCode + " " + result.DebugMessage);
        // a second connection attempt may report "already connecting" although the first one succeeds
        if (!_billingClient.IsReady)
          _listener.OnBillingUnavailable(result);
        return;
      }
      QueryProducts();
      ProcessExistingPurchases(null);
    }

    public void OnBillingServiceDisconnected()
    {
      // nothing to do, the connection is re-established automatically (EnableAutoServiceReconnection)
    }

    private void QueryProducts()
    {
      // one query can't mix product types, so in-app products and subscriptions are queried one after the other
      QueryProducts(OneTimeProductIds, BillingClient.ProductType.Inapp, (oneTimeResult, oneTimeProducts) =>
      {
        QueryProducts(YearlyProductIds, BillingClient.ProductType.Subs, (yearlyResult, yearlyProducts) =>
        {
          if (oneTimeResult.ResponseCode != BillingResponseCode.Ok && yearlyResult.ResponseCode != BillingResponseCode.Ok)
          {
            _listener.OnBillingUnavailable(oneTimeResult);
            return;
          }
          _listener.OnProductsLoaded(oneTimeProducts.Concat(yearlyProducts).ToList());
        });
      });
    }

    private void QueryProducts(string[] productIds, string productType, Action<BillingResult, IList<ProductDetails>> onFinished)
    {
      var products = productIds.Select(id => QueryProductDetailsParams.Product.NewBuilder()
              .SetProductId(id)
              .SetProductType(productType)
              .Build())
          .ToList();
      var queryParams = QueryProductDetailsParams.NewBuilder().SetProductList(products).Build();
      _billingClient.QueryProductDetails(queryParams, new ProductDetailsResponseListener((result, details) =>
      {
        if (result.ResponseCode != BillingResponseCode.Ok)
        {
          Kp2aLog.Log("Querying donation products (" + productType + ") failed: " + result.ResponseCode + " " + result.DebugMessage);
          onFinished(result, new List<ProductDetails>());
          return;
        }
        foreach (var unfetched in details?.UnfetchedProductList ?? new List<UnfetchedProduct>())
        {
          Kp2aLog.Log("Donation product not available: " + unfetched.ProductId + " (status " + unfetched.StatusCodeValue + ")");
        }
        onFinished(result, details?.ProductDetailsList ?? new List<ProductDetails>());
      }));
    }

    /// <summary>
    /// The base plan of a subscription (i.e. the offer without trial or intro price), or null if there is none.
    /// </summary>
    public static ProductDetails.SubscriptionOfferDetails? GetBasePlan(ProductDetails product)
    {
      var offers = product.GetSubscriptionOfferDetails();
      return offers?.FirstOrDefault(o => o.OfferId == null) ?? offers?.FirstOrDefault();
    }

    /// <summary>
    /// The formatted price, for subscriptions the recurring price of the base plan. Null if not available.
    /// </summary>
    public static string? GetFormattedPrice(ProductDetails product)
    {
      if (IsSubscription(product.ProductId))
        return GetBasePlan(product)?.PricingPhases?.PricingPhaseList?.LastOrDefault()?.FormattedPrice;
      return product.OneTimePurchaseOfferDetailsList?.FirstOrDefault()?.FormattedPrice;
    }

    public void LaunchDonation(Activity activity, ProductDetails product)
    {
      LaunchDonation(activity, product, true);
    }

    private void LaunchDonation(Activity activity, ProductDetails product, bool canRetry)
    {
      var paramsBuilder = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(product);
      // products may have several purchase options, so pass the offer explicitly (required for subscriptions)
      var offerToken = IsSubscription(product.ProductId)
          ? GetBasePlan(product)?.OfferToken
          : product.OneTimePurchaseOfferDetailsList?.FirstOrDefault()?.OfferToken;
      if (offerToken != null)
        paramsBuilder.SetOfferToken(offerToken);

      var flowParams = BillingFlowParams.NewBuilder()
          .SetProductDetailsParamsList(new List<BillingFlowParams.ProductDetailsParams> { paramsBuilder.Build() })
          .Build();
      _launchActivity = activity;
      _launchProduct = product;
      // retrying only helps for unconsumed one-time donations. An owned subscription stays owned.
      _launchCanRetry = canRetry && !IsSubscription(product.ProductId);
      var result = _billingClient.LaunchBillingFlow(activity, flowParams);
      if (result.ResponseCode != BillingResponseCode.Ok)
        OnLaunchFailed(result);
    }

    private void OnLaunchFailed(BillingResult result)
    {
      var activity = _launchActivity;
      var product = _launchProduct;
      bool canRetry = _launchCanRetry;
      _launchActivity = null;
      _launchProduct = null;

      if (result.ResponseCode == BillingResponseCode.ItemAlreadyOwned
          && canRetry && activity != null && product != null)
      {
        ProcessExistingPurchases(success =>
        {
          if (success)
            LaunchDonation(activity, product, false);
          else
            _listener.OnDonationFailed(result);
        });
      }
      else
      {
        _listener.OnDonationFailed(result);
      }
    }

    public void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
    {
      if (result.ResponseCode == BillingResponseCode.Ok)
      {
        _launchActivity = null;
        _launchProduct = null;
        foreach (var purchase in purchases ?? new List<Purchase>())
          HandlePurchase(purchase, true, null);
      }
      else if (result.ResponseCode == BillingResponseCode.UserCancelled)
      {
        _launchActivity = null;
        _launchProduct = null;
        _listener.OnDonationCanceled();
      }
      else
      {
        OnLaunchFailed(result);
      }
    }

    /// <summary>
    /// Consumes all purchased (but not yet consumed) one-time donations, acknowledges new yearly donations and
    /// reports the active subscriptions. Call when the billing client is ready.
    /// </summary>
    public void ProcessExistingPurchases(Action<bool>? onFinished)
    {
      ProcessExistingPurchasesOfType(BillingClient.ProductType.Inapp, oneTimeSuccess =>
      {
        ProcessExistingPurchasesOfType(BillingClient.ProductType.Subs, subsSuccess =>
        {
          onFinished?.Invoke(oneTimeSuccess && subsSuccess);
        });
      });
    }

    private void ProcessExistingPurchasesOfType(string productType, Action<bool> onFinished)
    {
      var queryParams = QueryPurchasesParams.NewBuilder().SetProductType(productType).Build();
      _billingClient.QueryPurchases(queryParams, new PurchasesResponseListener((result, purchases) =>
      {
        if (result.ResponseCode != BillingResponseCode.Ok)
        {
          Kp2aLog.Log("Querying donation purchases (" + productType + ") failed: " + result.ResponseCode + " " + result.DebugMessage);
          onFinished(false);
          return;
        }
        var all = purchases ?? new List<Purchase>();
        Kp2aLog.Log("Existing donation purchases (" + productType + "): " + (all.Count == 0 ? "none" : string.Join("; ",
            all.Select(p => string.Join(",", p.Products) + " state=" + p.PurchaseState + " acknowledged=" + p.IsAcknowledged
                             + " autoRenewing=" + p.IsAutoRenewing))));
        var purchased = all
            .Where(p => p.PurchaseState == PurchaseState.Purchased)
            .ToList();
        if (productType == BillingClient.ProductType.Subs)
        {
          // only active subscriptions are returned, so this also drops subscriptions which expired meanwhile
          lock (_activeSubscriptions)
          {
            _activeSubscriptions.Clear();
            foreach (var purchase in purchased)
              _activeSubscriptions.UnionWith(purchase.Products);
          }
          ReportActiveSubscriptions();
        }
        HandleSequentially(purchased, 0, onFinished);
      }));
    }

    private void ReportActiveSubscriptions()
    {
      List<string> active;
      lock (_activeSubscriptions)
        active = _activeSubscriptions.ToList();
      Kp2aLog.Log("Active yearly donations: " + (active.Count == 0 ? "none" : string.Join(",", active)));
      _listener.OnActiveSubscriptionsChanged(active);
    }

    private void HandleSequentially(List<Purchase> purchases, int index, Action<bool> onFinished)
    {
      if (index >= purchases.Count)
      {
        onFinished(true);
        return;
      }
      HandlePurchase(purchases[index], false, success =>
      {
        if (success)
          HandleSequentially(purchases, index + 1, onFinished);
        else
          onFinished(false);
      });
    }

    private void HandlePurchase(Purchase purchase, bool isNewPurchase, Action<bool>? onHandled)
    {
      if (purchase.PurchaseState == PurchaseState.Pending)
      {
        // will be handled once the payment completes (reported through OnPurchasesUpdated or ProcessExistingPurchases)
        if (isNewPurchase)
          _listener.OnDonationPending();
        onHandled?.Invoke(true);
        return;
      }
      if (purchase.PurchaseState != PurchaseState.Purchased)
      {
        onHandled?.Invoke(true);
        return;
      }

      if (purchase.Products.Any(IsSubscription))
        HandleSubscriptionPurchase(purchase, isNewPurchase, onHandled);
      else
        ConsumePurchase(purchase, isNewPurchase, onHandled);
    }

    private void HandleSubscriptionPurchase(Purchase purchase, bool isNewPurchase, Action<bool>? onHandled)
    {
      bool added;
      lock (_activeSubscriptions)
      {
        int countBefore = _activeSubscriptions.Count;
        _activeSubscriptions.UnionWith(purchase.Products);
        added = _activeSubscriptions.Count != countBefore;
      }
      if (added)
        ReportActiveSubscriptions();

      string token = purchase.PurchaseToken;
      // a renewal keeps the token of the original purchase, which is acknowledged already
      if (purchase.IsAcknowledged || !_handledTokens.Add(token))
      {
        onHandled?.Invoke(true);
        return;
      }

      var ackParams = AcknowledgePurchaseParams.NewBuilder().SetPurchaseToken(token).Build();
      _billingClient.AcknowledgePurchase(ackParams, new AcknowledgePurchaseResponseListener(result =>
      {
        if (result.ResponseCode == BillingResponseCode.Ok)
        {
          Kp2aLog.Log("Acknowledged yearly donation " + string.Join(",", purchase.Products));
          _listener.OnDonationCompleted();
          onHandled?.Invoke(true);
        }
        else
        {
          // the payment went through, but the purchase is not acknowledged yet. Acknowledging is retried by
          // ProcessExistingPurchases (next time the donation screen is shown).
          Kp2aLog.Log("Acknowledging yearly donation failed: " + result.ResponseCode + " " + result.DebugMessage);
          _handledTokens.Remove(token);
          if (isNewPurchase)
            _listener.OnDonationCompleted();
          onHandled?.Invoke(false);
        }
      }));
    }

    private void ConsumePurchase(Purchase purchase, bool isNewPurchase, Action<bool>? onHandled)
    {
      string token = purchase.PurchaseToken;
      if (!_handledTokens.Add(token))
      {
        onHandled?.Invoke(true);
        return;
      }
      Kp2aLog.Log("Consuming donation " + string.Join(",", purchase.Products) + " (quantity " + purchase.Quantity + ")");

      var consumeParams = ConsumeParams.NewBuilder().SetPurchaseToken(token).Build();
      _billingClient.Consume(consumeParams, new ConsumeResponseListener((result, _) =>
      {
        if (result.ResponseCode == BillingResponseCode.Ok)
        {
          _listener.OnDonationCompleted();
          onHandled?.Invoke(true);
        }
        else
        {
          // the payment went through, but the purchase remains unconsumed. Consuming is retried by
          // ProcessExistingPurchases (next time the donation screen is shown).
          Kp2aLog.Log("Consuming donation failed: " + result.ResponseCode + " " + result.DebugMessage);
          _handledTokens.Remove(token);
          if (isNewPurchase)
            _listener.OnDonationCompleted();
          onHandled?.Invoke(false);
        }
      }));
    }

    private class ProductDetailsResponseListener : Java.Lang.Object, IProductDetailsResponseListener
    {
      private readonly Action<BillingResult, QueryProductDetailsResult?> _callback;
      public ProductDetailsResponseListener(Action<BillingResult, QueryProductDetailsResult?> callback) { _callback = callback; }
      public void OnProductDetailsResponse(BillingResult result, QueryProductDetailsResult details) { _callback(result, details); }
    }

    private class PurchasesResponseListener : Java.Lang.Object, IPurchasesResponseListener
    {
      private readonly Action<BillingResult, IList<Purchase>?> _callback;
      public PurchasesResponseListener(Action<BillingResult, IList<Purchase>?> callback) { _callback = callback; }
      public void OnQueryPurchasesResponse(BillingResult result, IList<Purchase> purchases) { _callback(result, purchases); }
    }

    private class ConsumeResponseListener : Java.Lang.Object, IConsumeResponseListener
    {
      private readonly Action<BillingResult, string> _callback;
      public ConsumeResponseListener(Action<BillingResult, string> callback) { _callback = callback; }
      public void OnConsumeResponse(BillingResult result, string purchaseToken) { _callback(result, purchaseToken); }
    }

    private class AcknowledgePurchaseResponseListener : Java.Lang.Object, IAcknowledgePurchaseResponseListener
    {
      private readonly Action<BillingResult> _callback;
      public AcknowledgePurchaseResponseListener(Action<BillingResult> callback) { _callback = callback; }
      public void OnAcknowledgePurchaseResponse(BillingResult result) { _callback(result); }
    }
  }
}
#endif
