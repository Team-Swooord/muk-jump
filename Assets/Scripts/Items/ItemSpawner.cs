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
        [SerializeField] Vector2 verticalSpacing = new(10f, 16f);
        [SerializeField] Vector2 horizontalRange = new(-4f, 4f);
        [Tooltip("게임 시작점 기준 첫 아이템 고도. 첫 슬롯은 항상 먹분신이다.")]
        [SerializeField] float firstSpawnHeight = 12f;
        [SerializeField] float spawnAhead = 12f;
        [SerializeField] float despawnBelow = 10f;
        [Header("먹떼 출현")]
        [Range(0f, 1f), SerializeField] float cloneChanceAt30m = 0.35f;
        [Range(0f, 1f), SerializeField] float cloneChanceAt250m = 0.5f;
        // 씬에 저장된 예전 직렬화 값과 무관하게 모든 아이템의 크기를 동일하게 유지한다.
        const float ItemWorldWidth = 0.9f;
        const float CameraSidePadding = 0.25f;
        const int PoolCapacity = 8;

        readonly List<ItemPickup> active = new();
        readonly HashSet<ItemType> missingSpriteWarnings = new();
        ComponentPool<ItemPickup> pool;
        GameManager subscribedManager;
        Camera cam;
        float nextSpawnHeight;
        int scheduledSessionVersion = -1;
        bool introClonePending;

        void OnEnable()
        {
            TrySubscribeToGameManager();
        }

        void Start()
        {
            cam = Camera.main;
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

            EnsureSessionSchedule();
            float cameraTop = cam.transform.position.y + cam.orthographicSize;
            float cutoff = cam.transform.position.y - cam.orthographicSize - despawnBelow;
            float cameraTopHeight = GameHeightAtWorldY(cameraTop);
            float cutoffHeight = GameHeightAtWorldY(cutoff);

            // 디버그 순간이동이나 카메라 급상승 뒤 화면 아래의 과거 예약 슬롯은
            // 오브젝트를 만들지 않고 건너뛰어 한 프레임 생성/파괴 폭증을 막는다.
            while (nextSpawnHeight < cutoffHeight)
                nextSpawnHeight += NextSpacing();

            while (nextSpawnHeight <= cameraTopHeight + spawnAhead)
            {
                bool forceIntroClone = introClonePending;
                if (Spawn(nextSpawnHeight, forceIntroClone) && forceIntroClone)
                    introClonePending = false;
                nextSpawnHeight += NextSpacing();
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

        bool Spawn(float gameHeight, bool forceIntroClone)
        {
            EnsurePool();
            var type = ChooseItemType(gameHeight, forceIntroClone);
            Sprite sprite = SpriteFor(type);
            if (sprite == null)
            {
                if (missingSpriteWarnings.Add(type))
                    Debug.LogWarning($"[MukJump] {type} 아이템 스프라이트가 없어 해당 스폰을 건너뜁니다.",
                        this);
                return false;
            }
            bool usesDedicatedSprite = sprite != placeholderSprite;
            var pickup = pool.Acquire();
            var go = pickup.gameObject;
            go.name = $"Item_{type}";
            go.layer = LayerMask.NameToLayer("Item");
            float x = ChooseSpawnX(forceIntroClone);
            go.transform.position = new Vector3(x, WorldYAtGameHeight(gameHeight), 0f);
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
            return true;
        }

        ItemType ChooseItemType(float gameHeight, bool forceIntroClone)
        {
            var manager = GameManager.Instance;
            bool canCreateClone = manager == null || manager.CanCreateInkClone;
            if (forceIntroClone && canCreateClone)
                return ItemType.InkClone;

            float cloneChance = Mathf.Lerp(
                cloneChanceAt30m,
                cloneChanceAt250m,
                Mathf.InverseLerp(30f, 250f, gameHeight));
            if (canCreateClone &&
                GameplayRandom.Value(GameplayRandomStream.Items) < cloneChance)
                return ItemType.InkClone;

            // 상한에 도달한 뒤에는 먹어도 적용되지 않는 분신 아이템을 만들지 않는다.
            return GameplayRandom.Range(GameplayRandomStream.Items, 0, 3) switch
            {
                0 => ItemType.InkDrop,
                1 => ItemType.GoldenBrush,
                _ => ItemType.InkShield,
            };
        }

        float ChooseSpawnX(bool favorLivingPlayer)
        {
            ResolveHorizontalRange(out float minimum, out float maximum);
            if (favorLivingPlayer && GameManager.Instance != null)
            {
                var player = GameManager.Instance.HighestLivingPlayer;
                if (player != null)
                {
                    return Mathf.Clamp(
                        player.transform.position.x +
                        GameplayRandom.Range(GameplayRandomStream.Items, -1f, 1f),
                        minimum, maximum);
                }
            }
            return GameplayRandom.Range(GameplayRandomStream.Items, minimum, maximum);
        }

        void ResolveHorizontalRange(out float minimum, out float maximum)
        {
            minimum = Mathf.Min(horizontalRange.x, horizontalRange.y);
            maximum = Mathf.Max(horizontalRange.x, horizontalRange.y);
            if (cam == null || !cam.orthographic)
                return;

            float halfWidth = Mathf.Max(0.01f, cam.orthographicSize * cam.aspect);
            float halfItemWidth = ItemWorldWidth * 0.5f;
            float center = cam.transform.position.x;
            minimum = center - halfWidth + CameraSidePadding + halfItemWidth;
            maximum = center + halfWidth - CameraSidePadding - halfItemWidth;
            if (maximum < minimum)
                minimum = maximum = center;
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
            float baseSpacing = GameplayRandom.Range(
                GameplayRandomStream.Items, minimum, maximum);
            return Mathf.Max(0.1f, baseSpacing);
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
                : WorldYAtGameHeight(Mathf.Max(firstSpawnHeight, targetHeight));
            scheduledSessionVersion = GameplayRandom.SessionVersion;
            introClonePending = targetHeight < firstSpawnHeight;
            nextSpawnHeight = Mathf.Max(firstSpawnHeight, GameHeightAtWorldY(visibleBottom));
        }

        void EnsureSessionSchedule()
        {
            int version = GameplayRandom.SessionVersion;
            if (scheduledSessionVersion == version) return;
            scheduledSessionVersion = version;
            nextSpawnHeight = firstSpawnHeight;
            introClonePending = true;
        }

        float GameHeightAtWorldY(float worldY)
        {
            return ScoreManager.Instance != null
                ? ScoreManager.Instance.HeightAt(worldY)
                : worldY;
        }

        float WorldYAtGameHeight(float gameHeight)
        {
            if (ScoreManager.Instance == null) return gameHeight;
            float anchorY = cam != null ? cam.transform.position.y : 0f;
            return anchorY + gameHeight - ScoreManager.Instance.HeightAt(anchorY);
        }

        Sprite SpriteFor(ItemType type)
        {
            return type switch
            {
                ItemType.InkDrop when inkDropSprite != null => inkDropSprite,
                ItemType.GoldenBrush when goldenBrushSprite != null => goldenBrushSprite,
                ItemType.InkShield when inkShieldSprite != null => inkShieldSprite,
                ItemType.InkClone when inkCloneSprite != null => inkCloneSprite,
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
                _ => new Color(0.2f, 0.18f, 0.16f),
            };
        }

        void OnValidate()
        {
            verticalSpacing.x = Mathf.Max(0.1f, verticalSpacing.x);
            verticalSpacing.y = Mathf.Max(verticalSpacing.x, verticalSpacing.y);
            firstSpawnHeight = Mathf.Max(0f, firstSpawnHeight);
            spawnAhead = Mathf.Max(0f, spawnAhead);
            despawnBelow = Mathf.Max(0f, despawnBelow);
            cloneChanceAt30m = Mathf.Clamp01(cloneChanceAt30m);
            cloneChanceAt250m = Mathf.Clamp(cloneChanceAt250m, cloneChanceAt30m, 1f);
        }
    }
}
