using System;
using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Player;

namespace MukJump.Obstacles
{
    /// 카메라 위쪽에 좌우 이동 장애물을 미리 만들고 지나간 장애물을 정리한다.
    /// 높이 올라갈수록 이동 속도가 점진적으로 증가한다.
    public class ObstacleSpawner : MonoBehaviour
    {
        const string DragonAnimationResourcePath =
            "MukJump/Obstacles/child_ink_dragon_4frame_v3";
        const string DragonAnimationTextureName =
            "child_ink_dragon_4frame_v3";
        const string HaetaeAnimationResourcePath =
            "MukJump/Obstacles/child_ink_haetae_4frame_v2";
        const string HaetaeAnimationTextureName =
            "child_ink_haetae_4frame_v2";

        [SerializeField] Sprite obstacleSprite;
        [Tooltip("초등학생이 그린 듯한 동양 용 장애물 스프라이트")]
        [SerializeField] Sprite dragonSprite;
        [Tooltip("2×2 시트에서 분리한 어린 용 루프 프레임")]
        [SerializeField] Sprite[] dragonFrames;
        [Tooltip("게임 시작점 기준 첫 이동 장애물 고도")]
        [SerializeField] float firstSpawnHeight = 30f;
        [SerializeField] Vector2 verticalSpacing = new(8f, 12f);
        [SerializeField] Vector2 horizontalRange = new(-4.1f, 4.1f);
        [SerializeField] float spawnAhead = 14f;
        [SerializeField] float despawnBelow = 12f;
        [SerializeField] float obstacleWorldWidth = 1.2f;
        [SerializeField] Vector2 moveAmplitudeRange = new(1.2f, 2.4f);
        [Tooltip("0m 부근 장애물의 좌우 이동 속도 범위")]
        [SerializeField] Vector2 baseMoveSpeedRange = new(0.55f, 0.8f);
        [Tooltip("이 높이부터 최고 속도 범위를 사용")]
        [SerializeField] float maxSpeedHeight = 300f;
        [Tooltip("최고 난도에서의 좌우 이동 속도 범위")]
        [SerializeField] Vector2 maxMoveSpeedRange = new(1.35f, 1.8f);
        [Header("어린 용 변형")]
        [Min(30f), SerializeField] float dragonUnlockHeight = 60f;
        [Tooltip("해태 해금 전 기존 어린 용 출현 확률")]
        [Range(0f, 1f), SerializeField] float dragonChanceBeforeHaetae = 0.28f;
        [Range(0f, 1f), SerializeField] float dragonChance = 0.18f;
        [Min(0.1f), SerializeField] float dragonWorldWidth = 3.2f;
        [Min(0.1f), SerializeField] float dragonColliderWorldHeight = 0.52f;
        [Min(0.04f), SerializeField] float dragonFrameSeconds = 0.2f;
        [SerializeField] Vector2 dragonMoveAmplitudeRange = new(1f, 1.6f);
        [SerializeField] Vector2 dragonMoveSpeedRange = new(0.45f, 0.7f);
        [Header("먹해태 수문장")]
        [Tooltip("2×2 시트에서 분리한 서기·웅크리기·돌진·착지 프레임")]
        [SerializeField] Sprite haetaeSprite;
        [SerializeField] Sprite[] haetaeFrames;
        [Min(250f), SerializeField] float haetaeUnlockHeight = 320f;
        [Range(0f, 1f), SerializeField] float haetaeChance = 0.12f;
        [Min(0.1f), SerializeField] float haetaeWorldWidth = 2.2f;
        [SerializeField] Vector2 haetaeColliderWorldSize = new(1.45f, 0.72f);
        [SerializeField] FallingInkRockSpawner fallingInkRockSpawner;
        [SerializeField] WindWeatherController windWeatherController;
        const int PoolCapacity = 10;
        const int HaetaePoolCapacity = 2;

        readonly List<Obstacle> active = new();
        readonly List<HaetaeObstacle> activeHaetae = new(1);
        ComponentPool<Obstacle> pool;
        ComponentPool<HaetaeObstacle> haetaePool;
        Action<HaetaeObstacle> haetaeReleaseHandler;
        Func<PlayerController> haetaeTargetResolver;
        Func<bool> haetaeTelegraphGate;
        GameManager subscribedManager;
        Camera cam;
        float nextSpawnHeight;
        int scheduledSessionVersion = -1;
        bool firstDragonPending = true;
        bool firstHaetaePending = true;
        bool activeHaetaeRestoresFirstGuarantee;
        bool haetaePoolPrewarmed;

