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

        [SerializeField, Min(20f)] float zoneHeight = 100f;
        [SerializeField] float windAcceleration = 1.8f;
        [SerializeField, Range(0.4f, 1f)] float rainPlatformLifetimeMultiplier = 0.72f;
        [SerializeField, Range(0.35f, 1f)] float gorgeRockIntervalMultiplier = 0.62f;

        Zone currentZone;
        int currentBand = -1;
        FallingInkRockSpawner rockSpawner;
        Camera worldCamera;
        LineRenderer[] weatherLines;

        public Zone CurrentZone => currentZone;

        void Start()
        {
            rockSpawner = FindFirstObjectByType<FallingInkRockSpawner>();
            worldCamera = Camera.main;
            CreateWeatherLines();
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
        }

        void ApplyZone(int band)
        {
            currentBand = band;
            currentZone = (Zone)(band % 4);
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

        void OnDisable()
        {
            PlatformCollider.RuntimeLifetimeMultiplier = 1f;
            if (rockSpawner != null) rockSpawner.RuntimeIntervalMultiplier = 1f;
            if (weatherLines != null)
                for (int i = 0; i < weatherLines.Length; i++)
                    if (weatherLines[i] != null) weatherLines[i].enabled = false;
        }
    }
}
