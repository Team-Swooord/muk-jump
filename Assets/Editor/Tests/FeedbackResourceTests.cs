using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MukJump.Core;

namespace MukJump.EditorTests
{
    public sealed class FeedbackResourceTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void RuntimeGeneratedAudioAndSpriteAreReleasedWhenDisabled()
        {
            root = new GameObject("FeedbackResourceTests");
            var feedback = root.AddComponent<GameFeedbackController>();
            Invoke(feedback, "EnsureInitialized");

            var clips = (IList)GetField(feedback, "ownedRuntimeClips");
            Assert.That(clips.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(GetField(feedback, "dotSprite"), Is.Not.Null);

            Invoke(feedback, "OnDisable");

            Assert.That(clips.Count, Is.Zero);
            Assert.That(GetField(feedback, "dotSprite"), Is.Null);
            Assert.That(GetField(feedback, "jumpClip"), Is.Null);
        }

        [Test]
        public void FallbackGoldenBrushTextureIsReleasedWithHud()
        {
            var source = new Texture2D(8, 8);
            try
            {
                root = new GameObject("PrototypeHudResourceTests");
                var hud = root.AddComponent<PrototypeHud>();
                SetField(hud, "inkBrushIcon", source);
                Invoke(hud, "Start");
                var generated = (Texture2D)GetField(hud, "goldenBrushIcon");

                Assert.That(generated, Is.Not.Null);
                Assert.AreNotSame(source, generated);
                Assert.That((bool)GetField(hud, "ownsGoldenBrushIcon"), Is.True);
                // EditMode에서는 런타임 생명주기 콜백이 자동 호출되지 않을 수 있으므로
                // Unity가 Play 중 보장하는 OnDestroy 경로를 직접 검증한다.
                Invoke(hud, "OnDestroy");
                Object.DestroyImmediate(root);
                root = null;

                Assert.That(generated == null, Is.True,
                    "HUD가 만든 황금 붓 폴백 Texture2D는 소유자와 함께 해제되어야 합니다.");
                Assert.That(source != null, Is.True,
                    "직렬화된 원본 텍스처의 소유권은 HUD에 없습니다.");
            }
            finally
            {
                if (source != null)
                    Object.DestroyImmediate(source);
            }
        }

        static object Invoke(object target, string methodName)
        {
            return target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }

        static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
