using MukJump.Core;
using MukJump.Core.Pooling;
using UnityEngine;

namespace MukJump.Items
{
    /// 정해진 고도마다 성장 선택을 보장한다.
    /// 월드 두루마리는 다음 이정표를 미리 보여 주는 비충돌 연출이며,
    /// 실제 선택은 먹떼 진행도가 이정표에 도달한 순간 직접 연다.
    [DisallowMultipleComponent]
    public sealed class GrowthScrollSpawner : MonoBehaviour
    {
        public const float DefaultFirstHeight = 25f;
        public const float DefaultInterval = 200f;

        const float ScrollWorldWidth = 0.9f;
        const float SpawnHorizontalOffset = 0.72f;
        const int PoolCapacity = 1;
        const string ResourceSpritePath = "MukJump/UI/Growth/growth_scroll";

        [SerializeField] Sprite growthScrollSprite;
        [SerializeField, Min(0f)] float firstHeight = DefaultFirstHeight;
        [SerializeField, Min(1f)] float interval = DefaultInterval;
        [SerializeField, Min(0f)] float spawnAhead = 10f;

        ComponentPool<GrowthScrollPickup> pool;
        GrowthScrollPickup activePickup;
        GameManager subscribedManager;
        Camera worldCamera;
        float nextScheduledHeight = DefaultFirstHeight;
        bool scheduleInitialized;
        bool fallbackSpriteLoadAttempted;
        bool missingSpriteWarned;

        public float FirstHeight => firstHeight;
        public float Interval => interval;
        public float NextScheduledHeight => nextScheduledHeight;
        public bool HasActivePickup => activePickup != null;
        public GrowthScrollPickup ActivePickup => activePickup;
        public int PoolAvailableCount => pool?.AvailableCount ?? 0;
        public int PoolLeasedCount => pool?.LeasedCount ?? 0;

        void OnEnable()
        {
            worldCamera = Camera.main;
            EnsurePool();
            TrySubscribeToGameManager();
            if (GameManager.Instance != null &&
                GameManager.Instance.State == GameState.Playing)
            {
                BeginSessionSchedule();
            }
        }

        void Start()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            EnsurePool();
            TrySubscribeToGameManager();
        }

        void OnDisable()
        {
            UnsubscribeFromGameManager();
            ReleaseActive();
            scheduleInitialized = false;
        }

        void Update()
        {
            TrySubscribeToGameManager();
            var manager = GameManager.Instance;
            var growth = RunGrowthController.Instance;
            if (manager == null || growth == null)
            {
                ReleaseActive();
                return;
            }
            if (growth.IsFullyUpgraded)
            {
                ReleaseActive();
                return;
            }
            if (!manager.IsGameplayTicking || growth.HasPendingChoice)
            {
                return;
            }

            if (!scheduleInitialized)
                BeginSessionSchedule();

            // SwarmProgressHeight는 세션 동안 감소하지 않는 먹떼 진행도다.
            // 카메라 위치나 단독 분신의 순간 부스트가 아닌 이 값으로 확정 선택을 연다.
            if (manager.SwarmProgressHeight >= nextScheduledHeight)
            {
                TryOpenScheduledChoice(growth);
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null)
                return;

            float cameraTop = worldCamera.transform.position.y +
                worldCamera.orthographicSize;

            float cameraTopHeight = GameHeightAtWorldY(cameraTop);
            if (activePickup == null &&
                nextScheduledHeight <= cameraTopHeight + spawnAhead &&
                TrySpawn(nextScheduledHeight))
                return;
        }

        /// 씬 빌더와 테스트가 정식 스프라이트 및 일정 값을 명시적으로 주입하는 진입점.
        public void Configure(
            Sprite sprite,
            float guaranteedFirstHeight = DefaultFirstHeight,
            float repeatInterval = DefaultInterval)
        {
            growthScrollSprite = sprite;
            fallbackSpriteLoadAttempted = sprite != null;
            missingSpriteWarned = false;
            firstHeight = Mathf.Max(0f, guaranteedFirstHeight);
            interval = Mathf.Max(1f, repeatInterval);
            BeginSessionSchedule();
        }

        public void SetSprite(Sprite sprite)
        {
            growthScrollSprite = sprite;
            fallbackSpriteLoadAttempted = sprite != null;
            missingSpriteWarned = false;
        }

