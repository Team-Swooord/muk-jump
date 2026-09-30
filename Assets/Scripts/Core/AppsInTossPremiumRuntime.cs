using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AppsInToss;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MukJump.Core
{
    public sealed class AppsInTossPremiumRuntime : MonoBehaviour
    {
        sealed class WaitingStore : IInkPremiumStore, IInkPremiumStoreState
        {
            public bool IsOwned => false;
            public bool OwnershipResolved => false;
            public bool IsBusy => false;
            public bool CanPurchase => false;
            public string LocalizedPrice => string.Empty;
            public string StatusMessage => "구매 내역을 확인하고 있어요";
            public void Purchase() { }
            public void Restore() { }
        }
        readonly WaitingStore waiting = new();
        readonly Dictionary<string, TaskCompletionSource<TossPremiumOrder[]>> requests = new();
        delegate Action PurchaseStarter(string sku, Func<string, bool> grant, Action completed, Action<string> failed);
        readonly Dictionary<string, (Func<string, bool> grant, Action completed, Action<string> failed)> purchases = new();
        AppsInTossPremiumStore service;
        string owner;
        bool active;
#if UNITY_WEBGL && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<AppsInTossPremiumRuntime>() == null)
                new GameObject(nameof(AppsInTossPremiumRuntime)).AddComponent<AppsInTossPremiumRuntime>();
        }
        [DllImport("__Internal")] static extern void MukJumpTossPremiumReadOrders(string target, string request);
        [DllImport("__Internal")] static extern void MukJumpTossPremiumPurchase(string target, string request, string sku);
        [DllImport("__Internal")] static extern void MukJumpTossPremiumGrantReply(string request, string grantId, int allowed);
        [DllImport("__Internal")] static extern void MukJumpTossPremiumCancelPurchase(string request);
#endif
        void OnEnable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            active = true; DontDestroyOnLoad(gameObject);
            InkPremiumAppearance.Register(waiting);
#endif
        }
        void Update()
        {
            if (!active) return;
            string verified = AppsInTossIdentityPolicy.VerifiedOwnerToken;
            if (owner != verified || service == null && !string.IsNullOrEmpty(verified))
            {
                service?.Dispose(); service = null; owner = verified;
                InkPremiumAppearance.Register(waiting);
                if (!string.IsNullOrEmpty(owner))
                {
                    var settings = Resources.Load<AppsInTossPremiumSettings>(AppsInTossPremiumSettings.ResourcePath);
                    var gateway = new Gateway(ReadOrders, StartPurchase, owner);
                    service = new AppsInTossPremiumStore(gateway, settings?.RemoveAdsSku, owner,
                        () => gateway.OwnerMatches ? AppsInTossIdentityPolicy.VerifiedOwnerToken : string.Empty,
                        () => Time.realtimeSinceStartupAsDouble);
                    InkPremiumAppearance.Register(service);
                }
            }
            if (MobileApplicationLifecycle.IsApplicationActive) service?.Tick();
        }
        Action StartPurchase(string sku, Func<string, bool> grant, Action completed, Action<string> failed)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string id = Guid.NewGuid().ToString("N");
            purchases.Add(id, (grant, completed, failed));
            try { MukJumpTossPremiumPurchase(gameObject.name, id, sku); }
            catch { purchases.Remove(id); MukJumpTossPremiumCancelPurchase(id); throw; }
            return () => { purchases.Remove(id); MukJumpTossPremiumCancelPurchase(id); };
#else
            throw new InvalidOperationException("Toss app required");
#endif
        }
        [UnityEngine.Scripting.Preserve]
        public void ReceivePremiumPurchase(string json)
        {
            if (!active) return;
            var response = JObject.Parse(json);
            string id = (string)response["request"];
            if (id == null || !purchases.TryGetValue(id, out var callbacks)) return;
            if ((string)response["kind"] == "grant")
            {
                bool allowed = false;
                try { allowed = callbacks.grant((string)response["orderId"]); }
                finally
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    MukJumpTossPremiumGrantReply(id, (string)response["grantId"], allowed ? 1 : 0);
#endif
                }
                return;
            }
            purchases.Remove(id);
            if ((string)response["kind"] == "success") callbacks.completed();
            else callbacks.failed((string)response["code"] ?? "ERROR");
        }
        void OnApplicationFocus(bool focused) { if (focused && active && service != null) _ = service.Refresh(); }
        void OnApplicationPause(bool paused) { if (!paused && active && service != null) _ = service.Refresh(); }
        Task<TossPremiumOrder[]> ReadOrders()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string id = Guid.NewGuid().ToString("N");
            var request = new TaskCompletionSource<TossPremiumOrder[]>();
            requests.Add(id, request);
            try { MukJumpTossPremiumReadOrders(gameObject.name, id); }
            catch (Exception ex) { requests.Remove(id); request.TrySetException(ex); }
            return request.Task;
#else
            return Task.FromException<TossPremiumOrder[]>(new InvalidOperationException("Toss app required"));
