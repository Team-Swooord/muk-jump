using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;

namespace MukJump.EditorTests
{
    public sealed class ApplicationPauseBoundaryTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject host;
        GameManager game;
        float timeScale;
        bool audioPaused;

        [SetUp]
        public void SetUp()
        {
            timeScale = Time.timeScale;
            audioPaused = AudioListener.pause;
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            host = new GameObject("ApplicationPauseBoundary");
            game = host.AddComponent<GameManager>();
            Invoke("OnEnable");
            typeof(GameManager).GetProperty("State").SetValue(game, GameState.Playing);
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            PointerInput.ResetSuppressionForTests();
            Time.timeScale = timeScale;
            AudioListener.pause = audioPaused;
        }

        void Invoke(string name, params object[] args) =>
            typeof(GameManager).GetMethod(name, Private).Invoke(game, args);
        void Transition(bool active) => typeof(GameManager)
            .GetField("transitionInProgress", Private).SetValue(game, active);

        [Test]
        public void BackgroundDuringScreenTransitionStillStopsPhysics()
        {
            Transition(true);
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
        }

        [Test]
        public void ForegroundDuringTransitionResumesAfterTransitionFinishes()
        {
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            Assert.That(game.IsPaused, Is.True);
            Transition(true);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Transition(false);
            Invoke("Update");
            Assert.That(game.IsPaused, Is.False);
            Assert.That(game.IsGameplayTicking, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void StartingPlayAfterAppWasHiddenDoesNotRunBehindTheApp()
        {
            typeof(GameManager).GetProperty("State").SetValue(game, GameState.Lobby);
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            Invoke("SetState", GameState.Playing);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(game.IsGameplayTicking, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
        }

        [Test]
        public void ForegroundNeverClosesAnExistingUserPause()
        {
            Assert.That(game.PauseGame(), Is.True);
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Invoke("Update");
            Assert.That(game.PauseReason, Is.EqualTo(GameplayPauseReason.UserMenu));
            Assert.That(Time.timeScale, Is.Zero);
        }

        [Test]
        public void TutorialOpenedWhileHiddenKeepsItsPauseAfterForeground()
        {
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            Assert.That(game.PauseForFirstRunTutorial(), Is.True);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Invoke("Update");
            Assert.That(game.PauseReason, Is.EqualTo(GameplayPauseReason.FirstRunTutorial));
            Assert.That(Time.timeScale, Is.Zero);
        }

        [Test]
        public void LateResumeWhileHiddenTransfersToBackgroundUntilForeground()
        {
            game.PauseGame();
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            Assert.That(game.ResumeGame(), Is.True);
            Assert.That(game.PauseReason, Is.EqualTo(GameplayPauseReason.ApplicationBackground));
            Assert.That(Time.timeScale, Is.Zero);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            Assert.That(game.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void DisablingHiddenSessionRestoresGlobalClockForTheNextScene()
        {
            MobileApplicationLifecycle.SetPlatformVisibility(false);
            Invoke("OnDisable");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(GameManager.Instance, Is.Null);
        }

        [Test]
        public void RepeatedTransitionAndVisibilityCyclesDoNotAccumulatePauseState()
        {
            for (int i = 0; i < 30; i++)
            {
                Transition(true);
                MobileApplicationLifecycle.SetPlatformVisibility(false);
                Assert.That(Time.timeScale, Is.Zero, $"백그라운드 반복 {i}");
                MobileApplicationLifecycle.SetPlatformVisibility(true);
                Assert.That(Time.timeScale, Is.Zero, "전환 완료 전에는 재개하지 않습니다.");
                Transition(false);
                Invoke("Update");
                Assert.That(game.IsGameplayTicking, Is.True, $"복귀 반복 {i}");
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
        }
    }
}
