using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkSkinTests
    {
        readonly List<Object> cleanup = new();
        MemoryLobbySettingsStore settings;
        MemoryPermanentGrowthStore growthStore;
        MemoryScoreStore scoreStore;
        ScoreManager score;

        [SetUp]
        public void Setup()
        {
            settings = new MemoryLobbySettingsStore();
            growthStore = new MemoryPermanentGrowthStore();
            InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(growthStore);
            scoreStore = new MemoryScoreStore { Best = 100 };
            ScoreManager.UseStoreForTests(scoreStore);
            score = Track(new GameObject("SkinScore")).AddComponent<ScoreManager>();
            Invoke(score, "OnEnable"); Invoke(score, "Awake");
            for (int i = 0; i < 10; i++) PermanentGrowthProfile.SettleRun("skin-test-" + i, 1, 0, true);
        }
        [TearDown]
        public void Teardown()
        {
            settings.ThrowOnSave = false;
            for (int i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
            InkSkinPreferences.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            ScoreManager.RestoreDefaultStoreForTests();
            PointerInput.ResetSuppressionForTests();
        }

        [Test]
        public void MissingInvalidAndFutureSelectionsFallBackWithoutOverwritingSave()
        {
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            settings.SetInt(InkSkinPreferences.StorageKey, 987);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkSkinPreferences.TryEquip((InkSkinId)(-1)), Is.False);
            Assert.That(settings.GetInt(InkSkinPreferences.StorageKey, 0), Is.EqualTo(987));
        }
        [Test]
        public void EquipPersistsLocallyWithoutChangingGrowthAndFailedSaveRollsBack()
        {
            string before = growthStore.Json;
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Gat), Is.True);
            InkSkinPreferences.UseStoreForTests(settings);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
            settings.ThrowOnSave = true;
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.False);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Gat));
            Assert.That(growthStore.Json, Is.EqualTo(before));
        }
        [Test]
        public void RunLocksSkinAcrossReviveAndCannotEquipDuringPlay()
        {
            InkSkinPreferences.TryEquip(InkSkinId.Gat);
            var manager = Manager(out var growth);
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSnapshot, Is.EqualTo(InkSkinId.Gat));
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.False);
            settings.SetInt(InkSkinPreferences.StorageKey, (int)InkSkinId.SpiderMask);
            Invoke(manager, "SetState", GameState.GameOver);
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSnapshot, Is.EqualTo(InkSkinId.Gat));
            Invoke(manager, "SetState", GameState.Lobby);
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSnapshot, Is.EqualTo(InkSkinId.SpiderMask));
        }
        [TestCase(InkSkinId.Ink, false)]
        [TestCase(InkSkinId.Gat, true)]
        [TestCase(InkSkinId.SpiderMask, false)]
        public void OnlyGatAddsOneAirJumpAndKeepsPrimaryJump(InkSkinId id, bool expected)
        {
            InkSkinPreferences.TryEquip(id);
            var manager = Manager(out _);
            Invoke(manager, "SetState", GameState.Playing);
            var player = Player();
            var jump = player.gameObject.AddComponent<AutoJump>();
            Invoke(jump, "Awake");
            Field(jump, "baseJumpSpeed", 10f);
            Field(jump, "jumpStrengthMultiplier", 1f);
            Field(jump, "normalInfluence", 0f);
            Invoke(jump, "Jump");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(10).Within(.001));
            Assert.That(Invoke(jump, "TryPerformDoubleJump"), Is.EqualTo(expected));
            if (expected) Assert.That(player.Body.linearVelocity.y, Is.EqualTo(4).Within(.001));
            Assert.That(Invoke(jump, "TryPerformDoubleJump"), Is.False);
            Invoke(jump, "Jump");
            jump.CancelForSpecialLaunch();
            Assert.That(Invoke(jump, "TryPerformDoubleJump"), Is.False);
            Invoke(jump, "Jump");
            jump.NotifyLanding(false);
            Assert.That(Invoke(jump, "TryPerformDoubleJump"), Is.False);
        }
        [TestCase(InkSkinId.Ink, false)]
        [TestCase(InkSkinId.Gat, false)]
        [TestCase(InkSkinId.SpiderMask, true)]
        public void SpiderClingsAndReleasesThroughAutomaticJumpOnly(InkSkinId id, bool expected)
        {
            InkSkinPreferences.TryEquip(id);
            var manager = Manager(out _);
            Invoke(manager, "SetState", GameState.Playing);
            var player = Player();
            var wall = Track(new GameObject("SkinWall")).AddComponent<ScreenSideWall>();
            Assert.That(Invoke(player, "TryBeginWallCling", wall, 1f), Is.EqualTo(expected));
            Assert.That(player.IsWallClinging, Is.EqualTo(expected));
            if (!expected) return;
            Assert.That(player.Body.gravityScale, Is.Zero);
            Assert.That(player.IsGrounded, Is.True);
            var jump = player.gameObject.AddComponent<AutoJump>();
            Invoke(jump, "Awake");
            Invoke(jump, "Jump");
            Assert.That(player.IsWallClinging, Is.False);
            Assert.That(player.Body.gravityScale, Is.GreaterThan(0));
            Assert.That(player.Body.linearVelocity.x, Is.GreaterThan(0));
            Assert.That(player.Body.linearVelocity.y, Is.GreaterThan(0));
        }
        [Test]
        public void GatWaitsForApexAndPauseDoesNotConsumeTheAirJump()
        {
            InkSkinPreferences.TryEquip(InkSkinId.Gat);
            var manager = Manager(out _);
            Invoke(manager, "SetState", GameState.Playing);
            var player = Player();
            var jump = player.gameObject.AddComponent<AutoJump>(); Invoke(jump, "Awake");
            Field(jump, "baseJumpSpeed", 10f); Field(jump, "jumpStrengthMultiplier", 1f); Field(jump, "normalInfluence", 0f);
            Invoke(jump, "Jump"); Invoke(jump, "FixedUpdate");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(10).Within(.001));
            player.Body.linearVelocity = new Vector2(0, 1.5f);
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, true);
            Invoke(jump, "FixedUpdate");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(1.5f));
            typeof(GameManager).GetProperty("IsPaused").SetValue(manager, false);
            Invoke(jump, "FixedUpdate");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(4).Within(.001));
            Invoke(jump, "FixedUpdate");
            Assert.That(player.Body.linearVelocity.y, Is.EqualTo(4).Within(.001));
        }
        [Test]
        public void WardrobeOnlyOpensAtCoverAndPreviewDoesNotEquip()
        {
            var manager = Manager(out _);
            var wardrobe = manager.gameObject.AddComponent<InkWardrobeView>();
            wardrobe.BuildForTests(Idle());
            var navigator = manager.gameObject.AddComponent<LobbyScreenNavigator>();
            navigator.BuildForTests();
            Assert.That(navigator.OpenWardrobe(), Is.True);
            Assert.That(wardrobe.IsOpen, Is.False);
            Assert.That(navigator.CanStartGame, Is.False);
            navigator.CompleteCoverForTests();
            navigator.CompleteRevealForTests();
            wardrobe.Select(InkSkinId.Gat);
            Assert.That(wardrobe.Selected, Is.EqualTo(InkSkinId.Gat));
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            wardrobe.BackButton.onClick.Invoke();
            navigator.CompleteCoverForTests(); navigator.CompleteRevealForTests();
            navigator.OpenWardrobe(); navigator.CompleteCoverForTests(); navigator.CompleteRevealForTests();
            Assert.That(wardrobe.Selected, Is.EqualTo(InkSkinId.Ink));
            wardrobe.Select(InkSkinId.SpiderMask);
            wardrobe.EquipButton.onClick.Invoke();
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.SpiderMask));
            Assert.That(wardrobe.EquipButton.interactable, Is.False);
        }
        [TestCase(GameLanguage.Korean)]
        [TestCase(GameLanguage.English)]
        [TestCase(GameLanguage.Japanese)]
        public void WardrobeLabelsAreTranslatedAndFit(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            var manager = Manager(out _);
            var wardrobe = manager.gameObject.AddComponent<InkWardrobeView>();
            wardrobe.BuildForTests(Idle());
            wardrobe.SetNavigationPresentation(true, true);
            for (int i = 0; i < InkSkinCatalog.Count; i++)
            {
                wardrobe.Select((InkSkinId)i);
                Canvas.ForceUpdateCanvases();
                foreach (var text in wardrobe.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    Assert.That(text.font, Is.Not.Null, text.name);
                    if (text.text.Length == 0) continue;
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2), text.name + ": " + text.text);
                    var binding = text.GetComponent<InkLocalizedText>();
                    Assert.That(binding, Is.Not.Null);
                    if (language != GameLanguage.Korean)
                        Assert.That(text.text, Is.Not.EqualTo(binding.SourceText), text.name);
                }
            }
        }
        [TestCase(InkSkinId.Gat)]
        [TestCase(InkSkinId.SpiderMask)]
        public void AccessoryFollowsBodyWithoutChangingPhysicsAndCloneKeepsItsOwnRenderer(InkSkinId id)
        {
            InkSkinPreferences.TryEquip(id);
            var manager = Manager(out _);
            Invoke(manager, "SetState", GameState.Playing);
            var player = Player();
            var body = player.GetComponent<SpriteRenderer>();
            body.sprite = Idle(); body.flipX = true;
            var view = player.gameObject.AddComponent<InkSkinView>();
            Invoke(view, "OnEnable"); Invoke(view, "LateUpdate");
            Assert.That(view.AccessoryRenderer.enabled, Is.True);
            Assert.That(view.AccessoryRenderer.flipX, Is.True);
            Assert.That(body.sprite, Is.SameAs(Idle()));
            Assert.That(player.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(player.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.4f));
            var clone = Track(Object.Instantiate(player.gameObject));
            var cloneView = clone.GetComponent<InkSkinView>();
            Invoke(cloneView, "OnEnable"); Invoke(cloneView, "LateUpdate");
            Assert.That(cloneView.AccessoryRenderer, Is.Not.SameAs(view.AccessoryRenderer));
            Assert.That(cloneView.AccessoryRenderer.transform.parent, Is.SameAs(clone.transform));
            Assert.That(clone.transform.Cast<Transform>().Count(t => t.name == "InkSkinAccessory"), Is.EqualTo(1));
            body.enabled = false; Invoke(view, "LateUpdate");
            Assert.That(view.AccessoryRenderer.enabled, Is.False);
            body.enabled = true; Invoke(view, "LateUpdate");
            var health = player.gameObject.AddComponent<PlayerHealthBillboard>();
            Invoke(health, "OnEnable");
            Bounds combined = (Bounds)Invoke(health, "ResolveOwnerBounds");
            Assert.That(combined.max.y, Is.GreaterThanOrEqualTo(view.AccessoryRenderer.bounds.max.y));
            player.Kill(); Invoke(view, "LateUpdate");
            Assert.That(view.AccessoryRenderer.enabled, Is.False);
        }
        [Test]
        public void LobbyOrderingKeepsWardrobeImmediatelyAboveOptions()
        {
            Assert.That(LobbyMenuLayout.StartAnchor.y, Is.GreaterThan(LobbyMenuLayout.GrowthAnchor.y));
            Assert.That(LobbyMenuLayout.GrowthAnchor.y, Is.GreaterThan(LobbyMenuLayout.WardrobeAnchor.y));
            Assert.That(LobbyMenuLayout.WardrobeAnchor.y, Is.GreaterThan(LobbyMenuLayout.OptionsAnchor.y));
            Assert.That(LobbyMenuLayout.OptionsAnchor.x, Is.EqualTo(LobbyMenuLayout.WardrobeAnchor.x));
        }

        void ClearAchievements()
        {
            Assert.That(PermanentGrowthProfile.TryClearForAccountDeletion(), Is.True);
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(0), Is.True);
        }

        [Test]
        public void LockedPreviewAndRawEquipKeyCannotGrantAnAbility()
        {
            ClearAchievements();
            settings.SetInt(InkSkinPreferences.StorageKey, (int)InkSkinId.Gat);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Gat), Is.False);
            var manager = Manager(out var growth);
            var wardrobe = manager.gameObject.AddComponent<InkWardrobeView>();
            wardrobe.BuildForTests(Idle()); wardrobe.SetNavigationPresentation(true, true);
            wardrobe.Select(InkSkinId.SpiderMask);
            Assert.That(wardrobe.Selected, Is.EqualTo(InkSkinId.SpiderMask));
            Assert.That(wardrobe.EquipButton.interactable, Is.False);
            wardrobe.EquipButton.onClick.Invoke();
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(growth.SkinSnapshot, Is.EqualTo(InkSkinId.Ink));
        }

        [Test]
        public void GatUsesConfirmedBestNotLiveHeightOrFailedSave()
        {
            ClearAchievements();
            Assert.That(score.TryCommitBestCandidate(99), Is.True);
            Assert.That(InkSkinAchievements.Progress(InkSkinId.Gat), Is.EqualTo(99));
            scoreStore.ThrowOnSave = true;
            Assert.That(score.TryCommitBestCandidate(100), Is.False);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat), Is.False);
            scoreStore.ThrowOnSave = false;
            Assert.That(score.TryCommitBestCandidate(100), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat), Is.True);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Gat), Is.True);
        }

        [Test]
        public void TenUniqueEligibleSettlementsUnlockWithoutDuplicateOrFailedSaves()
        {
            ClearAchievements();
            for (int i = 0; i < 9; i++) PermanentGrowthProfile.SettleRun("achievement-" + i, 1, 0, true);
            PermanentGrowthProfile.SettleRun("achievement-8", 1, 0, true);
            PermanentGrowthProfile.SettleRun("debug", 900, 0, false);
            Assert.That(InkSkinAchievements.Progress(InkSkinId.SpiderMask), Is.EqualTo(9));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.False);
            growthStore.ThrowOnPrimarySave = true;
            Assert.That(PermanentGrowthProfile.SettleRun("achievement-9", 1, 0, true).Accepted, Is.False);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.False);
            growthStore.ThrowOnPrimarySave = false;
            Assert.That(PermanentGrowthProfile.TryRestoreBackup(), Is.True);
            Assert.That(InkSkinAchievements.Progress(InkSkinId.SpiderMask), Is.EqualTo(9));
            Assert.That(PermanentGrowthProfile.SettleRun("achievement-9", 1, 0, true).Accepted, Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.True);
            for (int i = 10; i < 75; i++) PermanentGrowthProfile.SettleRun("achievement-" + i, 1, 0, true);
            Assert.That(PermanentGrowthProfile.CompletedRunHistoryCount, Is.EqualTo(64));
            Assert.That(InkSkinAchievements.Progress(InkSkinId.SpiderMask), Is.EqualTo(10));
            PermanentGrowthProfile.UseStoreForTests(growthStore);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.True);
        }

        [Test]
        public void AccountReplacementDeletionAndGrowthResetRespectAchievementOwnership()
        {
            Assert.That(PermanentGrowthProfile.TryExportCloudJson(out string earnedAccount), Is.True);
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.SpiderMask), Is.True);
            Assert.That(PermanentGrowthProfile.TryResetPurchasedNodes(), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.SpiderMask), Is.True);
            ClearAchievements();
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat), Is.False);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(earnedAccount), Is.True);
            Assert.That(ScoreManager.TryReplaceVerifiedBestForAccountSwitch(100), Is.True);
            Assert.That(InkSkinAchievements.IsUnlocked(InkSkinId.Gat), Is.True);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.SpiderMask));
        }

        GameManager Manager(out RunGrowthController growth)
        {
            var host = Track(new GameObject("SkinTestSystems"));
            var manager = host.AddComponent<GameManager>(); Invoke(manager, "OnEnable");
            growth = host.GetComponent<RunGrowthController>() ?? host.AddComponent<RunGrowthController>();
            Invoke(growth, "OnEnable");
            return manager;
        }
        PlayerController Player()
        {
            var host = Track(new GameObject("SkinTestPlayer", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D)));
            host.GetComponent<CircleCollider2D>().radius = .4f;
            var player = host.AddComponent<PlayerController>(); Invoke(player, "Awake");
            typeof(PlayerController).GetProperty("GroundNormal").SetValue(player, Vector2.up);
            return player;
        }
        static Sprite Idle() => AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s => s.name == "idle");
        T Track<T>(T value) where T : Object { cleanup.Add(value); return value; }
        static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
        static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        public static void CompilePlayerAndRebuild()
        {
            var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
            {
                target = BuildTarget.iOS, group = BuildTargetGroup.iOS,
                options = UnityEditor.Build.Player.ScriptCompilationOptions.None,
            };
            var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, "Temp/MukJumpInkSkinsPlayer");
            if (result.assemblies == null || result.assemblies.Count == 0)
                throw new UnityEditor.Build.BuildFailedException("스킨 iOS Player 컴파일 실패");
            MukJump.EditorTools.MukJumpSceneBuilder.Build();
            var audit = Newtonsoft.Json.Linq.JObject.FromObject(MukJump.EditorTools.MukJumpAgentAudit.Audit());
            System.IO.File.WriteAllText("output/qa/ink-skins/scene-audit.json", audit.ToString());
            if ((bool?)audit["readyForValidation"] != true) throw new Exception("스킨 씬 감사 실패: " + audit);
            Debug.Log("[InkSkins] iOS Player assemblies=" + result.assemblies.Count + "; " + audit);
        }
    }
}
