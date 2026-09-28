using System;
using System.Threading.Tasks;
using NUnit.Framework;
using MukJump.Core;

namespace MukJump.EditorTools.Tests
{
    public sealed class AppsInTossPremiumTests
    {
        sealed class Gateway : IAppsInTossPremiumGateway
        {
            public TossPremiumOrder[] Orders = Array.Empty<TossPremiumOrder>();
            public bool Fail, GrantResult = true;
            public int Purchases, Completions;
            public Func<string, bool> Grant;
            public Action Success;
            public Action<string> Failure;
            public TaskCompletionSource<TossPremiumOrder[]> Pending;
            public Task<string> FetchPrice(string sku) => Task.FromResult("₩4,900");
            public Task<TossPremiumOrder[]> FetchOrders() => Pending?.Task ?? (Fail
                ? Task.FromException<TossPremiumOrder[]>(new Exception("offline")) : Task.FromResult(Orders));
            public Task<bool> CompleteGrant(string id) { Completions++; return Task.FromResult(GrantResult); }
            public void Purchase(string sku, Func<string, bool> grant, Action completed, Action<string> failed)
            { Purchases++; Grant = grant; Success = completed; Failure = failed; }
            public void Dispose() { }
        }
        Gateway gateway;
        AppsInTossPremiumStore store;
        string owner;
        [SetUp] public void Setup()
        {
            owner = "owner-a"; gateway = new Gateway();
            store = new AppsInTossPremiumStore(gateway, "premium", owner, () => owner, () => 100);
            InkPremiumAppearance.UseForTests(true, store);
        }
        [TearDown] public void Cleanup() { store.Dispose(); InkPremiumAppearance.RestoreForTests(); }
        TossPremiumOrder Order(string id, string state, string sku = "premium") => new(id, sku, state);

        [Test] public async Task LookupFailureDoesNotBecomeUnowned()
        {
            gateway.Fail = true; await store.Refresh();
            Assert.That(store.OwnershipResolved, Is.False);
            Assert.That(InkPremiumAppearance.SuppressAds, Is.True);
            Assert.That(store.CanPurchase, Is.False);
        }
        [Test] public async Task ExactProductOnlyAndLocalizedPrice()
        {
            gateway.Orders = new[] { Order("other", "COMPLETED", "different") }; await store.Refresh();
            Assert.That(store.CanPurchase, Is.True); Assert.That(store.IsOwned, Is.False);
            Assert.That(store.LocalizedPrice, Is.EqualTo("₩4,900"));
        }
        [Test] public async Task RestoreThenOfflineKeepsConfirmedOwnership()
        {
            gateway.Orders = new[] { Order("first", "COMPLETED") }; await store.Refresh();
            gateway.Fail = true; await store.Refresh();
            Assert.That(store.IsOwned, Is.True); Assert.That(store.CanPurchase, Is.False);
        }
        [Test] public async Task RefundWinsForSameOrderButRepurchaseIsIndependent()
        {
            gateway.Orders = new[] { Order("first", "REFUNDED"), Order("first", "COMPLETED") };
            await store.Refresh(); Assert.That(store.IsOwned, Is.False);
            gateway.Orders = new[] { Order("first", "REFUNDED"), Order("second", "COMPLETED") };
            await store.Refresh(); Assert.That(store.IsOwned, Is.True);
        }
        [Test] public async Task PendingPaidOrderGrantsBeforeCompletionAndCanRetry()
        {
            gateway.Orders = new[] { Order("pending", "PENDING") }; gateway.GrantResult = false;
            await store.Refresh(); Assert.That(store.IsOwned, Is.True); Assert.That(gateway.Completions, Is.EqualTo(1));
            gateway.GrantResult = true; await store.Refresh(); Assert.That(gateway.Completions, Is.EqualTo(2));
        }
        [Test] public async Task PurchaseIsSingleFlightAndGrantSurvivesHistoryDelay()
        {
            await store.Refresh(); store.Purchase(); store.Purchase();
            Assert.That(gateway.Purchases, Is.EqualTo(1)); Assert.That(store.IsOwned, Is.False);
            Assert.That(gateway.Grant("new"), Is.True); Assert.That(gateway.Grant("new"), Is.True);
            gateway.Success(); await store.Refresh(); Assert.That(store.IsOwned, Is.True);
            gateway.Orders = new[] { Order("new", "REFUNDED") }; await store.Refresh();
            Assert.That(store.IsOwned, Is.False);
        }
        [TestCase("USER_CANCELED")]
        [TestCase("PAYMENT_PENDING")]
        [TestCase("ERROR")]
        public async Task FailedOrDeferredPaymentDoesNotGrant(string code)
        {
            await store.Refresh(); store.Purchase(); gateway.Failure(code);
            Assert.That(store.IsOwned, Is.False);
            if (code == "PAYMENT_PENDING") Assert.That(store.CanPurchase, Is.False);
        }
        [Test] public async Task ChangedOwnerRejectsLateGrant()
        {
            await store.Refresh(); store.Purchase(); owner = "owner-b";
            Assert.That(gateway.Grant("late"), Is.False); gateway.Success(); store.Tick();
            Assert.That(store.IsOwned, Is.False); Assert.That(store.CanPurchase, Is.False);
        }
        [Test] public async Task ChangedOwnerRejectsLateLookup()
        {
            gateway.Pending = new(); var task = store.Refresh(); owner = "owner-b";
            gateway.Pending.SetResult(new[] { Order("late", "COMPLETED") }); await task;
            Assert.That(store.IsOwned, Is.False); Assert.That(store.OwnershipResolved, Is.False);
        }
        [Test] public async Task InvalidSnapshotDoesNotRevoke()
        {
            gateway.Orders = new[] { Order("owned", "COMPLETED") }; await store.Refresh();
            gateway.Orders = new[] { Order("bad", "UNKNOWN") }; await store.Refresh();
            Assert.That(store.IsOwned, Is.True);
        }
        [Test] public void MissingSkuFailsClosed()
        {
            using var missing = new AppsInTossPremiumStore(gateway, "", owner, () => owner, () => 0);
            missing.Refresh().GetAwaiter().GetResult();
            Assert.That(missing.OwnershipResolved, Is.False); Assert.That(missing.CanPurchase, Is.False);
        }
    }