#endif
        }
        // 설치 SDK의 첫 페이지가 전체 내역인 경우만 성공으로 받는다.
        [UnityEngine.Scripting.Preserve]
        public void ReceivePremiumOrders(string json)
        {
            if (!active) return;
            TaskCompletionSource<TossPremiumOrder[]> request = null;
            try
            {
                var response = JObject.Parse(json);
                string id = (string)response["request"];
                if (id == null || !requests.Remove(id, out request)) return;
                if ((bool?)response["ok"] != true || (bool?)response["historyComplete"] != true ||
                    response["orders"] is not JArray orders)
                { request.TrySetException(new TossPremiumLookupException((string)response["code"] ?? "ERROR")); return; }
                request.TrySetResult(orders.Select(order => new TossPremiumOrder(
                    (string)order["orderId"], (string)order["sku"], (string)order["status"])).ToArray());
            }
            catch (Exception error) { request?.TrySetException(error); }
        }
        void OnDisable()
        {
            active = false;
            if (ReferenceEquals(InkPremiumAppearance.Store, service) || ReferenceEquals(InkPremiumAppearance.Store, waiting))
                InkPremiumAppearance.Register(null);
            service?.Dispose(); service = null;
            purchases.Clear();
            foreach (var request in requests.Values) request.TrySetCanceled();
            requests.Clear();
        }

        sealed class Gateway : IAppsInTossPremiumGateway
        {
            readonly Func<Task<TossPremiumOrder[]>> readOrders;
            readonly PurchaseStarter startPurchase;
            readonly string expectedOwner;
            Action cleanup;
            NativePurchaseOverlay purchaseOverlay;
            bool disposed;
            public bool OwnerMatches { get; private set; } = true;
            public Gateway(Func<Task<TossPremiumOrder[]>> readOrders, PurchaseStarter startPurchase, string owner)
            { this.readOrders = readOrders; this.startPurchase = startPurchase; expectedOwner = owner; }
            async Task VerifyOwner()
            {
                string raw = await AIT.GetUserKeyForGame(15000);
                if (disposed) throw new ObjectDisposedException(nameof(Gateway));
                if (!AppsInTossIdentityPolicy.TryExtractUserHash(raw, out string hash))
                    throw new InvalidOperationException("Identity unavailable");
                if (AppsInTossIdentityPolicy.CreateOwnerToken(hash) != expectedOwner)
                {
                    OwnerMatches = false;
                    throw new InvalidOperationException("Identity changed");
                }
                if (!OwnerMatches || AppsInTossIdentityPolicy.VerifiedOwnerToken != expectedOwner)
                    throw new InvalidOperationException("Identity not verified");
            }
            public async Task<TossPremiumOrder[]> FetchOrders()
            {
                await VerifyOwner();
                var result = await readOrders();
                await VerifyOwner();
                return result;
            }
            public async Task<string> FetchPrice(string sku)
            {
                var result = await AIT.IAPGetProductItemList(15000);
                if (disposed || result == null || !string.IsNullOrEmpty(result.error)) throw new InvalidOperationException();
                return ProductPrice(result.Products?.FirstOrDefault(p => p.Sku == sku));
            }
            public async Task<bool> CompleteGrant(string orderId)
            {
                await VerifyOwner();
                return await AIT.IAPCompleteProductGrant(
                    new IAPCompleteProductGrantArgs_0 { Params = new IAPCompleteProductGrantArgs_0Params { OrderId = orderId } }, 15000);
            }
            public async void Purchase(string sku, Func<string, bool> grant, Action completed, Action<string> failed)
            {
                try { await VerifyOwner(); }
                catch { if (!disposed) failed("IDENTITY_UNAVAILABLE"); return; }
                bool finished = false;
                void Finish()
                {
                    finished = true;
                    try { cleanup?.Invoke(); }
                    finally { cleanup = null; purchaseOverlay?.Dispose(); purchaseOverlay = null; }
                }
                try
                {
                purchaseOverlay = new NativePurchaseOverlay();
                // SDK 3.5 Unity 생성 브리지는 error.code를 버리므로 프로젝트 어댑터로 보존한다.
                Action dispose = startPurchase(sku,
                    orderId => !disposed && OwnerMatches && grant(orderId), () =>
                {
                    if (disposed || finished) return;
                    Finish();
                    completed();
                }, code =>
                {
                    if (disposed || finished) return;
                    Finish();
                    failed(string.IsNullOrEmpty(code) ? "ERROR" : code);
                });
                if (finished || disposed) dispose?.Invoke(); else cleanup = dispose;
                }
                catch { Finish(); if (!disposed) failed("ERROR"); }
            }
            public void Dispose()
            {
                disposed = true;
                try { cleanup?.Invoke(); }
                finally { cleanup = null; purchaseOverlay?.Dispose(); purchaseOverlay = null; }
            }
        }

        public static string ProductPrice(IapProductListItem product)
        {
            // Unity 생성형에 아직 없는 최신 상품 종류는 SDK 확장 필드로 확인한다.
            return product?._extensionData != null && product._extensionData.TryGetValue("type", out var type) &&
                type?.ToString() == "NON_CONSUMABLE" ? product.DisplayAmount ?? string.Empty : string.Empty;
        }
    }
}
