using System.Collections.Generic;
using UnityEngine;
using MukJump.Core;

namespace MukJump.Obstacles
{
    /// 카메라 위쪽에 좌우 이동 장애물을 미리 만들고 지나간 장애물을 정리한다.
    /// 높이 올라갈수록 이동 속도가 점진적으로 증가한다.
    public class ObstacleSpawner : MonoBehaviour
    {
        [SerializeField] Sprite obstacleSprite;
        [SerializeField] float firstSpawnHeight = 14f;
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

        readonly List<Obstacle> active = new();
        Camera cam;
        float nextSpawnY;

        void Start()
        {
            cam = Camera.main;
            nextSpawnY = firstSpawnHeight;
            if (obstacleSprite == null)
                Debug.LogWarning("[MukJump] 장애물 스프라이트가 없어 장애물을 생성하지 않습니다.", this);
        }

        void Update()
        {
            if (cam == null || obstacleSprite == null || GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Playing) return;

            float cameraTop = cam.transform.position.y + cam.orthographicSize;
            while (nextSpawnY <= cameraTop + spawnAhead)
            {
                Spawn(nextSpawnY);
                nextSpawnY += Random.Range(verticalSpacing.x, verticalSpacing.y);
            }

            float cutoff = cam.transform.position.y - cam.orthographicSize - despawnBelow;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] == null)
                {
                    active.RemoveAt(i);
                    continue;
                }
                if (active[i].transform.position.y >= cutoff) continue;
                Destroy(active[i].gameObject);
                active.RemoveAt(i);
            }
        }

        void Spawn(float y)
        {
            float courseHeight = ScoreManager.Instance != null ? ScoreManager.Instance.HeightAt(y) : y;
            ObstacleMotion motion = ObstacleMotion.Horizontal;
            float amplitude = Random.Range(moveAmplitudeRange.x, moveAmplitudeRange.y);
            float minX = horizontalRange.x;
            float maxX = horizontalRange.y;
            if (motion == ObstacleMotion.Horizontal)
            {
                minX += amplitude;
                maxX -= amplitude;
            }

            var go = new GameObject("InkObstacle")
            {
                layer = LayerMask.NameToLayer("Obstacle"),
            };
            go.transform.position = new Vector3(Random.Range(minX, maxX), y, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = obstacleSprite;
            renderer.sortingOrder = 3;
            float spriteWidth = obstacleSprite.bounds.size.x;
            float scale = spriteWidth > 0f ? obstacleWorldWidth / spriteWidth : 1f;
            go.transform.localScale = Vector3.one * scale;

            var circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            // 바깥쪽 반투명 먹 번짐보다 실제 가시 몸통에 맞춰 판정을 약간 줄인다.
            circle.radius = obstacleSprite.bounds.extents.x * 0.78f;

            var obstacle = go.AddComponent<Obstacle>();
            float difficulty = Mathf.InverseLerp(0f, maxSpeedHeight, courseHeight);
            float minSpeed = Mathf.Lerp(baseMoveSpeedRange.x, maxMoveSpeedRange.x, difficulty);
            float maxSpeed = Mathf.Lerp(baseMoveSpeedRange.y, maxMoveSpeedRange.y, difficulty);
            obstacle.Configure(motion, amplitude,
                Random.Range(minSpeed, maxSpeed), Random.Range(0f, Mathf.PI * 2f));
            active.Add(obstacle);
        }
    }
}
