using UnityEngine;
using MukJump.Player;
using MukJump.Drawing;
using MukJump.Obstacles;
using MukJump.AI;

namespace MukJump.Core
{
    /// 고도에 따라 바람·먹비·낙묵석 협곡 규칙을 순환시켜 한 판 안에 변화를 만든다.
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
        [SerializeField] float windAcceleration = 1.8f;
        [SerializeField, Range(0.4f, 1f)] float rainPlatformLifetimeMultiplier = 0.72f;
        [SerializeField, Range(0.35f, 1f)] float gorgeRockIntervalMultiplier = 0.62f;

        Zone currentZone;
        int currentBand = -1;
        FallingInkRockSpawner rockSpawner;
        Camera worldCamera;
        SpriteRenderer backgroundRenderer;
        LineRenderer[] weatherLines;
        LineRenderer[] gorgeLines;

        public Zone CurrentZone => currentZone;
        public int CurrentMapStage { get; private set; }
        public float ZoneHeight => zoneHeight;

        void Start()
        {
            rockSpawner = FindFirstObjectByType<FallingInkRockSpawner>();
            worldCamera = Camera.main;
            var background = GameObject.Find("Background");
            if (background != null) backgroundRenderer = background.GetComponent<SpriteRenderer>();
            CreateWeatherLines();
            CreateGorgeLines();
            ApplyZone(0);
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            int height = ScoreManager.Instance != null ? ScoreManager.Instance.Height : 0;
            int band = Mathf.Max(0, Mathf.FloorToInt(height / zoneHeight));
            if (band != currentBand) ApplyZone(band);

            if (currentZone == Zone.WindPass)
                ApplyWind();
            UpdateWeatherVisuals();
            UpdateGorgeVisuals();
        }

        void ApplyZone(int band)
        {
            currentBand = band;
            currentZone = (Zone)(band % 4);
            ApplyMapStage(Mathf.Clamp(band, 0, 3));
            PlatformCollider.RuntimeLifetimeMultiplier =
                currentZone == Zone.InkRain ? rainPlatformLifetimeMultiplier : 1f;
            if (rockSpawner == null) rockSpawner = FindFirstObjectByType<FallingInkRockSpawner>();
            if (rockSpawner != null)
                rockSpawner.RuntimeIntervalMultiplier =
                    currentZone == Zone.RockGorge ? gorgeRockIntervalMultiplier : 1f;

            if (band <= 0) return;
            (string title, string subtitle) = currentZone switch
            {
                Zone.WindPass => ("바람 고개", "옆바람이 먹방울을 밀어냅니다"),
                Zone.InkRain => ("먹비 골짜기", "그린 발판이 더 빨리 마릅니다"),
                Zone.RockGorge => ("낙묵 협곡", "낙묵석이 더 자주 떨어집니다"),
                _ => ("고요한 산길", "잠시 숨을 고르세요"),
            };
            GameFeedbackController.Instance?.ShowZone(title, subtitle);
        }

        void ApplyMapStage(int stage)
        {
            CurrentMapStage = stage;
            Color tint = stage switch
            {
                1 => new Color(0.88f, 0.93f, 0.94f, 1f),
                2 => new Color(0.76f, 0.84f, 0.86f, 1f),
                3 => new Color(0.72f, 0.66f, 0.57f, 1f),
                _ => Color.white,
            };
            if (backgroundRenderer != null) backgroundRenderer.color = tint;
            if (worldCamera != null)
            {
                worldCamera.backgroundColor = stage switch
                {
                    1 => new Color(0.78f, 0.84f, 0.84f),
                    2 => new Color(0.62f, 0.69f, 0.7f),
                    3 => new Color(0.42f, 0.39f, 0.34f),
                    _ => InkPalette.Paper,
                };
            }
        }

        void ApplyWind()
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            float direction = Mathf.Sin(Time.time * 0.42f) >= 0f ? 1f : -1f;
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 0.85f));
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].IsDead) continue;
                var body = players[i].GetComponent<Rigidbody2D>();
                if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
                    body.AddForce(Vector2.right * (direction * windAcceleration * pulse),
                        ForceMode2D.Force);
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
                line.material = FallbackInkStyle.SharedInkMaterial;
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
                line.material = FallbackInkStyle.SharedInkMaterial;
                line.sortingOrder = -1;
                line.startWidth = line.endWidth = 0.05f + i % 2 * 0.025f;
                line.enabled = false;
                gorgeLines[i] = line;
            }
        }

        void UpdateWeatherVisuals()
        {
            if (weatherLines == null || worldCamera == null) return;
            bool wind = currentZone == Zone.WindPass;
            bool rain = currentZone == Zone.InkRain;
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * worldCamera.aspect;
            Vector3 center = worldCamera.transform.position;

            for (int i = 0; i < weatherLines.Length; i++)
            {
                var line = weatherLines[i];
                line.enabled = wind || rain;
                if (!line.enabled) continue;

                float phase = Mathf.Repeat(Time.time * (wind ? 1.7f : 3.1f) +
                                           i * 0.173f, 1f);
                float x = center.x - halfWidth + Mathf.Repeat(i * 1.71f, halfWidth * 2f);
                float y = center.y - halfHeight + phase * halfHeight * 2f;
                Color color = InkPalette.Ink;
                color.a = wind ? 0.12f : 0.18f;
                line.startColor = line.endColor = color;

                if (wind)
                {
                    float direction = Mathf.Sin(Time.time * 0.42f) >= 0f ? 1f : -1f;
                    float length = 0.6f + i % 4 * 0.22f;
                    line.SetPosition(0, new Vector3(x - direction * length * 0.5f, y, 0f));
                    line.SetPosition(1, new Vector3(x + direction * length * 0.5f,
                        y + Mathf.Sin(i * 1.4f) * 0.08f, 0f));
                }
                else
                {
                    float length = 0.8f + i % 4 * 0.2f;
                    line.SetPosition(0, new Vector3(x + 0.18f, y + length, 0f));
                    line.SetPosition(1, new Vector3(x - 0.18f, y, 0f));
                }
            }
        }

        void UpdateGorgeVisuals()
        {
            if (gorgeLines == null || worldCamera == null) return;
            bool visible = CurrentMapStage >= 3;
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
            PlatformCollider.RuntimeLifetimeMultiplier = 1f;
            if (rockSpawner != null) rockSpawner.RuntimeIntervalMultiplier = 1f;
            if (weatherLines != null)
                for (int i = 0; i < weatherLines.Length; i++)
                    if (weatherLines[i] != null) weatherLines[i].enabled = false;
            if (gorgeLines != null)
                for (int i = 0; i < gorgeLines.Length; i++)
                    if (gorgeLines[i] != null) gorgeLines[i].enabled = false;
            if (backgroundRenderer != null) backgroundRenderer.color = Color.white;
        }
    }
}
