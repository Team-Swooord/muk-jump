using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkWardrobeSpacingPlayTests
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        [UnityTest, Timeout(180000)]
        public IEnumerator CompactAndTallPhonesKeepFrameGapReadableProgressAndButtons()
        {
            yield return new EnterPlayMode();
            var settings = new MemoryLobbySettingsStore();
            LobbySettingsProfile.UseStoreForTests(settings); InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            InkPremiumAppearance.UseForTests(false, null);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            bool background = Application.runInBackground; Application.runInBackground = true;
            GameLanguage language = GameLocalization.Language;
            var gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            gameView.Focus();
            var selectedSize = gameView.GetType().GetProperty("selectedSizeIndex", Members);
            int oldIndex = (int)selectedSize.GetValue(gameView);
            var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = sizesType.BaseType.GetProperty("instance").GetValue(null);
            var group = sizesType.GetProperty("currentGroup", Members).GetValue(sizes);
            var temporarySizes = new System.Collections.Generic.List<object>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(MukJumpAccountRuntime.Instance.IsOnlineAuthenticated, Is.False);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                var view = Object.FindAnyObjectByType<InkWardrobeView>();
                yield return Wait(1);
                Assert.That(view.CanBrowse, Is.True);
                foreach (var resolution in new[] { new Vector2Int(320,568), new Vector2Int(390,844) })
                {
                    var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var size = Activator.CreateInstance(typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize"),
                        Enum.Parse(sizeType, "FixedResolution"), resolution.x, resolution.y, "MukJump temporary QA");
                    group.GetType().GetMethod("AddCustomSize", Members).Invoke(group, new[] { size });
                    temporarySizes.Add(size);
                    selectedSize.SetValue(gameView, (int)group.GetType().GetMethod("GetTotalCount", Members).Invoke(group,null)-1);
                    gameView.GetType().GetMethod("SetPlayModeViewSize", Members | BindingFlags.Static).Invoke(gameView, new object[] { (Vector2)resolution });
                    gameView.GetType().GetProperty("targetSize", Members).SetValue(gameView, (Vector2)resolution);
                    gameView.Repaint(); yield return Wait(.6f);
                    // Editor 콜백의 Screen 값은 마지막 Scene 뷰 렌더 크기일 수 있다.
                    // 실제 합성 PNG의 크기를 아래에서 확인한다.
                    foreach (var locale in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                    {
                        GameLocalization.SetLanguage(locale);
                        for (int state = 0; state < 7; state++)
                        {
                            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(state == 0 ? 0 : state == 1 ? 436 : 800), Is.True);
                            if (state == 5)
                            {
                                var stats = SkinRunStats.Begin(true, InkSkinId.Ink);
                                stats.inkDrops = stats.items = InkSkinAchievements.Goal(InkSkinId.WindFan);
                                PermanentGrowthProfile.SettleRun($"wardrobe-spacing-{resolution.x}-{locale}", 0, 0, 0, 1, true, stats);
                            }
                            view.Select(state < 3 ? InkSkinId.FoxMask : state == 3 || state == 5 ? InkSkinId.WindFan :
                                state == 6 ? InkSkinId.PhoenixCrown : InkSkinId.Astronaut);
                            view.Presentation.Cancel(); yield return Wait(.35f); Canvas.ForceUpdateCanvases();
                            var ring = (RectTransform)view.Paper.Find("ShowroomStage/PreviewBackdrop/FocusBrushRing");
                            var details = (RectTransform)view.Paper.Find("ItemDetailsPaper");
                            Assert.That(Bounds(view.Paper, ring).yMin - Bounds(view.Paper, details).yMax, Is.GreaterThanOrEqualTo(24f), "원과 설명 카드 사이 여백");
                            var progress = view.Paper.Find("AchievementProgress").GetComponent<Text>();
                            var track = (RectTransform)view.Paper.Find("AchievementTrack");
                            if (state < 3)
                            {
                                Assert.That(progress.text, Is.EqualTo((state == 0 ? "0" : state == 1 ? "436" : "800") + " / 800"));
                                Assert.That(Bounds(view.Paper, progress.rectTransform).yMin, Is.GreaterThan(Bounds(view.Paper, track).yMax));
                            }
                            if (state == 4) Assert.That(track.gameObject.activeSelf, Is.False);
                            if (state == 5)
                            {
                                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.WindFan), Is.True);
                                Assert.That(view.EquipButton.gameObject.activeSelf, Is.True);
                            }
                            foreach (var label in view.Paper.GetComponentsInChildren<Text>().Where(t => !string.IsNullOrEmpty(t.text)))
                                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name + "/" + locale);
                            Assert.That(((RectTransform)view.Paper.Find("CollectionScroll")).anchoredPosition, Is.EqualTo(new Vector2(0,-587)));
                            var corners = new Vector3[4]; ((RectTransform)view.EquipButton.transform).GetWorldCorners(corners);
                            Assert.That(corners.Min(v => v.y), Is.GreaterThanOrEqualTo(0));
                            Assert.That(corners.Max(v => v.y), Is.LessThanOrEqualTo(Screen.height));
                            yield return Capture($"{resolution.x}x{resolution.y}-{locale}-{state}", resolution);
                        }
                    }
                }
            }
            finally
            {
                selectedSize.SetValue(gameView, oldIndex);
                var custom = (IList)group.GetType().GetField("m_Custom", Members).GetValue(group);
                foreach (var temporary in temporarySizes) custom.Remove(temporary);
                sizesType.GetMethod("Changed", Members).Invoke(sizes,null);
                gameView.Repaint();
                GameLocalization.SetLanguage(language); Application.runInBackground = background;
                InkPremiumAppearance.RestoreForTests();
                LobbySettingsProfile.RestoreDefaultStoreForTests(); InkSkinPreferences.RestoreDefaultStoreForTests();
                PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
                GameManager.RestorePendingGameOverSettlementStoreForTests();
            }
            yield return new ExitPlayMode();
        }
        static Rect Bounds(RectTransform root, RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(root.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(v=>v.x), points.Min(v=>v.y), points.Max(v=>v.x), points.Max(v=>v.y));
        }
        static IEnumerator Wait(float seconds)
        { double end = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < end) yield return null; }
        static IEnumerator Capture(string name, Vector2Int resolution)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(File.Exists(path), Is.True);
            var png = File.ReadAllBytes(path);
            int WidthAt(int offset) => png[offset] << 24 | png[offset+1] << 16 | png[offset+2] << 8 | png[offset+3];
            Assert.That(WidthAt(16), Is.EqualTo(resolution.x));
            Assert.That(WidthAt(20), Is.EqualTo(resolution.y));
            const string folder = "output/qa/toss-recovery-20260925/wardrobe";
            Directory.CreateDirectory(folder); File.Copy(path, folder + "/" + name + ".png", true); File.Delete(path);
        }
    }
}
