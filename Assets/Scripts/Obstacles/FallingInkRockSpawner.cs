using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;
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
        [Range(0f, 0.45f), SerializeField] float viewportSideMargin = 0.08f;
        [Min(0f), SerializeField] float playerHorizontalClearance = 0.7f;
        [Min(1), SerializeField] int xSelectionAttempts = 5;
        [Min(0.1f), SerializeField] float rockWorldWidth = 1.35f;
        [Min(0f), SerializeField] float topInset = 0.15f;

        [Header("낙하 설정")]
        [Min(0.05f), SerializeField] float warningDuration = 0.8f;
        [Min(0f), SerializeField] float initialFallSpeed = 4f;
        [Min(0f), SerializeField] float maxFallSpeed = 9f;
        [Min(0f), SerializeField] float fallAcceleration = 8f;
        [Min(0.1f), SerializeField] float maxLifetime = 8f;

        readonly List<FallingInkRock> active = new();
        GameState previousState = GameState.Lobby;
        bool heightUnlocked;
        float spawnTimer;
        bool missingReferenceLogged;

        void Start()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (collisionMask.value == 0)
                collisionMask = LayerMask.GetMask("Default", "Platform");
            ValidateReferences();
            ResetSchedule();
        }

        void Update()
        {
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
            if (state != GameState.Playing || !ValidateReferences()) return;

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

            var go = new GameObject("FallingInkRock")
            {
                layer = LayerMask.NameToLayer("Obstacle"),
            };
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, top - halfHeight - topInset, 0f);
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = fallingInkRockSprite;
            renderer.sortingOrder = 4;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            // 고속 관통은 FallingInkRock의 이동 구간 CircleCast가 담당한다.
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = Mathf.Min(fallingInkRockSprite.bounds.extents.x,
                fallingInkRockSprite.bounds.extents.y) * 0.83f;
            circle.enabled = false;

            var rock = go.AddComponent<FallingInkRock>();
            rock.Initialize(this, worldCamera, collisionMask, warningDuration,
                initialFallSpeed, maxFallSpeed, fallAcceleration, maxLifetime);
            active.Add(rock);
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
                float candidate = Random.Range(left, right);
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
            return Random.Range(minimum, maximum);
        }

        bool ValidateReferences()
        {
            bool valid = fallingInkRockSprite != null && worldCamera != null && player != null;
            if (!valid && !missingReferenceLogged)
            {
                Debug.LogWarning("[MukJump] 낙묵석 Sprite/Camera/Player 참조가 없어 스폰을 중지합니다.", this);
                missingReferenceLogged = true;
            }
            return valid;
        }

        void ResetSchedule()
        {
            heightUnlocked = false;
            spawnTimer = initialDelay;
        }

        void CleanupList()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] == null || active[i].IsResolved)
                    active.RemoveAt(i);
            }
        }

        void ClearActive()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] != null)
                    active[i].ResolveImmediately();
            }
            active.Clear();
        }

        public void NotifyRemoved(FallingInkRock rock)
        {
            active.Remove(rock);
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
