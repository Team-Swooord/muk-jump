using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;

namespace MukJump.Items
{
    /// 카메라 위쪽에 아이템을 일정 간격으로 미리 생성하고 지나간 아이템을 정리한다.
    public class ItemSpawner : MonoBehaviour
    {
        [SerializeField] Sprite placeholderSprite;
        [Tooltip("먹물방울 정식 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite inkDropSprite;
        [Tooltip("황금 붓 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite goldenBrushSprite;
        [Tooltip("먹 방어막 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite inkShieldSprite;
        [Tooltip("먹분신 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite inkCloneSprite;
        [Tooltip("붓 여유 게이지 스프라이트. 비어 있으면 placeholderSprite를 사용한다.")]
        [SerializeField] Sprite inkReserveSprite;
        [SerializeField] Vector2 verticalSpacing = new(15f, 25f);
        [SerializeField] Vector2 horizontalRange = new(-4f, 4f);
        [SerializeField] float firstSpawnHeight = 12f;
        [SerializeField] float spawnAhead = 12f;
        [SerializeField] float despawnBelow = 10f;
        // 씬에 저장된 예전 직렬화 값과 무관하게 모든 아이템의 크기를 동일하게 유지한다.
        const float ItemWorldWidth = 0.9f;
        const int PoolCapacity = 8;

        readonly List<ItemPickup> active = new();
        readonly HashSet<ItemType> missingSpriteWarnings = new();
        ComponentPool<ItemPickup> pool;
        GameManager subscribedManager;
        Camera cam;
        float nextSpawnY;

        void OnEnable()
        {
            TrySubscribeToGameManager();
        }

        void Start()
        {
            cam = Camera.main;
            nextSpawnY = firstSpawnHeight;
            EnsurePool();
            TrySubscribeToGameManager();
        }

        void OnDisable()
        {
            UnsubscribeFromGameManager();
            ReleaseAllActive();
        }

        void Update()
        {
            TrySubscribeToGameManager();
            if (cam == null || GameManager.Instance == null ||
                !GameManager.Instance.IsGameplayTicking) return;

            float cameraTop = cam.transform.position.y + cam.orthographicSize;
            float cutoff = cam.transform.position.y - cam.orthographicSize - despawnBelow;

            // 디버그 순간이동이나 카메라 급상승 뒤 화면 아래의 과거 예약 슬롯은
            // 오브젝트를 만들지 않고 건너뛰어 한 프레임 생성/파괴 폭증을 막는다.
            while (nextSpawnY < cutoff)
                nextSpawnY += NextSpacing();

            while (nextSpawnY <= cameraTop + spawnAhead)
            {
                Spawn(nextSpawnY);
                nextSpawnY += NextSpacing();
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] == null)
                {
                    active.RemoveAt(i);
                    continue;
                }
                if (active[i].transform.position.y >= cutoff) continue;
                ReleaseAt(i);
            }
        }

        void Spawn(float y)
        {
            EnsurePool();
            var type = (ItemType)GameplayRandom.Range(
                GameplayRandomStream.Items, 0, 5);
            Sprite sprite = SpriteFor(type);
            if (sprite == null)
            {
                if (missingSpriteWarnings.Add(type))
                    Debug.LogWarning($"[MukJump] {type} 아이템 스프라이트가 없어 해당 스폰을 건너뜁니다.",
                        this);
                return;
            }
            bool usesDedicatedSprite = sprite != placeholderSprite;
            var pickup = pool.Acquire();
            var go = pickup.gameObject;
            go.name = $"Item_{type}";
            go.layer = LayerMask.NameToLayer("Item");
            go.transform.position = new Vector3(
                GameplayRandom.Range(GameplayRandomStream.Items,
                    horizontalRange.x, horizontalRange.y),
                y, 0f);
            go.transform.rotation = Quaternion.identity;

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 4;
            renderer.color = usesDedicatedSprite ? Color.white : ColorFor(type);
            float width = sprite.bounds.size.x;
            go.transform.localScale = Vector3.one * (width > 0f ? ItemWorldWidth / width : 1f);

            var trigger = go.GetComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.enabled = true;
            trigger.radius = Mathf.Min(sprite.bounds.extents.x, sprite.bounds.extents.y) * 0.72f;

            pickup.ReleaseRequested -= OnReleaseRequested;
            pickup.ReleaseRequested += OnReleaseRequested;
            pickup.Configure(type, GameplayRandom.Range(
                GameplayRandomStream.Items, 0f, Mathf.PI * 2f));
            active.Add(pickup);
        }

        ItemPickup CreatePooledItem()
        {
            var go = new GameObject("PooledItem")
            {
                layer = LayerMask.NameToLayer("Item"),
            };
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            return go.AddComponent<ItemPickup>();
        }

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new ComponentPool<ItemPickup>(CreatePooledItem, PoolCapacity);
            var existing = GetComponentsInChildren<ItemPickup>(true);
            for (int i = 0; i < existing.Length; i++)
                pool.Adopt(existing[i]);
        }

        void OnReleaseRequested(ItemPickup pickup)
        {
            int index = active.IndexOf(pickup);
            if (index < 0) return;
            ReleaseAt(index);
        }

        void ReleaseAt(int index)
        {
            var pickup = active[index];
            active.RemoveAt(index);
            if (pickup == null || pool == null) return;
            pickup.ReleaseRequested -= OnReleaseRequested;
            pool.Release(pickup);
        }

        void ReleaseAllActive()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                ReleaseAt(i);
        }

        float NextSpacing()
        {
            float minimum = Mathf.Max(0.1f, verticalSpacing.x);
            float maximum = Mathf.Max(minimum, verticalSpacing.y);
            return GameplayRandom.Range(
                GameplayRandomStream.Items, minimum, maximum);
        }

        void TrySubscribeToGameManager()
        {
            var manager = GameManager.Instance;
            if (subscribedManager == manager) return;
            UnsubscribeFromGameManager();
            if (manager == null) return;
            subscribedManager = manager;
            subscribedManager.StateChanged += OnStateChanged;
            subscribedManager.WorldHeightTeleported += OnWorldHeightTeleported;
        }

        void UnsubscribeFromGameManager()
        {
            if (subscribedManager == null) return;
            subscribedManager.StateChanged -= OnStateChanged;
            subscribedManager.WorldHeightTeleported -= OnWorldHeightTeleported;
            subscribedManager = null;
        }

        void OnStateChanged(GameState previous, GameState next)
        {
            if (next != GameState.Playing)
                ReleaseAllActive();
        }

        void OnWorldHeightTeleported(int targetHeight)
        {
            ReleaseAllActive();
            if (cam == null) cam = Camera.main;
            float visibleBottom = cam != null
                ? cam.transform.position.y - cam.orthographicSize
                : Mathf.Max(firstSpawnHeight, targetHeight);
            nextSpawnY = Mathf.Max(firstSpawnHeight, visibleBottom);
        }

        Sprite SpriteFor(ItemType type)
        {
            return type switch
            {
                ItemType.InkDrop when inkDropSprite != null => inkDropSprite,
                ItemType.GoldenBrush when goldenBrushSprite != null => goldenBrushSprite,
                ItemType.InkShield when inkShieldSprite != null => inkShieldSprite,
                ItemType.InkClone when inkCloneSprite != null => inkCloneSprite,
                ItemType.InkReserve when inkReserveSprite != null => inkReserveSprite,
                _ => placeholderSprite,
            };
        }

        static Color ColorFor(ItemType type)
        {
            return type switch
            {
                ItemType.InkDrop => new Color(0.42f, 0.62f, 0.72f),
                ItemType.GoldenBrush => new Color(0.95f, 0.72f, 0.2f),
                ItemType.InkShield => new Color(0.72f, 0.18f, 0.28f),
                ItemType.InkReserve => new Color(0.2f, 0.58f, 0.48f),
                _ => new Color(0.2f, 0.18f, 0.16f),
            };
        }
    }
}
