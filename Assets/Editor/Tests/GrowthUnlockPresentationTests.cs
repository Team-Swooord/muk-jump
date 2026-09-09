using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class GrowthUnlockPresentationTests
    {
        GameObject root;
        InkUiFeedbackController previousFeedback;

        [SetUp]
        public void SetUp()
        {
            previousFeedback = InkUiFeedbackController.Instance;
            typeof(InkUiFeedbackController).GetField("<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null && root.TryGetComponent<InkUiFeedbackController>(out var controller))
                InvokeLifecycle(controller, "OnDisable");
            if (root != null)
                Object.DestroyImmediate(root);
            typeof(InkUiFeedbackController).GetField("<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousFeedback);
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
            VfxQualityRuntime.SetTier(
                VfxQualityTier.Medium,
                VfxQualityChangeReason.DebugOverride);
        }

        [Test]
        public void UnlockOverlay_먹획과_중앙표식을_한번만_만들고_입력을_가리지_않는다()
        {
            GrowthUnlockPresentation view = CreateView();
            int childCount = view.PresentationRoot.childCount;
            Sprite growthIcon = Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_icon_capacity");
            Assert.That(growthIcon, Is.Not.Null);

            view.Play("먹그릇", growthIcon);
            view.EvaluateForTests(0.5f);
            view.Play("숨고르기", growthIcon);
            view.EvaluateForTests(0.5f);

            Assert.That(view.PresentationRoot.childCount, Is.EqualTo(childCount));
            Assert.That(view.PresentationGroup.blocksRaycasts, Is.False);
            Assert.That(view.PresentationGroup.interactable, Is.False);
            Assert.That(view.Title, Is.EqualTo("힘이 자랐어요"));
            Assert.That(view.UnlockedIcon, Is.SameAs(growthIcon));
            var iconPlate = view.PresentationRoot
                .Find("UnlockedIconPlate")
                ?.GetComponent<Image>();
            var iconImage = view.PresentationRoot
                .Find("UnlockedGrowthIcon")
                ?.GetComponent<Image>();
            Assert.That(iconPlate, Is.Not.Null);
            Assert.That(iconPlate.color.a, Is.GreaterThan(0.9f));
            Assert.That(iconImage, Is.Not.Null);
            Assert.That(iconImage.color, Is.EqualTo(Color.white));
            Assert.That(
                view.Subtitle,
                Does.Contain("숨고르기"));
            Assert.That(
                view.PresentationRoot.Find("UpperDiagonalBrush"),
                Is.Not.Null);
            Assert.That(
                view.PresentationRoot.Find("LowerDiagonalBrush"),
                Is.Not.Null);
            var veil = view.PresentationRoot
                .Find("InkVeil")
                ?.GetComponent<RectTransform>();
            Assert.That(veil, Is.Not.Null);
            Assert.That(veil.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(veil.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(
                view.PresentationRoot
                    .GetComponentsInChildren<Graphic>(true)
                    .All(graphic => !graphic.raycastTarget),
                Is.True);
        }

        [Test]
        public void UnlockOverlay_저사양에서도_핵심은_유지하고_장식만_줄인다()
        {
            GrowthUnlockPresentation view = CreateView();
            VfxQualityRuntime.SetTier(
                VfxQualityTier.Low,
                VfxQualityChangeReason.DebugOverride);
            view.Play("먹그릇");
            int lowCount = view.ActiveDecorativeDropCount;

            VfxQualityRuntime.SetTier(
                VfxQualityTier.High,
                VfxQualityChangeReason.DebugOverride);
            view.Play("먹그릇");
            int highCount = view.ActiveDecorativeDropCount;

            Assert.That(lowCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(lowCount, Is.LessThan(highCount));
            Assert.That(highCount, Is.EqualTo(8));
            Assert.That(
                view.PresentationRoot.Find("UnlockInkSplash"),
                Is.Not.Null);
            Assert.That(
                view.PresentationRoot.Find("UnlockTitle"),
                Is.Not.Null);
        }

        [Test]
        public void UnlockOverlay_선택노드에서_붉은열매가_피어나고_계층을_재사용한다()
        {
            GrowthUnlockPresentation view = CreateView();
            Sprite growthIcon = Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_icon_capacity");
            Sprite fruitSprite = Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_node_bloom_mask");
            var firstPosition = new Vector2(-214f, 326f);
            int childCount = view.PresentationRoot.childCount;

            view.PlayAtNode(
                "먹그릇",
                growthIcon,
                firstPosition,
                fruitSprite);
            view.EvaluateForTests(0.5f);

            Assert.That(view.HasNodeFeedback, Is.True);
            Assert.That(view.NodeFeedbackPosition, Is.EqualTo(firstPosition));
            Assert.That(view.NodeFruitSprite, Is.SameAs(fruitSprite));
            Assert.That(
                view.NodeFruitColor.r,
                Is.EqualTo(InkPalette.Red.r).Within(0.001f));
            Assert.That(
                view.NodeFruitColor.a,
                Is.GreaterThan(0.8f));
            Assert.That(
                view.PresentationRoot.Find(
                    "NodeFruitFeedback/FruitGlow"),
                Is.Not.Null);
            Assert.That(
                view.ActiveNodeDropCount,
                Is.LessThanOrEqualTo(4));
            for (int i = 0; i < view.ActiveNodeDropCount; i++)
            {
                Image drop = view.PresentationRoot
                    .Find($"NodeFruitFeedback/FruitDrop{i + 1:00}")
                    ?.GetComponent<Image>();
                Assert.That(drop, Is.Not.Null);
                Assert.That(
                    drop.color.r,
                    Is.EqualTo(InkPalette.Ink.r).Within(0.001f));
                Assert.That(
                    drop.color.g,
                    Is.EqualTo(InkPalette.Ink.g).Within(0.001f));
                Assert.That(
                    drop.color.b,
                    Is.EqualTo(InkPalette.Ink.b).Within(0.001f));
            }

            var secondPosition = new Vector2(208f, -144f);
            view.PlayAtNode(
                "숨고르기",
                growthIcon,
                secondPosition,
                fruitSprite);

            Assert.That(
                view.PresentationRoot.childCount,
                Is.EqualTo(childCount));
            Assert.That(view.NodeFeedbackPosition, Is.EqualTo(secondPosition));
        }

        [Test]
        public void UpgradeOverlay_같은계층을_압축재생하고_숫자단계를_숨긴다()
        {
            GrowthUnlockPresentation view = CreateView();
            Sprite growthIcon = Resources.Load<Sprite>(
                "MukJump/UI/PermanentGrowth/pg_icon_capacity");
            int childCount = view.PresentationRoot.childCount;

            view.PlayUpgrade("먹그릇", growthIcon, 3);
            view.EvaluateForTests(0.42f);

            Assert.That(view.IsPlaying, Is.True);
            Assert.That(
                view.ActiveSequenceDuration,
                Is.EqualTo(
                    GrowthUnlockPresentation.UpgradeSequenceDuration)
                    .Within(0.001f));
            Assert.That(view.Title, Is.EqualTo("힘이 자랐어요"));
            Assert.That(view.Subtitle, Does.Contain("먹그릇"));
            Assert.That(view.Subtitle, Does.Not.Contain("Lv."));
            Assert.That(
                view.PresentationRoot.Find("LockedInkPlate/LockedLabel")
                    ?.GetComponent<Text>()
                    ?.text,
                Is.EqualTo("더 자람"));
            Assert.That(
                view.PresentationRoot.childCount,
                Is.EqualTo(childCount));

            view.EvaluateForTests(
                GrowthUnlockPresentation.UpgradeSequenceDuration);

            Assert.That(view.IsPlaying, Is.False);
            Assert.That(view.PresentationGroup.alpha, Is.Zero);
        }

        [Test]
        public void UnlockOverlay_해금충격뒤_정확히_정리된다()
        {
            GrowthUnlockPresentation view = CreateView();
            view.Play("먹결");
            view.EvaluateForTests(0.5f);

            var splash = view.PresentationRoot
                .Find("UnlockInkSplash")
                ?.GetComponent<Image>();
            var lockPlate = view.PresentationRoot
                .Find("LockedInkPlate")
                ?.GetComponent<Image>();
            Assert.That(view.IsPlaying, Is.True);
            Assert.That(splash, Is.Not.Null);
            Assert.That(splash.color.a, Is.GreaterThan(0.5f));
            Assert.That(lockPlate, Is.Not.Null);
            Assert.That(lockPlate.color.a, Is.LessThan(0.01f));

            view.EvaluateForTests(
                GrowthUnlockPresentation.SequenceDuration);

            Assert.That(view.IsPlaying, Is.False);
            Assert.That(view.PresentationGroup.alpha, Is.Zero);
            Assert.That(view.PresentationGroup.blocksRaycasts, Is.False);
        }

        [Test]
        public void FeedbackController_첫해금_호출을_같은_프레젠테이션으로_재사용한다()
        {
            root = new GameObject("GrowthUnlockFeedbackTests");
            root.AddComponent<InkUiFeedbackController>();

            InkUiFeedbackController.PlayGrowthUnlock(
                "발놀림",
                null);
            GrowthUnlockPresentation presentation =
                root.GetComponent<GrowthUnlockPresentation>();
            Assert.That(presentation, Is.Not.Null);
            RectTransform firstRoot = presentation.PresentationRoot;
            int childCount = firstRoot.childCount;

            InkUiFeedbackController.PlayGrowthUnlock(
                "먹그릇",
                null);

            Assert.That(
                presentation.PresentationRoot,
                Is.SameAs(firstRoot));
            Assert.That(
                presentation.PresentationRoot.childCount,
                Is.EqualTo(childCount));
            Assert.That(presentation.IsPlaying, Is.True);
            Assert.That(presentation.Subtitle, Does.Contain("먹그릇"));
        }

        [Test]
        public void PermanentGrowth_성공한_모든강화가_재사용연출을_호출한다()
        {
            PermanentGrowthProfile.UseStoreForTests(new MemoryPermanentGrowthStore());
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            PermanentGrowthProfile.DebugRefillCurrency();
            root = new GameObject("GrowthBloomIntegrationTests");
            var managerHost = new GameObject("Manager");
            managerHost.transform.SetParent(root.transform, false);
            managerHost.AddComponent<GameManager>();
            managerHost.AddComponent<InkUiFeedbackController>();
            var viewHost = new GameObject("GrowthView");
            viewHost.transform.SetParent(root.transform, false);
            var growthView = viewHost.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();
            var presentation = viewHost.GetComponent<GrowthBloomPresentation>();
            Assert.That(presentation, Is.Not.Null);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var bloom = (RectTransform)typeof(GrowthBloomPresentation).GetField("root", flags).GetValue(presentation);
            int count = bloom.GetComponentsInChildren<Transform>(true).Length;
            try
            {
                foreach (string key in new[] { "brush.1", "ink.1", "ink.2" })
                {
                    presentation.Cancel();
                    typeof(PermanentGrowthView).GetField("purchaseLockedUntil", flags).SetValue(growthView, -1f);
                    typeof(PermanentGrowthView).GetMethod("Update", flags).Invoke(growthView, null);
                    growthView.SelectGrowthForTests(key);
                    Assert.That(growthView.PurchaseButton.interactable, Is.True);
                    growthView.PurchaseButton.onClick.Invoke();
                    var type = key.StartsWith("brush") ? PermanentGrowthType.InkBudgetEfficiency : PermanentGrowthType.InkCapacity;
                    int expectedLevel = key.EndsWith("2") ? 2 : 1;
                    Assert.That(PermanentGrowthProfile.GetLevel(type), Is.EqualTo(expectedLevel));
                    Assert.That(presentation.IsPlaying, Is.True);
                    Assert.That(bloom.gameObject.activeSelf, Is.True);
                    Assert.That(bloom.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count), "먹고리·금박 풀을 재사용합니다.");
                    Assert.That(growthView.PurchaseButton.interactable, Is.False);
                    Assert.That(growthView.BackButton.interactable, Is.False);
                    growthView.PurchaseButton.onClick.Invoke();
                    Assert.That(PermanentGrowthProfile.GetLevel(type), Is.EqualTo(expectedLevel), "연출 중 중복 구매를 막습니다.");
                    float lockedUntil = (float)typeof(PermanentGrowthView).GetField("purchaseLockedUntil", flags).GetValue(growthView);
                    Assert.That(lockedUntil - Time.unscaledTime, Is.GreaterThanOrEqualTo(GrowthBloomPresentation.Duration - .05f));
                }
                Assert.That(managerHost.GetComponent<GrowthUnlockPresentation>(), Is.Null, "폐기한 전체 화면 해금 연출을 다시 만들지 않습니다.");
                growthView.Close();
                Assert.That(presentation.IsPlaying, Is.False);
                Assert.That(bloom.gameObject.activeSelf, Is.False);
            }
            finally { LobbySettingsProfile.RestoreDefaultStoreForTests(); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReloadedDropArraysRebindExistingUiInDayAndNight(bool night)
        {
            WithNightState(night, () =>
            {
                var view = CreateView();
                view.PlayAtNode("먹결", null, Vector2.one, null);
                view.EvaluateForTests(.5f);
                var original = view.PresentationRoot;
                string[] order = original.Cast<Transform>().Select(child => child.name).ToArray();
                foreach (string name in new[] { "drops", "dropDirections", "dropDistances", "dropDelays",
                    "dropRotations", "nodeDrops", "nodeDropDirections", "nodeDropRotations" })
                {
                    var array = (System.Array)ReadField(view, name);
                    System.Array.Clear(array, 0, array.Length);
                }
                Assert.DoesNotThrow(() => InvokeLifecycle(view, "OnEnable"));
                Assert.That(view.IsPlaying, Is.False);
                Assert.That(view.PresentationGroup.alpha, Is.Zero);
                Assert.DoesNotThrow(() => view.PlayAtNode("먹결", null, Vector2.one, null));
                Assert.DoesNotThrow(() => view.EvaluateForTests(.5f));
                Assert.That(view.IsPlaying, Is.True);
                Assert.That(view.PresentationRoot, Is.SameAs(original));
                Assert.That(original.Cast<Transform>().Select(child => child.name).ToArray(), Is.EqualTo(order));
                Assert.That(LobbyNightState.TargetNight, Is.EqualTo(night));
                Assert.That(LobbyNightState.Progress, Is.EqualTo(night ? 1f : 0f));
            });
        }

        [Test]
        public void LostUiFieldsReuseNamedHierarchyInsteadOfDuplicatingOverlay()
        {
            var view = CreateView();
            var original = view.PresentationRoot;
            int descendants = original.GetComponentsInChildren<Transform>(true).Length;
            foreach (var field in typeof(GrowthUnlockPresentation).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (field.Name != "canvasOwner" && typeof(Object).IsAssignableFrom(field.FieldType))
                    field.SetValue(view, null);
            Assert.DoesNotThrow(() => view.Initialize(root.GetComponent<RectTransform>()));
            view.Play("복구");
            view.EvaluateForTests(.5f);
            Assert.That(view.PresentationRoot, Is.SameAs(original));
            Assert.That(original.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(descendants));
            Assert.That(root.transform.Cast<Transform>().Count(child => child.name == "GrowthUnlockPresentation"), Is.EqualTo(1));
        }

        [TestCase("UnlockDrop01", true)]
        [TestCase("NodeFruitFeedback/FruitDrop01", true)]
        [TestCase("InkVeil", true)]
        [TestCase("UpperDiagonalBrush", false)]
        [TestCase("CanvasGroup", false)]
        [TestCase("EntireOverlay", true)]
        public void DamagedOverlayCancelsWithoutGhostsAndRepairsNextPlay(string part, bool destroyObject)
        {
            var view = CreateView();
            view.PlayAtNode("먹결", null, Vector2.zero, null);
            view.EvaluateForTests(.5f);
            string[] order = view.PresentationRoot.Cast<Transform>().Select(child => child.name).ToArray();
            if (part == "CanvasGroup") Object.DestroyImmediate(view.PresentationGroup);
            else if (part == "EntireOverlay") Object.DestroyImmediate(view.PresentationRoot.gameObject);
            else
            {
                var target = view.PresentationRoot.Find(part);
                Object.DestroyImmediate(destroyObject ? target.gameObject : (Object)target.GetComponent<Image>());
            }
            Assert.DoesNotThrow(() => InvokeLifecycle(view, "Update"));
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(view.PresentationRoot == null || !view.PresentationRoot.gameObject.activeSelf
                || (view.PresentationGroup != null && view.PresentationGroup.alpha == 0f), Is.True);
            Assert.DoesNotThrow(() => view.PlayAtNode("다시 성장", null, Vector2.zero, null));
            Assert.DoesNotThrow(() => view.EvaluateForTests(.5f));
            Assert.That(view.IsPlaying, Is.True);
            Assert.That(view.PresentationRoot.gameObject.activeSelf, Is.True);
            Assert.That(view.PresentationGroup.blocksRaycasts, Is.False);
            Assert.That(view.PresentationRoot.Cast<Transform>().Select(child => child.name).ToArray(), Is.EqualTo(order));
            Assert.That(view.PresentationRoot.Find("InkVeil").GetSiblingIndex(), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TapPoolRebindsAfterManagedCacheLossWithoutDuplicateUi(bool night)
        {
            WithNightState(night, () =>
            {
                root = new GameObject("TapPoolReloadTests");
                var controller = root.AddComponent<InkUiFeedbackController>();
                InvokeLifecycle(controller, "OnEnable");
                var original = root.transform.Find("InkUiFeedbackCanvas");
                int descendants = original.GetComponentsInChildren<Transform>(true).Length;
                var marks = (System.Array)ReadField(controller, "marks");
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    System.Array.Clear(marks, 0, marks.Length);
                    WriteField(controller, "canvasRoot", null);
                    WriteField(controller, "unlockPresentation", null);
                    Assert.DoesNotThrow(() => InvokeLifecycle(controller, "OnEnable"));
                    Assert.That(controller.ActiveMarkCount, Is.Zero);
                    Assert.DoesNotThrow(() => InkUiFeedbackController.PlayTap(Vector2.zero));
                    Assert.That(controller.ActiveMarkCount, Is.EqualTo(6));
                    Assert.That(root.transform.Find("InkUiFeedbackCanvas"), Is.SameAs(original));
                    Assert.That(original.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(descendants));
                    Assert.That(root.transform.Cast<Transform>().Count(child => child.name == "InkUiFeedbackCanvas"), Is.EqualTo(1));
                }
                Assert.That(LobbyNightState.TargetNight, Is.EqualTo(night));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DestroyedTapSlotIsSafeToUpdateAndDisableThenRepairs(bool destroyObject)
        {
            root = new GameObject("TapPoolDamageTests");
            var controller = root.AddComponent<InkUiFeedbackController>();
            InkUiFeedbackController.PlayTap(Vector2.zero);
            var canvas = root.transform.Find("InkUiFeedbackCanvas");
            var target = canvas.Find("InkTapMark01");
            Object.DestroyImmediate(destroyObject ? target.gameObject : (Object)target.GetComponent<Image>());
            Assert.DoesNotThrow(() => InvokeLifecycle(controller, "Update"));
            Assert.That(controller.ActiveMarkCount, Is.EqualTo(5));
            Assert.DoesNotThrow(() => controller.enabled = false);
            Assert.DoesNotThrow(() => InvokeLifecycle(controller, "OnDisable"));
            Assert.DoesNotThrow(() => controller.enabled = true);
            Assert.DoesNotThrow(() => InvokeLifecycle(controller, "OnEnable"));
            Assert.DoesNotThrow(() => InkUiFeedbackController.PlayTap(Vector2.zero));
            Assert.That(controller.ActiveMarkCount, Is.EqualTo(6));
            Assert.That(canvas.Cast<Transform>().Count(child => child.name.StartsWith("InkTapMark")), Is.EqualTo(32));
            Assert.That(canvas.Find("InkTapMark01").GetComponent<Image>(), Is.Not.Null);
        }

        [Test]
        public void NormalTapDoesNotCancelAnActiveGrowthSequence()
        {
            root = new GameObject("TapDuringGrowthTests");
            root.AddComponent<InkUiFeedbackController>();
            InkUiFeedbackController.PlayGrowthUnlock("먹결", null);
            var view = root.GetComponent<GrowthUnlockPresentation>();
            view.EvaluateForTests(.5f);
            Assert.DoesNotThrow(() => InkUiFeedbackController.PlayTap(Vector2.zero));
            Assert.That(view.IsPlaying, Is.True);
            Assert.That(view.PresentationGroup.alpha, Is.EqualTo(1f));
            InkUiFeedbackController.CancelGrowthPresentation();
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(view.PresentationGroup.alpha, Is.Zero);
        }

        static object ReadField(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static void WriteField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void InvokeLifecycle(object target, string name) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        static void WithNightState(bool night, System.Action action)
        {
            float oldProgress = LobbyNightState.Progress, oldHold = LobbyNightState.DepartureHold;
            bool oldNight = LobbyNightState.TargetNight, oldInitialized = LobbyNightState.Initialized;
            var set = typeof(LobbyNightState).GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic);
            float oldTravel = LobbyNightState.CelestialTravel, oldVelocity = LobbyNightState.CelestialVelocity;
            set.Invoke(null, new object[] { night ? 1f : 0f, night, 0f, 0f, 0f });
            try { action(); }
            finally
            {
                set.Invoke(null, new object[] { oldProgress, oldNight, oldHold, oldTravel, oldVelocity });
                typeof(LobbyNightState).GetProperty(nameof(LobbyNightState.Initialized))
                    .GetSetMethod(true).Invoke(null, new object[] { oldInitialized });
            }
        }

        GrowthUnlockPresentation CreateView()
        {
            root = new GameObject(
                "GrowthUnlockPresentationTests",
                typeof(RectTransform));
            var host = new GameObject("PresentationHost");
            host.transform.SetParent(root.transform, false);
            var view = host.AddComponent<GrowthUnlockPresentation>();
            view.Initialize(root.GetComponent<RectTransform>());
            return view;
        }
    }
}
