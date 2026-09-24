using System;
using System.Collections.Generic;

namespace MukJump.Core
{
    public interface IInkPremiumStoreState { bool OwnershipResolved { get; } }

    /// 검증된 StoreKit 조회 결과와 결제 알림을 서로 다른 경로로 전달한다.
    public readonly struct InkPremiumPurchase
    {
        public readonly string ProductId, TransactionId;
        public readonly object PendingOrder;
        public readonly bool Verified;
        public InkPremiumPurchase(string productId, string transactionId, bool verified, object pendingOrder = null)
        { ProductId = productId; TransactionId = transactionId; Verified = verified; PendingOrder = pendingOrder; }
        public bool MatchesProduct => ProductId == InkPremiumStore.ProductId && !string.IsNullOrWhiteSpace(TransactionId);
    }

    public interface IInkPremiumGateway : IDisposable
    {
        event Action Connected;
        event Action Disconnected;
        event Action<string> Price;
        event Action<InkPremiumPurchase[]> Purchases;
        event Action PurchasesFailed;
        event Action<InkPremiumPurchase> Pending;
        event Action<bool> PurchaseFailed;
        event Action Deferred;
        event Action<string, bool> Confirmed;
        event Action<string> Revoked;
        event Action AccountChanged;
        void Connect();
        void FetchProduct();
        void FetchPurchases();
        void Purchase();
        void Restore(Action<bool> completed);
        void Confirm(InkPremiumPurchase purchase);
    }

