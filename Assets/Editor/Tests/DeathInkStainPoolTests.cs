using NUnit.Framework;
using UnityEngine;
using MukJump.Player;

namespace MukJump.EditorTests
{
    public sealed class DeathInkStainPoolTests
    {
        GameObject root;
        DeathInkStainPool pool;
        int createdCount;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("DeathInkStainPoolTests");
            pool = new DeathInkStainPool(CreateStain);
            createdCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void Show_화면_상한을_지키며_가장_오래된_인스턴스를_재사용한다()
        {
            DeathInkStainPool.Lease first = ShowAt(1f, 2);
            DeathInkStainPool.Lease second = ShowAt(2f, 2);
            GameObject firstObject = first.GameObject;

            DeathInkStainPool.Lease latest = ShowAt(3f, 2);

            Assert.That(pool.VisibleCount, Is.EqualTo(2));
            Assert.That(pool.AvailableCount, Is.Zero);
            Assert.That(createdCount, Is.EqualTo(2));
            Assert.That(first.IsCurrent, Is.False);
            Assert.That(second.IsCurrent, Is.True);
            Assert.That(latest.IsCurrent, Is.True);
            Assert.That(latest.GameObject, Is.SameAs(firstObject));
        }

        [Test]
        public void Show_상한이_하나여도_마지막_자국은_항상_활성으로_남긴다()
        {
            DeathInkStainPool.Lease first = ShowAt(1f, 1);
            DeathInkStainPool.Lease latest = ShowAt(5f, 1);

            Assert.That(first.IsCurrent, Is.False);
            Assert.That(latest.IsCurrent, Is.True);
            Assert.That(latest.GameObject.activeSelf, Is.True);
            Assert.That(latest.GameObject.transform.position.x, Is.EqualTo(5f));
            Assert.That(pool.VisibleCount, Is.EqualTo(1));
            Assert.That(createdCount, Is.EqualTo(1));
        }

        [Test]
        public void Show_재사용할_때_렌더러와_Transform_상태를_초기화한다()
        {
            DeathInkStainPool.Lease first = ShowAt(1f, 1);
            var renderer = first.GameObject.GetComponent<SpriteRenderer>();
            renderer.color = Color.red;
            first.GameObject.transform.localScale = Vector3.one * 99f;

            Quaternion rotation = Quaternion.Euler(0f, 0f, 27f);
            DeathInkStainPool.Lease latest = pool.Show(
                null,
                new Vector3(4f, 7f, 0f),
                rotation,
                0.45f,
                2,
                1);

            renderer = latest.GameObject.GetComponent<SpriteRenderer>();
            Assert.That(renderer.color, Is.EqualTo(Color.white));
            Assert.That(renderer.sortingOrder, Is.EqualTo(2));
            Assert.That(renderer.enabled, Is.True);
            Assert.That(latest.GameObject.transform.position,
                Is.EqualTo(new Vector3(4f, 7f, 0f)));
            Assert.That(latest.GameObject.transform.rotation.eulerAngles.z,
                Is.EqualTo(27f).Within(0.01f));
            Assert.That(latest.GameObject.transform.localScale.x,
                Is.EqualTo(0.45f).Within(0.001f));
        }

        [Test]
        public void Prewarm_대량분신_동시사망_상한을_한번만_생성한다()
        {
            pool.Prewarm(20);
            pool.Prewarm(20);

            Assert.That(createdCount, Is.EqualTo(20));
            Assert.That(pool.AvailableCount, Is.EqualTo(20));
            for (int i = 0; i < 20; i++)
                ShowAt(i, 20);

            Assert.That(createdCount, Is.EqualTo(20));
            Assert.That(pool.VisibleCount, Is.EqualTo(20));
            Assert.That(pool.AvailableCount, Is.Zero);
        }

        DeathInkStainPool.Lease ShowAt(float x, int capacity)
        {
            return pool.Show(
                null,
                new Vector3(x, 0f, 0f),
                Quaternion.identity,
                1f,
                2,
                capacity);
        }

        GameObject CreateStain()
        {
            createdCount++;
            var stain = new GameObject($"DeathInkStain_{createdCount}");
            stain.transform.SetParent(root.transform, false);
            stain.AddComponent<SpriteRenderer>();
            stain.SetActive(false);
            return stain;
        }
    }
}
