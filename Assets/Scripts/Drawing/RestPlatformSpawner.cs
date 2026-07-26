using System.Collections.Generic;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;

namespace MukJump.Drawing
{
    /// 일정 고도 간격마다 상승 기류를 주는 풍맥 발판을 배치한다.
    /// 기존 씬·빌더 호환을 위해 클래스 이름은 유지한다.
    public class RestPlatformSpawner : MonoBehaviour
    {
        [SerializeField] Vector2 windHeightIntervalRange = new(82f, 128f);
        [SerializeField, Min(2f)] float spawnAheadHeight = 8f;
        [SerializeField, Min(1f)] float cleanupBelowCamera = 8f;

        public static RestPlatformSpawner Instance { get; private set; }

        readonly List<PlatformCollider> spawned = new();
        Camera worldCamera;
        float nextWindHeight;
        int platformIndex;

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            worldCamera = Camera.main;
            ScheduleNextWind(25f);
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            int height = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            while (height + spawnAheadHeight >= nextWindHeight)
            {
                SpawnWindAtGameHeight(nextWindHeight);
                ScheduleNextWind(nextWindHeight);
            }
            CleanupOldPlatforms();
        }

        public void DebugResetSchedule(int currentHeight)
        {
            ScheduleNextWind(Mathf.Max(0, currentHeight));
        }

        public void DebugSpawnWindNearPlayer()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.HighestLivingPlayer : null;
            if (player == null) return;
            SpawnWindPlatform(new Vector2(player.transform.position.x,
                player.transform.position.y + 3.2f), 3.4f, "DEBUG");
            GameFeedbackController.Instance?.ShowZone("풍맥 발판", "위에 착지해 상승 기류를 타세요");
        }

        void SpawnWindAtGameHeight(float gameHeight)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.HighestLivingPlayer : null;
            if (player == null) return;
            int currentHeight = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            float worldY = player.transform.position.y + gameHeight - currentHeight;
            float halfWidth = worldCamera != null
                ? worldCamera.orthographicSize * worldCamera.aspect
                : 4.8f;
            float width = Random.Range(2.8f, 3.8f);
            float limit = Mathf.Max(0.2f, halfWidth - width * 0.5f - 0.25f);
            SpawnWindPlatform(new Vector2(Random.Range(-limit, limit), worldY), width,
                $"{Mathf.RoundToInt(gameHeight)}m");
        }

        void SpawnWindPlatform(Vector2 center, float width, string suffix)
        {
            var points = new List<Vector2>(7);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                points.Add(new Vector2(Mathf.Lerp(center.x - width * 0.5f,
                    center.x + width * 0.5f, t), center.y + Mathf.Sin(t * Mathf.PI) * 0.12f));
            }
            var platform = PlatformCollider.SpawnWindCurrentPlatform(points);
            platform.name = $"WindCurrentPlatform_{++platformIndex:00}_{suffix}";
            spawned.Add(platform);
            AddWindMark(platform.transform);
        }

        static void AddWindMark(Transform parent)
        {
            for (int arc = 0; arc < 3; arc++)
            {
                var mark = new GameObject($"WindArc_{arc + 1}");
                mark.transform.SetParent(parent, false);
                mark.transform.localPosition = new Vector3(0f, 0.25f + arc * 0.22f, 0f);
                var line = mark.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 9;
                line.sharedMaterial = FallbackInkStyle.SharedTintableBrushMaterial;
                line.sortingOrder = 4;
                line.startWidth = line.endWidth = 0.035f;
                var accent = InkPalette.WindAccent;
                accent.a = 0.8f;
                line.startColor = line.endColor = accent;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = i / (line.positionCount - 1f);
                    line.SetPosition(i, new Vector3(Mathf.Sin(t * Mathf.PI) * (0.22f + arc * 0.04f),
                        t * 0.18f, 0f));
                }
            }
        }

        void ScheduleNextWind(float fromHeight)
        {
            nextWindHeight = fromHeight +
                             Random.Range(windHeightIntervalRange.x, windHeightIntervalRange.y);
        }

        void CleanupOldPlatforms()
        {
            if (worldCamera == null) return;
            float cutoff = worldCamera.transform.position.y - worldCamera.orthographicSize -
                           cleanupBelowCamera;
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] == null)
                {
                    spawned.RemoveAt(i);
                    continue;
                }
                if (spawned[i].transform.position.y >= cutoff) continue;
                Destroy(spawned[i].gameObject);
                spawned.RemoveAt(i);
            }
        }
    }
}
