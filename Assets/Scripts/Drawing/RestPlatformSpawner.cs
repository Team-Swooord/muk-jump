using System.Collections.Generic;
using UnityEngine;
using MukJump.AI;
using MukJump.Core;

namespace MukJump.Drawing
{
    /// 일정 고도 간격마다 오래 유지되는 넓은 먹 발판을 배치해 플레이 호흡을 만든다.
    public class RestPlatformSpawner : MonoBehaviour
    {
        [SerializeField] Vector2 heightIntervalRange = new(38f, 58f);
        [SerializeField] Vector2 widthRange = new(3.8f, 5.2f);
        [SerializeField, Min(2f)] float spawnAheadHeight = 8f;
        [SerializeField, Min(1f)] float cleanupBelowCamera = 8f;

        public static RestPlatformSpawner Instance { get; private set; }

        readonly List<PlatformCollider> spawned = new();
        Camera worldCamera;
        float nextRestHeight;
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
            ScheduleNext(0f);
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            int height = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            while (height + spawnAheadHeight >= nextRestHeight)
            {
                SpawnAtGameHeight(nextRestHeight);
                ScheduleNext(nextRestHeight);
            }
            CleanupOldPlatforms();
        }

        public void DebugSpawnNearPlayer()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.HighestLivingPlayer : null;
            if (player == null) return;
            float halfWidth = worldCamera != null
                ? worldCamera.orthographicSize * worldCamera.aspect
                : 4.8f;
            float width = Mathf.Lerp(widthRange.x, widthRange.y, 0.75f);
            float x = Mathf.Clamp(player.transform.position.x, -halfWidth + width * 0.5f,
                halfWidth - width * 0.5f);
            SpawnPlatform(new Vector2(x, player.transform.position.y + 3.2f), width, "DEBUG");
            GameFeedbackController.Instance?.ShowZone("안전 먹 발판", "잠시 쉬었다 다시 오르세요");
        }

        public void DebugResetSchedule(int currentHeight)
        {
            ScheduleNext(Mathf.Max(0, currentHeight));
        }

        void SpawnAtGameHeight(float gameHeight)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.HighestLivingPlayer : null;
            if (player == null) return;

            int currentHeight = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            float worldY = player.transform.position.y + gameHeight - currentHeight;
            float halfWidth = worldCamera != null
                ? worldCamera.orthographicSize * worldCamera.aspect
                : 4.8f;
            float width = Random.Range(widthRange.x, widthRange.y);
            float xLimit = Mathf.Max(0.2f, halfWidth - width * 0.5f - 0.25f);
            float x = Random.Range(-xLimit, xLimit);
            SpawnPlatform(new Vector2(x, worldY), width, $"{Mathf.RoundToInt(gameHeight)}m");
        }

        void SpawnPlatform(Vector2 center, float width, string suffix)
        {
            var points = new List<Vector2>(7);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float x = Mathf.Lerp(center.x - width * 0.5f, center.x + width * 0.5f, t);
                float y = center.y + Mathf.Sin(t * Mathf.PI) * 0.08f;
                points.Add(new Vector2(x, y));
            }

            var platform = PlatformCollider.SpawnRestPlatform(points);
            platform.name = $"RestInkPlatform_{++platformIndex:00}_{suffix}";
            spawned.Add(platform);
            AddRestSeal(platform.transform, width);
        }

        static void AddRestSeal(Transform parent, float platformWidth)
        {
            var sealObject = new GameObject("RestSeal");
            sealObject.transform.SetParent(parent, false);
            sealObject.transform.localPosition = new Vector3(platformWidth * 0.34f, 0.25f, 0f);
            var line = sealObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 18;
            line.material = FallbackInkStyle.SharedInkMaterial;
            line.sortingOrder = 4;
            line.startWidth = line.endWidth = 0.055f;
            line.startColor = line.endColor = InkPalette.Red;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                float wobble = 1f + Mathf.Sin(angle * 5f) * 0.08f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                    0.2f * wobble);
            }
        }

        void ScheduleNext(float fromHeight)
        {
            nextRestHeight = fromHeight + Random.Range(heightIntervalRange.x, heightIntervalRange.y);
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
