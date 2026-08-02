using System.Collections.Generic;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class PermanentGrowthKeystoneViewTests
    {
        readonly List<GameObject> cleanup = new();
        MemoryPermanentGrowthStore store;

        [SetUp]
        public void SetUp()
        {
            store = new MemoryPermanentGrowthStore();
            PermanentGrowthProfile.UseStoreForTests(store);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null)
                    Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
            PermanentGrowthProfile.RestoreDefaultStoreForTests();
        }

        [Test]
        public void UnlockedNormalNode_ShowsRedFruitAndAppliedStateWithoutCost()
        {
            SeedV2(new[] { "I00" });
            PermanentGrowthView view = BuildView();

            SelectNode(view, "I00");

            Transform node = FindNode(view, "I00");
            AssertColorRgb(
                node.Find("Fruit").GetComponent<Image>().color,
                InkPalette.Red);
            Assert.That(
                node.Find("Fruit").GetComponent<Image>().color.a,
                Is.GreaterThan(0.99f));

            Transform popup = FindPopup(view);
            Assert.That(
                popup.Find("ActionCostIcon").gameObject.activeSelf,
                Is.False);
            Assert.That(
                popup.Find("ActionCost").gameObject.activeSelf,
                Is.False);
            Assert.That(PurchaseLabel(view), Is.EqualTo("적용 중"));
            Assert.That(view.PurchaseButton.interactable, Is.False);
        }

        [Test]
        public void UnlockedUnequippedKeystone_ShowsOnlyRedFruitStateMarker()
        {
            SeedV2(new[] { "S-KA" });
            PermanentGrowthView view = BuildView();

            Transform node = FindNode(view, "S-KA");
            Image fruit = node.Find("Fruit").GetComponent<Image>();
            Image selectionRing =
                node.Find("SelectionRing").GetComponent<Image>();
            Image equippedRing =
                node.Find("EquippedRing").GetComponent<Image>();
            Image completionMark =
                node.Find("CompletionMark").GetComponent<Image>();

            AssertColorRgb(fruit.color, InkPalette.Red);
            Assert.That(fruit.color.a, Is.GreaterThan(0.99f));
            Assert.That(selectionRing.color.a, Is.EqualTo(0f).Within(0.001f));
            Assert.That(equippedRing.color.a, Is.EqualTo(0f).Within(0.001f));
            Assert.That(completionMark.color.a, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void EquippedSelectedKeystone_ShowsGoldEquipAndInkSelectionRingsTogether()
        {
            SeedV2(new[] { "S-KA" }, survivalKeystoneId: "S-KA");
            PermanentGrowthView view = BuildView();

            SelectNode(view, "S-KA");

            Transform node = FindNode(view, "S-KA");
            Color equippedColor =
                node.Find("EquippedRing").GetComponent<Image>().color;
            Color selectionColor =
                node.Find("SelectionRing").GetComponent<Image>().color;

            Assert.That(view.IsNodePopupOpen, Is.True);
            AssertColorRgb(equippedColor, InkPalette.Gold);
            Assert.That(equippedColor.a, Is.GreaterThan(0.9f));
            AssertColorRgb(selectionColor, InkPalette.Ink);
            Assert.That(selectionColor.a, Is.GreaterThan(0.7f));
        }

        [Test]
        public void ReplacingBranchKeystone_RequiresSecondConfirmationClick()
        {
            SeedV2(
                new[] { "S-KA", "S-KB" },
                survivalKeystoneId: "S-KA");
            PermanentGrowthView view = BuildView();

            SelectNode(view, "S-KB");

            Assert.That(PurchaseLabel(view), Is.EqualTo("교체하기"));
            Assert.That(view.PurchaseButton.interactable, Is.True);
            Assert.That(ActiveSurvivalKeystone(), Is.EqualTo("S-KA"));

            view.PurchaseButton.onClick.Invoke();

            Assert.That(
                ActiveSurvivalKeystone(),
                Is.EqualTo("S-KA"),
                "첫 클릭은 교체 의사만 확인하고 기존 비기를 유지해야 합니다.");
            Assert.That(PurchaseLabel(view), Is.EqualTo("교체 확인"));
            Assert.That(view.PurchaseButton.interactable, Is.True);

            view.PurchaseButton.onClick.Invoke();

            Assert.That(ActiveSurvivalKeystone(), Is.EqualTo("S-KB"));
            Assert.That(PermanentGrowthProfile.IsKeystoneActive("S-KA"), Is.False);
            Assert.That(PermanentGrowthProfile.IsKeystoneActive("S-KB"), Is.True);
            Assert.That(PurchaseLabel(view), Is.EqualTo("장착 해제"));
            Assert.That(
                FindNode(view, "S-KA").Find("EquippedRing")
                    .GetComponent<Image>().color.a,
                Is.Zero.Within(0.001f));
            Assert.That(
                FindNode(view, "S-KB").Find("EquippedRing")
                    .GetComponent<Image>().color.a,
                Is.GreaterThan(0.9f));
        }

        [Test]
        public void PendingKeystoneReplacement_ClearsWhenPopupCloses()
        {
            SeedV2(
                new[] { "S-KA", "S-KB" },
                survivalKeystoneId: "S-KA");
            PermanentGrowthView view = BuildView();

            SelectNode(view, "S-KB");
            view.PurchaseButton.onClick.Invoke();
            Assert.That(PurchaseLabel(view), Is.EqualTo("교체 확인"));

            view.NodePopupCloseButton.onClick.Invoke();
            SelectNode(view, "S-KB");

            Assert.That(PurchaseLabel(view), Is.EqualTo("교체하기"));
            Assert.That(ActiveSurvivalKeystone(), Is.EqualTo("S-KA"));
        }

        [Test]
        public void PopupLongCopy_UsesReadableBestFitInsteadOfClipping()
        {
            SeedV2(new string[0]);
            PermanentGrowthView view = BuildView();
            Transform popup = FindPopup(view);
            Text description = popup.Find("ActionDescription").GetComponent<Text>();
            Text status = popup.Find("ActionStatus").GetComponent<Text>();

            Assert.That(description.resizeTextForBestFit, Is.True);
            Assert.That(description.resizeTextMinSize, Is.GreaterThanOrEqualTo(28));
            Assert.That(status.resizeTextForBestFit, Is.True);
            Assert.That(status.resizeTextMinSize, Is.GreaterThanOrEqualTo(25));

            foreach (PermanentGrowthNodeDefinition node
                     in PermanentGrowthCatalog.Nodes)
            {
                SelectNode(view, node.Id);
                Canvas.ForceUpdateCanvases();
                Assert.That(description.text, Is.Not.Empty, node.Id);
                Assert.That(status.text, Is.Not.Empty, node.Id);
            }
        }

        PermanentGrowthView BuildView()
        {
            var managerHost = Track(new GameObject("KeystoneViewManager"));
            managerHost.AddComponent<GameManager>();
            var viewHost = Track(new GameObject("KeystoneView"));
            var view = viewHost.AddComponent<PermanentGrowthView>();
            view.BuildForTests();
            view.Open();
            return view;
        }

        void SeedV2(
            string[] ownedNodeIds,
            string survivalKeystoneId = "")
        {
            string owned = ownedNodeIds == null || ownedNodeIds.Length == 0
                ? "[]"
                : "[\"" + string.Join("\",\"", ownedNodeIds) + "\"]";
            store.Json =
                "{\"schemaVersion\":1,\"balanceVersion\":2," +
                "\"wallet\":0,\"spent\":0," +
                "\"tutorialRewardClaimed\":true," +
                "\"lastSettledRunId\":\"\",\"ranks\":[]," +
                $"\"ownedNodeIds\":{owned}," +
                $"\"survivalKeystoneId\":\"{survivalKeystoneId}\"," +
                "\"leapKeystoneId\":\"\"," +
                "\"inkHandlingKeystoneId\":\"\"}";
            PermanentGrowthProfile.ResetCacheForTests();
            _ = PermanentGrowthProfile.Currency;
        }

        GameObject Track(GameObject gameObject)
        {
            cleanup.Add(gameObject);
            return gameObject;
        }

        static Transform FindNode(PermanentGrowthView view, string nodeId)
        {
            Transform node = view.TreeCanvas.Find($"GrowthNode_{nodeId}");
            Assert.That(node, Is.Not.Null, nodeId);
            return node;
        }

        static void SelectNode(PermanentGrowthView view, string nodeId)
        {
            Transform node = FindNode(view, nodeId);
            Button button = node.GetComponent<Button>();
            Image hitSurface = node.GetComponent<Image>();
            Assert.That(button, Is.Not.Null, nodeId);
            Assert.That(button.interactable, Is.True, nodeId);
            Assert.That(hitSurface, Is.Not.Null, nodeId);
            Assert.That(hitSurface.raycastTarget, Is.True, nodeId);
            button.onClick.Invoke();
            Assert.That(view.SelectedNodeId, Is.EqualTo(nodeId));
            Assert.That(view.IsNodePopupOpen, Is.True);
        }

        static Transform FindPopup(PermanentGrowthView view)
        {
            Transform popup = view.ScreenRoot.Find(
                "SafeAreaRoot/PermanentGrowthScreen/SelectedGrowthAction");
            Assert.That(popup, Is.Not.Null);
            return popup;
        }

        static string PurchaseLabel(PermanentGrowthView view) =>
            view.PurchaseButton.GetComponentInChildren<Text>(true)?.text;

        static string ActiveSurvivalKeystone() =>
            PermanentGrowthProfile.GetActiveKeystoneId(
                PermanentGrowthBranch.Survival);

        static void AssertColorRgb(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        }
    }
}
