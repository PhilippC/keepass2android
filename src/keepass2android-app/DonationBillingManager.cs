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
  /// Donations are one-time products which are consumed right after the purchase, so they can be donated
  /// any number of times. Consuming also acknowledges the purchase (otherwise Google refunds it after 3 days).
  /// Purchases which could not be consumed immediately (e.g. app killed during the purchase, or a pending
  /// payment which completed later) are consumed by <see cref="ProcessExistingPurchases"/>.
  /// </summary>
  public class DonationBillingManager : Java.Lang.Object, IPurchasesUpdatedListener, IBillingClientStateListener
  {
    public interface IListener
    {
      void OnProductsLoaded(IList<ProductDetails> products);
      void OnDonationCompleted();
      void OnDonationPending();
      void OnDonationCanceled();
      void OnBillingUnavailable(BillingResult result);
      void OnDonationFailed(BillingResult result);
    }

    // must match the in-app products configured in the Google Play Console
    public static readonly string[] ProductIds = { "oktfestbeer", "smallbeer", "coke" };

    private readonly BillingClient _billingClient;
    private readonly IListener _listener;

    // the currently running purchase flow. If it fails with ITEM_ALREADY_OWNED (a previous donation of the same
    // product was not consumed yet), the owned purchases are consumed and the purchase flow is retried once.
    private Activity? _launchActivity;
    private ProductDetails? _launchProduct;
    private bool _launchCanRetry;

    // purchase tokens which are being or have been consumed. Avoids consuming (and thanking) twice when
    // OnPurchasesUpdated and ProcessExistingPurchases (e.g. from OnResume after the purchase dialog) overlap.
    private readonly HashSet<string> _consumedTokens = new HashSet<string>();

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
      var products = ProductIds.Select(id => QueryProductDetailsParams.Product.NewBuilder()
              .SetProductId(id)
              .SetProductType(BillingClient.ProductType.Inapp)
              .Build())
          .ToList();
      var queryParams = QueryProductDetailsParams.NewBuilder().SetProductList(products).Build();
      _billingClient.QueryProductDetails(queryParams, new ProductDetailsResponseListener((result, details) =>
      {
        if (result.ResponseCode != BillingResponseCode.Ok)
        {
          _listener.OnBillingUnavailable(result);
          return;
        }
        var loaded = details?.ProductDetailsList ?? new List<ProductDetails>();
        foreach (var unfetched in details?.UnfetchedProductList ?? new List<UnfetchedProduct>())
        {
          Kp2aLog.Log("Donation product not available: " + unfetched.ProductId + " (status " + unfetched.StatusCodeValue + ")");
        }
        _listener.OnProductsLoaded(loaded);
      }));
    }

    public void LaunchDonation(Activity activity, ProductDetails product)
    {
      LaunchDonation(activity, product, true);
    }

    private void LaunchDonation(Activity activity, ProductDetails product, bool canRetry)
    {
      var paramsBuilder = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(product);
      // products may have several purchase options, so pass the offer explicitly
      var offerToken = product.OneTimePurchaseOfferDetailsList?.FirstOrDefault()?.OfferToken;
      if (offerToken != null)
        paramsBuilder.SetOfferToken(offerToken);

      var flowParams = BillingFlowParams.NewBuilder()
          .SetProductDetailsParamsList(new List<BillingFlowParams.ProductDetailsParams> { paramsBuilder.Build() })
          .Build();
      _launchActivity = activity;
      _launchProduct = product;
      _launchCanRetry = canRetry;
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
    /// Consumes all purchased (but not yet consumed) donations. Call when the billing client is ready.
    /// </summary>
    public void ProcessExistingPurchases(Action<bool>? onFinished)
    {
      var queryParams = QueryPurchasesParams.NewBuilder().SetProductType(BillingClient.ProductType.Inapp).Build();
      _billingClient.QueryPurchases(queryParams, new PurchasesResponseListener((result, purchases) =>
      {
        if (result.ResponseCode != BillingResponseCode.Ok)
        {
          Kp2aLog.Log("Querying donation purchases failed: " + result.ResponseCode + " " + result.DebugMessage);
          onFinished?.Invoke(false);
          return;
        }
        var toConsume = (purchases ?? new List<Purchase>())
            .Where(p => p.PurchaseState == PurchaseState.Purchased)
            .ToList();
        ConsumeSequentially(toConsume, 0, onFinished);
      }));
    }

    private void ConsumeSequentially(List<Purchase> purchases, int index, Action<bool>? onFinished)
    {
      if (index >= purchases.Count)
      {
        onFinished?.Invoke(true);
        return;
      }
      HandlePurchase(purchases[index], false, success =>
      {
        if (success)
          ConsumeSequentially(purchases, index + 1, onFinished);
        else
          onFinished?.Invoke(false);
      });
    }

    private void HandlePurchase(Purchase purchase, bool isNewPurchase, Action<bool>? onConsumed)
    {
      if (purchase.PurchaseState == PurchaseState.Pending)
      {
        // will be consumed once the payment completes (reported through OnPurchasesUpdated or ProcessExistingPurchases)
        if (isNewPurchase)
          _listener.OnDonationPending();
        onConsumed?.Invoke(true);
        return;
      }
      if (purchase.PurchaseState != PurchaseState.Purchased)
      {
        onConsumed?.Invoke(true);
        return;
      }

      string token = purchase.PurchaseToken;
      if (!_consumedTokens.Add(token))
      {
        onConsumed?.Invoke(true);
        return;
      }

      var consumeParams = ConsumeParams.NewBuilder().SetPurchaseToken(token).Build();
      _billingClient.Consume(consumeParams, new ConsumeResponseListener((result, _) =>
      {
        if (result.ResponseCode == BillingResponseCode.Ok)
        {
          _listener.OnDonationCompleted();
          onConsumed?.Invoke(true);
        }
        else
        {
          // the payment went through, but the purchase remains unconsumed. Consuming is retried by
          // ProcessExistingPurchases (next time the donation screen is shown).
          Kp2aLog.Log("Consuming donation failed: " + result.ResponseCode + " " + result.DebugMessage);
          _consumedTokens.Remove(token);
          if (isNewPurchase)
            _listener.OnDonationCompleted();
          onConsumed?.Invoke(false);
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
  }
}
#endif
