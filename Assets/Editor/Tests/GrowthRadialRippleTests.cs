using System.Reflection;
using MukJump.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MukJump.EditorTests
{
    public sealed class GrowthRadialRippleTests
    {
        [TestCase(.45f)] [TestCase(.7f)] [TestCase(1.0f)]
        public void RippleExpandsEquallyInScreenXYWithoutDepthOrGold(float time)
        {
            LobbySettingsProfile.UseStoreForTests(new MemoryLobbySettingsStore());
            var host = new GameObject("RippleTest", typeof(RectTransform));
            try
            {
                var icon = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
                icon.SetParent(host.transform, false);
                var effect = host.AddComponent<GrowthBloomPresentation>();
                effect.Initialize((RectTransform)host.transform);
                effect.Play(icon, null);
                typeof(GrowthBloomPresentation).GetMethod("ApplyFrame", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, new object[] { time });
                foreach (var arc in host.GetComponentsInChildren<GrowthBloomArc>())
                {
                    if (!arc.IsWaterRipple) { Assert.That(arc.color.a, Is.Zero); continue; }
                    var scale = arc.rectTransform.localScale;
                    Assert.That(scale.x, Is.EqualTo(scale.y).Within(.0001f));
                    Assert.That(scale.z, Is.EqualTo(1f));
                    Assert.That(arc.rectTransform.localPosition.z, Is.Zero);
                    Assert.That(arc.raycastTarget, Is.False);
                }
                effect.Cancel();
                Assert.That(effect.IsPlaying, Is.False);
            }
            finally { Object.DestroyImmediate(host); LobbySettingsProfile.RestoreDefaultStoreForTests(); }
        }
    }
}
