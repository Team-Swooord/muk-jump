using UnityEngine;
using MukJump.Drawing;
using MukJump.Obstacles;
using MukJump.AI;

namespace MukJump.Core
{
    /// 고도에 따라 맵·먹비·낙묵석 협곡 규칙을 순환시켜 한 판 안에 변화를 만든다.
    /// 전 맵 공통 바람과 상승기류는 WindWeatherController가 독립적으로 관리한다.
    /// 점수는 기존처럼 최고 고도만 사용하며 구간 자체는 추가 점수를 주지 않는다.
    public class HeightZoneController : MonoBehaviour
    {
        public enum Zone
        {
            QuietMountain,
            WindPass,
            InkRain,
            RockGorge,
        }

        [SerializeField, Min(20f)] float zoneHeight = 250f;
        [SerializeField, Range(0.4f, 1f)] float rainPlatformLifetimeMultiplier = 0.72f;
        [SerializeField, Range(0.35f, 1f)] float gorgeRockIntervalMultiplier = 0.62f;

        const int DefaultBaseMapCount = 4;

        Zone currentZone;
        int currentBand = -1;
        FallingInkRockSpawner rockSpawner;
        Camera worldCamera;
        MapBackgroundView backgroundView;
        LineRenderer[] weatherLines;
        LineRenderer[] gorgeLines;

        public Zone CurrentZone => currentZone;

        void OnEnable()
        {
            // OnDisable에서 전역 배율을 기본값으로 되돌리므로 재활성화 시 같은
            // 고도 구간이라도 반드시 설정을 다시 적용한다.
            currentBand = -1;
        }

        void Start()
        {
            rockSpawner = FindFirstObjectByType<FallingInkRockSpawner>();
            worldCamera = Camera.main;
            backgroundView = FindFirstObjectByType<MapBackgroundView>();
            ApplyZone(0);
        }

        void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameplayTicking)
                return;

            int height = Mathf.RoundToInt(GameManager.Instance.SwarmProgressHeight);
            int band = Mathf.Max(0, Mathf.FloorToInt(height / zoneHeight));
            if (band != currentBand) ApplyZone(band);

