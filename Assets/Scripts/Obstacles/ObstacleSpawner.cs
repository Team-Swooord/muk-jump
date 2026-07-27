using System;
using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;

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
        [Range(0f, 1f), SerializeField] float dragonChance = 0.28f;
        [Min(0.1f), SerializeField] float dragonWorldWidth = 3.2f;
        [Min(0.1f), SerializeField] float dragonColliderWorldHeight = 0.52f;
        [Min(0.04f), SerializeField] float dragonFrameSeconds = 0.2f;
        [SerializeField] Vector2 dragonMoveAmplitudeRange = new(1f, 1.6f);
        [SerializeField] Vector2 dragonMoveSpeedRange = new(0.45f, 0.7f);
        const int PoolCapacity = 10;

        readonly List<Obstacle> active = new();
        ComponentPool<Obstacle> pool;
        GameManager subscribedManager;
        Camera cam;
        float nextSpawnHeight;
        int scheduledSessionVersion = -1;
        bool firstDragonPending = true;

        void OnEnable()
        {
            TrySubscribeToGameManager();
        }

        void Start()
        {
            cam = Camera.main;
            // 구형 Main 씬의 20m 첫 장애물 값을 안전 구간 규칙으로 승격한다.
            firstSpawnHeight = 30f;
            dragonUnlockHeight = Mathf.Max(60f, dragonUnlockHeight);
            LoadDragonVisuals();
            EnsurePool();
            TrySubscribeToGameManager();
            if (obstacleSprite == null)
                Debug.LogWarning("[MukJump] 장애물 스프라이트가 없어 장애물을 생성하지 않습니다.", this);
            if (dragonSprite == null)
                Debug.LogWarning("[MukJump] 어린 용 스프라이트가 없어 일반 먹가시만 생성합니다.", this);
        }

        void OnDisable()
        {
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
        }

        void Spawn(float courseHeight)
        {
            EnsurePool();
            bool useDragon = ShouldSpawnDragon(courseHeight);
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

        bool ShouldSpawnDragon(float courseHeight)
        {
            if (dragonSprite == null || courseHeight < dragonUnlockHeight || HasActiveDragon())
                return false;
            if (firstDragonPending)
            {
                firstDragonPending = false;
                return true;
            }
            return GameplayRandom.Value(GameplayRandomStream.Obstacles) < dragonChance;
        }

        bool HasActiveDragon()
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null && active[i].Kind == ObstacleKind.ChildDragon)
                    return true;
            return false;
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

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new ComponentPool<Obstacle>(CreatePooledObstacle, PoolCapacity);
            var existing = GetComponentsInChildren<Obstacle>(true);
            for (int i = 0; i < existing.Length; i++)
                pool.Adopt(existing[i]);
        }

        void ReleaseAt(int index)
        {
            var obstacle = active[index];
            active.RemoveAt(index);
            if (obstacle != null && pool != null)
                pool.Release(obstacle);
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
            nextSpawnHeight = Mathf.Max(firstSpawnHeight, GameHeightAtWorldY(visibleBottom));
        }

        void EnsureSessionSchedule()
        {
            int version = GameplayRandom.SessionVersion;
            if (scheduledSessionVersion == version) return;
            scheduledSessionVersion = version;
            nextSpawnHeight = firstSpawnHeight;
            firstDragonPending = true;
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
            dragonChance = Mathf.Clamp01(dragonChance);
            dragonWorldWidth = Mathf.Max(0.1f, dragonWorldWidth);
            dragonColliderWorldHeight = Mathf.Max(0.1f, dragonColliderWorldHeight);
            dragonFrameSeconds = Mathf.Max(0.04f, dragonFrameSeconds);
        }
    }
}
