using NUnit.Framework;
using UnityEngine;
using MukJump.Core.Pooling;

namespace MukJump.EditorTests
{
    public sealed class ComponentPoolTests
    {
        GameObject root;
        int createdCount;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ComponentPoolTests");
            createdCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void Acquire_반납된_동일_인스턴스를_재사용한다()
        {
            var pool = CreatePool();

            var first = pool.Acquire();
            Assert.That(pool.Release(first), Is.True);
            var second = pool.Acquire();

            Assert.That(second, Is.SameAs(first));
            Assert.That(createdCount, Is.EqualTo(1));
            Assert.That(second.AcquireCount, Is.EqualTo(2));
            Assert.That(second.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_중복_반납은_한_번만_처리한다()
        {
            var pool = CreatePool();
            var probe = pool.Acquire();

            Assert.That(pool.Release(probe), Is.True);
            Assert.That(pool.Release(probe), Is.False);
            Assert.That(probe.ReleaseCount, Is.EqualTo(1));
            Assert.That(pool.AvailableCount, Is.EqualTo(1));
            Assert.That(pool.LeasedCount, Is.Zero);
        }

        [Test]
        public void AcquireRelease_상태_콜백을_순서대로_호출한다()
        {
            var pool = CreatePool();

            var probe = pool.Acquire();
            Assert.That(probe.IsAcquired, Is.True);
            Assert.That(probe.gameObject.activeSelf, Is.True);

            pool.Release(probe);
            Assert.That(probe.IsAcquired, Is.False);
            Assert.That(probe.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Release_외부에서_파괴된_대여_참조도_제거한다()
        {
            var pool = CreatePool();
            var probe = pool.Acquire();

            Object.DestroyImmediate(probe.gameObject);

            Assert.That(pool.Release(probe), Is.True);
            Assert.That(pool.LeasedCount, Is.Zero);
            Assert.That(pool.AvailableCount, Is.Zero);
        }

        [Test]
        public void Adopt_스크립트_리로드_뒤_남은_인스턴스를_재사용한다()
        {
            var orphan = CreateProbe();
            orphan.gameObject.SetActive(true);
            orphan.IsAcquired = true;
            var pool = CreatePool();

            Assert.That(pool.Adopt(orphan), Is.True);
            Assert.That(orphan.gameObject.activeSelf, Is.False);
            var acquired = pool.Acquire();

            Assert.That(acquired, Is.SameAs(orphan));
            Assert.That(acquired.IsAcquired, Is.True);
        }

        ComponentPool<ComponentPoolProbe> CreatePool()
        {
            return new ComponentPool<ComponentPoolProbe>(() =>
            {
                return CreateProbe();
            }, 2);
        }

        ComponentPoolProbe CreateProbe()
        {
            createdCount++;
            var go = new GameObject($"PoolProbe_{createdCount}");
            go.transform.SetParent(root.transform, false);
            go.SetActive(false);
            return go.AddComponent<ComponentPoolProbe>();
        }
    }

    public sealed class ComponentPoolProbe : MonoBehaviour, IPoolableEntity
    {
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public bool IsAcquired { get; set; }

        public void OnPoolAcquire()
        {
            AcquireCount++;
            IsAcquired = true;
        }

        public void OnPoolRelease()
        {
            ReleaseCount++;
            IsAcquired = false;
        }
    }
}
