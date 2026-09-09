using UnityEngine;
using MukJump.AI;

namespace MukJump.Core
{
    /// 전 맵 공통 바람을 고정 개수의 붓결 선으로 보여 준다.
    /// 물리와 일정은 WindWeatherController가 맡고, 이 컴포넌트는 표현만 담당한다.
    public sealed class WindWeatherView : MonoBehaviour
    {
        const int LineCount = 10;

        [SerializeField, Range(0.02f, 0.2f)] float breezeAlpha = 0.08f;
        [SerializeField, Range(0.05f, 0.35f)] float updraftAlpha = 0.22f;

        readonly LineRenderer[] lines = new LineRenderer[LineCount];
        Camera worldCamera;

        void Update()
        {
            var manager = GameManager.Instance;
            var weather = WindWeatherController.Instance;
            bool visible = manager != null && manager.IsGameplayTicking &&
                           weather != null;
            if (!visible)
            {
                SetLinesVisible(false);
                return;
            }

            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;
            EnsureLines();
            UpdateLines(weather);
        }

        void EnsureLines()
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null) continue;

                string childName = $"WindWeatherLine_{i:00}";
                var child = transform.Find(childName);
                if (child == null)
                {
                    var lineObject = new GameObject(childName);
                    child = lineObject.transform;
                    child.SetParent(transform, false);
                }

                var line = child.GetComponent<LineRenderer>();
                if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.sharedMaterial = FallbackInkStyle.SharedTintableBrushMaterial;
                line.textureMode = LineTextureMode.Stretch;
                line.sortingOrder = 1;
                line.startWidth = line.endWidth = 0.025f + i % 3 * 0.008f;
                line.enabled = true;
                lines[i] = line;
            }
        }

        void UpdateLines(WindWeatherController weather)
        {
            bool rising = weather.Phase == WindWeatherPhase.Warning ||
                          weather.Phase == WindWeatherPhase.Updraft;
            float phaseStrength = weather.Phase switch
            {
                WindWeatherPhase.Warning => Mathf.Lerp(0.45f, 0.9f, weather.Strength01),
                WindWeatherPhase.Updraft => 1f,
                WindWeatherPhase.Recovery => 0.45f,
                _ => 0f,
            };
            float alpha = Mathf.Lerp(breezeAlpha, updraftAlpha, phaseStrength);
            if (rising) alpha = Mathf.Min(alpha, .12f);
            float halfHeight = worldCamera.orthographicSize;
            float halfWidth = halfHeight * worldCamera.aspect;
            Vector3 center = worldCamera.transform.position;
            float direction = Mathf.Abs(weather.DirectionBlend) > 0.05f
                ? Mathf.Sign(weather.DirectionBlend)
                : weather.DirectionSign;
            int activeLineCount = Mathf.Clamp(
                VfxQualityRuntime.Profile.WeatherLineCount,
                1,
                lines.Length);
            if (rising) activeLineCount = Mathf.Min(activeLineCount, 4);
            // HUD의 실제 하단(상단 기준 논리 좌표)을 월드로 옮겨 입자 끝까지 그 아래에 둔다.
            Rect hud = GameplayHudView.CalculateTopHudRect(MobileUiLayout.CurrentSafeArea,
                Screen.width, Screen.height);
            float ceiling = Mathf.Clamp(1f + hud.yMin / MobileUiLayout.ReferenceHeight - .025f, .25f, .90f);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (i >= activeLineCount)
                {
                    line.enabled = false;
                    continue;
                }
                line.enabled = true;
                Color color = InkPalette.WindAccent;
                color.a = alpha * (0.78f + i % 3 * 0.11f);
                line.startColor = line.endColor = color;

                if (rising)
                {
                    float travel = Mathf.Repeat(Time.time * .20f + i * .25f, 1f);
                    float x = center.x + Mathf.Lerp(-.78f, .78f, (i + .5f) / activeLineCount) * halfWidth;
                    float length = .38f + i % 3 * .08f;
                    float bottom = center.y - halfHeight * .72f + length * .5f;
                    float top = center.y - halfHeight + ceiling * halfHeight * 2f - length * .5f;
                    float y = Mathf.Lerp(bottom, Mathf.Max(bottom, top), travel);
                    float lean = direction * .035f;
                    color.a *= Mathf.SmoothStep(0f, 1f, travel / .12f) *
                        Mathf.SmoothStep(0f, 1f, (1f - travel) / .18f);
                    line.startColor = line.endColor = color;
                    line.startWidth = line.endWidth = .018f;
                    line.SetPosition(0, new Vector3(x, y - length * 0.5f, 0f));
                    line.SetPosition(1, new Vector3(x + lean, y + length * 0.5f, 0f));
                }
                else
                {
                    line.startWidth = line.endWidth = .025f + i % 3 * .008f;
                    float travel = Mathf.Repeat(Time.time * (0.25f + weather.Strength01 * 0.55f) +
                                                i * 0.157f, 1f);
                    if (direction < 0f) travel = 1f - travel;
                    float x = center.x - halfWidth + travel * halfWidth * 2f;
                    float y = center.y - halfHeight +
                              Mathf.Repeat(i * 0.219f, 1f) * halfHeight * 2f;
                    float length = 0.45f + i % 4 * 0.15f;
                    line.SetPosition(0, new Vector3(x - direction * length * 0.5f, y, 0f));
                    line.SetPosition(1, new Vector3(x + direction * length * 0.5f,
                        y + Mathf.Sin(i * 1.7f) * 0.04f, 0f));
                }
            }
        }

        void SetLinesVisible(bool visible)
        {
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null)
                    lines[i].enabled = visible;
        }

        void OnDisable()
        {
            SetLinesVisible(false);
        }
    }
}
