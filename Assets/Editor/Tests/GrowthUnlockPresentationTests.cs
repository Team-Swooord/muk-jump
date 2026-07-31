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

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
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
            Assert.That(view.Title, Is.EqualTo("성장 해금"));
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
        public void UpgradeOverlay_같은계층을_압축재생하고_강화단계를_표시한다()
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
            Assert.That(view.Title, Is.EqualTo("성장 강화"));
            Assert.That(view.Subtitle, Does.Contain("먹그릇"));
            Assert.That(view.Subtitle, Does.Contain("Lv. 3"));
            Assert.That(
                view.PresentationRoot.Find("LockedInkPlate/LockedLabel")
                    ?.GetComponent<Text>()
                    ?.text,
                Is.EqualTo("Lv. 3"));
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
            var store = new MemoryPermanentGrowthStore
            {
                Json =
                    "{\"schemaVersion\":1,\"balanceVersion\":1," +
                    "\"wallet\":100,\"spent\":0," +
                    "\"tutorialRewardClaimed\":false," +
                    "\"lastSettledRunId\":\"\",\"ranks\":[]}",
            };
            PermanentGrowthProfile.UseStoreForTests(store);

            root = new GameObject("GrowthUnlockIntegrationTests");
            var managerHost = new GameObject("Manager");
            managerHost.transform.SetParent(root.transform, false);
            managerHost.AddComponent<GameManager>();
            managerHost.AddComponent<InkUiFeedbackController>();
            var viewHost = new GameObject("GrowthView");
            viewHost.transform.SetParent(root.transform, false);
            var growthView =
                viewHost.AddComponent<PermanentGrowthView>();
            growthView.BuildForTests();

            growthView.SelectGrowthForTests(0);
            growthView.PurchaseButton.onClick.Invoke();

            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkCapacity),
                Is.EqualTo(1));
            GrowthUnlockPresentation presentation =
                managerHost.GetComponent<GrowthUnlockPresentation>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.IsPlaying, Is.True);
            Assert.That(presentation.Title, Is.EqualTo("성장 해금"));
            Assert.That(presentation.Subtitle, Does.Contain("먹그릇"));
            Assert.That(presentation.HasNodeFeedback, Is.True);
            Assert.That(
                presentation.NodeFruitSprite?.name,
                Does.StartWith("pg_node_bloom_mask"));
            Assert.That(growthView.PurchaseButton.interactable, Is.False);
            Assert.That(growthView.BackButton.interactable, Is.False);
            RectTransform reusedRoot = presentation.PresentationRoot;
            int reusedChildCount = reusedRoot.childCount;

            growthView.PurchaseButton.onClick.Invoke();
            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkCapacity),
                Is.EqualTo(1),
                "전체 화면 해금 연출 중에는 연속 구매가 겹치면 안 됩니다.");
            float lockedUntil = (float)typeof(PermanentGrowthView)
                .GetField(
                    "purchaseLockedUntil",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(growthView);
            Assert.That(
                lockedUntil - Time.unscaledTime,
                Is.GreaterThanOrEqualTo(
                    GrowthUnlockPresentation.SequenceDuration - 0.05f));

            presentation.ResetPresentation();
            typeof(PermanentGrowthView)
                .GetField(
                    "purchaseLockedUntil",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(growthView, -1f);
            typeof(PermanentGrowthView)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(growthView, null);
            Assert.That(growthView.PurchaseButton.interactable, Is.True);
            Assert.That(growthView.BackButton.interactable, Is.True);
            growthView.PurchaseButton.onClick.Invoke();

            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkCapacity),
                Is.EqualTo(2));
            Assert.That(
                presentation.IsPlaying,
                Is.True,
                "2단계 이후에도 같은 먹획 강화 연출을 압축 재생해야 합니다.");
            Assert.That(presentation.Title, Is.EqualTo("성장 강화"));
            Assert.That(presentation.Subtitle, Does.Contain("Lv. 2"));
            Assert.That(
                presentation.HasNodeFeedback,
                Is.False,
                "같은 열매의 반복 강화에서는 새 열매 개화가 다시 나오면 안 됩니다.");
            Assert.That(
                presentation.PresentationRoot,
                Is.SameAs(reusedRoot));
            Assert.That(
                presentation.PresentationRoot.childCount,
                Is.EqualTo(reusedChildCount));
            lockedUntil = (float)typeof(PermanentGrowthView)
                .GetField(
                    "purchaseLockedUntil",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(growthView);
            Assert.That(
                lockedUntil - Time.unscaledTime,
                Is.GreaterThanOrEqualTo(
                    GrowthUnlockPresentation.UpgradeSequenceDuration -
                    0.05f));

            typeof(PermanentGrowthView)
                .GetField(
                    "purchaseLockedUntil",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(growthView, -1f);
            typeof(PermanentGrowthView)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(growthView, null);
            growthView.SelectGrowthForTests(1);
            growthView.PurchaseButton.onClick.Invoke();

            Assert.That(
                PermanentGrowthProfile.GetLevel(
                    PermanentGrowthType.InkRecovery),
                Is.EqualTo(1));
            Assert.That(
                presentation.IsPlaying,
                Is.True,
                "다른 계보도 0→1일 때는 각자 전체 화면 해금 연출을 사용해야 합니다.");
            Assert.That(presentation.HasNodeFeedback, Is.True);

            Assert.That(
                InkUiFeedbackController.Instance,
                Is.Not.Null,
                "성장 화면이 닫힐 때 기존 피드백 컨트롤러를 찾아야 합니다.");
            Assert.That(
                managerHost.GetComponent<InkUiFeedbackController>(),
                Is.SameAs(InkUiFeedbackController.Instance));
            growthView.Close();
            Assert.That(presentation.IsPlaying, Is.False);
            Assert.That(presentation.HasNodeFeedback, Is.False);
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