        enum MovingObstacleVariant
        {
            Spike,
            ChildDragon,
            Haetae,
        }

        public static ObstacleSpawner Instance { get; private set; }
        public bool HasActiveHaetae => activeHaetae.Count > 0;

        void OnEnable()
        {
            Instance = this;
            TrySubscribeToGameManager();
        }

        void Start()
        {
            cam = Camera.main;
            // 구형 Main 씬의 20m 첫 장애물 값을 안전 구간 규칙으로 승격한다.
            firstSpawnHeight = 30f;
            dragonUnlockHeight = Mathf.Max(60f, dragonUnlockHeight);
            // 구형 씬의 28% 직렬화 값이 남아 있어도 해태 12%와 합친 대형 동물
            // 예산이 30%를 넘지 않도록 320m 이후 기준만 승격한다.
            dragonChanceBeforeHaetae = Mathf.Max(
                0.28f, dragonChanceBeforeHaetae);
            dragonChance = Mathf.Min(0.18f, dragonChance);
            haetaeChance = Mathf.Min(0.12f, haetaeChance);
            haetaeUnlockHeight = Mathf.Max(320f, haetaeUnlockHeight);
            if (fallingInkRockSpawner == null)
                fallingInkRockSpawner = GetComponent<FallingInkRockSpawner>();
            if (windWeatherController == null)
                windWeatherController = WindWeatherController.Instance != null
                    ? WindWeatherController.Instance
                    : FindFirstObjectByType<WindWeatherController>();
            LoadDragonVisuals();
            LoadHaetaeVisuals();
            EnsurePool();
            EnsureHaetaePool();
            TrySubscribeToGameManager();
            if (obstacleSprite == null)
                Debug.LogWarning("[MukJump] 장애물 스프라이트가 없어 장애물을 생성하지 않습니다.", this);
            if (dragonSprite == null)
                Debug.LogWarning("[MukJump] 어린 용 스프라이트가 없어 일반 먹가시만 생성합니다.", this);
            if (haetaeSprite == null)
                Debug.LogWarning("[MukJump] 먹해태 스프라이트가 없어 해태 수문장을 생성하지 않습니다.", this);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            UnsubscribeFromGameManager();
            ReleaseAllActive();
        }

