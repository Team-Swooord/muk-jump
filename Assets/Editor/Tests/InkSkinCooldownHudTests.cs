using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using MukJump.Player;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    public sealed class InkSkinCooldownHudTests
    {
        [TestCase(InkSkinId.WindFan, 20, 12.1f)]
        [TestCase(InkSkinId.YakshaMask, 30, 23.2f)]
        [TestCase(InkSkinId.SpiderMask, 30, 29.3f)]
        [TestCase(InkSkinId.Gat, 0, 0)]
        [TestCase(InkSkinId.SpiritHalo, 0, 0)]
        [TestCase(InkSkinId.TurtleShell, 0, 0)]
        [TestCase(InkSkinId.PhoenixCrown, 0, 0)]
        [TestCase(InkSkinId.BondKnot, 0, 0)]
        [TestCase(InkSkinId.CloudBoots, 0, 0)]
        [TestCase(InkSkinId.AngelWings, 0, 0)]
        public void ReadsOnlyEquippedAbilityClock(InkSkinId skin, float duration, float remaining)
        {
            var go = new GameObject("CooldownModelTest");
            try
            {
                var growth = go.AddComponent<RunGrowthController>();
                Set(growth, "skinSnapshot", skin);
                Set(growth, "skinSkillCooldown", 12.1f);
                Set(growth, "maskStrikeCooldown", 23.2f);
                Set(growth, "webClimbCooldown", 29.3f);
                Assert.That(growth.EquippedAbilityCooldownDuration, Is.EqualTo(duration));
                Assert.That(growth.EquippedAbilityCooldownRemaining, Is.EqualTo(remaining));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SlotIsSmallRadialAndNeverConsumesDrawingInput()
        {
            var go = new GameObject("HudTest", typeof(RectTransform));
            try
            {
                InkSkinCooldownHud.Ensure((RectTransform)go.transform);
                InkSkinCooldownHud.Ensure((RectTransform)go.transform);
                Assert.That(go.GetComponentsInChildren<InkSkinCooldownHud>().Length, Is.EqualTo(1));
                var view = go.GetComponentInChildren<InkSkinCooldownHud>();
                Assert.That(view.Visible, Is.False);
                Assert.That(view.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(((RectTransform)view.transform).sizeDelta, Is.EqualTo(new Vector2(84, 84)));
                Assert.That(view.SecondsLabel.rectTransform.anchoredPosition.y, Is.GreaterThan(42));
                Assert.That(view.CooldownOverlay.fillMethod, Is.EqualTo(UnityEngine.UI.Image.FillMethod.Radial360));
                foreach (var graphic in view.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RealSkillsCountDownUnderTimerAndFreezeWithGameplay()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground;
            var language = GameLocalization.Language;
            Application.runInBackground = true;
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                Assert.That(InkSkinPreferences.TryEquip(InkSkinId.WindFan), Is.True);
                var game = GameManager.Instance;
                float deadline = Time.realtimeSinceStartup + 15;
                while ((game.IsTransitioning || StartupBrandSplash.IsBlockingInput || !LobbyScreenNavigator.Instance.CanStartGame) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                game.StartGameFromMenu();
                deadline = Time.realtimeSinceStartup + 15;
                while (!game.IsGameplayTicking && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(game.IsGameplayTicking, Is.True);
                game.ToggleDebugInvincible();
                yield return Wait(.1f);
                var growth = RunGrowthController.Instance;
                var view = Object.FindAnyObjectByType<InkSkinCooldownHud>();
                Assert.That(view.Visible, Is.True);
                Assert.That(view.CostumeIcon.sprite, Is.EqualTo(InkSkinCatalog.Sprite(InkSkinId.WindFan)));
                Assert.That(view.SecondsLabel.text, Is.Empty);
                Assert.That(InkSkinSkillView.Instance.SkillButton.interactable, Is.True);
                InkSkinSkillView.Instance.SkillButton.onClick.Invoke();
                yield return Wait(.1f);
                Assert.That(growth.SkinSkillCooldownRemaining, Is.InRange(19f, 20f));
                AssertClock(view, growth);
                float before = growth.EquippedAbilityCooldownRemaining;
                yield return Wait(.25f);
                Assert.That(growth.EquippedAbilityCooldownRemaining, Is.LessThan(before));
                Assert.That(game.PauseGame(), Is.True); yield return Wait(.1f);
                Assert.That(view.Visible, Is.False);
                before = growth.EquippedAbilityCooldownRemaining;
                yield return Wait(.25f);
                Assert.That(growth.EquippedAbilityCooldownRemaining, Is.EqualTo(before));
                Assert.That(game.ResumeGame(), Is.True); yield return Wait(.1f);
                MobileApplicationLifecycle.SetPlatformVisibility(false); yield return Wait(.1f);
                before = growth.EquippedAbilityCooldownRemaining;
                yield return Wait(.25f);
                Assert.That(view.Visible, Is.False);
                Assert.That(growth.EquippedAbilityCooldownRemaining, Is.EqualTo(before));
                MobileApplicationLifecycle.SetPlatformVisibility(true); yield return Wait(.1f);
                var debug = GameplayHudView.Instance.transform.Find("ItemTestControls");
                if (debug != null) debug.gameObject.SetActive(false);
                GameLocalization.SetLanguage(GameLanguage.Korean);
                yield return Capture("wind-fan");

                Set(growth, "skinSnapshot", InkSkinId.YakshaMask);
                var players = new List<PlayerController>(); game.GetLivingPlayersNonAlloc(players);
                var original = players.Find(p => !p.IsRuntimeClone);
                Assert.That(growth.TryUseMaskStrike(original, original.transform.position + Vector3.right), Is.True);
                yield return Wait(.1f);
                AssertClock(view, growth);
                Assert.That(view.SecondsLabel.text, Is.EqualTo("30"));
                yield return Capture("mask-strike");

                Set(growth, "skinSnapshot", InkSkinId.SpiderMask);
                Set(growth, "webClimbCooldown", 15.5f);
                yield return Wait(.1f);
                Assert.That(view.SecondsLabel.text, Is.EqualTo("16"));
                AssertClock(view, growth);
                yield return Capture("spider-half-cooldown");
                foreach (var lang in new[] { GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(lang); yield return Wait(.1f);
                    yield return Capture("spider-" + lang);
                }

                Canvas.ForceUpdateCanvases();
                var timer = GameplayHudView.Instance.transform.Find("TopHudRoot/ElapsedTime").GetComponent<UnityEngine.UI.Text>();
                var a = new Vector3[4]; var b = new Vector3[4];
                timer.rectTransform.GetWorldCorners(a); view.SecondsLabel.rectTransform.GetWorldCorners(b);
                Assert.That(b[1].y, Is.LessThan(a[0].y), "시간 아래에 겹치지 않는 숫자");
                ((RectTransform)view.transform).GetWorldCorners(a);
                // EditMode 코루틴의 Screen은 Test Runner 창 크기일 수 있어 실제 렌더 Canvas를 기준으로 검사한다.
                var canvasRect = (RectTransform)view.GetComponentInParent<Canvas>().transform;
                foreach (var corner in a)
                    Assert.That(canvasRect.rect.Contains(canvasRect.InverseTransformPoint(corner)), Is.True,
                        "실제 Game View의 HUD 영역 안에 배치한다");

                foreach (var skin in new[] { InkSkinId.Gat, InkSkinId.SpiritHalo, InkSkinId.TurtleShell,
                    InkSkinId.PhoenixCrown, InkSkinId.BondKnot, InkSkinId.CloudBoots, InkSkinId.AngelWings })
                {
                    Set(growth, "skinSnapshot", skin); yield return Wait(.1f);
                    Assert.That(view.Visible, Is.True, skin.ToString());
                    Assert.That(view.CostumeIcon.sprite, Is.EqualTo(InkSkinCatalog.Sprite(skin)));
                    Assert.That(view.SecondsLabel.text, Is.Empty);
                    Assert.That(view.CooldownOverlay.fillAmount, Is.Zero);
                }
                Set(growth, "skinSnapshot", InkSkinId.WindFan); Set(growth, "skinSkillCooldown", 0f); yield return Wait(.1f);
                Assert.That(view.SecondsLabel.text, Is.Empty); Assert.That(view.CooldownOverlay.fillAmount, Is.Zero);
                Set(growth, "skinSnapshot", (InkSkinId)0); yield return Wait(.1f);
                Assert.That(view.Visible, Is.False, "특수능력이 없는 기본 먹은 슬롯을 숨긴다");
                Assert.That(game.PauseGame(), Is.True);
                Assert.That(game.ReturnToLobby(), Is.True);
                deadline = Time.realtimeSinceStartup + 15;
                while ((game == GameManager.Instance || GameManager.Instance == null || GameManager.Instance.IsTransitioning) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));
                yield return Wait(.1f);
                Assert.That(Object.FindAnyObjectByType<InkSkinCooldownHud>().Visible, Is.False);
            }
            finally
            {
                SkinDebugSession.Reset(); MobileApplicationLifecycle.SetPlatformVisibility(true);
                GameLocalization.SetLanguage(language); Application.runInBackground = background; Time.timeScale = 1;
                InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests(); PointerInput.ResetSuppressionForTests();
            }
            yield return new ExitPlayMode();
        }
        static void AssertClock(InkSkinCooldownHud view, RunGrowthController growth)
        {
            Assert.That(view.CostumeIcon.sprite, Is.EqualTo(InkSkinCatalog.Sprite(growth.SkinSnapshot)));
            Assert.That(view.SecondsLabel.text, Is.EqualTo(Mathf.CeilToInt(growth.EquippedAbilityCooldownRemaining).ToString()));
            Assert.That(view.CooldownOverlay.fillAmount,
                Is.EqualTo(growth.EquippedAbilityCooldownRemaining / growth.EquippedAbilityCooldownDuration).Within(.02f));
        }
        static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/skin-cooldown-hud-20260914");
            File.Copy(path, "output/qa/skin-cooldown-hud-20260914/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
