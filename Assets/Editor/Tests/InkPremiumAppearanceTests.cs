using System;
using System.Linq;
using System.Reflection;
using MukJump.Player;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkPremiumAppearanceTests
    {
        public sealed class Entitlement : IInkPremiumEntitlement { public bool Owned; public bool IsOwned => Owned; }
        sealed class Ads : IFullScreenAdProvider
        {
            public int Loads, Shows;
            public bool IsReady(FullScreenAdPlacement _) => true;
            public void Preload(FullScreenAdPlacement _) => Loads++;
            public void Show(FullScreenAdPlacement _, Action<bool> done) { Shows++; done(true); }
        }
        MemoryLobbySettingsStore settings;
        Entitlement entitlement;
        GameObject host;
        [SetUp] public void SetUp()
        {
            entitlement = new Entitlement();
            InkPremiumAppearance.UseForTests(true, entitlement);
            settings = new MemoryLobbySettingsStore();
            InkSkinPreferences.UseStoreForTests(settings);
            LobbySettingsProfile.UseStoreForTests(settings);
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
        }
        [TearDown] public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            InkPremiumAppearance.RestoreForTests();
            InkSkinPreferences.RestoreDefaultStoreForTests();
            LobbySettingsProfile.RestoreDefaultStoreForTests();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            MonetizationAds.ResetProvider();
            PointerInput.ResetSuppressionForTests();
        }
        [Test] public void GoldAddsOnlyPresentationEntryAndLeavesExistingIdsAndMasksIntact()
        {
            Assert.That(InkSkinCatalog.Count, Is.EqualTo(63));
            Assert.That(InkSkinCatalog.AvailableRewardMask, Is.LessThanOrEqualTo(long.MaxValue));
            Assert.That(InkWardrobeEntry.Count, Is.EqualTo(InkSkinCatalog.BrowseCount + 1));
            Assert.That(InkWardrobeEntry.At(1).Gold, Is.True);
            Assert.That(InkWardrobeEntry.At(1).Group, Is.Zero);
            foreach (var id in Enumerable.Range(0, InkSkinCatalog.BrowseCount).Select(InkSkinCatalog.BrowseAt))
                Assert.That(InkWardrobeEntry.At(InkWardrobeEntry.Index(id)).Skin, Is.EqualTo(id));
            Assert.That(Enumerable.Range(0, InkWardrobeEntry.Count).Select(i => InkWardrobeEntry.At(i).CardKey).Distinct().Count(), Is.EqualTo(InkWardrobeEntry.Count));
        }
        [Test] public void NonIosNeverShowsOrGrantsPremiumEvenWithSavedGoldChoice()
        {
            entitlement.Owned = true; settings.SetInt(InkSkinPreferences.GoldSelectionKey, 1);
            InkPremiumAppearance.UseForTests(false, entitlement);
            Assert.That(InkPremiumAppearance.IsOwned, Is.False);
            Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
            Assert.That(InkSkinPreferences.TryEquipGold(), Is.False);
            Assert.That(InkWardrobeEntry.Count, Is.EqualTo(InkSkinCatalog.BrowseCount));
            Assert.That(Enumerable.Range(0, InkWardrobeEntry.Count).Any(i => InkWardrobeEntry.At(i).Gold), Is.False);
        }
        [Test] public void OwnershipRequiredAndGoldUsesBaseInkWithoutChangingGrowth()
        {
            Assert.That(InkSkinPreferences.TryEquipGold(), Is.False);
            Assert.That(settings.SaveCount, Is.Zero);
            entitlement.Owned = true;
            Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.Ink));
            Assert.That(InkPremiumAppearance.IsEquipped, Is.True);
            Assert.That(InkSkinCatalog.HasAbility(InkSkinPreferences.Equipped), Is.False);
            entitlement.Owned = false;
            Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
            Assert.That(InkSkinPreferences.GoldSelected, Is.True, "환불 뒤 개인 취향만 보존하며 권한은 회수한다");
        }
        [Test] public void FailedSaveRollsBackBothSelectionFieldsAndBaseInkClearsGold()
        {
            entitlement.Owned = true; settings.ThrowOnSave = true;
            Assert.That(InkSkinPreferences.TryEquipGold(), Is.False);
            Assert.That(InkSkinPreferences.GoldSelected, Is.False);
            settings.ThrowOnSave = false;
            Assert.That(InkSkinPreferences.TryEquipGold(), Is.True);
            settings.ThrowOnSave = true;
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Ink), Is.False);
            Assert.That(InkPremiumAppearance.IsEquipped, Is.True);
            settings.ThrowOnSave = false;
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.Ink), Is.True);
            Assert.That(InkPremiumAppearance.IsEquipped, Is.False);
        }
        [Test] public void PurchasedRewardsSkipEveryAdWithoutChangingReviveLimit()
        {
            var ads = new Ads(); MonetizationAds.RegisterProvider(ads);
            entitlement.Owned = true;
            foreach (FullScreenAdPlacement placement in Enum.GetValues(typeof(FullScreenAdPlacement)))
            {
                MonetizationAds.Provider.Preload(placement);
                bool reward = false;
                MonetizationAds.Provider.Show(placement, value => reward = value);
                Assert.That(reward, Is.EqualTo(placement != FullScreenAdPlacement.PostRunInterstitial));
            }
            Assert.That(ads.Loads + ads.Shows, Is.Zero);
            Assert.That(MonetizationPolicy.ShouldShowTopBanner(GameState.Playing, false, false, true, false), Is.False);
            Assert.That(MonetizationPolicy.ShouldShowPostRunInterstitial(3, 100, 200, false, true), Is.False);
            Assert.That(MonetizationPolicy.CanOfferGameOverRevive(true, true, true), Is.False);
            Assert.That(MonetizationPolicy.CanOfferPreRunShield(0, true, true), Is.False);
            entitlement.Owned = false;
            Assert.That(MonetizationAds.Provider, Is.SameAs(ads));
            Assert.That(MonetizationPolicy.ShouldShowTopBanner(GameState.Playing, false, false, true, false), Is.True);
        }
        [Test] public void GoldMaterialKeepsFramesColorColliderAndCloneBaseMaterial()
        {
            entitlement.Owned = true; InkSkinPreferences.TryEquipGold();
            host = new GameObject("GoldBodyTest");
            var renderer = host.AddComponent<SpriteRenderer>();
            Material original = renderer.sharedMaterial;
            var collider = host.AddComponent<CircleCollider2D>(); collider.radius = .42f;
            var skin = host.AddComponent<InkSkinView>();
            typeof(InkSkinView).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(skin, null);
            var update = typeof(InkSkinView).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().ToArray();
            Assert.That(sprites.Length, Is.EqualTo(8));
            foreach (var sprite in sprites)
            {
                renderer.sprite = sprite; renderer.color = new Color(.9f, .8f, .7f, .45f);
                update.Invoke(skin, null);
                Assert.That(renderer.sprite, Is.SameAs(sprite));
                Assert.That(renderer.color.a, Is.EqualTo(.45f));
                Assert.That(renderer.sharedMaterial, Is.SameAs(GoldInkVisual.World));
                Assert.That(collider.radius, Is.EqualTo(.42f));
            }
            var clone = Object.Instantiate(host);
            try
            {
                typeof(InkSkinView).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(clone.GetComponent<InkSkinView>(), null);
                entitlement.Owned = false;
                update.Invoke(clone.GetComponent<InkSkinView>(), null);
                update.Invoke(skin, null);
                Assert.That(renderer.sharedMaterial, Is.SameAs(original));
                Assert.That(clone.GetComponent<SpriteRenderer>().sharedMaterial, Is.SameAs(original));
            }
            finally { Object.DestroyImmediate(clone); }
            Assert.That(ShaderUtil.ShaderHasError(GoldInkVisual.World.shader), Is.False);
            Assert.That(ShaderUtil.ShaderHasError(GoldInkVisual.Ui.shader), Is.False);
        }
        [TestCase(VfxQualityTier.Low)]
        [TestCase(VfxQualityTier.Medium)]
        [TestCase(VfxQualityTier.High)]
        public void GoldLandingUsesExistingPoolAndRespectsPauseLifetimeCapacityAndReducedMotion(VfxQualityTier tier)
        {
            var previous = VfxQualityRuntime.Tier;
            host = new GameObject("GoldLandingTest");
            const string art = "Assets/MukJump/VFX/InkDropJump/Textures/";
            VfxQualityRuntime.SetTier(tier, VfxQualityChangeReason.DebugOverride);
            using var particles = new InkContactParticles(host.transform,
                AssetDatabase.LoadAssetAtPath<Texture2D>(art + "T_VFX_InkDropletAtlas_512.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(art + "T_VFX_InkSplash_512.png"),
                Resources.Load<Shader>("MukJump/Shaders/InkContactParticle"));
            try
            {
                particles.EmitLanding(Vector3.zero, 1f, true);
                var system = host.transform.Find("InkContactParticles/DryBrushMotes").GetComponent<ParticleSystem>();
                var snapshot = new ParticleSystem.Particle[40]; int count = system.GetParticles(snapshot);
                Assert.That(count, Is.EqualTo(Mathf.Min(VfxQualityRuntime.Profile.ScaleDecorativeCount(20, 8), system.main.maxParticles)));
                var color = (Color)snapshot[0].startColor;
                Assert.That(color.r, Is.GreaterThan(color.g)); Assert.That(color.g, Is.GreaterThan(color.b));
                Assert.That(snapshot[0].startSize3D.x, Is.GreaterThanOrEqualTo(.14f));
                Assert.That(snapshot[0].startLifetime, Is.GreaterThanOrEqualTo(.65f));
                var flakes = host.transform.Find("InkContactParticles/BallisticInk").GetComponent<ParticleSystem>();
                var flakeSnapshot = new ParticleSystem.Particle[48];
                int flakeCount = flakes.GetParticles(flakeSnapshot);
                Assert.That(flakeCount, Is.EqualTo(VfxQualityRuntime.Profile.ScaleDecorativeCount(14, 6)));
                for (int i = 0; i < flakeCount; i++)
                {
                    Assert.That(flakeSnapshot[i].startSize3D.x, Is.GreaterThanOrEqualTo(.24f));
                    Assert.That(flakeSnapshot[i].startLifetime, Is.GreaterThanOrEqualTo(.72f));
                    AssertRgb(flakeSnapshot[i].startColor,
                        i % 3 == 0 ? GoldInkVisual.LandingHighlight : GoldInkVisual.LandingTint);
                }
                var wash = host.transform.Find("InkContactParticles/ContactWash").GetComponent<ParticleSystem>();
                var washSnapshot = new ParticleSystem.Particle[16];
                Assert.That(wash.GetParticles(washSnapshot), Is.EqualTo(1));
                AssertRgb(washSnapshot[0].startColor, GoldInkVisual.LandingTint);
                Assert.That(washSnapshot[0].startSize3D.x, Is.EqualTo(3.4f));
                Assert.That(washSnapshot[0].startSize3D.y, Is.EqualTo(.8f));
                Assert.That(((Color)washSnapshot[0].startColor).a, Is.GreaterThan(.8f));
                float lifetime = snapshot[0].remainingLifetime;
                particles.Advance(.3f, false); system.GetParticles(snapshot);
                Assert.That(snapshot[0].remainingLifetime, Is.EqualTo(lifetime));
                for (int i = 0; i < 100; i++) particles.EmitLanding(Vector3.zero, 1, true);
                Assert.That(particles.ActiveCount, Is.LessThanOrEqualTo(particles.Capacity));
                Assert.That(host.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(3));
                for (int i = 0; i < 30; i++) particles.Advance(.05f, true);
                Assert.That(particles.ActiveCount, Is.Zero);
                particles.EmitLanding(Vector3.zero, 1f);
                Assert.That(wash.GetParticles(washSnapshot), Is.GreaterThan(0));
                AssertRgb(washSnapshot[0].startColor, InkPalette.Ink);
                particles.Clear();
                particles.EmitJump(Vector3.zero, Vector2.up, false);
                Assert.That(wash.GetParticles(washSnapshot), Is.GreaterThan(0));
                AssertRgb(washSnapshot[0].startColor, InkPalette.Ink);
                particles.Clear();
                LobbySettingsProfile.SetReducedMotionEnabled(true);
                particles.EmitLanding(Vector3.zero, 1f, true);
                Assert.That(particles.ActiveCount, Is.Zero);
            }
            finally { VfxQualityRuntime.SetTier(previous, VfxQualityChangeReason.DebugOverride); }
        }
        static void AssertRgb(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.005f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.005f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.005f));
        }
        [Test] public void GoldCardPreviewsBeforePurchaseAndOnlyPurchaseButtonOpensShop()
        {
            host = new GameObject("PremiumWardrobeTest");
            var view = host.AddComponent<InkWardrobeView>();
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Character/Player/muk_spritesheet.png").OfType<Sprite>().First(s => s.name == "idle");
            view.BuildForTests(idle); view.SetNavigationPresentation(true, true);
            int saves = settings.SaveCount;
            int requests = 0; InkPremiumAppearance.ShopRequested += () => requests++;
            view.SelectGold(); view.Presentation.Cancel();
            Assert.That(view.SelectedGold, Is.True);
            Assert.That(requests, Is.Zero);
            Assert.That(settings.SaveCount, Is.EqualTo(saves));
            var body = view.Paper.Find("ShowroomStage/Preview/OriginalInkBody").GetComponent<Image>();
            Assert.That(body.material.shader.name, Is.EqualTo("MukJump/GoldInkUi"));
            view.EquipButton.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(settings.SaveCount, Is.EqualTo(saves));
            entitlement.Owned = true;
            view.EquipButton.onClick.Invoke();
            Assert.That(InkPremiumAppearance.IsEquipped, Is.True);
            view.Presentation.Cancel(); view.StepSelection(-1); view.Presentation.Cancel();
            Assert.That(view.SelectedGold, Is.False);
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.Ink));
        }
    }
}