        void Update()
        {
            TrySubscribeToGameManager();
            if (cam == null || obstacleSprite == null || GameManager.Instance == null ||
                !GameManager.Instance.IsGameplayTicking) return;

            EnsureSessionSchedule();
            float cameraTop = cam.transform.position.y + cam.orthographicSize;
            float cutoff = cam.transform.position.y - cam.orthographicSize - despawnBelow;
            float cameraTopHeight = GameHeightAtWorldY(cameraTop);
            float cutoffHeight = GameHeightAtWorldY(cutoff);

            // 디버그 순간이동 등으로 이미 화면 아래가 된 예약 슬롯은 생성 없이 넘긴다.
            while (nextSpawnHeight < cutoffHeight)
                nextSpawnHeight += NextSpacing();

            while (nextSpawnHeight <= cameraTopHeight + spawnAhead)
            {
                Spawn(nextSpawnHeight);
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

            for (int i = activeHaetae.Count - 1; i >= 0; i--)
            {
                var haetae = activeHaetae[i];
                if (haetae == null)
                {
                    activeHaetae.RemoveAt(i);
                    continue;
                }
                if (haetae.ActivationWorldY >= cutoff) continue;
                haetae.ForceRelease();
            }
        }

        void Spawn(float courseHeight)
        {
            EnsurePool();
            bool isGuaranteedHaetaeSlot =
                courseHeight >= haetaeUnlockHeight && firstHaetaePending;
            MovingObstacleVariant variant = ChooseVariant(courseHeight);
            if (variant == MovingObstacleVariant.Haetae)
            {
                if (SpawnHaetae(courseHeight, isGuaranteedHaetaeSlot))
                    return;
                if (isGuaranteedHaetaeSlot)
                    firstHaetaePending = true;
                variant = MovingObstacleVariant.Spike;
            }

            bool useDragon = variant == MovingObstacleVariant.ChildDragon;
            Sprite selectedSprite = useDragon ? dragonSprite : obstacleSprite;
            float worldWidth = useDragon ? dragonWorldWidth : obstacleWorldWidth;
            Vector2 amplitudeRange = useDragon ? dragonMoveAmplitudeRange : moveAmplitudeRange;
            float amplitude = GameplayRandom.Range(GameplayRandomStream.Obstacles,
                Mathf.Min(amplitudeRange.x, amplitudeRange.y),
                Mathf.Max(amplitudeRange.x, amplitudeRange.y));
            float rangeLeft = Mathf.Min(horizontalRange.x, horizontalRange.y);
            float rangeRight = Mathf.Max(horizontalRange.x, horizontalRange.y);
            float halfWorldWidth = worldWidth * 0.5f;
            float maxAmplitude = Mathf.Max(
                0f, (rangeRight - rangeLeft) * 0.5f - halfWorldWidth - 0.05f);
            amplitude = Mathf.Min(amplitude, maxAmplitude);
            float minX = rangeLeft + halfWorldWidth + amplitude;
            float maxX = rangeRight - halfWorldWidth - amplitude;
            if (maxX < minX)
                minX = maxX = (rangeLeft + rangeRight) * 0.5f;

            var obstacle = pool.Acquire();
            var go = obstacle.gameObject;
            go.name = useDragon ? "ChildInkDragon" : "InkObstacle";
            go.layer = LayerMask.NameToLayer("Obstacle");
            go.transform.position = new Vector3(
                GameplayRandom.Range(GameplayRandomStream.Obstacles, minX, maxX),
                WorldYAtGameHeight(courseHeight), 0f);
            go.transform.rotation = Quaternion.identity;

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = selectedSprite;
            renderer.sortingOrder = 6;
            renderer.color = Color.white;
            renderer.enabled = true;
            float spriteWidth = selectedSprite.bounds.size.x;
            float scale = spriteWidth > 0f ? worldWidth / spriteWidth : 1f;
            go.transform.localScale = Vector3.one * scale;

            var circle = go.GetComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.enabled = false;
            // 바깥쪽 반투명 먹 번짐보다 실제 가시 몸통에 맞춰 판정을 약간 줄인다.
            circle.radius = selectedSprite.bounds.extents.x * 0.78f;

            var capsule = go.GetComponent<CapsuleCollider2D>();
            capsule.isTrigger = true;
            capsule.enabled = false;
            capsule.direction = CapsuleDirection2D.Horizontal;
            capsule.offset = Vector2.zero;
            capsule.size = new Vector2(
                selectedSprite.bounds.size.x * 0.8f,
                useDragon
                    ? dragonColliderWorldHeight / Mathf.Max(0.0001f, scale)
                    : selectedSprite.bounds.size.y * 0.49f);

            go.GetComponent<ObstacleVisibilityView>().Configure(
                preserveInkOutlines: useDragon);
            obstacle.ConfigureSpriteAnimation(
                useDragon ? dragonFrames : null,
                dragonFrameSeconds);
            float minSpeed;
            float maxSpeed;
            if (useDragon)
            {
                minSpeed = Mathf.Min(dragonMoveSpeedRange.x, dragonMoveSpeedRange.y);
                maxSpeed = Mathf.Max(dragonMoveSpeedRange.x, dragonMoveSpeedRange.y);
            }
            else
            {
                float difficulty = Mathf.InverseLerp(firstSpawnHeight, maxSpeedHeight, courseHeight);
                minSpeed = Mathf.Lerp(baseMoveSpeedRange.x, maxMoveSpeedRange.x, difficulty);
                maxSpeed = Mathf.Lerp(baseMoveSpeedRange.y, maxMoveSpeedRange.y, difficulty);
            }
            obstacle.Configure(amplitude,
                GameplayRandom.Range(GameplayRandomStream.Obstacles, minSpeed, maxSpeed),
                GameplayRandom.Range(GameplayRandomStream.Obstacles, 0f, Mathf.PI * 2f),
                useDragon ? ObstacleKind.ChildDragon : ObstacleKind.Spike);
            active.Add(obstacle);
        }

        MovingObstacleVariant ChooseVariant(float courseHeight)
        {
            // 해금 직후 첫 유효 슬롯은 반드시 해태다. 다른 큰 위험과 겹친 슬롯에서는
            // 보장을 소비하지 않고 일반 먹가시로 대체해 다음 슬롯으로 미룬다.
            if (courseHeight >= haetaeUnlockHeight && firstHaetaePending)
            {
                if (CanSpawnHaetaeNow(courseHeight))
                {
                    firstHaetaePending = false;
                    return MovingObstacleVariant.Haetae;
                }
                return MovingObstacleVariant.Spike;
            }

            if (courseHeight < haetaeUnlockHeight)
                return ShouldSpawnDragon(courseHeight)
                    ? MovingObstacleVariant.ChildDragon
                    : MovingObstacleVariant.Spike;

            if (HasActiveLargeAnimal())
                return MovingObstacleVariant.Spike;

            // DEBUG로 320m 이상에 바로 왔을 때도 어린 용의 첫 보장은 잃지 않는다.
            if (firstDragonPending &&
                dragonSprite != null &&
                courseHeight >= dragonUnlockHeight)
            {
                firstDragonPending = false;
                return MovingObstacleVariant.ChildDragon;
            }

            float roll = GameplayRandom.Value(GameplayRandomStream.Obstacles);
            if (roll < haetaeChance)
                return CanSpawnHaetaeNow(courseHeight)
                    ? MovingObstacleVariant.Haetae
                    : MovingObstacleVariant.Spike;

            if (roll < haetaeChance + dragonChance &&
                dragonSprite != null &&
                courseHeight >= dragonUnlockHeight)
                return MovingObstacleVariant.ChildDragon;

            return MovingObstacleVariant.Spike;
        }

        bool SpawnHaetae(
            float courseHeight,
            bool restoreFirstGuaranteeIfSkipped)
        {
            var target = ResolveHaetaeTarget();
            if (target == null || target.IsDead || !HasValidHaetaeFrames(haetaeFrames))
                return false;

            EnsureHaetaePool();
            var haetae = haetaePool.Acquire();
            var go = haetae.gameObject;
            go.name = "ChildInkHaetae";
            go.layer = LayerMask.NameToLayer("Obstacle");
            go.transform.position = new Vector3(
                0f, WorldYAtGameHeight(courseHeight), 0f);
            go.transform.rotation = Quaternion.identity;

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = haetaeFrames[0];
            renderer.sortingOrder = 7;
            renderer.color = Color.white;
            float spriteWidth = Mathf.Max(0.01f, haetaeFrames[0].bounds.size.x);
            float scale = haetaeWorldWidth / spriteWidth;
            go.transform.localScale = Vector3.one * scale;

            float difficulty = Mathf.InverseLerp(
                haetaeUnlockHeight, 750f, courseHeight);
            float telegraphSeconds = Mathf.Lerp(1.2f, 1.1f, difficulty);
            float pounceSeconds = Mathf.Lerp(0.75f, 0.65f, difficulty);
            Vector2 localColliderSize = haetaeColliderWorldSize /
                                        Mathf.Max(0.0001f, scale);
            haetaeReleaseHandler ??= ReleaseHaetae;
            haetaeTargetResolver ??= ResolveHaetaeTarget;
            haetaeTelegraphGate ??= CanBeginHaetaeTelegraph;
            haetae.Configure(
                haetaeFrames,
                cam,
                LayerMask.GetMask("Player", "Platform"),
                haetaeReleaseHandler,
                telegraphSeconds,
                pounceSeconds,
                0.14f,
                0.35f,
                localColliderSize,
                new Vector2(0f, -0.03f / Mathf.Max(0.0001f, scale)),
                haetaeTargetResolver,
                haetaeTelegraphGate);

            bool fromLeft = GameplayRandom.Value(
                GameplayRandomStream.Obstacles) < 0.5f;
            float verticalOffset = GameplayRandom.Range(
                GameplayRandomStream.Obstacles, 0.25f, 0.8f);
            haetae.Activate(
                target,
                WorldYAtGameHeight(courseHeight),
                fromLeft,
                verticalOffset);
            activeHaetae.Add(haetae);
            activeHaetaeRestoresFirstGuarantee =
                restoreFirstGuaranteeIfSkipped;
            return true;
        }

        void LoadDragonVisuals()
        {
            if (!HasValidDragonFrames(dragonFrames))
            {
                var resourceFrames = Resources.LoadAll<Sprite>(
                    DragonAnimationResourcePath);
                if (resourceFrames != null && resourceFrames.Length > 1)
                    Array.Sort(resourceFrames,
                        (left, right) => string.CompareOrdinal(left.name, right.name));
                dragonFrames = HasValidDragonFrames(resourceFrames)
                    ? resourceFrames
                    : Array.Empty<Sprite>();
            }

            if (HasValidDragonFrames(dragonFrames))
            {
                dragonSprite = dragonFrames[0];
                return;
            }

            if (dragonSprite == null)
                dragonSprite = Resources.Load<Sprite>(
                    "MukJump/Obstacles/child_ink_dragon");
        }

        static bool HasValidDragonFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length != 4) return false;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null ||
                    frames[i].name != $"child_ink_dragon_frame_{i:00}" ||
                    frames[i].texture == null ||
                    frames[i].texture.name != DragonAnimationTextureName)
                    return false;
            }
            return true;
        }

        void LoadHaetaeVisuals()
        {
            if (!HasValidHaetaeFrames(haetaeFrames))
            {
                var resourceFrames = Resources.LoadAll<Sprite>(
                    HaetaeAnimationResourcePath);
                if (resourceFrames != null && resourceFrames.Length > 1)
                    Array.Sort(resourceFrames,
                        (left, right) => string.CompareOrdinal(left.name, right.name));
                haetaeFrames = HasValidHaetaeFrames(resourceFrames)
                    ? resourceFrames
                    : Array.Empty<Sprite>();
            }

            haetaeSprite = HasValidHaetaeFrames(haetaeFrames)
                ? haetaeFrames[0]
                : null;
        }

        static bool HasValidHaetaeFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length != 4) return false;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null ||
                    frames[i].name != $"child_ink_haetae_frame_{i:00}" ||
                    frames[i].texture == null ||
                    frames[i].texture.name != HaetaeAnimationTextureName)
                    return false;
            }
            return true;
        }

        PlayerController ResolveHaetaeTarget()
        {
            var manager = GameManager.Instance;
            if (manager != null &&
                manager.TryGetSwarmAnchor(
                    out PlayerController representative, out _))
                return representative;
            return FindFirstObjectByType<PlayerController>();
        }

        bool CanBeginHaetaeTelegraph()
        {
            if (fallingInkRockSpawner != null &&
                fallingInkRockSpawner.HasActiveThreat)
                return false;
            return windWeatherController == null ||
                   windWeatherController.Phase == WindWeatherPhase.Breeze;
        }

        bool CanSpawnHaetaeNow(float courseHeight)
        {
            if (haetaeSprite == null ||
                courseHeight < haetaeUnlockHeight ||
                HasActiveLargeAnimal())
                return false;
            if (fallingInkRockSpawner != null &&
                fallingInkRockSpawner.HasActiveThreat)
                return false;
            return windWeatherController == null ||
                   windWeatherController.Phase == WindWeatherPhase.Breeze;
        }

        bool ShouldSpawnDragon(float courseHeight)
        {
            if (dragonSprite == null ||
                courseHeight < dragonUnlockHeight ||
                HasActiveLargeAnimal())
                return false;
            if (firstDragonPending)
            {
                firstDragonPending = false;
                return true;
            }
            float chance = courseHeight < haetaeUnlockHeight
                ? dragonChanceBeforeHaetae
                : dragonChance;
            return GameplayRandom.Value(GameplayRandomStream.Obstacles) < chance;
        }

        bool HasActiveDragon()
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null && active[i].Kind == ObstacleKind.ChildDragon)
                    return true;
            return false;
        }

        public bool HasActiveLargeAnimal()
        {
            return HasActiveDragon() || HasActiveHaetae;
        }

        Obstacle CreatePooledObstacle()
        {
            var go = new GameObject("PooledObstacle")
            {
                layer = LayerMask.NameToLayer("Obstacle"),
            };
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.enabled = false;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.isTrigger = true;
            capsule.direction = CapsuleDirection2D.Horizontal;
            capsule.enabled = false;
            go.AddComponent<ObstacleVisibilityView>();
            return go.AddComponent<Obstacle>();
        }

        HaetaeObstacle CreatePooledHaetae()
        {
            var go = new GameObject("PooledHaetae")
            {
                layer = LayerMask.NameToLayer("Obstacle"),
            };
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.isTrigger = true;
            capsule.direction = CapsuleDirection2D.Horizontal;
            capsule.enabled = false;
            return go.AddComponent<HaetaeObstacle>();
        }

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new ComponentPool<Obstacle>(CreatePooledObstacle, PoolCapacity);
            var existing = GetComponentsInChildren<Obstacle>(true);
            for (int i = 0; i < existing.Length; i++)
                pool.Adopt(existing[i]);
        }

        void EnsureHaetaePool()
        {
            if (haetaePool == null)
            {
                haetaePool = new ComponentPool<HaetaeObstacle>(
                    CreatePooledHaetae, HaetaePoolCapacity);
                var existing = GetComponentsInChildren<HaetaeObstacle>(true);
                for (int i = 0; i < existing.Length; i++)
                    haetaePool.Adopt(existing[i]);
            }

            if (haetaePoolPrewarmed) return;
            var first = haetaePool.Acquire();
            var second = haetaePool.Acquire();
            haetaePool.Release(second);
            haetaePool.Release(first);
            haetaePoolPrewarmed = true;
        }

        void ReleaseAt(int index)
        {
            var obstacle = active[index];
            active.RemoveAt(index);
            if (obstacle != null && pool != null)
                pool.Release(obstacle);
        }

        void ReleaseHaetae(HaetaeObstacle haetae)
        {
            if (haetae == null) return;
            bool restoreFirstGuarantee =
                activeHaetaeRestoresFirstGuarantee &&
                !haetae.HasLockedPath;
            activeHaetae.Remove(haetae);
            activeHaetaeRestoresFirstGuarantee = false;
            if (restoreFirstGuarantee)
                firstHaetaePending = true;
            if (haetaePool != null && haetaePool.Release(haetae))
                return;
            Destroy(haetae.gameObject);
        }

        void ReleaseAllActive()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                ReleaseAt(i);
            for (int i = activeHaetae.Count - 1; i >= 0; i--)
            {
                var haetae = activeHaetae[i];
                if (haetae == null)
                {
                    activeHaetae.RemoveAt(i);
                    continue;
                }
                haetae.ForceRelease();
            }
            activeHaetae.Clear();
        }

        /// Development Build의 DEBUG 패널에서 현재 화면에 해태 경고와 돌진을 즉시 검증한다.
        public bool DebugSpawnHaetae()
        {
            if (!GameManager.DebugToolsAvailable ||
                GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Playing)
                return false;

            var target = GameManager.Instance.HighestLivingPlayer;
            if (target == null || target.IsDead)
                return false;

            LoadHaetaeVisuals();
            if (!HasValidHaetaeFrames(haetaeFrames))
                return false;
            EnsureHaetaePool();
            ReleaseLargeAnimalsForDebug();

            var haetae = haetaePool.Acquire();
            var go = haetae.gameObject;
            go.name = "ChildInkHaetae_DEBUG";
            go.layer = LayerMask.NameToLayer("Obstacle");
            go.transform.rotation = Quaternion.identity;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = haetaeFrames[0];
            renderer.sortingOrder = 7;
            renderer.color = Color.white;
            float spriteWidth = Mathf.Max(0.01f, haetaeFrames[0].bounds.size.x);
            float scale = haetaeWorldWidth / spriteWidth;
            go.transform.localScale = Vector3.one * scale;

            Vector2 localColliderSize = haetaeColliderWorldSize /
                                        Mathf.Max(0.0001f, scale);
            haetaeReleaseHandler ??= ReleaseHaetae;
            haetae.Configure(
                haetaeFrames,
                cam != null ? cam : Camera.main,
                LayerMask.GetMask("Player", "Platform"),
                haetaeReleaseHandler,
                1.2f,
                0.72f,
                0.14f,
                0.35f,
                localColliderSize,
                new Vector2(0f, -0.03f / Mathf.Max(0.0001f, scale)));

            Camera worldCamera = cam != null ? cam : Camera.main;
            bool fromLeft = GameplayRandom.Value(
                GameplayRandomStream.Obstacles) < 0.5f;
            Vector2 targetPosition = target.transform.position;
            float edgeX = targetPosition.x + (fromLeft ? -5.9f : 5.9f);
            if (worldCamera != null)
            {
                float cameraDistance = Mathf.Abs(
                    worldCamera.transform.position.z - go.transform.position.z);
                edgeX = worldCamera.ViewportToWorldPoint(
                    new Vector3(fromLeft ? 0f : 1f, 0.5f, cameraDistance)).x +
                    (fromLeft ? -0.72f : 0.72f);
            }
            Vector2 startPosition = new(
                edgeX, targetPosition.y + 0.55f);
            go.transform.position = startPosition;
            haetae.Activate(startPosition, targetPosition, fromLeft);
            activeHaetae.Add(haetae);
            activeHaetaeRestoresFirstGuarantee = false;
            return true;
        }

        void ReleaseLargeAnimalsForDebug()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i] != null &&
                    active[i].Kind == ObstacleKind.ChildDragon)
                    ReleaseAt(i);
            for (int i = activeHaetae.Count - 1; i >= 0; i--)
                activeHaetae[i]?.ForceRelease();
            activeHaetae.Clear();
            activeHaetaeRestoresFirstGuarantee = false;
        }

        float NextSpacing()
        {
            float minimum = Mathf.Max(0.1f, verticalSpacing.x);
            float maximum = Mathf.Max(minimum, verticalSpacing.y);
            return GameplayRandom.Range(
                GameplayRandomStream.Obstacles, minimum, maximum);
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
            // 마지막 피격과 동시에 모든 위험물이 사라지면 먹가시가 축소 애니메이션으로
            // 소멸한 것처럼 보인다. GameOver에서는 현재 장면을 그대로 정지해 충돌 맥락을
            // 남기고, 새 로비로 돌아갈 때만 풀에 반납한다. 재도전은 씬을 다시 불러
            // OnDisable에서 먼저 정리되므로 이전 판 장애물이 다음 판에 남지 않는다.
            if (next == GameState.Lobby)
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
            // DEBUG 고도 이동은 아트·판정 검증 경로다. 60m 이상으로 바로 이동해도
            // 다음 슬롯에서 첫 어린 용을 확실히 볼 수 있어야 한다.
            firstDragonPending = true;
            firstHaetaePending = true;
            nextSpawnHeight = Mathf.Max(firstSpawnHeight, GameHeightAtWorldY(visibleBottom));
        }

        void EnsureSessionSchedule()
        {
            int version = GameplayRandom.SessionVersion;
            if (scheduledSessionVersion == version) return;
            scheduledSessionVersion = version;
            nextSpawnHeight = firstSpawnHeight;
            firstDragonPending = true;
            firstHaetaePending = true;
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

        void OnValidate()
        {
            firstSpawnHeight = Mathf.Max(30f, firstSpawnHeight);
            verticalSpacing.x = Mathf.Max(0.1f, verticalSpacing.x);
            verticalSpacing.y = Mathf.Max(verticalSpacing.x, verticalSpacing.y);
            spawnAhead = Mathf.Max(0f, spawnAhead);
            despawnBelow = Mathf.Max(0f, despawnBelow);
            obstacleWorldWidth = Mathf.Max(0.1f, obstacleWorldWidth);
            maxSpeedHeight = Mathf.Max(firstSpawnHeight + 0.1f, maxSpeedHeight);
            dragonUnlockHeight = Mathf.Max(firstSpawnHeight, dragonUnlockHeight);
            dragonChanceBeforeHaetae = Mathf.Clamp01(dragonChanceBeforeHaetae);
            dragonChance = Mathf.Clamp01(dragonChance);
            dragonWorldWidth = Mathf.Max(0.1f, dragonWorldWidth);
            dragonColliderWorldHeight = Mathf.Max(0.1f, dragonColliderWorldHeight);
            dragonFrameSeconds = Mathf.Max(0.04f, dragonFrameSeconds);
            haetaeUnlockHeight = Mathf.Max(320f, haetaeUnlockHeight);
            haetaeChance = Mathf.Clamp01(haetaeChance);
            if (haetaeChance + dragonChance > 1f)
                dragonChance = 1f - haetaeChance;
            haetaeWorldWidth = Mathf.Max(0.1f, haetaeWorldWidth);
            haetaeColliderWorldSize = new Vector2(
                Mathf.Max(0.1f, haetaeColliderWorldSize.x),
                Mathf.Max(0.1f, haetaeColliderWorldSize.y));
        }
    }
}
