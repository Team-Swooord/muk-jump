using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MukJump.Core
{
    public sealed class TossPremiumLookupException : Exception
    {
        public string Code { get; }
        public TossPremiumLookupException(string code) : base(code) { Code = code; }
    }

    public readonly struct TossPremiumOrder
    {
        public readonly string Id, Sku, Status;
        public TossPremiumOrder(string id, string sku, string status)
        { Id = id; Sku = sku; Status = status; }
    }

    public interface IAppsInTossPremiumGateway : IDisposable
    {
        Task<string> FetchPrice(string sku);
        Task<TossPremiumOrder[]> FetchOrders();
        Task<bool> CompleteGrant(string orderId);
        void Purchase(string sku, Func<string, bool> grant, Action completed, Action<string> failed);
    }

    /// 토스 주문 원본으로만 보유를 복원한다. 로컬 저장 경로·빌드 번호·의상 비트에 결제권한을 넣지 않는다.
    public sealed class AppsInTossPremiumStore : IInkPremiumStore, IInkPremiumStoreState, IDisposable
    {
        readonly IAppsInTossPremiumGateway gateway;
        readonly string sku, owner;
        readonly Func<string> currentOwner;
        readonly Func<double> clock;
        readonly HashSet<string> granted = new();
        bool disposed, refreshing, purchasing, deferred;
        int revision;
        double nextRefresh;
        bool owned, ownershipResolved;
        public bool IsOwned { get => ValidOwner && owned; private set => owned = value; }
        public bool OwnershipResolved { get => ValidOwner && ownershipResolved; private set => ownershipResolved = value; }
        public bool IsBusy => refreshing || purchasing;
        public bool CanPurchase => ValidOwner && OwnershipResolved && !IsOwned && !IsBusy &&
            !deferred && !string.IsNullOrWhiteSpace(sku) && !string.IsNullOrEmpty(LocalizedPrice);
        public string LocalizedPrice { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = "구매 내역을 확인하고 있어요";
        bool ValidOwner => !disposed && !string.IsNullOrEmpty(owner) && owner == currentOwner();

        public AppsInTossPremiumStore(IAppsInTossPremiumGateway gateway, string sku, string owner,
            Func<string> currentOwner, Func<double> clock)
        {
            this.gateway = gateway; this.sku = sku; this.owner = owner;
            this.currentOwner = currentOwner; this.clock = clock;
        }

        public async Task Refresh()
        {
            if (!ValidOwner || refreshing || purchasing) return;
            if (string.IsNullOrWhiteSpace(sku))
            {
                OwnershipResolved = false;
                StatusMessage = "상품을 준비하고 있어요";
                return;
            }
            refreshing = true;
            int request = revision;
            nextRefresh = clock() + 30;
            try
            {
                // 브리지는 전체 내역임을 확인한 목록만 반환하고 부분 결과는 실패 처리한다.
                TossPremiumOrder[] orders = await gateway.FetchOrders();
                if (!ValidOwner || request != revision) return;
                if (orders == null) throw new InvalidOperationException("Missing orders");
                var matching = new Dictionary<string, string>();
                foreach (var order in orders)
                {
                    if (order.Sku != sku) continue;
                    if (string.IsNullOrWhiteSpace(order.Id) ||
                        order.Status != "COMPLETED" && order.Status != "REFUNDED" && order.Status != "PENDING")
                        throw new InvalidOperationException("Invalid order");
                    // 같은 주문의 환불이 완료/미결보다 우선한다. 다른 주문의 재구매는 별개다.
                    if (!matching.TryGetValue(order.Id, out string previous) || previous != "REFUNDED")
                        matching[order.Id] = order.Status;
                }
                bool owned = false, pending = false;
                foreach (var order in matching)
                {
                    if (order.Value == "REFUNDED") { granted.Remove(order.Key); continue; }
                    owned = true;
                    granted.Remove(order.Key);
                    if (order.Value == "PENDING") pending = true;
                }
                // SDK 지급 콜백 직후 주문 목록 반영이 늦어져도 확인된 구매를 회수하지 않는다.
                // 해당 주문이 조회되면 서버 상태로 전환하며 명시적인 환불은 즉시 우선한다.
                owned |= granted.Count > 0;
                if (!pending) deferred = false;
                IsOwned = owned; OwnershipResolved = true;
                StatusMessage = owned ? "광고 제거와 금색 먹을 이용할 수 있어요" :
                    "한 번 구매하면 계속 이용할 수 있어요";
                // 결제 완료된 미결 주문에 먼저 혜택을 적용한 뒤 지급 완료를 확인한다.
                if (pending)
                    foreach (var order in matching)
                    {
                        if (!ValidOwner || request != revision) return;
                        if (order.Value != "PENDING") continue;
                        bool completed = await gateway.CompleteGrant(order.Key);
                        if (!ValidOwner || request != revision) return;
                        if (!completed)
                            StatusMessage = "구매는 확인했어요. 연결되면 마무리할게요";
                    }
                try
                {
                    string price = await gateway.FetchPrice(sku);
                    if (ValidOwner && request == revision) LocalizedPrice = price ?? string.Empty;
                }
                catch { if (ValidOwner && request == revision) LocalizedPrice = string.Empty; }
            }
            catch (Exception error)
            {
                if (ValidOwner && request == revision)
                {
                    OwnershipResolved = false; LocalizedPrice = string.Empty;
                    StatusMessage = error is TossPremiumLookupException lookup && lookup.Code == "UNSUPPORTED_APP_VERSION"
                        ? "토스 앱을 최신 버전으로 업데이트해 주세요"
                        : error is TossPremiumLookupException incomplete && incomplete.Code == "INCOMPLETE_ORDER_HISTORY"
                        ? "구매 내역이 많아 자동 확인을 마치지 못했어요"
                        : "구매 확인을 다시 시도해 주세요. 기존 구매는 유지돼요";
                }
            }
            finally { refreshing = false; }
        }

        bool Grant(string orderId)
        {
            // SDK의 결제 완료 콜백은 구매를 시작한 소유자와 연결된 상품에만 적용한다.
            if (!ValidOwner || !purchasing || string.IsNullOrWhiteSpace(orderId)) return false;
            if (!granted.Add(orderId)) return true;
            revision++;
            IsOwned = OwnershipResolved = true;
            StatusMessage = "광고 제거와 금색 먹을 이용할 수 있어요";
            return true;
        }

        public void Purchase()
        {
            if (!CanPurchase) return;
            purchasing = true;
            StatusMessage = "토스에서 구매를 진행해 주세요";
            try
            {
                gateway.Purchase(sku, Grant, () =>
                {
                    if (!ValidOwner || !purchasing) return;
                    purchasing = deferred = false; nextRefresh = clock();
                    _ = Refresh();
                }, error =>
                {
                    if (!ValidOwner || !purchasing) return;
                    purchasing = false;
                    if (error == "ITEM_ALREADY_OWNED" || error == "UNSUPPORTED_APP_VERSION")
                    {
                        OwnershipResolved = false; LocalizedPrice = string.Empty;
                        StatusMessage = error == "ITEM_ALREADY_OWNED" ? "구매 내역을 확인하고 있어요" :
                            "토스 앱을 최신 버전으로 업데이트해 주세요";
                        nextRefresh = clock();
                        return;
                    }
                    deferred = error == "PAYMENT_PENDING";
                    if (error != "USER_CANCELED")
                    { OwnershipResolved = false; LocalizedPrice = string.Empty; }
                    StatusMessage = error == "USER_CANCELED" ? "구매를 취소했어요" :
                        deferred ? "구매 승인 대기 중이에요" : "구매하지 못했어요. 구입 복원으로 확인해 주세요";
                    nextRefresh = clock() + 30;
                });
            }
            catch
            {
                purchasing = false; OwnershipResolved = false; LocalizedPrice = string.Empty;
                nextRefresh = clock() + 30;
                StatusMessage = "구매하지 못했어요. 구입 복원으로 확인해 주세요";
            }
        }
        public void Restore() { if (!IsBusy) _ = Refresh(); }
        public void Tick()
        {
            if (!ValidOwner)
            {
                IsOwned = OwnershipResolved = false; LocalizedPrice = string.Empty;
                StatusMessage = "토스 사용자를 다시 확인해야 해요. 게임을 다시 열어 주세요";
                return;
            }
            if (clock() >= nextRefresh && !IsBusy) _ = Refresh();
        }
        public void Dispose()
        { disposed = true; revision++; IsOwned = false; OwnershipResolved = false; gateway.Dispose(); }
    }
}
