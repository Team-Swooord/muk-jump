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
            Vector2 p = Vector2.zero, v = new(10, 0), limit = Vector2.one * 10;
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, .9f), Is.Zero);
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, .1f), Is.EqualTo(1));
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, 0), Is.Zero);
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, .1f), Is.Zero, "같은 벽의 다음 프레임은 중복이 아니다.");
            p = Vector2.zero; v = Vector2.one * 10;
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, 1f), Is.EqualTo(1));
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, 4f), Is.EqualTo(2));
            p = Vector2.zero; v = new Vector2(10, 0);
            Assert.That(LobbySkinPreview.AdvanceBounce(ref p, ref v, limit, 6f), Is.EqualTo(3), "긴 프레임의 다중 반사도 세 번까지 인정한다.");
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

        [Test] public void BilliardsAssetAndCatalogHaveMatchingTranslationsHiddenFlagAndStableIds()
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
                preview.Drift.anchoredPosition = Vector2.zero;
                e.position = Screen(preview, Vector2.zero); preview.OnPointerDown(e);
                Assert.That(preview.IsDragging, Is.True);
                for (int i = 1; i <= 3; i++)
                {
                    yield return null;
                    e.position = Screen(preview, new Vector2(i * 60, 0)); preview.OnDrag(e);
                }
                preview.OnPointerUp(e); preview.OnEndDrag(e);
                Assert.That(preview.Velocity.x, Is.GreaterThan(0));
                float previousVelocity = preview.Velocity.x, deadline = Time.realtimeSinceStartup + 25;
                int bounces = 0;
                while (!InkSkinAchievements.IsUnlocked(InkSkinId.ThreeCushion) && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    if (Mathf.Sign(previousVelocity) != Mathf.Sign(preview.Velocity.x)) bounces++;
                    previousVelocity = preview.Velocity.x;
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

                // 기존 꾸미기 화면에서 실제 착용과 해금 조건 표시를 확인한다.
                Assert.That(LobbyScreenNavigator.Instance.OpenWardrobe(), Is.True);
                deadline = Time.realtimeSinceStartup + 15;
                while (LobbyScreenNavigator.Instance.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(LobbyScreenNavigator.Instance.IsTransitioning, Is.False);
                var wardrobe = Object.FindAnyObjectByType<InkWardrobeView>(); wardrobe.Select(InkSkinId.ThreeCushion);
                yield return Wait(.4f);
                Assert.That(wardrobe.EquipButton.interactable, Is.True);
                wardrobe.EquipButton.onClick.Invoke();
                Assert.That(InkSkinPreferences.PersistedEquipped, Is.EqualTo(InkSkinId.ThreeCushion));
                Assert.That(body.sprite, Is.SameAs(originalBody));
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
