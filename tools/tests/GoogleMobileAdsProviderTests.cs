// 네이티브 전용 실제 공급자 소스를 SDK 대역과 컴파일한다. 네트워크/실광고 요청은 없다.
// csc -nologo -define:UNITY_IOS -out:/tmp/MukJumpAdsTests.exe tools/tests/GoogleMobileAdsProviderTests.cs Assets/Scripts/Core/GoogleMobileAdsProvider.cs Assets/Scripts/Core/MonetizationPolicy.cs
// mono /tmp/MukJumpAdsTests.exe
using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using MukJump.Core;

namespace UnityEngine
{
    public static class Time { public static double realtimeSinceStartupAsDouble; }
    public static class Debug { public static void LogWarning(object value) { } }
    public enum NetworkReachability { NotReachable, ReachableViaLocalAreaNetwork }
    public static class Application
    {
        public static NetworkReachability internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork;
    }
}
namespace MukJump.Core
{
    public enum GameState { Lobby, Playing, GameOver }
    public enum AnalyticsAdStage { LoadRequested, LoadFailed, Loaded, Opened, Clicked, RewardEarned, Closed, Failed }
    public static class MukJumpAnalytics { public static void Ad(AnalyticsAdStage stage) { } }
    public static class MobileApplicationLifecycle { public static bool IsApplicationActive = true; }
    public static class GoogleMobileAdsRequestFactory { public static object CreateNonPersonalized() => new object(); }
}
namespace GoogleMobileAds.Api
{
    public class AdError { }
    public class LoadAdError { }
    public class Reward { }
    public class FakeAd
    {
        public bool Ready = true, Destroyed;
        public bool SendOpened = true, CloseOnDestroy;
        public bool ThrowOnReady, ThrowOnShow, ThrowOnDestroy;
        public event Action OnAdFullScreenContentOpened, OnAdFullScreenContentClosed, OnAdClicked;
        public event Action<AdError> OnAdFullScreenContentFailed;
        public bool CanShowAd() { if (ThrowOnReady) throw new Exception("readiness"); return Ready && !Destroyed; }
        public void Open() { if (ThrowOnShow) throw new Exception("show"); Ready = false; if (SendOpened) OnAdFullScreenContentOpened?.Invoke(); }
        public void Close() => OnAdFullScreenContentClosed?.Invoke();
        public void Fail() => OnAdFullScreenContentFailed?.Invoke(new AdError());
        public void Click() => OnAdClicked?.Invoke();
        public void Destroy() { if (Destroyed) return; Destroyed = true; if (ThrowOnDestroy) throw new Exception("destroy"); if (CloseOnDestroy) Close(); }
    }
    public class RewardedAd : FakeAd
    {
        public static int Loads;
        public static bool FailLoads, DeferLoads, ThrowOnLoad;
        public static RewardedAd Last;
        public static Action<RewardedAd, LoadAdError> PendingLoad;
        public Action<Reward> Earned;
        public static void Load(string id, object request, Action<RewardedAd, LoadAdError> callback)
        {
            Loads++;
            if (ThrowOnLoad) throw new Exception("load");
            Last = new RewardedAd();
            if (DeferLoads) { PendingLoad = callback; return; }
            callback(FailLoads ? null : Last, FailLoads ? new LoadAdError() : null);
        }
        public void Show(Action<Reward> callback) { Earned = callback; Open(); }
    }
    public class InterstitialAd : FakeAd
    {
        public static int Loads;
        public static InterstitialAd Last;
        public static void Load(string id, object request, Action<InterstitialAd, LoadAdError> callback)
        { Loads++; Last = new InterstitialAd(); callback(Last, null); }
        public void Show() => Open();
    }
}
public static class GoogleMobileAdsProviderTests
{
    static int passed;
    const FullScreenAdPlacement RewardPlacement = FullScreenAdPlacement.GameOverReviveReward;
    static double Now { get => UnityEngine.Time.realtimeSinceStartupAsDouble; set => UnityEngine.Time.realtimeSinceStartupAsDouble = value; }
    static void Expect(bool value, string message) { if (!value) throw new Exception(message); }
    static void Test(string name, Action<GoogleMobileAdsProvider> body)
    {
        Now = 0;
        RewardedAd.Loads = InterstitialAd.Loads = 0;
        RewardedAd.FailLoads = RewardedAd.DeferLoads = RewardedAd.ThrowOnLoad = false;
        RewardedAd.PendingLoad = null;
        MobileApplicationLifecycle.IsApplicationActive = true;
        UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork;
        using (var provider = new GoogleMobileAdsProvider("fake-reward", "fake-interstitial")) body(provider);
        passed++;
        Console.WriteLine("PASS " + name);
    }
    public static int Main()
    {
        Test("unusable cache is replaced", p => {
            p.Preload(RewardPlacement); var old = RewardedAd.Last; old.Ready = false;
            Expect(!p.IsReady(RewardPlacement), "unusable ready"); p.Tick();
            Expect(old.Destroyed && RewardedAd.Loads == 2 && p.IsReady(RewardPlacement), "reload stalled");
        });
        Test("55 minute cache refresh", p => {
            p.Preload(RewardPlacement); var old = RewardedAd.Last;
            Now = 3300; p.Tick();
            Expect(old.Destroyed && RewardedAd.Loads == 2, "expired cache retained");
        });
        Test("fresh cache does not reload repeatedly", p => {
            p.Preload(RewardPlacement);
            for (int i = 0; i < 100; i++) { Now = i; p.Tick(); }
            Expect(RewardedAd.Loads == 1, "duplicate loads");
        });
        Test("long ad retains reward and completes once", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; var results = new List<bool>();
            p.Show(RewardPlacement, results.Add);
            for (int i = 1; i <= 1600; i++) { Now = i * .1; p.Tick(); }
            Expect(results.Count == 0 && p.HasPendingPresentation && !ad.Destroyed, "active ad timed out");
            Expect(RewardedAd.Loads == 1, "loads during full screen");
            ad.Earned(new Reward()); ad.Close(); ad.Close(); ad.Fail();
            Expect(results.Count == 1 && results[0] && !p.HasPendingPresentation, "reward lost/duplicated");
            Expect(ad.Destroyed && RewardedAd.Loads == 2, "next ad not preloaded");
        });
        Test("unopened ad still times out", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; ad.SendOpened = false; var results = new List<bool>();
            p.Show(RewardPlacement, results.Add);
            for (int i = 1; i <= 910; i++) { Now = i * .1; p.Tick(); }
            Expect(results.Count == 1 && !results[0] && ad.Destroyed, "missing callback locks game");
        });
        Test("paused frame gap allows delayed callbacks", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; ad.SendOpened = false; var results = new List<bool>();
            p.Show(RewardPlacement, results.Add); Now = 600; p.Tick();
            Expect(results.Count == 0, "resume discarded callback");
            ad.Earned(new Reward()); ad.Close(); Expect(results.Count == 1 && results[0], "resume reward lost");
        });
        Test("offline waits without requesting", p => {
            UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.NotReachable;
            p.Preload(RewardPlacement); p.Tick(); Expect(RewardedAd.Loads == 0, "offline request");
            UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork;
            p.Tick(); Expect(RewardedAd.Loads == 1, "network recovery stalled");
        });
        Test("background waits without requesting", p => {
            MobileApplicationLifecycle.IsApplicationActive = false; p.Preload(RewardPlacement); p.Tick();
            Expect(RewardedAd.Loads == 0, "background request");
            MobileApplicationLifecycle.IsApplicationActive = true; p.Tick(); Expect(RewardedAd.Loads == 1, "foreground stalled");
        });
        Test("bounded failure backoff and successful reset", p => {
            RewardedAd.FailLoads = true; p.Preload(RewardPlacement);
            double[] attempts = { 15, 45, 105, 225, 345 };
            for (int i = 0; i < attempts.Length; i++) {
                Now = attempts[i] - .1; p.Tick(); Expect(RewardedAd.Loads == i + 1, "early retry");
                Now = attempts[i]; p.Tick(); Expect(RewardedAd.Loads == i + 2, "retry missed");
            }
            RewardedAd.FailLoads = false; Now = 465; p.Tick();
            Expect(p.IsReady(RewardPlacement), "success not ready");
            var ad = RewardedAd.Last; p.Show(RewardPlacement, _ => {});
            RewardedAd.FailLoads = true; ad.Close(); int loads = RewardedAd.Loads;
            Now += 15; p.Tick(); Expect(RewardedAd.Loads == loads + 1, "backoff not reset after success");
        });
        Test("late load is destroyed after timeout", p => {
            RewardedAd.DeferLoads = true; p.Preload(RewardPlacement);
            var late = RewardedAd.Last; var callback = RewardedAd.PendingLoad; Now = 31; p.Tick();
            callback(late, null); Expect(late.Destroyed && !p.IsReady(RewardPlacement), "stale load accepted");
            RewardedAd.DeferLoads = false; Now = 46; p.Tick(); Expect(p.IsReady(RewardPlacement), "retry failed");
        });
        Test("closing without reward does not grant revive", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; bool? reward = null;
            p.Show(RewardPlacement, value => reward = value); ad.Close(); Expect(reward == false, "unearned reward");
        });
        Test("earned reward survives SDK failure", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; bool? reward = null;
            p.Show(RewardPlacement, value => reward = value); ad.Earned(new Reward()); ad.Fail();
            Expect(reward == true, "earned reward revoked");
        });
        Test("interstitial expiry and long presentation", p => {
            var placement = FullScreenAdPlacement.PostRunInterstitial; p.Preload(placement);
            var old = InterstitialAd.Last; Now = 3300; p.Tick();
            Expect(old.Destroyed && InterstitialAd.Loads == 2, "interstitial cache stalled");
            var ad = InterstitialAd.Last; bool? result = null; p.Show(placement, value => result = value);
            Now += 200; p.Tick(); Expect(result == null, "interstitial prematurely completed");
            ad.Close(); Expect(result == true, "interstitial close failed");
        });
        Test("dispose resolves once and blocks late reward", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; var results = new List<bool>();
            p.Show(RewardPlacement, results.Add); p.Dispose(); ad.Earned(new Reward()); ad.Close();
            Expect(results.Count == 1 && !results[0] && ad.Destroyed && !p.HasPendingPresentation, "dispose leak/duplicate");
        });
        Test("synchronous close during destroy is idempotent", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; ad.CloseOnDestroy = true; var results = new List<bool>();
            p.Show(RewardPlacement, results.Add); ad.Earned(new Reward()); ad.Close();
            Expect(results.Count == 1 && results[0] && RewardedAd.Loads == 2, "reentrant cleanup duplicated completion");
        });
        Test("SDK readiness exception destroys cache and retries", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; ad.ThrowOnReady = true;
            Expect(!p.IsReady(RewardPlacement) && ad.Destroyed, "readiness exception leaked");
            Now = 15; p.Tick(); Expect(p.IsReady(RewardPlacement), "readiness retry stalled");
        });
        Test("SDK load throw releases latch", p => {
            RewardedAd.ThrowOnLoad = true; p.Preload(RewardPlacement); RewardedAd.ThrowOnLoad = false;
            Now = 15; p.Tick(); Expect(RewardedAd.Loads == 2 && p.IsReady(RewardPlacement), "load throw stuck busy");
        });
        Test("SDK show throw resolves and prepares next ad", p => {
            p.Preload(RewardPlacement); RewardedAd.Last.ThrowOnShow = true; bool? result = null;
            p.Show(RewardPlacement, value => result = value);
            Expect(result == false && !p.HasPendingPresentation && RewardedAd.Loads == 2, "show throw stuck busy");
        });
        Test("cleanup and consumer exceptions do not skip next preload", p => {
            p.Preload(RewardPlacement); var ad = RewardedAd.Last; ad.ThrowOnDestroy = true; int calls = 0;
            p.Show(RewardPlacement, _ => { calls++; throw new Exception("consumer"); });
            ad.Earned(new Reward()); ad.Close();
            Expect(calls == 1 && !p.HasPendingPresentation && RewardedAd.Loads == 2, "exception skipped recovery");
        });
        Console.WriteLine("TOTAL " + passed + " PASSED");
        return 0;
    }
}
