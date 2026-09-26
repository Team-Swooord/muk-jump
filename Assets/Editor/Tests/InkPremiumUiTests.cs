using System.Linq;
using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MukJump.EditorTests
{
    public sealed class InkPremiumUiTests
    {
        public sealed class Store : IInkPremiumStore
        {
            public bool Owned, Busy;
            public int Purchases, Restores;
            public string Message = "";
            public string Price = "$2.99";
            public bool IsOwned => Owned;
            public bool IsBusy => Busy;
            public bool CanPurchase => !Owned && !Busy;
            public string LocalizedPrice => Price;
            public string StatusMessage => Message;
            public void Purchase() { Purchases++; Busy = true; }
            public void Restore() { Restores++; Busy = true; Message = "구입을 복원하고 있어요"; }
        }
        GameObject host;
        Store store;
        GameLanguage previousLanguage;
        static void Invoke(object target, string method) => target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        [SetUp] public void SetUp()
        {
            previousLanguage = GameLocalization.Language;
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            store = new Store(); InkPremiumAppearance.UseForTests(true, store);
        }
        [TearDown] public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            typeof(GoldInkVisual).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            InkPremiumAppearance.RestoreForTests();
            GameLocalization.SetLanguage(previousLanguage);
            LobbySettingsProfile.RestoreDefaultStoreForTests();
        }
        [TestCase(GameLanguage.Korean)] [TestCase(GameLanguage.English)] [TestCase(GameLanguage.Japanese)]
        public void PurchaseIsSinglePrimaryActionWithRealPriceAndNoRestoreButton(GameLanguage language)
        {
            GameLocalization.SetLanguage(language);
            host = new GameObject("Premium UI test");
            var shop = host.AddComponent<InkPremiumShopView>();
            Invoke(shop, "Build"); Invoke(shop, "Refresh");
            var paper = host.transform.Find("PremiumShopCanvas/SafeArea/PremiumPaper");
            Assert.That(paper.Find("Restore"), Is.Null);
            var purchase = paper.Find("Purchase").GetComponent<Button>();
            Assert.That(purchase.GetComponentInChildren<Text>().text, Does.Contain("$2.99"));
            foreach (var label in paper.GetComponentsInChildren<Text>())
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name);
            purchase.onClick.Invoke(); purchase.onClick.Invoke();
            Assert.That(store.Purchases, Is.EqualTo(1)); Assert.That(store.Restores, Is.Zero);
            store.Busy = false; store.Owned = true; Invoke(shop, "Refresh");
            Assert.That(purchase.interactable, Is.False);
        }
        [Test] public void OptionsRestoreUsesStoreOnlyAndTracksPendingAndResult()
        {
            host = new GameObject("Restore UI test");
            host.SetActive(false);
            var view = host.AddComponent<LobbyOptionsView>(); view.BuildForPlatformForTests(RuntimePlatform.IPhonePlayer);
            var button = host.GetComponentsInChildren<Button>(true).Single(b => b.name == "RestorePurchasesButton");
            Invoke(view, "RefreshPremiumRestore"); button.onClick.Invoke(); button.onClick.Invoke();
            Assert.That(store.Restores, Is.EqualTo(1)); Assert.That(store.Purchases, Is.Zero);
            Assert.That(store.Owned, Is.False); Assert.That(button.interactable, Is.False);
            var status = host.GetComponentsInChildren<Text>(true).Single(t => t.name == "ConnectionStatus");
            Assert.That(status.text, Is.EqualTo(GameLocalization.Translate("구입을 복원하고 있어요")));
            store.Busy = false; store.Message = "구입 복원을 완료하지 못했어요";
            Invoke(view, "RefreshPremiumRestore");
            Assert.That(button.interactable, Is.True);
            Assert.That(status.text, Is.EqualTo(GameLocalization.Translate(store.Message)));
            Assert.That(store.Owned, Is.False);
            InkPremiumAppearance.UseForTests(false, store); Invoke(view, "RefreshPremiumRestore");
            button.onClick.Invoke(); Assert.That(store.Restores, Is.EqualTo(1));
            Assert.That(button.gameObject.activeSelf, Is.False);
            Object.DestroyImmediate(host);
            host = new GameObject("Toss restore boundary"); host.SetActive(false);
            view = host.AddComponent<LobbyOptionsView>();
            view.BuildForPlatformForTests(RuntimePlatform.WebGLPlayer);
            Assert.That(host.GetComponentsInChildren<Button>(true).Any(b => b.name == "RestorePurchasesButton"), Is.False);
        }
        [Test] public void SharedGoldMaterialsRespectReducedMotionAndCompile()
        {
            typeof(GoldInkVisual).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var world = GoldInkVisual.World; var ui = GoldInkVisual.Ui;
            Assert.That(ShaderUtil.ShaderHasError(world.shader), Is.False);
            Assert.That(ShaderUtil.ShaderHasError(ui.shader), Is.False);
            LobbySettingsProfile.SetReducedMotionEnabled(true);
            Assert.That(world.GetFloat("_GoldShimmer"), Is.Zero);
            Assert.That(ui.GetFloat("_GoldShimmer"), Is.Zero);
            LobbySettingsProfile.SetReducedMotionEnabled(false);
            Assert.That(world.GetFloat("_GoldShimmer"), Is.EqualTo(1));
            Assert.That(GoldInkVisual.World, Is.SameAs(world)); Assert.That(GoldInkVisual.Ui, Is.SameAs(ui));
        }
        [Test] public void GoldShimmerChangesBodyButNotEyesOrAlpha()
        {
            var source = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var material = new Material(GoldInkVisual.Ui);
            var target = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var oldTarget = RenderTexture.active;
            var oldTime = Shader.GetGlobalVector("_Time");
            try
            {
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                    source.SetPixel(x, y, x < 8 ? Color.black : x < 16 ? Color.white : x < 24 ? new Color(.12f, .12f, .12f, 1) : Color.clear);
                source.Apply(); material.SetFloat("_GoldShimmer", 1);
                Color[] Render(float time)
                {
                    Shader.SetGlobalVector("_Time", new Vector4(time / 20, time, time * 2, time * 3));
                    RenderTexture.active = target; GL.Clear(true, true, Color.clear);
                    Graphics.Blit(source, target, material);
                    pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); pixels.Apply();
                    return pixels.GetPixels();
                }
                var first = Render(0.95f); var later = Render(2.85f);
                int row = 16 * 32;
                Assert.That(first[row + 20].a, Is.GreaterThan(.9f), "몸체가 실제 렌더되어야 한다");
                Assert.That(Mathf.Abs(first[row + 20].g - later[row + 20].g), Is.GreaterThan(.02f));
                foreach (int x in new[] { 4, 12, 28 }) Assert.That(later[row + x], Is.EqualTo(first[row + x]));
                for (int x = 0; x < 32; x++) Assert.That(later[row + x].a, Is.EqualTo(first[row + x].a));
                material.SetFloat("_GoldShimmer", 0);
                var reducedFirst = Render(.95f); var reducedLater = Render(2.85f);
                Assert.That(reducedLater, Is.EqualTo(reducedFirst), "움직임 줄이기는 정적인 금색을 유지한다");
            }
            finally
            {
                Shader.SetGlobalVector("_Time", oldTime); RenderTexture.active = oldTarget;
                Object.DestroyImmediate(source); Object.DestroyImmediate(material);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
