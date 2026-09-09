using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class CodeBoundaryRegressionTests
    {
        readonly List<Object> cleanup = new();
        MemoryLobbySettingsStore settings;

        [SetUp]
        public void SetUp()
        {
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            GameManager.RestorePendingGameOverSettlementStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }

        [Test]
        public void StartButtonOpensRequiredAccountSyncInsteadOfSilentlyReturning()
        {
            var systems = Track(new GameObject("StartSyncBoundary"));
            var manager = systems.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            var options = systems.AddComponent<LobbyOptionsView>();
            var lobbyRoot = Track(new GameObject("Lobby", typeof(RectTransform), typeof(CanvasGroup)));
            var lobby = lobbyRoot.AddComponent<LobbyView>();
            var buttonRoot = new GameObject("Start", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonRoot.transform.SetParent(lobbyRoot.transform, false);
            var button = buttonRoot.GetComponent<Button>();
            var navigator = systems.AddComponent<LobbyScreenNavigator>();
            Invoke(navigator, "OnEnable");
            Set(lobby, "startButton", button);
            Set(lobby, "screenNavigator", navigator);
            Set(lobby, "optionsView", options);
            Invoke(lobby, "Start");
            var account = systems.AddComponent<MukJumpAccountRuntime>();
            Invoke(account, "OnEnable");
            Set(account, "cloudLoadInFlight", true);
            try
            {
                Assert.That(navigator.CanStartGame, Is.False);
                button.onClick.Invoke();
                Assert.That(manager.State, Is.EqualTo(GameState.Lobby));
                Assert.That(options.IsAccountOpen, Is.True,
                    "동기화가 필요한 실제 시작 탭은 복구 선택 화면을 열어야 합니다.");
            }
            finally { Invoke(account, "OnDisable"); }
        }

        [Test]
        public void RewardRevivePreservesUncollectedItemsAndSpawnSchedule()
        {
            var texture = Track(new Texture2D(8, 8));
            var sprite = Track(Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f));
            var spawner = Track(new GameObject("ReviveItems")).AddComponent<ItemSpawner>();
            Set(spawner, "placeholderSprite", sprite);
            Set(spawner, "nextSpawnHeight", 42f);
            Set(spawner, "scheduledSessionVersion", 17);
            Assert.That((bool)Invoke(spawner, "Spawn", 12f, true), Is.True);
            var items = (List<ItemPickup>)Get(spawner, "active");
            var original = items[0];
            Invoke(spawner, "OnStateChanged", GameState.Playing, GameState.GameOver);
            Assert.That(items.Count, Is.EqualTo(1), "부활 선택을 기다리는 동안에도 아이템을 보존합니다.");
            Assert.That(original.gameObject.activeSelf, Is.True);
            Invoke(spawner, "OnStateChanged", GameState.GameOver, GameState.Playing);
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0], Is.SameAs(original));
            Assert.That(original.gameObject.activeSelf, Is.True);
            Assert.That(original.GetComponent<Collider2D>().enabled, Is.True);
            Assert.That(Get(spawner, "nextSpawnHeight"), Is.EqualTo(42f));
            Assert.That(Get(spawner, "scheduledSessionVersion"), Is.EqualTo(17));
            Invoke(spawner, "OnStateChanged", GameState.GameOver, GameState.Lobby);
            Assert.That(items, Is.Empty);
            Assert.That(original.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void CollectedItemWaitsThroughGameOverAndFinishesOnlyOnceAfterRevive()
        {
            var manager = Track(new GameObject("PickupPauseManager")).AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            var pickup = Track(new GameObject("AbsorbingPickup")).AddComponent<ItemPickup>();
            pickup.Configure(ItemType.InkShield, 0f);
            var trigger = pickup.GetComponent<CircleCollider2D>();
            Set(pickup, "collected", true);
            trigger.enabled = false;
            int releases = 0;
            pickup.ReleaseRequested += _ => releases++;
            Invoke(pickup, "BeginCollection", pickup.transform);
            Invoke(pickup, "AdvanceCollection", .09f);
            float alpha = pickup.GetComponent<SpriteRenderer>().color.a;
            typeof(GameManager).GetProperty(nameof(GameManager.State)).SetValue(manager, GameState.GameOver);
            for (int i = 0; i < 5; i++) Invoke(pickup, "Update");
            Assert.That(Get(pickup, "collectionTime"), Is.EqualTo(.09f));
            Assert.That(pickup.GetComponent<SpriteRenderer>().color.a, Is.EqualTo(alpha));
            Assert.That(trigger.enabled, Is.False, "부활 대기 중 이미 획득한 아이템이 다시 충돌하면 안 됩니다.");
            Assert.That(releases, Is.Zero);
            typeof(GameManager).GetProperty(nameof(GameManager.State)).SetValue(manager, GameState.Playing);
            Assert.That(manager.IsGameplayTicking, Is.True);
            Invoke(pickup, "AdvanceCollection", .1f);
            Invoke(pickup, "AdvanceCollection", .1f);
            Assert.That(releases, Is.EqualTo(1));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidVolumesCannotPoisonSettingsOrCloudRollback(float invalid)
        {
            LobbySettingsProfile.SetBgmVolume(.4f);
            LobbySettingsProfile.SetSfxVolume(.6f);
            LobbySettingsProfile.SetBgmVolume(invalid);
            LobbySettingsProfile.SetSfxVolume(invalid);
            Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(.4f));
            Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(.6f));
            LobbySettingsProfile.ApplyCloudSettings(invalid, invalid, 0);
            Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(.4f));
            Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(.6f));
            Assert.That(LobbySettingsProfile.TryRestoreCloudSettings(
                new LobbyCloudSettingsSnapshot(invalid, invalid, invalid, invalid, 0, false)), Is.True);
            AssertFinite(LobbySettingsProfile.BgmVolume);
            AssertFinite(LobbySettingsProfile.SfxVolume);
            AssertFinite(LobbySettingsProfile.BgmResumeVolume);
            AssertFinite(LobbySettingsProfile.SfxResumeVolume);
            AssertFinite(settings.GetFloat("MukJump.Settings.BgmVolume", -1));
        }

        [Test]
        public void CorruptLocalVolumesLoadAsFiniteValues()
        {
            foreach (string key in new[] { "BgmVolume", "SfxVolume", "BgmResumeVolume", "SfxResumeVolume" })
                settings.SetFloat("MukJump.Settings." + key, float.NaN);
            AssertFinite(LobbySettingsProfile.BgmVolume);
            AssertFinite(LobbySettingsProfile.SfxVolume);
            AssertFinite(LobbySettingsProfile.BgmResumeVolume);
            AssertFinite(LobbySettingsProfile.SfxResumeVolume);
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        public void NonFiniteServerVolumeUsesFallback(string invalid)
        {
            var row = new LitJson.JsonData();
            row["volume"] = invalid;
            var reader = typeof(MukJumpAccountRuntime).GetMethod("ReadFloat", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reader.Invoke(null, new object[] { row, "volume", .3f }), Is.EqualTo(.3f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TossLeaderboardEntryOpensOfficialGameCenter(bool fromLobby)
        {
            var host = Track(new GameObject("TossRankBoundary"));
            var manager = host.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            var options = host.AddComponent<LobbyOptionsView>();
            options.BuildForPlatformForTests(RuntimePlatform.WebGLPlayer);
            if (!fromLobby)
            {
                options.Open();
                Assert.That(host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/OptionsPage/AccountButton"),
                    Is.Null, "순위 진입은 메인 최고 기록 버튼으로 통합했다.");
                options.Close();
            }
            LogAssert.Expect(LogType.Log, "[MukJump] 토스 게임센터는 Apps in Toss에서 열립니다.");
            options.OpenLeaderboard();
            Assert.That(host.transform.Find("LobbyOptionsCanvas/SafeAreaRoot/OptionsScroll/LeaderboardPage")
                .GetComponent<CanvasGroup>().blocksRaycasts, Is.False,
                "지원하지 않는 행 조회 대신 공식 순위를 열어야 합니다.");
        }

        [Test]
        public void SceneTeardownDoesNotReopenInactiveGameOverPopup()
        {
            var host = Track(new GameObject("InactiveTransitionOwner"));
            host.SetActive(false);
            var manager = host.AddComponent<GameManager>();
            var popup = host.AddComponent<GameOverPopupView>();
            typeof(GameManager).GetField("gameOverPopupView", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, popup);
            typeof(GameManager).GetField("transitionInProgress", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, true);
            typeof(GameManager).GetProperty(nameof(GameManager.State)).SetValue(manager, GameState.GameOver);
            Invoke(manager, "HandleTransitionFailure");
            Assert.That(manager.IsTransitioning, Is.False);
            Assert.That(host.transform.childCount, Is.Zero, "비활성 결과 UI와 코루틴을 만들지 않는다.");
        }

        [Test]
        public void NativeAdDisableClearsInitializedStateAlongsideProvider()
        {
            string source = File.ReadAllText("Assets/Scripts/Core/GoogleMobileAdsRuntime.cs");
            int start = source.IndexOf("void OnDisable()");
            int end = source.IndexOf("void ProcessRequestWatchdogs()", start);
            string disable = source.Substring(start, end - start);
            Assert.That(disable, Does.Contain("initialized = false;"),
                "폐기한 광고 공급자는 재활성화 시 초기화부터 다시 구성해야 합니다.");
        }

        static void AssertFinite(float value)
        {
            Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
            Assert.That(value, Is.InRange(0f, 1f));
        }
        T Track<T>(T value) where T : Object { cleanup.Add(value); return value; }
        static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
