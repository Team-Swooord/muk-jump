using System;
using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Player
{
    /// 죽음 먹 자국을 화면에 남기되 고정 수의 GameObject만 순환 재사용한다.
    /// 오래된 자국이 퍼지는 도중 재대여되더라도 세대 토큰으로 이전 코루틴의
    /// Transform 갱신을 차단한다.
    public sealed class DeathInkStainPool
    {
        internal sealed class Entry
        {
            public GameObject Instance;
            public uint Version;
        }

        public readonly struct Lease
        {
            readonly Entry entry;
            readonly uint version;

            internal Lease(Entry entry, uint version)
            {
                this.entry = entry;
                this.version = version;
            }

            /// 현재 대여가 유효할 때만 활성 자국을 반환한다.
            /// 용량 초과로 재사용된 이전 대여에는 null을 반환한다.
            public GameObject GameObject =>
                IsCurrent ? entry.Instance : null;

            public bool IsCurrent =>
                entry != null &&
                entry.Version == version &&
                entry.Instance != null &&
                entry.Instance.activeSelf;
        }

        readonly Func<GameObject> factory;
        readonly Queue<Entry> visible = new();
        readonly Stack<Entry> available = new();
        readonly Stack<Entry> pruneScratch = new();

        public int VisibleCount
        {
            get
            {
                PruneDestroyedEntries();
                return visible.Count;
            }
        }

        public int AvailableCount
        {
            get
            {
                PruneDestroyedEntries();
                return available.Count;
            }
        }

        public DeathInkStainPool(Func<GameObject> factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// 대량 분신이 같은 프레임에 사망해도 첫 사망 순간에는 렌더러를 만들지 않도록
        /// 로비에서 필요한 고정 상한을 비활성 상태로 준비한다.
        public void Prewarm(int capacity)
        {
            capacity = Mathf.Max(1, capacity);
            PruneDestroyedEntries();
            while (visible.Count + available.Count < capacity)
            {
                GameObject instance = factory();
                if (instance == null)
                    throw new InvalidOperationException(
                        "죽음 먹 자국 풀 팩토리가 null을 반환했습니다.");
                instance.SetActive(false);
                available.Push(new Entry { Instance = instance });
            }
        }

        /// 새 먹 자국을 표시한다. 화면 상한에 도달했다면 가장 오래된 자국을
        /// 먼저 반납하고 같은 GameObject를 즉시 재사용한다.
        public Lease Show(
            Sprite sprite,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float uniformScale,
            int sortingOrder,
            int maxVisible)
        {
            int capacity = Mathf.Max(1, maxVisible);
            PruneDestroyedEntries();

            while (visible.Count >= capacity)
                ReturnToAvailable(visible.Dequeue());

            Entry entry = TakeAvailableOrCreate();
            AdvanceVersion(entry);
            PrepareForDisplay(
                entry,
                sprite,
                worldPosition,
                worldRotation,
                uniformScale,
                sortingOrder);
            visible.Enqueue(entry);

            // 런타임에서 상한을 낮춰도 비활성 인스턴스가 이전 상한만큼
            // 메모리에 계속 남지 않도록 총 보유량도 새 상한에 맞춘다.
            while (visible.Count + available.Count > capacity)
                DestroyEntry(available.Pop());

            return new Lease(entry, entry.Version);
        }

        Entry TakeAvailableOrCreate()
        {
            while (available.Count > 0)
            {
                Entry pooled = available.Pop();
                if (pooled?.Instance != null)
                    return pooled;
            }

            GameObject instance = factory();
            if (instance == null)
                throw new InvalidOperationException(
                    "죽음 먹 자국 풀 팩토리가 null을 반환했습니다.");

            instance.SetActive(false);
            return new Entry { Instance = instance };
        }

        static void PrepareForDisplay(
            Entry entry,
            Sprite sprite,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float uniformScale,
            int sortingOrder)
        {
            GameObject instance = entry.Instance;
            instance.SetActive(false);
            instance.name = "DeathInkStain";
            instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
            instance.transform.localScale =
                Vector3.one * Mathf.Max(0f, uniformScale);

            var renderer = instance.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = true;
            instance.SetActive(true);
        }

        void ReturnToAvailable(Entry entry)
        {
            if (entry == null) return;
            AdvanceVersion(entry);
            if (entry.Instance == null) return;

            ResetInstance(entry.Instance);
            available.Push(entry);
        }

        static void ResetInstance(GameObject instance)
        {
            instance.SetActive(false);
            instance.name = "DeathInkStain (Pooled)";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var renderer = instance.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            renderer.enabled = false;
            renderer.sprite = null;
            renderer.color = Color.white;
            renderer.sortingOrder = 0;
        }

        void PruneDestroyedEntries()
        {
            int visibleCount = visible.Count;
            for (int i = 0; i < visibleCount; i++)
            {
                Entry entry = visible.Dequeue();
                if (entry?.Instance != null && entry.Instance.activeSelf)
                    visible.Enqueue(entry);
                else if (entry?.Instance != null)
                    ReturnToAvailable(entry);
            }

            pruneScratch.Clear();
            while (available.Count > 0)
            {
                Entry entry = available.Pop();
                if (entry?.Instance != null)
                    pruneScratch.Push(entry);
            }
            while (pruneScratch.Count > 0)
                available.Push(pruneScratch.Pop());
        }

        static void AdvanceVersion(Entry entry)
        {
            unchecked
            {
                entry.Version++;
                if (entry.Version == 0)
                    entry.Version = 1;
            }
        }

        static void DestroyEntry(Entry entry)
        {
            if (entry?.Instance == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(entry.Instance);
            else
                UnityEngine.Object.DestroyImmediate(entry.Instance);
            entry.Instance = null;
            AdvanceVersion(entry);
        }
    }
}
