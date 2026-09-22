using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class TossPopupLayoutTests
    {
        [TestCase(360, 640, 820, 1640)]
        [TestCase(390, 844, 940, 940)]
        [TestCase(412, 915, 760, 680)]
        [TestCase(375, 667, 840, 1200)]
        [TestCase(430, 932, 940, 1560)]
        public void WebPopupsLeaveOutsideTapSpace(int width, int height, int panelWidth, int panelHeight)
        {
            var design = new Vector2(panelWidth, panelHeight);
            var safe = new Rect(0, 24, width, height - 92);
            float scale = MobileUiLayout.CalculatePopupFitScale(design, safe, width, height,
                Vector2.one * 24, RuntimePlatform.WebGLPlayer);
            Vector2 logical = MobileUiLayout.GetLogicalSafeSize(safe, width, height);
            Assert.That(scale, Is.GreaterThan(0));
            Assert.That(scale, Is.LessThanOrEqualTo(.9f));
            Assert.That(panelWidth * scale, Is.LessThanOrEqualTo(logical.x * .86f + .01f));
            Assert.That(panelHeight * scale, Is.LessThanOrEqualTo(logical.y * .84f + .01f));
            Assert.That(scale, Is.LessThan(MobileUiLayout.CalculateFitScale(design, safe, width, height, Vector2.one * 24)));
        }

        [TestCase(RuntimePlatform.IPhonePlayer)]
        [TestCase(RuntimePlatform.Android)]
        public void NativePopupLayoutIsUnchanged(RuntimePlatform platform)
        {
            var design = new Vector2(940, 940);
            var safe = new Rect(0, 34, 390, 751);
            Assert.That(MobileUiLayout.CalculatePopupFitScale(design, safe, 390, 844, Vector2.one * 32, platform),
                Is.EqualTo(MobileUiLayout.CalculateFitScale(design, safe, 390, 844, Vector2.one * 32)));
        }

        [Test]
        public void BackdropDismissIgnoresInsidePressDragRightClickAndBlockedState()
        {
            var root = new GameObject("PopupDismissTest", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var dimObject = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimObject.transform.SetParent(root.transform, false);
            var paperObject = new GameObject("Paper", typeof(RectTransform));
            var paper = (RectTransform)paperObject.transform;
            paper.SetParent(root.transform, false);
            paper.sizeDelta = new Vector2(200, 200);
            int calls = 0;
            bool allowed = true;
            try
            {
                InkUiStyle.ConfigurePopupDismiss(dimObject.GetComponent<Image>(), paper, () => calls++, () => allowed);
                var events = dimObject.GetComponent<EventTrigger>();
                Canvas.ForceUpdateCanvases();
                Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, paper.position);
                Vector2 outside = new Vector2(-2000, -2000);
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                pointer.pressPosition = pointer.position = inside;
                events.OnPointerClick(pointer);
                pointer.position = outside;
                events.OnPointerClick(pointer);
                pointer.pressPosition = outside; pointer.dragging = true;
                events.OnPointerClick(pointer);
                pointer.dragging = false; pointer.button = PointerEventData.InputButton.Right;
                events.OnPointerClick(pointer);
                pointer.button = PointerEventData.InputButton.Left; allowed = false;
                events.OnPointerClick(pointer);
                Assert.That(calls, Is.Zero);
                allowed = true;
                events.OnPointerClick(pointer);
                Assert.That(calls, Is.EqualTo(1));
                paperObject.SetActive(false);
                events.OnPointerClick(pointer);
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                PointerInput.ResetSuppressionForTests();
            }
        }
    }
}
