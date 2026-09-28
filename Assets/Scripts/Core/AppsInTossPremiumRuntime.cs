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
                    var gateway = new Gateway(ReadOrders, owner);
                    service = new AppsInTossPremiumStore(gateway, settings?.RemoveAdsSku, owner,
                        () => gateway.OwnerMatches ? AppsInTossIdentityPolicy.VerifiedOwnerToken : string.Empty,
                        () => Time.realtimeSinceStartupAsDouble);
                    InkPremiumAppearance.Register(service);
                }
            }
            if (MobileApplicationLifecycle.IsApplicationActive) service?.Tick();
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
        // 페이지네이션 인자를 노출하지 않는 설치 SDK를 보완하는 자체 브리지의 응답이다.
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
                if ((bool?)response["ok"] != true || response["orders"] is not JArray orders)
                { request.TrySetException(new InvalidOperationException("Order lookup failed")); return; }
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
            foreach (var request in requests.Values) request.TrySetCanceled();
            requests.Clear();
        }

        sealed class Gateway : IAppsInTossPremiumGateway
        {
            readonly Func<Task<TossPremiumOrder[]>> readOrders;
            readonly string expectedOwner;
            Action cleanup;
            bool disposed;
            public bool OwnerMatches { get; private set; } = true;
            public Gateway(Func<Task<TossPremiumOrder[]>> readOrders, string owner)
            { this.readOrders = readOrders; expectedOwner = owner; }
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
                return result.Products?.FirstOrDefault(p => p.Sku == sku)?.DisplayAmount ?? string.Empty;
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
                void Finish() { finished = true; cleanup?.Invoke(); cleanup = null; }
                try
                {
                Action dispose = AIT.IAPCreateOneTimePurchaseOrder(success =>
                {
                    if (disposed || finished) return;
                    Finish();
                    if (success?.Type == "success" && !string.IsNullOrEmpty(success.Data?.OrderId)) completed();
                    else failed("INVALID_RESPONSE");
                }, new IapCreateOneTimePurchaseOrderOptionsOptions
                {
                    ProductId = sku, Sku = sku,
                    ProcessProductGrant = order => !disposed && OwnerMatches && grant(order.OrderId)
                }, error =>
                {
                    if (disposed || finished) return;
                    Finish();
                    string code = error?.ErrorCode ?? "ERROR";
                    failed(code);
                });
                if (finished || disposed) dispose?.Invoke(); else cleanup = dispose;
                }
                catch { Finish(); if (!disposed) failed("ERROR"); }
            }
            public void Dispose() { disposed = true; cleanup?.Invoke(); cleanup = null; }
        }
    }
}
