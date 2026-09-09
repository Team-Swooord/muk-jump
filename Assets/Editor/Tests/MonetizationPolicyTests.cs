using System;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class MonetizationPolicyTests
    {
        [TearDown]
        public void TearDown()
        {
            MonetizationAds.ResetProvider();
        }

        [TestCase(GameState.Lobby, false, false, true, false, true)]
        [TestCase(GameState.Playing, false, false, true, false, true)]
        [TestCase(GameState.Playing, false, false, false, false, true)]
        [TestCase(GameState.Lobby, false, false, false, false, false)]
        [TestCase(GameState.Playing, true, false, true, false, false)]
        [TestCase(GameState.Playing, false, true, true, false, false)]
        [TestCase(GameState.Playing, false, false, true, true, false)]
        [TestCase(GameState.Lobby, false, false, true, true, false)]
        [TestCase(GameState.GameOver, false, false, true, false, false)]
        public void TopBannerStaysDuringGameplayButNeverCoversBlockingScreens(GameState state,
            bool paused, bool transition, bool mainLobby, bool overlay, bool expected)
        {
            Assert.That(MonetizationPolicy.ShouldShowTopBanner(state, paused, transition, mainLobby, overlay),
                Is.EqualTo(expected));
        }

        [Test]
        public void PreRunShieldIsAnOptionalFirstRunOfferOnly()
        {
            Assert.That(MonetizationPolicy.CanOfferPreRunShield(0, false, true),
                Is.True);
            Assert.That(MonetizationPolicy.CanOfferPreRunShield(0, true, true),
                Is.False);
            Assert.That(MonetizationPolicy.CanOfferPreRunShield(1, false, true),
                Is.False);
        }

        [Test]
        public void GameOverReviveRequiresReadyAdAndUnusedRunReward()
        {
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(true, false, true),
                Is.True);
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(false, false, true),
                Is.False);
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(true, true, true),
                Is.False);
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(true, false, false),
                Is.False);
        }

        [Test]
        public void EarnedRewardCannotBeRevokedByLateProviderFailure()
        {
            foreach (FullScreenAdPlacement placement in new[]
                     {
                         FullScreenAdPlacement.PreRunShieldReward,
                         FullScreenAdPlacement.GameOverReviveReward,
                     })
            {
                Assert.That(
                    MonetizationPolicy.ResolveFullScreenCompletion(
                        placement,
                        rewardEarned: true,
                        nonRewardedCompleted: false),
                    Is.True,
                    $"{placement} 보상 이벤트 뒤 timeout·dispose가 성공을 취소하면 안 됩니다.");
                Assert.That(
                    MonetizationPolicy.ResolveFullScreenCompletion(
                        placement,
                        rewardEarned: false,
                        nonRewardedCompleted: true),
                    Is.False,
                    "보상형 광고는 닫힘만으로 보상을 지급하면 안 됩니다.");
            }
        }

        [Test]
        public void InterstitialCompletionStillRequiresNormalDismissal()
        {
            Assert.That(
                MonetizationPolicy.ResolveFullScreenCompletion(
                    FullScreenAdPlacement.PostRunInterstitial,
                    rewardEarned: true,
                    nonRewardedCompleted: false),
                Is.False);
            Assert.That(
                MonetizationPolicy.ResolveFullScreenCompletion(
                    FullScreenAdPlacement.PostRunInterstitial,
                    rewardEarned: false,
                    nonRewardedCompleted: true),
                Is.True);
        }

        [Test]
        public void InterstitialUsesThreeRunAndTwoMinuteCaps()
        {
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                3, 60f, 180f, false, true), Is.True);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                2, 60f, 180f, false, true), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                3, 20f, 180f, false, true), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                3, 60f, 30f, false, true), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(
                3, 60f, 180f, true, true), Is.False);
        }

        [Test]
        public void MissingProviderStartsUnavailableWhileSupportedRuntimeMayStillRegister()
        {
            Assert.That(MonetizationAds.HasProvider, Is.False);
            Assert.That(MonetizationAds.RuntimeProviderExpected, Is.True,
                "에디터·모바일·WebGL은 초기화가 늦어도 광고 공급자 등록을 기다려야 합니다.");
            Assert.That(MonetizationAds.Provider.IsReady(
                FullScreenAdPlacement.PreRunShieldReward), Is.False);
            Assert.That(MonetizationAds.Provider.IsReady(
                FullScreenAdPlacement.GameOverReviveReward), Is.False);
        }

        [Test]
        public void AppsInTossLoadRequestThrowReleasesLatchAndSchedulesRetry()
        {
            var provider = new AppsInTossAdProvider("rewarded", string.Empty);
            SetField(
                provider,
                "loadRequestForTests",
                new Func<string, Action<bool>, Action, Action>(
                    (_, _, _) => throw new InvalidOperationException("load failed")));
            LogAssert.Expect(
                LogType.Warning,
                "먹점프 토스 보상형 광고 로드 요청 실패: load failed");

            provider.Preload(FullScreenAdPlacement.GameOverReviveReward);

            Assert.That(GetField<bool>(provider, "rewardedLoading"), Is.False);
            Assert.That(provider.IsReady(
                FullScreenAdPlacement.GameOverReviveReward), Is.False);
            Assert.That(GetField<double>(provider, "nextRewardedLoadTime"),
                Is.GreaterThan(Time.realtimeSinceStartupAsDouble));
            provider.Dispose();
        }

        [Test]
        public void AppsInTossTimedOutLoadIgnoresLateSuccessAndDisposerFailure()
        {
            var provider = new AppsInTossAdProvider("rewarded", string.Empty);
            Action<bool> completeLoad = null;
            SetField(
                provider,
                "loadRequestForTests",
                new Func<string, Action<bool>, Action, Action>(
                    (_, onLoaded, _) =>
                    {
                        completeLoad = onLoaded;
                        return () => throw new InvalidOperationException(
                            "dispose failed");
                    }));
            provider.Preload(FullScreenAdPlacement.GameOverReviveReward);
            SetField(provider, "rewardedLoadDeadline", -1d);
            LogAssert.Expect(
                LogType.Warning,
                "먹점프 토스 보상형 광고 로드 시간이 초과되었습니다.");
            LogAssert.Expect(
                LogType.Warning,
                "먹점프 토스 광고 실패한 보상형 로드 구독 해제 실패: " +
                "dispose failed");

            provider.Tick();
            completeLoad?.Invoke(true);

            Assert.That(GetField<bool>(provider, "rewardedLoading"), Is.False);
            Assert.That(provider.IsReady(
                FullScreenAdPlacement.GameOverReviveReward), Is.False,
                "timeout 뒤 늦은 loaded 이벤트가 폐기된 광고를 되살리면 안 됩니다.");
            provider.Dispose();
        }

        [Test]
        public void AppsInTossShowThrowCompletesOnceAndStartsFreshPreload()
        {
            var provider = new AppsInTossAdProvider("rewarded", string.Empty);
            int preloadCalls = 0;
            int completionCalls = 0;
            bool completion = true;
            SetField(provider, "rewardedReady", true);
            SetField(
                provider,
                "loadRequestForTests",
                new Func<string, Action<bool>, Action, Action>(
                    (_, _, _) =>
                    {
                        preloadCalls++;
                        return null;
                    }));
            SetField(
                provider,
                "showRequestForTests",
                new Func<string, Action<string>, Action, Action>(
                    (_, _, _) => throw new InvalidOperationException("show failed")));
            LogAssert.Expect(
                LogType.Warning,
                "먹점프 토스 광고 표시 요청 실패: show failed");

            provider.Show(
                FullScreenAdPlacement.GameOverReviveReward,
                result =>
                {
                    completionCalls++;
                    completion = result;
                });

            Assert.That(completionCalls, Is.EqualTo(1));
            Assert.That(completion, Is.False);
            Assert.That(GetField<Action<bool>>(provider, "pendingCompletion"),
                Is.Null);
            Assert.That(preloadCalls, Is.EqualTo(1));
            provider.Dispose();
        }

        [Test]
        public void AppsInTossDisposerAndConsumerExceptionsDoNotSkipCompletionCleanup()
        {
            var provider = new AppsInTossAdProvider("rewarded", string.Empty);
            Action<string> emitShowEvent = null;
            int preloadCalls = 0;
            SetField(provider, "rewardedReady", true);
            SetField(
                provider,
                "loadRequestForTests",
                new Func<string, Action<bool>, Action, Action>(
                    (_, _, _) =>
                    {
                        preloadCalls++;
                        return null;
                    }));
            SetField(
                provider,
                "showRequestForTests",
                new Func<string, Action<string>, Action, Action>(
                    (_, onEvent, _) =>
                    {
                        emitShowEvent = onEvent;
                        return () => throw new InvalidOperationException(
                            "show dispose failed");
                    }));

            provider.Show(
                FullScreenAdPlacement.GameOverReviveReward,
                _ => throw new InvalidOperationException("consumer failed"));
            emitShowEvent?.Invoke("userEarnedReward");
            LogAssert.Expect(
                LogType.Warning,
                "먹점프 토스 광고 표시 완료 구독 해제 실패: " +
                "show dispose failed");
            LogAssert.Expect(
                LogType.Warning,
                "먹점프 토스 광고 완료 처리 실패: consumer failed");
            emitShowEvent?.Invoke("dismissed");

            Assert.That(GetField<Action<bool>>(provider, "pendingCompletion"),
                Is.Null);
            Assert.That(preloadCalls, Is.EqualTo(1),
                "외부 완료 콜백이 예외를 내도 다음 광고 preload는 진행해야 합니다.");
            provider.Dispose();
        }

        [Test]
        public void AppsInTossNullConsumerStillCompletesAndPreloadsAgain()
        {
            var provider = new AppsInTossAdProvider("rewarded", string.Empty);
            Action<string> emitShowEvent = null;
            int preloadCalls = 0;
            SetField(provider, "rewardedReady", true);
            SetField(
                provider,
                "loadRequestForTests",
                new Func<string, Action<bool>, Action, Action>(
                    (_, _, _) =>
                    {
                        preloadCalls++;
                        return null;
                    }));
            SetField(
                provider,
                "showRequestForTests",
                new Func<string, Action<string>, Action, Action>(
                    (_, onEvent, _) =>
                    {
                        emitShowEvent = onEvent;
                        return null;
                    }));

            provider.Show(
                FullScreenAdPlacement.GameOverReviveReward,
                onCompleted: null);
            Assert.That(
                GetField<Action<bool>>(provider, "pendingCompletion"),
                Is.Not.Null,
                "콜백 유무와 광고 진행 상태를 같은 null 값으로 표현하면 안 됩니다.");

            emitShowEvent?.Invoke("userEarnedReward");
            emitShowEvent?.Invoke("dismissed");

            Assert.That(
                GetField<Action<bool>>(provider, "pendingCompletion"),
                Is.Null);
            Assert.That(preloadCalls, Is.EqualTo(1));
            provider.Dispose();
        }

        static void SetField<T>(object target, string name, T value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        static T GetField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }
    }
}