        /// minimumHeight와 같거나 그 위에 있는 첫 정규 예약 고도를 반환한다.
        /// 간격은 현재 이정표만큼 증가하다 repeatInterval에서 상한을 갖는다.
        /// 기본값은 25, 50, 100, 200, 400 이후 200m 간격이다.
        public static float NextScheduleAtOrAbove(
            float minimumHeight,
            float guaranteedFirstHeight = DefaultFirstHeight,
            float repeatInterval = DefaultInterval)
        {
            float first = Mathf.Max(0f, guaranteedFirstHeight);
            float maxSpacing = Mathf.Max(1f, repeatInterval);
            if (minimumHeight <= first)
                return first;

            float scheduled = first;
            for (int guard = 0; guard < 64 && scheduled < minimumHeight; guard++)
            {
                float spacing = ScheduleSpacing(scheduled, maxSpacing);
                if (spacing >= maxSpacing)
                    break;
                scheduled += spacing;
            }

            if (scheduled >= minimumHeight)
                return scheduled;

            float remainingSlots = Mathf.Ceil(
                (minimumHeight - scheduled) / maxSpacing);
            return scheduled + Mathf.Max(1f, remainingSlots) * maxSpacing;
        }

        /// height보다 엄격히 높은 첫 정규 예약 고도를 반환한다.
        /// 디버그 순간이동이 정확히 이정표에 도착해도 지난 선택이 몰아서 열리지 않는다.
        public static float NextScheduleAfter(
            float height,
            float guaranteedFirstHeight = DefaultFirstHeight,
            float repeatInterval = DefaultInterval)
        {
            float maxSpacing = Mathf.Max(1f, repeatInterval);
            float scheduled = NextScheduleAtOrAbove(
                height, guaranteedFirstHeight, maxSpacing);
            return scheduled > height
                ? scheduled
                : AdvanceSchedule(scheduled, maxSpacing);
        }

        static float AdvanceSchedule(float scheduled, float maxSpacing)
        {
            return scheduled + ScheduleSpacing(scheduled, maxSpacing);
        }

        static float ScheduleSpacing(float scheduled, float maxSpacing)
        {
            // firstHeight가 0으로 주입된 기존 씬도 0에서 멈추지 않게 최소 1m 전진한다.
            return Mathf.Min(Mathf.Max(1f, scheduled), maxSpacing);
        }

        bool TryOpenScheduledChoice(RunGrowthController growth)
        {
            if (growth == null || !growth.RequestChoice())
                return false;

            if (activePickup != null)
                activePickup.CompletePreview();
            else
                ReleaseActive();
            nextScheduledHeight = NextScheduleAfter(
                nextScheduledHeight, firstHeight, interval);
            return true;
        }

        bool TrySpawn(float gameHeight)
        {
            if (activePickup != null) return false;

            Sprite sprite = ResolveSprite();
            if (sprite == null)
            {
                if (!missingSpriteWarned)
                {
                    missingSpriteWarned = true;
                    Debug.LogWarning(
                        "[MukJump] 성장 두루마리 스프라이트를 찾지 못해 스폰을 보류합니다.",
                        this);
                }
                return false;
            }

            EnsurePool();
            var pickup = pool.Acquire();
            var pickupObject = pickup.gameObject;
            pickupObject.name = "GrowthScroll";
            int itemLayer = LayerMask.NameToLayer("Item");
            if (itemLayer >= 0)
                pickupObject.layer = itemLayer;

            float worldY = WorldYAtGameHeight(gameHeight);
            int slotIndex = ScheduleIndex(gameHeight);
            pickupObject.transform.position = new Vector3(
                ChooseSpawnX(slotIndex), worldY, 0f);
            pickupObject.transform.rotation = Quaternion.identity;

            var renderer = pickupObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 5;

            float spriteWidth = sprite.bounds.size.x;
            pickupObject.transform.localScale = Vector3.one *
                (spriteWidth > 0f ? ScrollWorldWidth / spriteWidth : 1f);

            var trigger = pickupObject.GetComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(
                0.05f,
                Mathf.Min(sprite.bounds.extents.x, sprite.bounds.extents.y) * 0.7f);

            pickup.ReleaseRequested -= HandleReleaseRequested;
            pickup.ReleaseRequested += HandleReleaseRequested;
            pickup.Configure(slotIndex * 1.618f);
            activePickup = pickup;
            return true;
        }

        int ScheduleIndex(float gameHeight)
        {
            float scheduled = firstHeight;
            int index = 0;
            for (int guard = 0;
                 guard < 100000 && scheduled < gameHeight;
                 guard++, index++)
            {
                scheduled = AdvanceSchedule(scheduled, interval);
            }
            return index;
        }

        float ChooseSpawnX(int slotIndex)
        {
            float centerX = worldCamera != null
                ? worldCamera.transform.position.x
                : 0f;
            var manager = GameManager.Instance;
            var player = manager != null ? manager.HighestLivingPlayer : null;
            if (player != null)
                centerX = player.transform.position.x;

            float direction = slotIndex % 2 == 0 ? 1f : -1f;
            float desired = centerX + SpawnHorizontalOffset * direction;
            if (worldCamera == null)
                return desired;

            float viewportHalfWidth =
                worldCamera.orthographicSize * worldCamera.aspect;
            float padding = ScrollWorldWidth * 0.6f;
            float minimum = worldCamera.transform.position.x -
                viewportHalfWidth + padding;
            float maximum = worldCamera.transform.position.x +
                viewportHalfWidth - padding;

            // 비정상적으로 좁은 테스트 카메라에서도 Clamp 인자 순서를 보장한다.
            if (minimum > maximum)
                return worldCamera.transform.position.x;
            return Mathf.Clamp(desired, minimum, maximum);
        }

