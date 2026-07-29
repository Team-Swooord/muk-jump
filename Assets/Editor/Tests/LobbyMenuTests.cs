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

        [TearDown]
        public void TearDown()
        {
            GrowthFocusProfile.ResetForTests();
            if (playerHost != null)
                Object.DestroyImmediate(playerHost);
            if (managerHost != null)
                Object.DestroyImmediate(managerHost);
            if (viewHost != null)
                Object.DestroyImmediate(viewHost);
        }

        [Test]
        public void CollectionUsesSixPooledRowsForEightActiveAndHundredPlannedEntries()
        {
            viewHost = new GameObject("LobbyCollectionTestHost");
            var view = viewHost.AddComponent<LobbyCollectionView>();
            view.BuildForTests();

            view.OpenGrowth();
            Assert.That(view.IsOpen, Is.True);
            Assert.That(view.CurrentModeName, Is.EqualTo("Growth"));
            Assert.That(view.FilteredCount, Is.EqualTo(8));
            Assert.That(view.CreatedRowCount, Is.EqualTo(6),
                "100개 도감을 열 때도 고정된 행 여섯 개만 재사용해야 합니다.");

            view.OpenCodex();
            Assert.That(view.CurrentModeName, Is.EqualTo("Codex"));
            Assert.That(view.FilteredCount, Is.EqualTo(100));
            Assert.That(view.CreatedRowCount, Is.EqualTo(6));

            view.Close();
            Assert.That(view.IsOpen, Is.False);
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

        static object Invoke(object target, string methodName)
        {
            return target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }
    }
}
