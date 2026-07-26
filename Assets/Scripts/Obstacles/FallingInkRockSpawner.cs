using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;
using MukJump.Core.Pooling;
using MukJump.Player;

namespace MukJump.Obstacles
{
    /// 현재 카메라 상단에 공정한 X 좌표를 선택해 낙묵석을 시간 기반으로 생성한다.
    public class FallingInkRockSpawner : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] Sprite fallingInkRockSprite;
        [SerializeField] Camera worldCamera;
        [SerializeField] PlayerController player;
        [SerializeField] LayerMask collisionMask;

        [Header("출현 조건")]
        [Min(0f), SerializeField] float startHeight = 8f;
        [Min(0f), SerializeField] float initialDelay = 3f;
        [SerializeField] Vector2 lowHeightInterval = new(5f, 8f);
        [SerializeField] Vector2 highHeightInterval = new(3.5f, 5f);
        [Min(0.1f), SerializeField] float highDifficultyHeight = 200f;
        [Min(1), SerializeField] int maxActiveRocks = 1;

        [Header("배치")]
        [Range(0f, 0.45f), SerializeField] float viewportSideMargin = 0.13f;
        [Min(0f), SerializeField] float playerHorizontalClearance = 0.7f;
        [Min(1), SerializeField] int xSelectionAttempts = 5;
        [Min(0.1f), SerializeField] float rockWorldWidth = 1.35f;
        [Min(0f), SerializeField] float topInset = 0.15f;

        [Header("낙하 설정")]
        [Min(0.05f), SerializeField] float warningDuration = 0.9f;
        [Min(0f), SerializeField] float initialFallSpeed = 4f;
        [Min(0f), SerializeField] float maxFallSpeed = 9f;
        [Min(0f), SerializeField] float fallAcceleration = 8f;
        [Min(0.1f), SerializeField] float maxLifetime = 8f;

        readonly List<FallingInkRock> active = new();
        ComponentPool<FallingInkRock> pool;
        GameManager subscribedManager;
        GameState previousState = GameState.Lobby;
        bool heightUnlocked;
        float spawnTimer;
        bool missingReferenceLogged;
        public float RuntimeIntervalMultiplier { get; set; } = 1f;

        void OnEnable()
        {
            TrySubscribeToGameManager();
        }

        void Start()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (collisionMask.value == 0)
                collisionMask = LayerMask.GetMask("Default", "Platform", "Player");
            else
                collisionMask |= LayerMask.GetMask("Player");
            EnsurePool();
            ValidateReferences();
            ResetSchedule();
            TrySubscribeToGameManager();
        }

        void OnDisable()
        {
            UnsubscribeFromGameManager();
            ClearActive();
            ResetSchedule();
            previousState = GameState.Lobby;
        }

        void Update()
        {
            TrySubscribeToGameManager();
            var manager = GameManager.Instance;
            GameState state = manager != null ? manager.State : GameState.Lobby;
            if (state != previousState)
            {
                if (state != GameState.Playing)
                    ClearActive();
                ResetSchedule();
                previousState = state;
            }

            CleanupList();
            if (manager == null || !manager.IsGameplayTicking || !ValidateReferences()) return;

            float height = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0f;
            if (height < startHeight) return;

            if (!heightUnlocked)
            {
                heightUnlocked = true;
                spawnTimer = initialDelay;
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f || active.Count >= maxActiveRocks) return;

            Spawn();
            spawnTimer = NextInterval(height);
        }

        void Spawn()
        {
            float spriteWidth = fallingInkRockSprite.bounds.size.x;
            float scale = spriteWidth > 0f ? rockWorldWidth / spriteWidth : 1f;
            float halfWidth = fallingInkRockSprite.bounds.extents.x * scale;
            float halfHeight = fallingInkRockSprite.bounds.extents.y * scale;

            float cameraDistance = -worldCamera.transform.position.z;
            float left = worldCamera.ViewportToWorldPoint(
                new Vector3(viewportSideMargin, 0f, cameraDistance)).x + halfWidth;
            float right = worldCamera.ViewportToWorldPoint(
                new Vector3(1f - viewportSideMargin, 0f, cameraDistance)).x - halfWidth;
            float top = worldCamera.ViewportToWorldPoint(
                new Vector3(0.5f, 1f, cameraDistance)).y;
            float x = ChooseSafestX(left, right);

            EnsurePool();
            var rock = pool.Acquire();
            var go = rock.gameObject;
            go.name = "FallingInkRock";
            go.layer = LayerMask.NameToLayer("Obstacle");
            go.transform.position = new Vector3(x, top - halfHeight - topInset, 0f);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = fallingInkRockSprite;
            renderer.sortingOrder = 6;
            renderer.color = Color.white;
            renderer.enabled = true;

            var body = go.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            // 고속 관통은 FallingInkRock의 이동 구간 CircleCast가 담당한다.
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

            var circle = go.GetComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = Mathf.Min(fallingInkRockSprite.bounds.extents.x,
                fallingInkRockSprite.bounds.extents.y) * 0.83f;
            circle.enabled = false;

            go.GetComponent<ObstacleVisibilityView>().Configure();
            rock.Initialize(this, worldCamera, collisionMask, warningDuration,
                initialFallSpeed, maxFallSpeed, fallAcceleration, maxLifetime);
            active.Add(rock);
        }

        FallingInkRock CreatePooledRock()
        {
            var go = new GameObject("PooledFallingInkRock")
            {
                layer = LayerMask.NameToLayer("Obstacle"),
            };
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            go.AddComponent<ObstacleVisibilityView>();
            return go.AddComponent<FallingInkRock>();
        }

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new ComponentPool<FallingInkRock>(CreatePooledRock, 2);
            var existing = GetComponentsInChildren<FallingInkRock>(true);
            for (int i = 0; i < existing.Length; i++)
                pool.Adopt(existing[i]);
        }

        float ChooseSafestX(float left, float right)
        {
            if (right <= left) return (left + right) * 0.5f;
            var livingPlayer = GameManager.Instance != null
                ? GameManager.Instance.HighestLivingPlayer
                : null;
            if (livingPlayer != null) player = livingPlayer;

            float safestX = (left + right) * 0.5f;
            float safestDistance = -1f;
            for (int i = 0; i < xSelectionAttempts; i++)
            {
                float candidate = GameplayRandom.Range(
                    GameplayRandomStream.FallingRocks, left, right);
                float distance = player != null
                    ? Mathf.Abs(candidate - player.transform.position.x)
                    : float.MaxValue;
                if (distance > safestDistance)
                {
                    safestDistance = distance;
                    safestX = candidate;
                }
                if (distance >= playerHorizontalClearance)
                    return candidate;
            }
            return safestX;
        }

        float NextInterval(float height)
        {
            float difficulty = Mathf.InverseLerp(startHeight, highDifficultyHeight, height);
            float minimum = Mathf.Lerp(lowHeightInterval.x, highHeightInterval.x, difficulty);
            float maximum = Mathf.Lerp(lowHeightInterval.y, highHeightInterval.y, difficulty);
            return GameplayRandom.Range(
                       GameplayRandomStream.FallingRocks, minimum, maximum) *
                   Mathf.Clamp(RuntimeIntervalMultiplier, 0.35f, 1f);
        }

        bool ValidateReferences()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (player == null)
            {
                player = GameManager.Instance != null
                    ? GameManager.Instance.HighestLivingPlayer
                    : FindFirstObjectByType<PlayerController>();
            }

            bool valid = fallingInkRockSprite != null && worldCamera != null && player != null;
            if (!valid && !missingReferenceLogged)
            {
                Debug.LogWarning("[MukJump] 낙묵석 Sprite/Camera/Player 참조가 없어 스폰을 중지합니다.", this);
                missingReferenceLogged = true;
            }
            else if (valid)
                missingReferenceLogged = false;
            return valid;
        }

        void ResetSchedule()
        {
            heightUnlocked = false;
            spawnTimer = initialDelay;
        }

        void TrySubscribeToGameManager()
        {
            var manager = GameManager.Instance;
            if (manager == subscribedManager) return;
            UnsubscribeFromGameManager();
            if (manager == null) return;
            subscribedManager = manager;
            subscribedManager.WorldHeightTeleported += HandleWorldHeightTeleported;
        }

        void UnsubscribeFromGameManager()
        {
            if (subscribedManager == null) return;
            subscribedManager.WorldHeightTeleported -= HandleWorldHeightTeleported;
            subscribedManager = null;
        }

        void HandleWorldHeightTeleported(int targetHeight)
        {
            ClearActive();
            ResetSchedule();
        }

        void CleanupList()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                // Resolved 상태라도 짧은 용해 코루틴이 끝나 소유 풀로 돌아오기 전까지는
                // 추적을 유지한다. 먼저 목록에서 빼면 비활성화 시 leased 객체가 고립된다.
                if (active[i] == null)
                    active.RemoveAt(i);
            }
        }

        void ClearActive()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var rock = active[i];
                if (rock == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                if (rock.IsResolved)
                    Release(rock);
                else
                    rock.ResolveImmediately();
            }
            active.Clear();
        }

        public void NotifyRemoved(FallingInkRock rock)
        {
            active.Remove(rock);
        }

        /// 낙묵석의 해결 경로가 Destroy와 목록 정리에 의존하지 않고 소유 풀로 돌아오게 한다.
        public void Release(FallingInkRock rock)
        {
            if (rock == null) return;
            active.Remove(rock);
            if (pool != null && pool.Release(rock)) return;
            Destroy(rock.gameObject);
        }

        void OnValidate()
        {
            startHeight = Mathf.Max(0f, startHeight);
            initialDelay = Mathf.Max(0f, initialDelay);
            lowHeightInterval.x = Mathf.Max(3.5f, lowHeightInterval.x);
            lowHeightInterval.y = Mathf.Max(lowHeightInterval.x, lowHeightInterval.y);
            highHeightInterval.x = Mathf.Max(3.5f, highHeightInterval.x);
            highHeightInterval.y = Mathf.Max(highHeightInterval.x, highHeightInterval.y);
            highDifficultyHeight = Mathf.Max(startHeight + 0.1f, highDifficultyHeight);
            maxActiveRocks = Mathf.Max(1, maxActiveRocks);
            xSelectionAttempts = Mathf.Max(1, xSelectionAttempts);
            rockWorldWidth = Mathf.Max(0.1f, rockWorldWidth);
            warningDuration = Mathf.Max(0.05f, warningDuration);
            maxFallSpeed = Mathf.Max(initialFallSpeed, maxFallSpeed);
            maxLifetime = Mathf.Max(warningDuration + 0.1f, maxLifetime);
        }
    }
}