    public sealed class PostRunAdSessionTests
    {
        [SetUp] public void Setup() => InkPremiumAppearance.UseForTests(false, null);
        [TearDown] public void Cleanup() => InkPremiumAppearance.RestoreForTests();
        [Test] public void ThreeRunsAndTwoMinutesBothRequired()
        {
            var session = new PostRunAdSession();
            Assert.That(session.ConsumeExit("1", 130, false, true), Is.False);
            Assert.That(session.ConsumeExit("2", 131, false, true), Is.False);
            Assert.That(session.ConsumeExit("3", 132, false, true), Is.True);
            session.MarkFullScreen(150);
            session.ConsumeExit("4", 180, false, true); session.ConsumeExit("5", 200, false, true);
            Assert.That(session.ConsumeExit("6", 269, false, true), Is.False);
            session.ConsumeExit("7", 271, false, true); session.ConsumeExit("8", 272, false, true);
            Assert.That(session.ConsumeExit("9", 273, false, true), Is.True);
        }
        [Test] public void NoAdDuringFirstTwoMinutesOrDuplicateExit()
        {
            var session = new PostRunAdSession();
            session.ConsumeExit("1", 1, false, true); session.ConsumeExit("2", 2, false, true);
            Assert.That(session.ConsumeExit("3", 119, false, true), Is.False);
            Assert.That(session.ConsumeExit("3", 180, false, true), Is.False);
            Assert.That(session.CompletedRuns, Is.EqualTo(3));
        }
        [TestCase(true, true, false)] [TestCase(false, false, false)] [TestCase(false, true, true)]
        public void PurchaseUnavailableOrRewardedSkipsWithoutQueue(bool owned, bool ready, bool rewarded)
        {
            var session = new PostRunAdSession();
            session.ConsumeExit("1", 121, false, true); session.ConsumeExit("2", 122, false, true);
            if (rewarded) session.MarkRewarded("3", 123);
            Assert.That(session.ConsumeExit("3", 400, owned, ready), Is.False);
            Assert.That(session.ConsumeExit("3", 401, false, true), Is.False);
        }
    }

    public sealed class PostRunAdGameManagerTests
    {
        sealed class Ad : IFullScreenAdProvider, IFullScreenAdLifecycle
        {
            public int Shows; public Action<bool> Complete; public bool HasPendingPresentation { get; set; }
            public bool IsReady(FullScreenAdPlacement p) => p == FullScreenAdPlacement.PostRunInterstitial;
            public void Preload(FullScreenAdPlacement p) { }
            public void Show(FullScreenAdPlacement p, Action<bool> completed)
            { Shows++; HasPendingPresentation = true; Complete = completed; }
        }
        const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static object Call(object target, string name) => target.GetType().GetMethod(name, Flags).Invoke(target, null);
        [Test] public void ActualGameOverExitWaitsForAdAndForegroundAndIgnoresDuplicateCallbacks()
        {
            bool originalPause = UnityEngine.AudioListener.pause;
            UnityEngine.GameObject host = null;
            try
            {
                InkPremiumAppearance.UseForTests(false, null);
                typeof(PostRunAdSession).GetMethod("ResetSession", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
                Set(PostRunAdSession.Current, "lastFullScreenFinished", -1000d);
                double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
                PostRunAdSession.Current.ConsumeExit("one", now, false, true);
                PostRunAdSession.Current.ConsumeExit("two", now, false, true);
                var ad = new Ad(); MonetizationAds.RegisterProvider(ad);
                host = new UnityEngine.GameObject("PostRunAdFixture");
                var manager = host.AddComponent<GameManager>();
                Set(manager, "<State>k__BackingField", GameState.GameOver);
                Set(manager, "gameOverResultSettled", true); Set(manager, "currentRunId", "three");
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                Call(manager, "HandleGameOverLobbyRequested"); Call(manager, "HandleGameOverLobbyRequested");
                Assert.That(ad.Shows, Is.EqualTo(1)); Assert.That(UnityEngine.AudioListener.pause, Is.True);
                Assert.That(manager.IsTransitioning, Is.True);
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                ad.HasPendingPresentation = false; ad.Complete(false); ad.Complete(true);
                Call(manager, "Update"); Assert.That(manager.IsTransitioning, Is.True);
                // 실제 씬 전환 없이 광고 종료 경계만 검증한다.
                Set(manager, "<State>k__BackingField", GameState.Lobby);
                MobileApplicationLifecycle.SetPlatformVisibility(true); Call(manager, "Update");
                Assert.That(manager.IsTransitioning, Is.False); Assert.That(UnityEngine.AudioListener.pause, Is.False);
                ad.Complete(true); Assert.That(manager.IsTransitioning, Is.False);
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                MonetizationAds.ResetProvider(); InkPremiumAppearance.RestoreForTests();
                MobileApplicationLifecycle.SetPlatformVisibility(true); UnityEngine.AudioListener.pause = originalPause;
                typeof(PostRunAdSession).GetMethod("ResetSession", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            }
        }
    }
}