    /// 구매권한은 매 실행 StoreKit에서 확인한다. 내구 표식은 중단 복구용이며 권한 증명이 아니다.
    public sealed class InkPremiumStore : IInkPremiumStore, IInkPremiumStoreState, IDisposable
    {
        public const string ProductId = "com.CYSB.MukJump.removeads";
        public const string GrantMarkerKey = "MukJump.Premium.FulfilledTransaction";
        const double RetrySeconds = 30;
        readonly IInkPremiumGateway gateway;
        readonly Action<string> persistFulfillment;
        readonly Func<double> clock;
        readonly HashSet<string> confirmed = new();
        readonly HashSet<string> confirming = new();
        readonly HashSet<string> saved = new();
        bool connected, connecting, fetching, productFetching, purchaseBusy, restoring, restoreWaiting;
        bool disposed, refreshQueued, deferred;
        int entitlementGeneration, fetchGeneration, productGeneration;
        double nextRetry, nextProductRetry, busyDeadline;
        string pendingTransaction;
        public bool IsOwned { get; private set; }
        public bool OwnershipResolved { get; private set; }
        public bool IsBusy => connecting || purchaseBusy || restoring || restoreWaiting;
        public bool CanPurchase => connected && OwnershipResolved && !IsOwned && !deferred &&
            !string.IsNullOrEmpty(LocalizedPrice) && !IsBusy && string.IsNullOrEmpty(pendingTransaction);
        public string LocalizedPrice { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = "스토어에 연결하고 있어요";

        public InkPremiumStore(IInkPremiumGateway gateway, Action<string> persistFulfillment, Func<double> clock)
        {
            this.gateway = gateway; this.persistFulfillment = persistFulfillment; this.clock = clock;
            gateway.Connected += OnConnected; gateway.Disconnected += OnDisconnected;
            gateway.Price += OnPrice; gateway.Purchases += OnPurchases; gateway.PurchasesFailed += OnPurchasesFailed;
            gateway.Pending += OnPending; gateway.PurchaseFailed += OnPurchaseFailed; gateway.Deferred += OnDeferred;
            gateway.Confirmed += OnConfirmed; gateway.Revoked += OnRevoked; gateway.AccountChanged += OnAccountChanged;
        }
        public void Start() => Connect();
        void Connect()
        {
            if (disposed || connecting || connected) return;
            connecting = true; nextRetry = clock() + RetrySeconds;
            try { gateway.Connect(); } catch { OnDisconnected(); }
        }
        void OnConnected()
        {
            if (disposed) return;
            connecting = false; connected = true;
            // 상품 메타데이터가 없어도 SK2는 오프라인 currentEntitlements를 반환한다.
            RefreshOwnership(); FetchProduct();
        }
        void OnDisconnected()
        {
            if (disposed) return;
            connecting = connected = productFetching = false;
            LocalizedPrice = string.Empty;
            nextRetry = clock() + RetrySeconds;
            StatusMessage = "스토어 연결을 다시 시도하고 있어요";
            // 통신 실패를 미보유로 바꾸지 않는다. 게임 플레이는 이 상태를 기다리지 않는다.
        }
        void FetchProduct()
        {
            if (!connected || productFetching || disposed) return;
            productFetching = true; productGeneration = entitlementGeneration;
            nextProductRetry = clock() + RetrySeconds;
            try { gateway.FetchProduct(); } catch { OnPrice(string.Empty); }
        }
        void OnPrice(string price)
        {
            if (disposed) return;
            productFetching = false;
            if (productGeneration != entitlementGeneration)
            { LocalizedPrice = string.Empty; FetchProduct(); return; }
            LocalizedPrice = price ?? string.Empty;
            nextProductRetry = clock() + RetrySeconds;
            if (!IsOwned && !IsBusy && string.IsNullOrEmpty(LocalizedPrice))
                StatusMessage = "가격을 불러오지 못했어요. 구입 복원은 이용할 수 있어요";
        }
        public void RefreshOwnership()
        {
            if (disposed) return;
            if (!connected) { Connect(); return; }
            if (fetching || restoring) { refreshQueued = true; return; }
            fetching = true; fetchGeneration = entitlementGeneration;
            nextRetry = clock() + RetrySeconds;
            try { gateway.FetchPurchases(); } catch { OnPurchasesFailed(); }
        }
        void OnPending(InkPremiumPurchase purchase)
        {
            if (disposed || !purchase.MatchesProduct) return;
            // 성공 알림 자체는 검증 증거가 아니다. 시작 중이던 오래된 조회도 버린다.
            pendingTransaction = purchase.TransactionId;
            purchaseBusy = false; deferred = false;
            entitlementGeneration++;
            StatusMessage = "구매 내역을 확인하고 있어요";
            RefreshOwnership();
        }
        void OnPurchases(InkPremiumPurchase[] purchases)
        {
            if (disposed) return;
            bool stale = fetching && fetchGeneration != entitlementGeneration;
            fetching = false;
            if (stale) { refreshQueued = false; RefreshOwnership(); return; }
            bool owned = false, pendingSeen = false, saveFailed = false;
            var toConfirm = new List<InkPremiumPurchase>();
            var scheduled = new HashSet<string>();
            foreach (var purchase in purchases ?? Array.Empty<InkPremiumPurchase>())
            {
                if (!purchase.Verified || !purchase.MatchesProduct) continue;
                owned = true;
                if (purchase.TransactionId == pendingTransaction) pendingSeen = true;
                if (purchase.PendingOrder == null || confirmed.Contains(purchase.TransactionId) ||
                    confirming.Contains(purchase.TransactionId) || !scheduled.Add(purchase.TransactionId)) continue;
                try
                {
                    if (!saved.Contains(purchase.TransactionId))
                    {
                        persistFulfillment(purchase.TransactionId);
                        saved.Add(purchase.TransactionId);
                    }
                    toConfirm.Add(purchase);
                }
                catch { saveFailed = true; }
            }
            IsOwned = owned; OwnershipResolved = true;
            if (pendingSeen) pendingTransaction = null;
            bool wasRestoring = restoreWaiting;
            restoreWaiting = false;
            StatusMessage = saveFailed ? "구매는 확인했어요. 저장 후 다시 마무리할게요" :
                owned ? "광고 제거와 금색 먹을 이용할 수 있어요" :
                wasRestoring ? "현재 App Store 계정의 구매 내역이 없어요" :
                !string.IsNullOrEmpty(pendingTransaction) ? "구매 검증을 기다리고 있어요" :
                deferred ? "구매 승인 대기 중이에요" : "한 번 구매하면 계속 이용할 수 있어요";
            // 보유 적용과 내구 저장이 끝난 검증된 PendingOrder만 확정한다.
            foreach (var purchase in toConfirm)
            {
                confirming.Add(purchase.TransactionId);
                try { gateway.Confirm(purchase); }
                catch { OnConfirmed(purchase.TransactionId, false); }
            }
            nextRetry = clock() + RetrySeconds;
            if (refreshQueued) { refreshQueued = false; RefreshOwnership(); }
        }
        void OnPurchasesFailed()
        {
            if (disposed) return;
            fetching = false; restoreWaiting = false; refreshQueued = false;
            nextRetry = clock() + RetrySeconds;
            StatusMessage = "구매 확인을 다시 시도해 주세요. 기존 구매는 유지돼요";
        }
        void OnConfirmed(string transaction, bool success)
        {
            if (disposed) return;
            confirming.Remove(transaction);
            if (success) confirmed.Add(transaction);
            else { nextRetry = clock() + RetrySeconds; StatusMessage = "구매는 확인했어요. 연결되면 마무리할게요"; }
        }
        void OnRevoked(string product)
        {
            if (disposed || product != ProductId) return;
            entitlementGeneration++; IsOwned = false; OwnershipResolved = true;
            pendingTransaction = null; purchaseBusy = deferred = false;
            confirmed.Clear(); confirming.Clear(); saved.Clear();
            StatusMessage = "구매 권한이 변경되었어요. 필요하면 구입을 복원해 주세요";
            RefreshOwnership();
        }
        void OnAccountChanged()
        {
            if (disposed) return;
            entitlementGeneration++; LocalizedPrice = string.Empty;
            deferred = purchaseBusy = false; pendingTransaction = null;
            confirmed.Clear(); confirming.Clear(); saved.Clear();
            RefreshOwnership(); FetchProduct();
        }
        void OnPurchaseFailed(bool cancelled)
        {
            if (disposed) return;
            purchaseBusy = false;
            StatusMessage = cancelled ? "구매를 취소했어요" : "구매하지 못했어요. 잠시 후 다시 시도해 주세요";
            RefreshOwnership();
        }
        void OnDeferred()
        {
            if (disposed) return;
            purchaseBusy = false; deferred = true;
            StatusMessage = "구매 승인 대기 중이에요";
        }
        public void Purchase()
        {
            if (!CanPurchase) return;
            purchaseBusy = true; busyDeadline = clock() + 120;
            StatusMessage = "App Store에서 구매를 진행해 주세요";
            try { gateway.Purchase(); } catch { OnPurchaseFailed(false); }
        }
        public void Restore()
        {
            if (disposed || IsBusy) return;
            if (!connected) { Connect(); return; }
            if (fetching) { StatusMessage = "구매 내역을 확인하고 있어요"; return; }
            restoring = true; restoreWaiting = true; fetching = true;
            fetchGeneration = entitlementGeneration; busyDeadline = clock() + 60;
            StatusMessage = "구입을 복원하고 있어요";
            try
            {
                // Unity IAP가 성공 시 FetchPurchases를 이어서 호출한다. 콜백만으로 보유하지 않는다.
                gateway.Restore(success =>
                {
                    if (disposed) return;
                    restoring = false;
                    if (!success) { fetching = restoreWaiting = false; StatusMessage = "구입 복원을 완료하지 못했어요"; }
                });
            }
            catch { fetching = restoring = restoreWaiting = false; StatusMessage = "구입 복원을 완료하지 못했어요"; }
        }
        public void Tick()
        {
            if (disposed) return;
            double now = clock();
            if (now >= busyDeadline && (purchaseBusy || restoring || restoreWaiting))
            {
                purchaseBusy = restoring = restoreWaiting = false;
                StatusMessage = "스토어 응답을 기다리고 있어요. 구입 복원으로 확인해 주세요";
                RefreshOwnership();
            }
            if (now >= nextRetry)
            {
                // 진행 중인 SDK 요청은 겹쳐 재요청하지 않는다. 완료 시 이벤트가 이어진다.
                if (!connected) Connect(); else if (!fetching && !restoring) RefreshOwnership();
            }
            if (connected && !productFetching && string.IsNullOrEmpty(LocalizedPrice) && now >= nextProductRetry) FetchProduct();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            gateway.Connected -= OnConnected; gateway.Disconnected -= OnDisconnected;
            gateway.Price -= OnPrice; gateway.Purchases -= OnPurchases; gateway.PurchasesFailed -= OnPurchasesFailed;
            gateway.Pending -= OnPending; gateway.PurchaseFailed -= OnPurchaseFailed; gateway.Deferred -= OnDeferred;
            gateway.Confirmed -= OnConfirmed; gateway.Revoked -= OnRevoked; gateway.AccountChanged -= OnAccountChanged;
            gateway.Dispose();
        }
    }
}
