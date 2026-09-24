#if UNITY_IOS || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using Purchasing.Utilities;

namespace MukJump.Core
{
    /// Apple 기본 구매 창만 사용한다. 에디터/토스에서 실제 결제를 자동 초기화하지 않는다.
    public sealed class AppleInkPremiumRuntime : MonoBehaviour
    {
        InkPremiumStore service;
#if UNITY_IOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<AppleInkPremiumRuntime>() == null)
                new GameObject(nameof(AppleInkPremiumRuntime)).AddComponent<AppleInkPremiumRuntime>();
        }
#endif
        void OnEnable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            DontDestroyOnLoad(gameObject);
            if (service == null)
                service = new InkPremiumStore(new AppleInkPremiumGateway(), SaveFulfillment,
                    () => Time.realtimeSinceStartupAsDouble);
            InkPremiumAppearance.Register(service);
            service.Start();
#endif
        }
        static void SaveFulfillment(string transactionId)
        {
            // 이 값으로 IsOwned를 복원하지 않는다. 매번 StoreKit의 검증된 목록이 필요하다.
            PlayerPrefs.SetString(InkPremiumStore.GrantMarkerKey, transactionId);
            PlayerPrefs.Save();
        }
        void Update() { if (MobileApplicationLifecycle.IsApplicationActive) service?.Tick(); }
        void OnApplicationPause(bool paused) { if (!paused) service?.RefreshOwnership(); }
        void OnApplicationFocus(bool focused) { if (focused) service?.RefreshOwnership(); }
        void OnDisable()
        {
            if (ReferenceEquals(InkPremiumAppearance.Store, service)) InkPremiumAppearance.Register(null);
            service?.Dispose(); service = null;
        }
    }

    /// IAP 5.4.3의 FetchPurchases → fetchAllTransactions는 검증된 현재/미확정 거래만 반환한다.
    /// 직접 purchase 성공 알림은 verified 플래그를 관리 코드에 보장하지 않아 조회를 거쳐야 한다.
    public sealed class AppleInkPremiumGateway : IInkPremiumGateway
    {
        readonly StoreController store;
        Product product;
        bool disposed;
        public event Action Connected;
        public event Action Disconnected;
        public event Action<string> Price;
        public event Action<InkPremiumPurchase[]> Purchases;
        public event Action PurchasesFailed;
        public event Action<InkPremiumPurchase> Pending;
        public event Action<bool> PurchaseFailed;
        public event Action Deferred;
        public event Action<string, bool> Confirmed;
        public event Action<string> Revoked;
        public event Action AccountChanged;
        public AppleInkPremiumGateway()
        {
            store = UnityIAPServices.StoreController(AppleAppStore.Name);
            store.OnStoreConnected += OnConnected;
            store.OnStoreDisconnected += OnDisconnected;
            store.OnProductsFetched += OnProducts;
            store.OnProductsFetchFailed += OnProductsFailed;
            store.OnPurchasesFetched += OnPurchases;
            store.OnPurchasesFetchFailed += OnPurchasesFailed;
            store.OnPurchasePending += OnPending;
            store.OnPurchaseFailed += OnPurchaseFailed;
            store.OnPurchaseDeferred += OnDeferred;
            store.OnPurchaseConfirmed += OnConfirmed;
            store.OnAuthAccountChanged += OnAccountChanged;
            if (store.AppleStoreExtendedPurchaseService != null)
                store.AppleStoreExtendedPurchaseService.OnEntitlementRevoked += OnRevoked;
            // 수신 순서가 Pending → Purchases인 자동 재전달 대신 검증된 목록을 한 번만 처리한다.
            store.ProcessPendingOrdersOnPurchasesFetched(false);
        }
        public async void Connect()
        {
            if (disposed) return;
            try
            {
                if (StoreKitSelector.UseStoreKit1()) { Disconnected?.Invoke(); return; }
                await store.Connect();
            }
            catch { if (!disposed) Disconnected?.Invoke(); }
        }
        public void FetchProduct() => store.FetchProductsWithNoRetries(new List<ProductDefinition>
            { new(InkPremiumStore.ProductId, ProductType.NonConsumable) });
        public void FetchPurchases() => store.FetchPurchases();
        public void Purchase()
        {
            if (product == null || !product.availableToPurchase) throw new InvalidOperationException();
            store.PurchaseProduct(product);
        }
        public void Restore(Action<bool> completed) => store.RestoreTransactions((ok, _) => completed(ok));
        public void Confirm(InkPremiumPurchase purchase)
        {
            if (!purchase.Verified || !purchase.MatchesProduct || purchase.PendingOrder is not PendingOrder pending ||
                pending.Info.TransactionID != purchase.TransactionId) throw new InvalidOperationException();
            store.ConfirmPurchase(pending);
        }
        void OnConnected() { if (!disposed) Connected?.Invoke(); }
        void OnDisconnected(StoreConnectionFailureDescription _) { if (!disposed) Disconnected?.Invoke(); }
        void OnProducts(List<Product> products)
        {
            if (disposed) return;
            product = products.FirstOrDefault(p => p.definition.storeSpecificId == InkPremiumStore.ProductId &&
                p.definition.type == ProductType.NonConsumable && p.availableToPurchase);
            Price?.Invoke(product?.metadata?.localizedPriceString ?? string.Empty);
        }
        void OnProductsFailed(ProductFetchFailed _) { if (!disposed) { product = null; Price?.Invoke(string.Empty); } }
        void OnPurchases(Orders orders)
        {
            if (disposed) return;
            var result = new List<InkPremiumPurchase>();
            foreach (var order in orders.ConfirmedOrders) result.Add(ReadOrder(order, true));
            foreach (var order in orders.PendingOrders) result.Add(ReadOrder(order, true));
            // Ask to Buy 대기는 결제 완료가 아니므로 보유 목록에 포함하지 않는다.
            Purchases?.Invoke(result.ToArray());
        }
        static InkPremiumPurchase ReadOrder(Order order, bool fromVerifiedFetch)
        {
            var apple = order.Info.Apple;
            string id = order.Info.PurchasedProductInfo?.FirstOrDefault()?.productId;
            if (string.IsNullOrEmpty(id))
                id = order.CartOrdered.Items().FirstOrDefault()?.Product.definition.storeSpecificId;
            bool verified = fromVerifiedFetch && apple?.StoreName == AppleAppStore.Name &&
                !string.IsNullOrWhiteSpace(apple.jwsRepresentation) &&
                ulong.TryParse(order.Info.TransactionID, out ulong transaction) && transaction > 0;
            return new InkPremiumPurchase(id, order.Info.TransactionID, verified, order as PendingOrder);
        }
        void OnPurchasesFailed(PurchasesFetchFailureDescription _) { if (!disposed) PurchasesFailed?.Invoke(); }
        void OnPending(PendingOrder order) { if (!disposed) Pending?.Invoke(ReadOrder(order, false)); }
        void OnPurchaseFailed(FailedOrder order)
        { if (!disposed) PurchaseFailed?.Invoke(order.FailureReason == PurchaseFailureReason.UserCancelled); }
        void OnDeferred(DeferredOrder _) { if (!disposed) Deferred?.Invoke(); }
        void OnConfirmed(Order order)
        { if (!disposed) Confirmed?.Invoke(order.Info.TransactionID, order is ConfirmedOrder); }
        void OnRevoked(string id) { if (!disposed) Revoked?.Invoke(id); }
        void OnAccountChanged() { if (!disposed) { product = null; AccountChanged?.Invoke(); } }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            store.OnStoreConnected -= OnConnected; store.OnStoreDisconnected -= OnDisconnected;
            store.OnProductsFetched -= OnProducts; store.OnProductsFetchFailed -= OnProductsFailed;
            store.OnPurchasesFetched -= OnPurchases; store.OnPurchasesFetchFailed -= OnPurchasesFailed;
            store.OnPurchasePending -= OnPending; store.OnPurchaseFailed -= OnPurchaseFailed;
            store.OnPurchaseDeferred -= OnDeferred; store.OnPurchaseConfirmed -= OnConfirmed;
            store.OnAuthAccountChanged -= OnAccountChanged;
            if (store.AppleStoreExtendedPurchaseService != null)
                store.AppleStoreExtendedPurchaseService.OnEntitlementRevoked -= OnRevoked;
        }
    }
}
#endif
