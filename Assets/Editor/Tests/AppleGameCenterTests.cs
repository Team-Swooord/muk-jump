using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class AppleGameCenterTests
    {
        const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        GameObject host;
        GameObject runtimeHost;
        GameLanguage previousLanguage;

        [SetUp] public void SetUp()
        {
            previousLanguage = GameLocalization.Language;
            MukJumpIdentityProfile.UseStoreForTests(new MemoryIdentityStore());
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            typeof(AppleGameCenterRuntime).GetMethod("ResetStatics", PrivateStatic).Invoke(null, null);
        }

        [TearDown] public void TearDown()
        {
            if (host != null)
            {
                typeof(LobbyOptionsView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(host.GetComponent<LobbyOptionsView>(), null);
                Object.DestroyImmediate(host);
            }
            if (runtimeHost != null) Object.DestroyImmediate(runtimeHost);
            typeof(AppleGameCenterRuntime).GetMethod("ResetStatics", PrivateStatic).Invoke(null, null);
            GameLocalization.SetLanguage(previousLanguage);
            MukJumpIdentityProfile.UseStoreForTests(null);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }

        [Test] public void ConfigurationMatchesTheActualAppleAndBackendLeaderboards()
        {
            Assert.That(AppleGameCenterRuntime.LeaderboardId,
                Is.EqualTo("com.CYSB.MukJump.bestHeight"));
            Assert.That(AppleGameCenterRuntime.LeaderboardId,
                Is.EqualTo(MukJumpBackendReleaseValidator.ExpectedAppleGameCenterLeaderboardId));
            Assert.That(MukJumpBackendSettings.Load().AllTimeRankUuid,
                Is.EqualTo("01a08afd-b237-7723-9e6a-b8d7950285fd"));
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(237, true)]
        [TestCase(int.MaxValue, true)]
        public void OnlyPositiveCompletedRunsQualify(int height, bool expected)
        {
            // 가져온 최고 기록이 더 높아도 자격 판단은 이번 판의 고도만 사용한다.
            var result = new GameOverResult(height, int.MaxValue, false, 0, 0, true);
            Assert.That(AppleGameCenterRuntime.IsEligible(result), Is.EqualTo(expected));
        }

        [TestCase(GameOverPersistenceState.ScoreBaselinePending)]
        [TestCase(GameOverPersistenceState.GrowthRecoveryRequired)]
        [TestCase(GameOverPersistenceState.RecordWritePending)]
        public void PendingSettlementNeverSubmits(GameOverPersistenceState state) =>
            Assert.That(AppleGameCenterRuntime.IsEligible(
                new GameOverResult(237, 999, false, 0, 0, true, persistenceState: state)), Is.False);

        [Test] public void DebugPreviewAndFailedWritesNeverSubmit()
        {
            Assert.That(AppleGameCenterRuntime.IsEligible(new GameOverResult(237, 999, false, 0, 0, false)), Is.False);
            Assert.That(AppleGameCenterRuntime.IsEligible(new GameOverResult(237, 999, false, 0, 0, true,
                isGrowthPreview: true)), Is.False);
            Assert.That(AppleGameCenterRuntime.IsEligible(new GameOverResult(237, 999, false, 0, 0, true,
                recordSaved: false)), Is.False);
            Assert.That(AppleGameCenterRuntime.IsEligible(new GameOverResult(237, 999, false, 0, 0, true,
                growthRewardSaved: false)), Is.False);
        }

        [Test] public void AutomaticConnectionWaitsForAnUnblockedLobby()
        {
            Assert.That(AppleGameCenterRuntime.CanAutoConnect(GameState.Lobby, false, false, false, false), Is.True);
            Assert.That(AppleGameCenterRuntime.CanAutoConnect(GameState.Playing, false, false, false, false), Is.False);
            Assert.That(AppleGameCenterRuntime.CanAutoConnect(GameState.GameOver, false, false, false, false), Is.False);
            Assert.That(AppleGameCenterRuntime.CanAutoConnect(GameState.Lobby, true, false, false, false), Is.False);
            Assert.That(AppleGameCenterRuntime.CanAutoConnect(GameState.Lobby, false, true, false, false), Is.False);
            Assert.That(AppleGameCenterRuntime.CanAutoConnect(GameState.Lobby, false, false, true, false), Is.False);
            Assert.That(AppleGameCenterRuntime.CanAutoConnect(GameState.Lobby, false, false, false, true), Is.False);
        }

        AppleGameCenterRuntime MakeRuntime()
        {
            runtimeHost = new GameObject("GameCenterTests");
            return runtimeHost.AddComponent<AppleGameCenterRuntime>();
        }

        static void ArmResponse(int request)
        {
            typeof(AppleGameCenterRuntime).GetField("request", PrivateStatic).SetValue(null, request);
            typeof(AppleGameCenterRuntime).GetField("<Loading>k__BackingField", PrivateStatic).SetValue(null, true);
        }

        const string ValidResponse = "{\"request\":7,\"rows\":[{\"rank\":1,\"height\":237,\"name\":\"ApplePlayer\"}]}";

        [Test] public void StaleAndPostAccountChangeRepliesCannotRestoreAnotherPlayersRows()
        {
            var runtime = MakeRuntime();
            ArmResponse(8);
            runtime.ReceiveLeaderboard(ValidResponse);
            Assert.That(AppleGameCenterRuntime.Entries, Is.Empty);
            Assert.That(AppleGameCenterRuntime.Loading, Is.True);
            ArmResponse(7);
            runtime.ReceiveLeaderboard(ValidResponse);
            Assert.That(AppleGameCenterRuntime.Entries.Count, Is.EqualTo(1));
            runtime.AccountChanged("");
            runtime.ReceiveLeaderboard(ValidResponse);
            Assert.That(AppleGameCenterRuntime.Entries, Is.Empty);
            Assert.That(AppleGameCenterRuntime.Loading, Is.False);
        }

        [Test] public void EmptyInvalidAndUnauthenticatedResponsesNeverShowAZeroMetrePlayer()
        {
            var runtime = MakeRuntime();
            ArmResponse(7);
            runtime.ReceiveLeaderboard("{\"request\":7,\"rows\":[null,{\"height\":0},{\"height\":-4}]}");
            Assert.That(AppleGameCenterRuntime.Entries, Is.Empty);
            ArmResponse(7);
            runtime.ReceiveLeaderboard(ValidResponse);
            ArmResponse(8);
            runtime.ReceiveLeaderboard("{\"request\":8,\"error\":\"auth\"}");
            Assert.That(AppleGameCenterRuntime.Entries, Is.Empty);
            Assert.That(AppleGameCenterRuntime.Status, Is.EqualTo("Game Center 로그인이 필요해요"));
        }

#if UNITY_IOS
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void LeaderboardTabsFitAndKeepAppleRowsSeparateWithoutInventedFlags(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            host = new GameObject("GameCenterLeaderboardUiTests");
            var view = host.AddComponent<LobbyOptionsView>();
            view.BuildForTests();
            // EditMode에서는 MonoBehaviour의 런타임 활성화 이벤트를 명시적으로 연결한다.
            typeof(LobbyOptionsView).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            var page = host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage");
            var global = page.Find("GlobalLeaderboardTab").GetComponent<Button>();
            var apple = page.Find("AppleLeaderboardTab").GetComponent<Button>();
            var refresh = page.Find("LeaderboardRefresh").GetComponent<Button>();
            Canvas.ForceUpdateCanvases();
            foreach (var button in new[] { global, apple, refresh })
            {
                var text = button.GetComponentInChildren<Text>(true);
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.text);
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.text);
            }
            var title = (RectTransform)page.Find("LeaderboardTitle");
            var tab = (RectTransform)apple.transform;
            var heading = (RectTransform)page.Find("RankHeading");
            Assert.That(title.anchoredPosition.y + title.rect.yMin,
                Is.GreaterThan(tab.anchoredPosition.y + tab.rect.yMax));
            Assert.That(tab.anchoredPosition.y + tab.rect.yMin,
                Is.GreaterThan(heading.anchoredPosition.y + heading.rect.yMax));
            Assert.That(page.Find("LeaderboardStatus"), Is.Null, "제목 아래 설명 문구는 다시 만들지 않습니다.");

            apple.onClick.Invoke(); // 에디터에서 네이티브 인증이나 서버 호출은 실행하지 않는다.
            var runtime = MakeRuntime();
            ArmResponse(7);
            runtime.ReceiveLeaderboard(ValidResponse);
            Assert.That(page.Find("NameCell1/Name1").GetComponent<Text>().text, Is.EqualTo("ApplePlayer"));
            Assert.That(page.Find("RegionFlag1").GetComponent<Image>().enabled, Is.False);
            Assert.That(apple.GetComponentInChildren<Text>(true).color, Is.EqualTo(InkPalette.Red));
            global.onClick.Invoke();
            Assert.That(page.Find("NameCell1/Name1").GetComponent<Text>().text, Is.Empty);
            Assert.That(global.GetComponentInChildren<Text>(true).color, Is.EqualTo(InkPalette.Red));
        }
#endif
    }
}
