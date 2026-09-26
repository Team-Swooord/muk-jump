using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MukJump.Core;
using MukJump.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkCosmeticPresentationTests
    {
        readonly List<Object> objects = new();
        MemoryLobbySettingsStore settings;
        [SetUp] public void Setup()
        {
            settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings); LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            ScoreManager.UseStoreForTests(new MemoryScoreStore());
            SkinDebugSession.Reset(); InkPremiumAppearance.UseForTests(false, null);
            MobileApplicationLifecycle.SetPlatformVisibility(true);
        }
        [TearDown] public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear(); SkinDebugSession.Reset(); InkPremiumAppearance.RestoreForTests();
            InkSkinPreferences.RestoreDefaultStoreForTests(); LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests(); ScoreManager.RestoreDefaultStoreForTests();
        }
        T Track<T>(T item) where T : Object { objects.Add(item); return item; }
        static object Invoke(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
        static Sprite Idle() => AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png")
            .OfType<Sprite>().First(s => s.name == "idle");
        void Earn()
        {
            var stats = SkinRunStats.Begin(false, InkSkinId.Ink);
            stats.drawnLandings = 200; stats.items = 1000; stats.maxLivingPlayers = 12;
            Assert.That(PermanentGrowthProfile.SettleRun(Guid.NewGuid().ToString("N"), 500, 500, 0, 60, true, stats).Accepted, Is.True);
        }
        InkWardrobeView Wardrobe()
        {
            var view = Track(new GameObject("CosmeticWardrobeTest")).AddComponent<InkWardrobeView>();
            view.BuildForTests(Idle()); view.SetNavigationPresentation(true, true); return view;
        }
        [Test] public void HomeCategoriesAndBackNeverSaveOrEquipAPreview()
        {
            Earn();
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest);
            var view = Wardrobe(); int saves = settings.SaveCount;
            Assert.That(view.IsCategoryHome, Is.True);
            Assert.That(view.Paper.Find("CosmeticCategories").gameObject.activeSelf, Is.True);
            Assert.That(view.Paper.Find("ShowroomStage").gameObject.activeSelf, Is.False);
            Assert.That(view.CollectionScroll.gameObject.activeSelf, Is.False);
            Assert.That(view.EquipButton.gameObject.activeSelf, Is.False);
            Assert.That(view.Paper.Find("CosmeticCategories/EquippedInk/Accessory").GetComponent<Image>().sprite,
                Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.MagpieNest)));
            Assert.That(view.Paper.Find("CosmeticCategories/CategoryBrush").GetComponent<Button>().interactable, Is.False);
            view.SelectSlot(InkCosmeticSlot.Brush);
            view.StepSelection(1); view.ToggleCollection(); view.EquipButton.onClick.Invoke();
            Assert.That(view.IsCategoryHome, Is.True);
            Assert.That(view.SelectedNest, Is.True);
            view.Paper.Find("CosmeticCategories/CategoryLanding").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.IsCategoryHome, Is.False);
            Assert.That(view.SelectedSlot, Is.EqualTo(InkCosmeticSlot.Landing));
            Assert.That(view.Paper.Find("EffectChoices").gameObject.activeSelf, Is.True);
            Assert.That(view.Paper.Find("CosmeticCategories").gameObject.activeSelf, Is.False);
            view.SelectEffect(InkCosmeticCatalog.BambooLeaf);
            view.BackButton.onClick.Invoke();
            Assert.That(view.IsCategoryHome, Is.True);
            Assert.That(view.IsOpen, Is.True, "목록에서 뒤로 가면 로비가 아니라 꾸미기 첫 화면");
            Assert.That(settings.SaveCount, Is.EqualTo(saves));
            view.SelectSlot(InkCosmeticSlot.Landing);
            Assert.That(view.SelectedEffect, Is.EqualTo(InkCosmeticCatalog.DefaultLanding), "선택만 한 효과는 복귀 후 유지하지 않는다");
            view.SelectEffect(InkCosmeticCatalog.BambooLeaf); view.EquipButton.onClick.Invoke();
            view.BackButton.onClick.Invoke();
            view.SetNavigationPresentation(false, false); view.SetNavigationPresentation(true, true);
            Assert.That(view.IsCategoryHome, Is.True);
            view.SelectSlot(InkCosmeticSlot.Landing);
            Assert.That(view.SelectedEffect, Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
            Assert.That(settings.SaveCount, Is.EqualTo(saves + 1));
        }
        [Test] public void HomeReflectsAccountChangesWithoutSavingOrLeakingTheOldOutfit()
        {
            Earn(); InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest);
            var view = Wardrobe();
            settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            Invoke(view, "RefreshSelection");
            var accessory = view.Paper.Find("CosmeticCategories/EquippedInk/Accessory").GetComponent<Image>();
            Assert.That(accessory.enabled, Is.False);
            Assert.That(view.SelectedNest, Is.False);
            Assert.That(settings.SaveCount, Is.Zero);
        }
        [Test] public void PreviewNeverSavesAndEquipChangesOnlyOneSlot()
        {
            Earn(); InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest);
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.Tadpole);
            var view = Wardrobe(); int saves = settings.SaveCount;
            view.SelectSlot(InkCosmeticSlot.Landing); view.SelectEffect(InkCosmeticCatalog.BambooLeaf);
            Assert.That(settings.SaveCount, Is.EqualTo(saves));
            Assert.That(view.Paper.Find("EffectChoices/Choice1/SelectedBrush").gameObject.activeSelf, Is.True);
            Assert.That(view.Paper.Find("EffectChoices/Choice0/EquippedSeal").gameObject.activeSelf, Is.True);
            Assert.That(view.Paper.Find("EffectChoices/Choice1/EquippedSeal").gameObject.activeSelf, Is.False, "미리보기와 착용 낙관은 구분한다");
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Landing), Is.EqualTo(InkCosmeticCatalog.DefaultLanding));
            Assert.That(view.Paper.Find("ShowroomStage/Preview/Accessory").GetComponent<Image>().sprite,
                Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.MagpieNest)));
            Assert.That(view.Paper.Find("ShowroomStage/CombinedClonePreview/TadpoleTail").GetComponent<Image>().enabled, Is.True);
            view.EquipButton.onClick.Invoke();
            Assert.That(settings.SaveCount, Is.EqualTo(saves + 1));
            Assert.That(view.Paper.Find("EffectChoices/Choice0/EquippedSeal").gameObject.activeSelf, Is.False);
            Assert.That(view.Paper.Find("EffectChoices/Choice1/EquippedSeal").gameObject.activeSelf, Is.True);
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Landing), Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Skin), Is.EqualTo(InkCosmeticCatalog.MagpieNest));
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Clone), Is.EqualTo(InkCosmeticCatalog.Tadpole));
            view.SelectSlot(InkCosmeticSlot.Brush);
            Assert.That(view.SelectedSlot, Is.EqualTo(InkCosmeticSlot.Landing));
        }
        [Test] public void LockedEffectsShowPublicProgressAndCannotBeEquipped()
        {
            var view = Wardrobe(); int saves = settings.SaveCount;
            view.SelectSlot(InkCosmeticSlot.Clone); view.SelectEffect(InkCosmeticCatalog.Tadpole);
            Assert.That(view.EquipButton.gameObject.activeSelf, Is.False);
            Assert.That(view.Paper.GetComponentsInChildren<Text>().Any(t => t.text == "0 / 12"), Is.True);
            view.EquipButton.onClick.Invoke();
            Assert.That(settings.SaveCount, Is.EqualTo(saves));
            Assert.That(InkSkinPreferences.EquippedCosmetic(InkCosmeticSlot.Clone), Is.EqualTo(InkCosmeticCatalog.FollowBody));
            view.SelectNest();
            Assert.That(view.Paper.GetComponentsInChildren<Text>().Any(t => t.text == "0 / 1000"), Is.True);
        }
        [Test] public void AccountSwitchClearsCombinedPreviewWithoutOverwritingPreference()
        {
            PermanentGrowthProfile.TryExportCloudJson(out string empty);
            Earn(); InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest);
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.Tadpole);
            var view = Wardrobe(); view.SelectSlot(InkCosmeticSlot.Landing);
            Assert.That(PermanentGrowthProfile.TryReplaceFromCloudJson(empty), Is.True);
            Invoke(view, "RefreshSelection");
            Assert.That(view.Paper.Find("ShowroomStage/Preview/Accessory").GetComponent<Image>().enabled, Is.False);
            Assert.That(view.Paper.Find("ShowroomStage/CombinedClonePreview/TadpoleTail").GetComponent<Image>().enabled, Is.False);
            Assert.That(InkSkinPreferences.PreferredCosmetic(InkCosmeticSlot.Clone), Is.EqualTo(InkCosmeticCatalog.Tadpole));
        }
        [Test] public void NewCosmeticsKeepOriginalBodyFramesAndClonePhysics()
        {
            Earn(); InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest);
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.Tadpole);
            var host = Track(new GameObject("CosmeticBody", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D)));
            var body = host.GetComponent<SpriteRenderer>(); body.sprite = Idle();
            var collider = host.GetComponent<CircleCollider2D>(); collider.radius = .42f;
            var player = host.AddComponent<PlayerController>(); Invoke(player, "Awake");
            var view = host.AddComponent<InkSkinView>(); Invoke(view, "OnEnable"); Invoke(view, "LateUpdate");
            Assert.That(view.AccessoryRenderer.sprite, Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.MagpieNest)));
            var clone = Track(Object.Instantiate(host)); clone.GetComponent<PlayerController>().ConfigureAsClone(3f);
            var cloneView = clone.GetComponent<InkSkinView>(); Invoke(cloneView, "OnEnable");
            foreach (var frame in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>())
            {
                var cloneBody = clone.GetComponent<SpriteRenderer>(); cloneBody.sprite = frame; cloneBody.color = new Color(1, 1, 1, .45f);
                Invoke(cloneView, "LateUpdate");
                Assert.That(cloneView.AccessoryRenderer.sprite, Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.Tadpole)));
                Assert.That(cloneBody.sprite, Is.SameAs(frame)); Assert.That(cloneView.AccessoryRenderer.color.a, Is.EqualTo(.45f));
                Assert.That(clone.GetComponent<CircleCollider2D>().radius, Is.EqualTo(.42f));
                Assert.That(clone.GetComponent<Rigidbody2D>().gravityScale, Is.EqualTo(3f));
            }
            var nextClone = Track(Object.Instantiate(clone)); nextClone.GetComponent<PlayerController>().ConfigureAsClone(3f);
            Invoke(nextClone.GetComponent<InkSkinView>(), "OnEnable"); Invoke(nextClone.GetComponent<InkSkinView>(), "LateUpdate");
            Assert.That(nextClone.GetComponent<InkSkinView>().AccessoryRenderer.sprite, Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.Tadpole)));
            body.enabled = false; Invoke(view, "LateUpdate"); Assert.That(view.AccessoryRenderer.enabled, Is.False);
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.FollowBody);
            Invoke(cloneView, "LateUpdate");
            Assert.That(cloneView.AccessoryRenderer.sprite, Is.SameAs(InkCosmeticCatalog.Sprite(InkCosmeticCatalog.MagpieNest)));
        }
        [Test] public void AllSlotsStayFixedAcrossReviveAndRefreshOnlyForANewRun()
        {
            Earn();
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Skin, InkCosmeticCatalog.MagpieNest);
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Landing, InkCosmeticCatalog.BambooLeaf);
            InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.Tadpole);
            var host = Track(new GameObject("CosmeticRunSnapshot"));
            var manager = host.AddComponent<GameManager>(); Invoke(manager, "OnEnable");
            var run = host.GetComponent<RunGrowthController>() ?? host.AddComponent<RunGrowthController>(); Invoke(run, "OnEnable");
            Invoke(manager, "SetState", GameState.Playing);
            Assert.That(InkSkinPreferences.TryEquipCosmetic(InkCosmeticSlot.Clone, InkCosmeticCatalog.FollowBody), Is.False);
            settings.SetString(InkSkinPreferences.CosmeticsKey, JsonUtility.ToJson(new InkCosmeticSelection()));
            Invoke(manager, "SetState", GameState.GameOver); Invoke(manager, "SetState", GameState.Playing);
            Assert.That(InkCosmeticCatalog.InRun(InkCosmeticSlot.Skin), Is.EqualTo(InkCosmeticCatalog.MagpieNest));
            Assert.That(InkCosmeticCatalog.InRun(InkCosmeticSlot.Landing), Is.EqualTo(InkCosmeticCatalog.BambooLeaf));
            Assert.That(InkCosmeticCatalog.InRun(InkCosmeticSlot.Clone), Is.EqualTo(InkCosmeticCatalog.Tadpole));
            Invoke(manager, "SetState", GameState.Lobby); Invoke(manager, "SetState", GameState.Playing);
            Assert.That(run.LandingSnapshot, Is.EqualTo(InkCosmeticCatalog.DefaultLanding));
            Assert.That(run.CloneSnapshot, Is.EqualTo(InkCosmeticCatalog.FollowBody));
        }
        [TestCase(VfxQualityTier.Low, 6)] [TestCase(VfxQualityTier.Medium, 9)] [TestCase(VfxQualityTier.High, 12)]
        public void LeavesUseFixedPoolRespectPauseAndKeepGoldMotes(VfxQualityTier tier, int limit)
        {
            var previous = VfxQualityRuntime.Tier;
            var host = Track(new GameObject("CosmeticParticles"));
            const string art = "Assets/MukJump/VFX/InkDropJump/Textures/";
            VfxQualityRuntime.SetTier(tier, VfxQualityChangeReason.DebugOverride);
            using var effect = new InkContactParticles(host.transform,
                AssetDatabase.LoadAssetAtPath<Texture2D>(art + "T_VFX_InkDropletAtlas_512.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(art + "T_VFX_InkSplash_512.png"),
                Resources.Load<Shader>("MukJump/Shaders/InkContactParticle"), InkCosmeticCatalog.Sprite(InkCosmeticCatalog.BambooLeaf).texture);
            try
            {
                effect.EmitLanding(Vector3.zero, 1, true, true);
                var leaves = host.transform.Find("InkContactParticles/BambooLeaves").GetComponent<ParticleSystem>();
                var snapshot = new ParticleSystem.Particle[12]; leaves.GetParticles(snapshot); float lifetime = snapshot[0].remainingLifetime;
                effect.Advance(.3f, false); leaves.GetParticles(snapshot);
                Assert.That(snapshot[0].remainingLifetime, Is.EqualTo(lifetime));
                Assert.That(host.transform.Find("InkContactParticles/DryBrushMotes").GetComponent<ParticleSystem>().particleCount, Is.GreaterThan(0));
                for (int i = 0; i < 100; i++) effect.EmitLanding(Vector3.zero, 1, true, true);
                Assert.That(effect.LeafCount, Is.InRange(1, limit)); Assert.That(effect.ActiveCount, Is.LessThanOrEqualTo(effect.Capacity));
                Assert.That(host.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(4));
                for (int i = 0; i < 30; i++) effect.Advance(.05f, true);
                Assert.That(effect.ActiveCount, Is.Zero);
                LobbySettingsProfile.SetReducedMotionEnabled(true); effect.EmitLanding(Vector3.zero, 1, true, true);
                Assert.That(effect.ActiveCount, Is.Zero);
            }
            finally { VfxQualityRuntime.SetTier(previous, VfxQualityChangeReason.DebugOverride); }
        }
        [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void NewNamesAndRequirementsAreTranslated(GameLanguage language)
        {
            foreach (string text in new[] { "까치 둥지", "댓잎", "먹 올챙이", "착지 효과", "분신 외형", "능력 변화 없음",
                "아이템 1,000회 획득", "그린 발판에 누적 200회 착지", "원먹 포함 12마리 동시 생존" })
                Assert.That(GameLocalization.Translate(text, language), Is.Not.EqualTo(text), text);
        }
    }
}