        GrowthScrollPickup CreatePooledPickup()
        {
            var pooledObject = new GameObject("PooledGrowthScroll");
            pooledObject.transform.SetParent(transform, false);
            int itemLayer = LayerMask.NameToLayer("Item");
            if (itemLayer >= 0)
                pooledObject.layer = itemLayer;
            pooledObject.SetActive(false);
            pooledObject.AddComponent<SpriteRenderer>();
            var trigger = pooledObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            return pooledObject.AddComponent<GrowthScrollPickup>();
        }

        void EnsurePool()
        {
            if (pool != null) return;

            pool = new ComponentPool<GrowthScrollPickup>(
                CreatePooledPickup, PoolCapacity);
            var existing = GetComponentsInChildren<GrowthScrollPickup>(true);
            for (int i = 0; i < existing.Length; i++)
                pool.Adopt(existing[i]);

            // 최대 한 개만 활성화되므로 OnEnable에서 한 개를 미리 만들어 둔다.
            // 이후 정상 플레이에서는 Instantiate/Destroy 없이 같은 객체를 반복 사용한다.
            if (pool.AvailableCount == 0)
                pool.Adopt(CreatePooledPickup());
        }

        Sprite ResolveSprite()
        {
            if (growthScrollSprite != null)
                return growthScrollSprite;
            if (fallbackSpriteLoadAttempted)
                return null;

            fallbackSpriteLoadAttempted = true;
            growthScrollSprite = Resources.Load<Sprite>(ResourceSpritePath);
            return growthScrollSprite;
        }

        void HandleReleaseRequested(GrowthScrollPickup pickup)
        {
            if (pickup == null || pickup != activePickup)
                return;
            ReleaseActive();
        }

        void ReleaseActive()
        {
            var pickup = activePickup;
            activePickup = null;
            if (pickup == null || pool == null)
                return;

            pickup.ReleaseRequested -= HandleReleaseRequested;
            pool.Release(pickup);
        }

        void TrySubscribeToGameManager()
        {
            var manager = GameManager.Instance;
            if (subscribedManager == manager) return;

            UnsubscribeFromGameManager();
            if (manager == null) return;

            subscribedManager = manager;
            subscribedManager.StateChanged += HandleStateChanged;
            subscribedManager.WorldHeightTeleported += HandleWorldHeightTeleported;
        }

        void UnsubscribeFromGameManager()
        {
            if (subscribedManager == null) return;
            subscribedManager.StateChanged -= HandleStateChanged;
            subscribedManager.WorldHeightTeleported -= HandleWorldHeightTeleported;
            subscribedManager = null;
        }

        void HandleStateChanged(GameState previous, GameState next)
        {
            ReleaseActive();
            if (next == GameState.Playing)
                BeginSessionSchedule();
            else
                scheduleInitialized = false;
        }

        void HandleWorldHeightTeleported(int targetHeight)
        {
            ReleaseActive();
            if (worldCamera == null)
                worldCamera = Camera.main;

            float minimumHeight = Mathf.Max(firstHeight, targetHeight);
            if (worldCamera != null)
            {
                float visibleBottom = worldCamera.transform.position.y -
                    worldCamera.orthographicSize;
                minimumHeight = Mathf.Max(
                    minimumHeight, GameHeightAtWorldY(visibleBottom));
            }

            nextScheduledHeight = NextScheduleAfter(
                minimumHeight, firstHeight, interval);
            scheduleInitialized = true;
        }

        void BeginSessionSchedule()
        {
            nextScheduledHeight = firstHeight;
            scheduleInitialized = true;
            ReleaseActive();
        }

        float GameHeightAtWorldY(float worldY)
        {
            return ScoreManager.Instance != null
                ? ScoreManager.Instance.HeightAt(worldY)
                : worldY;
        }

        float WorldYAtGameHeight(float gameHeight)
        {
            if (ScoreManager.Instance == null)
                return gameHeight;

            float anchorY = worldCamera != null
                ? worldCamera.transform.position.y
                : 0f;
            return anchorY + gameHeight - ScoreManager.Instance.HeightAt(anchorY);
        }

        void OnValidate()
        {
            firstHeight = Mathf.Max(0f, firstHeight);
            interval = Mathf.Max(1f, interval);
            spawnAhead = Mathf.Max(0f, spawnAhead);
        }
    }
}
