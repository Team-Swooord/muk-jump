using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class LobbyMenuTests
    {
        GameObject viewHost;
        GameObject managerHost;
        GameObject playerHost;
        MemoryLobbySettingsStore lobbySettingsStore;

        [SetUp]
        public void SetUp()
        {
            PermanentGrowthProfile.UseStoreForTests(
                new MemoryPermanentGrowthStore());
            lobbySettingsStore = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(lobbySettingsStore);
        }

        [TearDown]
        public void TearDown()
        {
            if (playerHost != null)
                Object.DestroyImmediate(playerHost);
            if (managerHost != null)
                Object.DestroyImmediate(managerHost);
            if (viewHost != null)
                Object.DestroyImmediate(viewHost);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void LobbySeparatesFourPermanentGrowthsFromHundredRunCodexEntries()
        {
            managerHost = new GameObject("LobbyCollectionTestManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("LobbyCollectionTestHost");
            var growthView = viewHost.AddComponent<PermanentGrowthView>();
            var codexView = viewHost.AddComponent<LobbyCollectionView>();
            growthView.BuildForTests();
            codexView.BuildForTests();

            growthView.Open();
            Assert.That(growthView.IsOpen, Is.True);
            Assert.That(growthView.CreatedRowCount, Is.EqualTo(4));
            Assert.That(growthView.BalanceLabel, Is.EqualTo("보유 먹빛 0"));

            growthView.Close();
            codexView.OpenCodex();
            Assert.That(codexView.CurrentModeName, Is.EqualTo("Codex"));
            Assert.That(codexView.FilteredCount, Is.EqualTo(100));
            Assert.That(codexView.CreatedRowCount, Is.EqualTo(4),
                "100개 도감을 열 때도 고정된 큰 카드 네 개만 재사용해야 합니다.");
            Assert.That(codexView.IsCardBackVisible(0), Is.False);
            codexView.FlipCardForTests(0);
            Assert.That(codexView.IsCardBackVisible(0), Is.True,
                "도감 카드를 누르면 큰 그림 앞면에서 설명 뒷면으로 전환돼야 합니다.");
            codexView.FlipCardForTests(0);
            Assert.That(codexView.IsCardBackVisible(0), Is.False);

            codexView.Close();
            Assert.That(codexView.IsOpen, Is.False);
        }

        [Test]
        public void OptionsTutorialUsesFourSequentialPagesAndMarksCompletion()
        {
            viewHost = new GameObject("LobbyOptionsTestHost");
            var optionsView = viewHost.AddComponent<LobbyOptionsView>();
            optionsView.BuildForTests();

            optionsView.OpenTutorialForTests();

            Assert.That(optionsView.IsOpen, Is.True);
            Assert.That(optionsView.IsTutorialOpen, Is.True);
            Assert.That(optionsView.TutorialPageCount, Is.EqualTo(4));
            Assert.That(optionsView.CurrentTutorialPage, Is.EqualTo(0));
            Assert.That(optionsView.PlayerUidLabel,
                Does.StartWith("플레이어 UID   MUK-"));

            for (int expectedPage = 1; expectedPage < 4; expectedPage++)
            {
                Invoke(optionsView, "NextTutorialPage");
                Assert.That(optionsView.CurrentTutorialPage,
                    Is.EqualTo(expectedPage));
                Assert.That(optionsView.IsTutorialOpen, Is.True);
            }

            Invoke(optionsView, "NextTutorialPage");

            Assert.That(LobbySettingsProfile.TutorialSeen, Is.True);
            Assert.That(optionsView.IsTutorialOpen, Is.False,
                "네 번째 안내의 완료 버튼은 옵션 본문으로 돌아가야 합니다.");
            Assert.That(optionsView.IsOpen, Is.True);
        }

        [Test]
        public void LobbySettingsMemoryStorePersistsAudioTutorialAndUid()
        {
            LobbySettingsProfile.SetBgmVolume(0.35f);
            LobbySettingsProfile.SetSfxVolume(0.6f);
            LobbySettingsProfile.SetBgmVolume(0f);
            LobbySettingsProfile.SetSfxVolume(0f);
            LobbySettingsProfile.MarkTutorialSeen();
            string firstUid = LobbySettingsProfile.PlayerUid;
            LobbySettingsProfile.Flush();

            Assert.That(firstUid, Does.Match("^MUK-[0-9A-F]{8}$"));
            Assert.That(lobbySettingsStore.SaveCount, Is.GreaterThanOrEqualTo(2));

            LobbySettingsProfile.UseStoreForTests(lobbySettingsStore);

            Assert.That(LobbySettingsProfile.BgmVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(LobbySettingsProfile.SfxVolume, Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                LobbySettingsProfile.BgmResumeVolume,
                Is.EqualTo(0.35f).Within(0.001f),
                "음소거를 껐다 켜면 사용자가 마지막으로 고른 배경음 크기로 돌아가야 합니다.");
            Assert.That(
                LobbySettingsProfile.SfxResumeVolume,
                Is.EqualTo(0.6f).Within(0.001f),
                "음소거를 껐다 켜면 사용자가 마지막으로 고른 효과음 크기로 돌아가야 합니다.");
            Assert.That(LobbySettingsProfile.TutorialSeen, Is.True);
            Assert.That(LobbySettingsProfile.PlayerUid, Is.EqualTo(firstUid),
                "로컬 UID는 옵션 화면을 다시 열어도 바뀌면 안 됩니다.");
        }

        [Test]
        public void ExplicitMenuStartReleasesLobbyPlayerExactlyOnce()
        {
            managerHost = new GameObject("LobbyStartManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");

            playerHost = new GameObject("LobbyStartPlayer");
            playerHost.AddComponent<SpriteRenderer>();
            var body = playerHost.AddComponent<Rigidbody2D>();
            playerHost.AddComponent<CircleCollider2D>();
            var player = playerHost.AddComponent<PlayerController>();
            Invoke(player, "Awake");
            body.bodyType = RigidbodyType2D.Kinematic;
            manager.RegisterPlayer(player);

            manager.StartGameFromMenu();

            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(manager.LivingPlayerCount, Is.EqualTo(1));

            manager.StartGameFromMenu();
            Assert.That(manager.State, Is.EqualTo(GameState.Playing),
                "시작 버튼 중복 탭은 새 세션 전환을 다시 실행하면 안 됩니다.");
            Assert.That(manager.LivingPlayerCount, Is.EqualTo(1));
        }

        [Test]
        public void PermanentGrowthCannotOpenAfterGameplayStarts()
        {
            managerHost = new GameObject("LobbyGrowthBoundaryManager");
            var manager = managerHost.AddComponent<GameManager>();
            Invoke(manager, "OnEnable");
            viewHost = new GameObject("LobbyGrowthBoundaryView");
            var growthView = viewHost.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            manager.StartGameFromMenu();
            growthView.Open();

            Assert.That(manager.State, Is.EqualTo(GameState.Playing));
            Assert.That(growthView.IsOpen, Is.False,
                "영구 성장 UI는 게임 시작 전 로비에서만 열려야 합니다.");
        }

        static object Invoke(object target, string methodName)
        {
            return target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }
    }
}