            UpdateWeatherVisuals();
            UpdateGorgeVisuals();
        }

        void ApplyZone(int band)
        {
            currentBand = band;
            currentZone = (Zone)(band % 4);
            if (backgroundView == null)
                backgroundView = FindFirstObjectByType<MapBackgroundView>();
            int baseMapCount = backgroundView != null
                ? Mathf.Max(1, backgroundView.BaseStageCount)
                : DefaultBaseMapCount;
            int endlessMapCount = backgroundView != null
                ? backgroundView.EndlessStageCount
                : 0;
            int mapStage = ResolveMapStage(band, baseMapCount, endlessMapCount);
            bool mirrorMap = ResolveMapMirror(band, baseMapCount, endlessMapCount);
            ApplyMapStage(mapStage, mirrorMap);
            if (currentZone == Zone.InkRain && weatherLines == null)
                CreateWeatherLines();
            if (currentZone == Zone.RockGorge && gorgeLines == null)
                CreateGorgeLines();
            PlatformCollider.RuntimeLifetimeMultiplier =
                currentZone == Zone.InkRain ? rainPlatformLifetimeMultiplier : 1f;
            if (rockSpawner == null) rockSpawner = FindFirstObjectByType<FallingInkRockSpawner>();
            if (rockSpawner != null)
                rockSpawner.RuntimeIntervalMultiplier =
                    currentZone == Zone.RockGorge ? gorgeRockIntervalMultiplier : 1f;

            if (band <= 0) return;
            (string title, string subtitle) = mapStage >= baseMapCount
                ? CosmicAnnouncement(mapStage - baseMapCount)
                : currentZone switch
                {
                    Zone.WindPass => ("바람 고개", "산등성이의 바람이 조금 거세집니다"),
                    Zone.InkRain => ("먹비 골짜기", "그린 발판이 더 빨리 마릅니다"),
                    Zone.RockGorge => ("낙묵 협곡", "낙묵석이 더 자주 떨어집니다"),
                    _ => ("고요한 산길", "잠시 숨을 고르세요"),
                };
            GameFeedbackController.Instance?.ShowZone(title, subtitle);
        }

        public static int ResolveMapStage(int band, int baseMapCount, int endlessMapCount)
        {
            int safeBand = Mathf.Max(0, band);
            int safeBaseCount = Mathf.Max(1, baseMapCount);
            if (safeBand < safeBaseCount) return safeBand;
            if (endlessMapCount <= 0) return safeBaseCount - 1;
            return safeBaseCount +
                   (safeBand - safeBaseCount) % endlessMapCount;
        }

        public static bool ResolveMapMirror(int band, int baseMapCount, int endlessMapCount)
        {
            int safeBaseCount = Mathf.Max(1, baseMapCount);
            if (band < safeBaseCount || endlessMapCount <= 0) return false;
            int cycle = (band - safeBaseCount) / endlessMapCount;
            return cycle % 2 == 1;
        }

        static (string title, string subtitle) CosmicAnnouncement(int cosmicStage)
        {
            return (cosmicStage % 3) switch
            {
                0 => ("먹빛 성문", "산과 별이 한 획으로 이어집니다"),
                1 => ("월련 성해", "달빛 연꽃 성운이 피어납니다"),
                _ => ("천하수", "은하가 먹강처럼 흐릅니다"),
            };
        }

        void ApplyMapStage(int stage, bool mirrorX = false)
        {
            if (backgroundView == null) backgroundView = FindFirstObjectByType<MapBackgroundView>();
            backgroundView?.SetStage(stage, false, mirrorX);
            if (worldCamera != null)
            {
                worldCamera.backgroundColor = stage switch
                {
                    1 => new Color(0.9f, 0.9f, 0.84f),
                    2 => new Color(0.87f, 0.87f, 0.81f),
                    3 => new Color(0.84f, 0.81f, 0.73f),
                    4 => new Color(0.86f, 0.85f, 0.8f),
                    5 => new Color(0.87f, 0.86f, 0.9f),
                    6 => new Color(0.84f, 0.86f, 0.87f),
                    _ => InkPalette.Paper,
                };
            }
        }

        void CreateWeatherLines()
        {
            weatherLines = new LineRenderer[14];
            for (int i = 0; i < weatherLines.Length; i++)
            {
                var lineObject = new GameObject($"ZoneWeatherLine_{i:00}");
                lineObject.transform.SetParent(transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
                line.sortingOrder = 1;
                line.startWidth = line.endWidth = 0.025f + i % 3 * 0.009f;
                line.enabled = false;
                weatherLines[i] = line;
            }
        }

        void CreateGorgeLines()
        {
            gorgeLines = new LineRenderer[6];
            for (int i = 0; i < gorgeLines.Length; i++)
            {
                var lineObject = new GameObject($"GorgeCliffLine_{i:00}");
                lineObject.transform.SetParent(transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 6;
                line.sharedMaterial = FallbackInkStyle.SharedInkMaterial;
                line.sortingOrder = -1;
                line.startWidth = line.endWidth = 0.05f + i % 2 * 0.025f;
                line.enabled = false;
                gorgeLines[i] = line;
            }
        }

        void UpdateWeatherVisuals()
        {
            if (weatherLines == null || worldCamera == null) return;
            bool rain = currentZone == Zone.InkRain;
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * worldCamera.aspect;
            Vector3 center = worldCamera.transform.position;

            for (int i = 0; i < weatherLines.Length; i++)
            {
                var line = weatherLines[i];
                line.enabled = rain;
                if (!line.enabled) continue;

                float phase = Mathf.Repeat(Time.time * 3.1f + i * 0.173f, 1f);
                float x = center.x - halfWidth + Mathf.Repeat(i * 1.71f, halfWidth * 2f);
                float y = center.y - halfHeight + phase * halfHeight * 2f;
                Color color = InkPalette.Ink;
                color.a = 0.18f;
                line.startColor = line.endColor = color;

                float length = 0.8f + i % 4 * 0.2f;
                line.SetPosition(0, new Vector3(x + 0.18f, y + length, 0f));
                line.SetPosition(1, new Vector3(x - 0.18f, y, 0f));
            }
        }

        void UpdateGorgeVisuals()
        {
            if (gorgeLines == null || worldCamera == null) return;
            bool visible = currentZone == Zone.RockGorge;
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * worldCamera.aspect;
            Vector3 center = worldCamera.transform.position;
            for (int i = 0; i < gorgeLines.Length; i++)
            {
                var line = gorgeLines[i];
                line.enabled = visible;
                if (!visible) continue;

                bool left = i % 2 == 0;
                float depth = i / 2 * 0.34f;
                float edgeX = center.x + (left ? -halfWidth : halfWidth) +
                              (left ? 1f : -1f) * (0.15f + depth);
                for (int point = 0; point < line.positionCount; point++)
                {
                    float t = point / (float)(line.positionCount - 1);
                    float y = center.y - halfHeight + t * halfHeight * 2f;
                    float jag = Mathf.Sin(point * 2.7f + i * 1.3f) *
                                (0.18f + depth * 0.22f);
                    line.SetPosition(point, new Vector3(edgeX + (left ? jag : -jag), y, 0f));
                }
                Color color = InkPalette.Ink;
                color.a = 0.17f + i / 2 * 0.045f;
                line.startColor = line.endColor = color;
            }
        }

        void OnDisable()
        {
            currentBand = -1;
            PlatformCollider.RuntimeLifetimeMultiplier = 1f;
            if (rockSpawner != null) rockSpawner.RuntimeIntervalMultiplier = 1f;
            if (weatherLines != null)
                for (int i = 0; i < weatherLines.Length; i++)
                    if (weatherLines[i] != null) weatherLines[i].enabled = false;
            if (gorgeLines != null)
                for (int i = 0; i < gorgeLines.Length; i++)
                    if (gorgeLines[i] != null) gorgeLines[i].enabled = false;
            backgroundView?.SetStage(0, true);
        }
    }
}
