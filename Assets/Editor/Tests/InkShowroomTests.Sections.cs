using System.Linq;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed partial class InkShowroomTests
    {
        [Test, Category("WardrobeSections")]
        public void WardrobeSectionsUseAbilitiesAndKeepOwnedSecretsLast()
        {
            var before = Enumerable.Range(0, InkSkinCatalog.BrowseCount).Select(InkSkinCatalog.BrowseAt).ToArray();
            Assert.That(before[0], Is.EqualTo(InkSkinId.Ink));
            foreach (var id in before)
            {
                bool hidden = InkSkinCatalog.Get(id).Tier is SkinTier.Hidden or SkinTier.UltraHidden;
                Assert.That(InkSkinCatalog.BrowseGroup(id), Is.EqualTo(hidden ? 2 : InkSkinCatalog.HasAbility(id) ? 1 : 0));
            }
            Assert.That(SkinDebugSession.SetAllUnlocked(true), Is.True);
            try
            {
                InkSkinCatalog.RefreshBrowseOrder();
                Assert.That(Enumerable.Range(0, before.Length).Select(InkSkinCatalog.BrowseAt), Is.EqualTo(before));
                Assert.That(InkSkinCatalog.BrowseGroup(InkSkinId.NightBat), Is.EqualTo(2));
            }
            finally { SkinDebugSession.Reset(); }
        }

        [Test, Category("WardrobeSections")]
        public void WardrobeSectionsAreSeparateRowsWithCorrectScrollHeightAndStableSelection()
        {
            Assert.That(InkSkinPreferences.TryEquip(InkSkinId.FirstRibbon), Is.True);
            view.Select(InkSkinId.FirstRibbon);
            int saves = settings.SaveCount;
            view.ToggleCollection(); Canvas.ForceUpdateCanvases();
            var grid = view.CollectionContent.GetComponent<InkWardrobeCollectionLayout>();
            var content = view.CollectionContent;
            float lastBottom = 0;
            for (int group = 0; group < 3; group++)
            {
                var heading = (RectTransform)content.Find("CollectionSection" + group);
                Assert.That(heading.gameObject.activeSelf, Is.True);
                Rect headingBounds = BoundsIn(content, heading);
                if (group > 0) Assert.That(headingBounds.yMax, Is.LessThan(lastBottom));
                var ids = Enumerable.Range(0, InkSkinCatalog.BrowseCount).Select(InkSkinCatalog.BrowseAt)
                    .Where(id => InkSkinCatalog.BrowseGroup(id) == group).ToArray();
                foreach (var id in ids)
                {
                    var card = (RectTransform)content.Find("SkinCard" + (int)id);
                    Rect bounds = BoundsIn(content, card);
                    AssertInside(bounds, content.rect, 0, id.ToString());
                    Assert.That(bounds.yMax, Is.LessThan(headingBounds.yMin));
                    lastBottom = bounds.yMin;
                }
                Assert.That(grid.RevealTop(InkSkinCatalog.BrowseIndex(ids[0])),
                    Is.EqualTo(-heading.anchoredPosition.y - heading.rect.height * .5f).Within(.1f));
            }
            Assert.That(content.rect.height, Is.GreaterThan(view.CollectionScroll.viewport.rect.height));
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.FirstRibbon));
            Assert.That(content.Find("SkinCard31/CardName").GetComponent<Text>().text, Is.EqualTo("?"));
            content.Find("SkinCard1").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.Selected, Is.EqualTo(InkSkinId.Gat));
            Assert.That(view.CollectionExpanded, Is.False);
            Assert.That(InkSkinPreferences.Equipped, Is.EqualTo(InkSkinId.FirstRibbon));
            Assert.That(settings.SaveCount, Is.EqualTo(saves));
            for (int i = 0; i < 3; i++) Assert.That(content.Find("CollectionSection" + i).gameObject.activeSelf, Is.False);
        }
    }
}
