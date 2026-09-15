using System.Collections;
using System.IO;
using System.Linq;
using MukJump.Core;
using MukJump.EditorTools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MukJump.EditorTests
{
    [Category("ThreeCushion")]
    public sealed class LobbyThreeCushionTests
    {
        MemoryPermanentGrowthStore store;
        [SetUp] public void Setup()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
            var settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            SkinDebugSession.Reset();
        }
        [TearDown] public void Teardown()
        {
            SkinDebugSession.Reset();
            InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
        }

        [Test] public void WallContactsCountImpactsNotFramesAndSimultaneousCornerCountsOnce()
        {
            Vector2 p = new(99, 0), v = new(400, 0), limit = new(100, 1000);
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, .01f), Is.EqualTo(1));
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, 0), Is.Zero);
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, .01f), Is.Zero, "같은 벽의 다음 프레임은 중복이 아니다.");
            limit = Vector2.one * 100; p = limit; v = Vector2.one * 400;
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, .001f), Is.EqualTo(1));
            p = Vector2.zero; v = new Vector2(LobbySkinPreview.MaxThrowSpeed, 0);
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, 3f), Is.EqualTo(3), "감쇠가 있어도 빠른 직접 투척으로 세 번을 달성할 수 있다.");
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, Vector2.zero, 1f), Is.Zero);
        }

        [Test] public void OnlyOneUninterruptedThrowCanCompleteAndAccountsCannotShareBounces()
        {
            var attempt = new LobbySkinPreview.CushionAttempt(); var a = new object(); var b = new object();
            Assert.That(attempt.Record(a, 3), Is.Null, "자동 부유로는 도전이 시작되지 않는다.");
            attempt.Begin(a);
            Assert.That(attempt.Record(a, 1), Is.Null);
            Assert.That(attempt.Record(a, 1), Is.Null);
            Assert.That(attempt.Record(a, 1), Is.SameAs(a));
            Assert.That(attempt.Record(a, 3), Is.Null, "같은 던지기는 한 번만 달성한다.");
            attempt.Begin(a); attempt.Record(a, 2); attempt.Cancel();
            Assert.That(attempt.Record(a, 1), Is.Null);
            attempt.Begin(a); attempt.Record(a, 2);
            Assert.That(attempt.Record(b, 1), Is.Null);
            Assert.That(attempt.Record(a, 1), Is.Null);
            attempt.Begin(a); attempt.Record(a, 2); attempt.Begin(a);
            Assert.That(attempt.Record(a, 1), Is.Null, "다시 잡아 던지면 처음부터 센다.");
        }

        [Test] public void RewardSavesOnceSurvivesReloadAndKeepsExistingHighBitsAndCloudPayload()
        {
            object owner = PermanentGrowthProfile.SkinAccountToken;
            Assert.That(InkSkinCatalog.IsConcealed(InkSkinId.ThreeCushion), Is.True);
            Assert.That(PermanentGrowthProfile.TryGrantLiveAchievements(owner, 1UL << (int)InkSkinId.NineTails, 0), Is.True);
            Assert.That(SkinSecrets.CompletedThreeCushion(owner), Is.EqualTo(1UL << 61));
            Assert.That(SkinSecrets.CompletedThreeCushion(owner), Is.Zero);
            Assert.That(PermanentGrowthProfile.AchievementSnapshot().IsValid, Is.True);
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string cloud), Is.True);
            PermanentGrowthProfile.UseStoreForTests(store);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NineTails), Is.True);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(cloud), Is.True);
            Assert.That(InkSkinCatalog.IsConcealed(InkSkinId.ThreeCushion), Is.False);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.ThreeCushion), Is.True);
            Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.ThreeCushion));
            Assert.That(PermanentGrowthProfile.AchievementSnapshot().runs, Is.Zero);
            Assert.That(PermanentGrowthProfile.AchievementSnapshot().best, Is.Zero);
            var progress = InkAchievementCatalog.SavedProgress(PermanentGrowthProfile.AchievementSnapshot(), 0);
            Assert.That(progress.Single(p => p.id == "mukjump.achievement.skin.61").percent, Is.EqualTo(100));
        }

        [Test] public void FailedSaveDoesNotGrantOrAnnounceAndCanBeRetriedAfterRecovery()
        {
            object owner = PermanentGrowthProfile.SkinAccountToken;
            Assert.That(PermanentGrowthProfile.TryGrantLiveAchievements(owner, 1UL << (int)InkSkinId.NineTails, 0), Is.True);
            store.ThrowOnPrimarySave = true;
            Assert.That(SkinSecrets.CompletedThreeCushion(owner), Is.Zero);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion), Is.False);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.True);
            store.ThrowOnPrimarySave = false;
            Assert.That(SkinSecrets.CompletedThreeCushion(PermanentGrowthProfile.SkinAccountToken), Is.Zero,
                "저장 복구를 건너뛰고 다시 지급하면 안 된다.");
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(PermanentGrowthProfile.RequiresRecovery, Is.False);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.NineTails), Is.True);
            Assert.That(SkinSecrets.CompletedThreeCushion(PermanentGrowthProfile.SkinAccountToken), Is.EqualTo(1UL << 61));
        }

        [Test] public void DebugAndOldAccountsCannotGrantRealReward()
        {
            object previous = PermanentGrowthProfile.SkinAccountToken;
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            Assert.That(SkinSecrets.CompletedThreeCushion(previous), Is.Zero);
            object owner = PermanentGrowthProfile.SkinAccountToken;
            Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
            Assert.That(SkinSecrets.CompletedThreeCushion(owner), Is.Zero);
            SkinDebugSession.Reset();
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion), Is.False);
            Assert.That(InkAchievementCatalog.SavedProgress(PermanentGrowthProfile.AchievementSnapshot(), 0), Is.Empty);
        }

        [Test, Category("ThreeCushionCopy")] public void BilliardsAssetAndCatalogHaveMatchingTranslationsHiddenFlagAndStableIds()
        {
            var id = InkSkinId.ThreeCushion;
            Assert.That((int)id, Is.EqualTo(61));
            Assert.That((int)InkSkinId.NineTails, Is.EqualTo(60));
            Assert.That(InkSkinCatalog.Get(id).Tier, Is.EqualTo(SkinTier.UltraHidden));
            Assert.That(InkSkinCatalog.HasAbility(id), Is.False);
            Assert.That(InkSkinCatalog.Sprite(id), Is.Not.Null);
            string asset = "Assets/Resources/" + InkSkinCatalog.ResourcePath(id) + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.maxTextureSize, Is.EqualTo(512));
            Assert.That(importer.isReadable || importer.mipmapEnabled, Is.False);
            var image = new Texture2D(2, 2);
            try
            {
                Assert.That(image.LoadImage(File.ReadAllBytes(asset)), Is.True);
                var pixels = image.GetPixels32();
                Assert.That(pixels.Count(p => p.a == 0), Is.GreaterThan(pixels.Length / 2), "실제 투명 원화여야 한다.");
                Assert.That(pixels.Count(p => p.a > 220), Is.GreaterThan(pixels.Length / 10));
            }
            finally { Object.DestroyImmediate(image); }
            foreach (string text in new[] { InkSkinCatalog.Name(id), InkSkinLore.Get(id).ko, InkSkinAchievements.Requirement(id) })
                foreach (bool japanese in new[] { false, true })
                {
                    Assert.That(InkSkinTranslations.TryTranslate(text, japanese, out var translated), Is.True);
                    Assert.That(translated, Is.Not.Empty.And.Not.EqualTo(text));
                }
            var catalog = MukJumpAchievementExport.BuildCatalog();
            var entry = ((JArray)catalog["achievements"]).Single(e => (string)e["id"] == "mukjump.achievement.skin.61");
            Assert.That((bool)entry["hidden"], Is.True);
            Assert.That((int)catalog["totalPoints"], Is.EqualTo(980));
            Assert.That(((JArray)entry["localizations"]).Count, Is.EqualTo(3));
            Assert.That(InkSkinAchievements.Goal(id), Is.EqualTo(1));
            var expected = new[] {
                (locale: "ko", name: "팔번공", lore: "큐대는 못 잡아도, 각은 제법 잘 본다.", requirement: "로비에서 한 번 던진 먹을 벽에 3회 연속 튕기기"),
                (locale: "en-US", name: "Eight Ball", lore: "Can't hold a cue. Can still work the angles.", requirement: "In the lobby, get 3 wall bounces from one toss."),
                (locale: "ja", name: "8番ボール", lore: "キューは持てないが、角度を読むのは得意。", requirement: "ロビーで墨を1回投げ、壁で連続3回跳ね返す")
            };
            foreach (var copy in expected)
            {
                var localized = ((JArray)entry["localizations"]).Single(e => (string)e["locale"] == copy.locale);
                Assert.That((string)localized["name"], Is.EqualTo(copy.name));
                Assert.That((string)localized["beforeEarnedDescription"], Is.EqualTo(copy.requirement));
                Assert.That((string)localized["afterEarnedDescription"], Is.EqualTo(copy.requirement));
                var lore = InkSkinLore.Get(id);
                Assert.That(copy.locale == "ko" ? lore.ko : copy.locale == "ja" ? lore.ja : lore.en, Is.EqualTo(copy.lore));
            }
        }

        [UnityTest, Timeout(120000), Category("ThreeCushionCopy")]
        public IEnumerator RevisedBilliardsCopyRendersInThreeLanguagesWithoutRevealingTheSecret()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            Setup(); LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                float deadline = Time.realtimeSinceStartup + 15;
                while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False);
                var wardrobe = Object.FindAnyObjectByType<InkWardrobeView>();
                foreach (var language in new[] { GameLanguage.Korean, GameLanguage.English, GameLanguage.Japanese })
                {
                    GameLocalization.SetLanguage(language); wardrobe.Select(InkSkinId.ThreeCushion);
                    yield return Wait(.5f); Canvas.ForceUpdateCanvases();
                    string[] labels = { "SkinName", "ItemLore", "Achievement" };
                    string[] sources = { InkSkinCatalog.Name(InkSkinId.ThreeCushion), InkSkinLore.Get(InkSkinId.ThreeCushion).ko,
                        InkSkinAchievements.Requirement(InkSkinId.ThreeCushion) };
                    for (int i = 0; i < labels.Length; i++)
                    {
                        string expected = sources[i];
                        if (language != GameLanguage.Korean)
                            Assert.That(InkSkinTranslations.TryTranslate(expected, language == GameLanguage.Japanese, out expected), Is.True);
                        var label = wardrobe.Paper.Find(labels[i]).GetComponent<UnityEngine.UI.Text>();
                        Assert.That(label.text, Is.EqualTo(expected));
                        Assert.That(label.canvasRenderer.cull, Is.False);
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), labels[i]);
                        foreach (char glyph in label.text.Where(c => !char.IsWhiteSpace(c)))
                            Assert.That(label.font.HasCharacter(glyph), Is.True, "누락 글자: " + glyph);
                    }
                    Assert.That(wardrobe.SkillDetailsRoot.gameObject.activeSelf, Is.False);
                    Assert.That(wardrobe.Paper.Find("SpecialSkillButton"), Is.Null);
                    yield return Capture("billiards-copy-" + language);
                }
                SkinDebugSession.Reset(); wardrobe.Select(InkSkinId.ThreeCushion);
                yield return Wait(.3f);
                Assert.That(wardrobe.Paper.Find("SkinName").GetComponent<UnityEngine.UI.Text>().text, Is.Empty);
                Assert.That(wardrobe.Paper.Find("ItemLore").GetComponent<UnityEngine.UI.Text>().text, Is.Empty);
                Assert.That(wardrobe.Paper.Find("Achievement").GetComponent<UnityEngine.UI.Text>().text, Is.Empty);
                Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion), Is.False);
            }
            finally
            {
                MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = background;
                GameManager.RestorePendingGameOverSettlementStoreForTests(); Teardown();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(120000)] public IEnumerator RealLobbyThrowAwardsOnThirdBounceAndShowsOnlyOneCostumeNotice()
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new EnterPlayMode();
            bool background = Application.runInBackground; Application.runInBackground = true;
            Setup(); LobbySettingsProfile.TryMarkGameplayTutorialCompleted();
            GameManager.UsePendingGameOverSettlementStoreForTests(new MemoryPendingGameOverSettlementStore());
            MobileApplicationLifecycle.SetPlatformVisibility(true);
            try
            {
                yield return SceneManager.LoadSceneAsync("Main"); yield return Wait(1);
                var preview = Object.FindAnyObjectByType<LobbySkinPreview>();
                var body = preview.transform.Find("Drift/Artwork/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                var originalBody = body.sprite;
                var toast = GameManager.Instance.GetComponent<InkAchievementToast>();
                var e = new PointerEventData(EventSystem.current) { pointerId = 81 };
                Vector2 origin = new(-preview.Travel.x * .85f, 0);
                preview.Drift.anchoredPosition = origin;
                e.position = Screen(preview, origin); preview.OnPointerDown(e);
                Assert.That(preview.IsDragging, Is.True);
                for (int i = 1; i <= 3; i++)
                {
                    yield return null;
                    e.position = Screen(preview, origin + new Vector2(i * 120, 0)); preview.OnDrag(e);
                }
                preview.OnPointerUp(e); preview.OnEndDrag(e);
                Assert.That(preview.Velocity.x, Is.GreaterThan(0));
                Vector2 previousVelocity = preview.Velocity;
                float deadline = Time.realtimeSinceStartup + 25;
                int bounces = 0;
                while (!InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion) && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    bool side = Mathf.Abs(previousVelocity.x) >= 120f && previousVelocity.x * preview.Velocity.x < 0;
                    bool floor = previousVelocity.y <= -120f && preview.Velocity.y > 0;
                    if (side || floor) bounces++;
                    previousVelocity = preview.Velocity;
                    Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion), Is.EqualTo(bounces >= 3));
                }
                Assert.That(bounces, Is.EqualTo(3));
                Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink), "해금이 자동 착용을 강제하지 않는다.");
                yield return Wait(.35f);
                var notice = toast.transform.Find("AchievementToastCanvas/SafeArea/InkBanner/CostumeIcon").GetComponent<UnityEngine.UI.Image>();
                Assert.That(notice.sprite, Is.SameAs(InkSkinCatalog.Sprite(InkSkinId.ThreeCushion)));
                Assert.That(toast.SoundPlayCountForTests, Is.EqualTo(1));
                yield return Capture("three-cushion-notice");
                yield return Wait(.5f);
                Assert.That(toast.SoundPlayCountForTests, Is.EqualTo(1));
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Lobby));

                // 로비 해금은 꾸미기 진입 시 전용 공개 씬을 먼저 거친 뒤 새 Main으로 복귀한다.
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                deadline = Time.realtimeSinceStartup + 15;
                InkSkinUnlockView reveal = null;
                while (Time.realtimeSinceStartup < deadline)
                {
                    reveal = Object.FindAnyObjectByType<InkSkinUnlockView>();
                    if (reveal != null && reveal.IsOpen) break;
                    yield return null;
                }
                Assert.That(reveal, Is.Not.Null);
                Assert.That(reveal.IsOpen, Is.True);
                Assert.That(reveal.Current, Is.EqualTo(InkSkinId.ThreeCushion));
                yield return Wait(.4f); // 공개 직후의 중복 입력 방지 시간이 지난 뒤 확인한다.
                reveal.AcknowledgeAll();
                deadline = Time.realtimeSinceStartup + 15;
                while ((LobbyScreenNavigator.Instance == null || LobbyScreenNavigator.Instance.IsTransitioning ||
                    LobbyScreenNavigator.Instance.CurrentSection != LobbyScreenNavigator.LobbySection.Wardrobe) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(LobbyScreenNavigator.Instance, Is.Not.Null);
                Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False);
                Assert.That(LobbyScreenNavigator.Instance.CurrentSection, Is.EqualTo(LobbyScreenNavigator.LobbySection.Wardrobe));
                var wardrobe = Object.FindAnyObjectByType<InkWardrobeView>(); wardrobe.Select(InkSkinId.ThreeCushion);
                yield return Wait(.4f);
                Assert.That(wardrobe.EquipButton.interactable, Is.True);
                wardrobe.EquipButton.onClick.Invoke();
                Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.ThreeCushion));
                var returnedPreview = Object.FindAnyObjectByType<LobbySkinPreview>();
                var returnedBody = returnedPreview.transform.Find("Drift/Artwork/OriginalInkBody").GetComponent<UnityEngine.UI.Image>();
                Assert.That(returnedBody.sprite, Is.SameAs(originalBody));
                yield return Capture("three-cushion-wardrobe");
            }
            finally
            {
                MobileApplicationLifecycle.SetPlatformVisibility(true); Application.runInBackground = background;
                GameManager.RestorePendingGameOverSettlementStoreForTests(); Teardown();
            }
            yield return new ExitPlayMode();
        }
        static Vector2 Screen(LobbySkinPreview preview, Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, preview.Area.TransformPoint(local));
        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
        static IEnumerator Capture(string name)
        {
            string path = JObject.FromObject(MukJumpAgentAudit.CaptureUi())["path"].Value<string>();
            float end = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(File.Exists(path), Is.True);
            Directory.CreateDirectory("output/qa/three-cushion");
            File.Copy(path, "output/qa/three-cushion/" + name + ".png", true);
            File.Delete(path);
        }
    }
}
