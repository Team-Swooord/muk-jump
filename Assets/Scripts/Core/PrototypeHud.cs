using UnityEngine;
using MukJump.Drawing;

namespace MukJump.Core
{
    /// 그레이박스 단계용 임시 HUD (OnGUI). Week 2에서 한지 스타일 uGUI로 교체 예정.
    public class PrototypeHud : MonoBehaviour
    {
        [Header("먹 게이지 이미지 (붓 획 모양) — 미할당 시 단색 막대로 폴백")]
        [Tooltip("붓 획 실루엣, 채워진 상태 (왼쪽 가늘게 → 오른쪽 두껍게)")]
        [SerializeField] Texture2D inkGaugeFill;
        [Tooltip("같은 실루엣의 빈 상태 트랙 (fill과 캔버스·위치 동일)")]
        [SerializeField] Texture2D inkGaugeTrack;
        [Tooltip("게이지 오른쪽 끝의 붓 아이콘")]
        [SerializeField] Texture2D inkBrushIcon;
        [Tooltip("황금 붓 아이템 활성 중 게이지 끝에 표시할 실제 아이템 이미지")]
        [SerializeField] Texture2D goldenBrushItemIcon;

        StrokeCapture strokeCapture;
        Texture2D goldenBrushIcon;

        void Start()
        {
            strokeCapture = FindFirstObjectByType<StrokeCapture>();
            goldenBrushIcon = goldenBrushItemIcon != null
                ? goldenBrushItemIcon
                : CreateColoredSilhouette(inkBrushIcon, new Color(1f, 0.68f, 0.08f));
        }

        void OnGUI()
        {
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.State == GameState.Lobby)
                return;

            if (GameManager.Instance.State == GameState.GameOver)
            {
                return;
            }

            // 화면 하단 먹 게이지: 전역 잉크 잔량
            if (strokeCapture != null)
                DrawInkGauge(strokeCapture.InkRemaining01);
        }

        /// 붓 획 모양 먹 게이지: 트랙 위에 fill을 왼쪽부터 잔량만큼 잘라 그리고,
        /// 오른쪽 끝에 붓 아이콘을 붙인다. 이미지 미할당 시 단색 막대 폴백.
        void DrawInkGauge(float ratio)
        {
            float baseRatio = Mathf.Clamp01(ratio);
            float reserveRatio = Mathf.Max(0f, ratio - 1f);
            if (inkGaugeFill == null || inkGaugeTrack == null)
            {
                float bw = Screen.width * 0.6f;
                float bh = Screen.height * 0.014f;
                float by = Screen.height * 0.955f;
                var back = new Rect((Screen.width - bw) / 2, by, bw, bh);
                DrawRect(back, InkPalette.Paper2);
                var fillRect = back;
                fillRect.width = bw * baseRatio;
                DrawRect(fillRect, InkPalette.Ink);
                DrawReserveGauge(back, reserveRatio);
                return;
            }

            // 게이지 본체: 화면 폭 55%, 세로는 이미지 비율(4:1) 유지
            float w = Screen.width * 0.55f;
            float h = w * (inkGaugeFill.height / (float)inkGaugeFill.width);
            float iconSize = inkBrushIcon != null ? h * 1.0f : 0f;
            float overlap = iconSize * 0.65f;     // 붓이 획의 끝을 그리고 있는 것처럼 깊게 겹침
            float totalW = w + iconSize - overlap;

            // 아이콘(게이지보다 큼)까지 포함한 전체가 화면 아래로 짤리지 않도록 배치
            float clusterH = Mathf.Max(h, iconSize);
            float centerY = Screen.height * 0.975f - clusterH / 2;
            float x = (Screen.width - totalW) / 2;
            float y = centerY - h / 2;

            var area = new Rect(x, y, w, h);
            GUI.DrawTexture(area, inkGaugeTrack, ScaleMode.StretchToFill);

            bool golden = strokeCapture != null && strokeCapture.HasUnlimitedInk;

            if (baseRatio > 0f)
            {
                // 왼쪽부터 잔량 비율만큼만 가로로 잘라 그린다 (UV도 같은 비율로 잘라 왜곡 방지)
                // 먹이 줄어들 때 왼쪽부터 비워지고 붓이 있는 오른쪽 방향으로 잔량이 남는다.
                float remainingX = x + w * (1f - baseRatio);
                var clipped = new Rect(remainingX, y, w * baseRatio, h);
                GUI.DrawTextureWithTexCoords(clipped, inkGaugeFill,
                    new Rect(1f - baseRatio, 0f, baseRatio, 1f));
            }
            DrawReserveGauge(area, reserveRatio);

            if (golden)
                DrawGoldenGaugeEffect(area);

            if (inkBrushIcon != null)
            {
                var iconRect = new Rect(x + w - overlap, centerY - iconSize / 2, iconSize, iconSize);
                Color previousColor = GUI.color;
                if (golden)
                {
                    float pulse = 1f + 0.055f * Mathf.Sin(Time.unscaledTime * 6f);
                    iconRect = ScaleAroundCenter(iconRect, pulse);
                    iconRect = new Rect(iconRect.center.x - iconRect.width * 0.78f,
                        iconRect.center.y - iconRect.height * 0.53f,
                        iconRect.width * 1.56f, iconRect.height * 1.06f);
                    DrawGoldenIconHalo(iconRect);
                    // golden_brush 원본 색이 보이도록 별도의 Tint를 곱하지 않는다.
                    GUI.color = Color.white;
                }
                GUI.DrawTexture(iconRect, golden && goldenBrushIcon != null
                    ? goldenBrushIcon : inkBrushIcon, ScaleMode.ScaleToFit);
                if (golden) DrawGoldenIconSparkles(iconRect);
                GUI.color = previousColor;
            }
        }

        static void DrawReserveGauge(Rect area, float reserveRatio)
        {
            if (reserveRatio <= 0f) return;
            float blockWidth = Mathf.Max(8f, area.width * 0.085f);
            float gap = Mathf.Max(2f, area.width * 0.008f);
            int fullBlocks = Mathf.FloorToInt(reserveRatio / 0.35f);
            float partial = Mathf.Repeat(reserveRatio, 0.35f) / 0.35f;
            int visibleBlocks = Mathf.Min(8, fullBlocks + (partial > 0.01f ? 1 : 0));
            for (int i = 0; i < visibleBlocks; i++)
            {
                float fill = i < fullBlocks ? 1f : partial;
                var block = new Rect(area.x + i * (blockWidth + gap),
                    area.y - area.height * 0.34f, blockWidth * fill,
                    Mathf.Max(3f, area.height * 0.16f));
                DrawRect(block, new Color(0.18f, 0.5f, 0.42f, 0.95f));
            }
            var label = new Rect(area.xMax - 110f, area.y - area.height * 0.62f, 110f, 24f);
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = InkPalette.UiFont,
                alignment = TextAnchor.MiddleRight,
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.015f, 18f, 30f)),
            };
            labelStyle.normal.textColor = InkPalette.TextDark;
            GUI.Label(label, $"+{Mathf.RoundToInt(reserveRatio * 100f)}%", labelStyle);
        }

        static void DrawGoldenGaugeEffect(Rect area)
        {
            float time = Time.unscaledTime;
            Color previous = GUI.color;

            // 먹 게이지 위를 흐르는 얇은 금빛 세 줄. 이미지가 아니라 GUI 벡터 면으로 그린다.
            for (int i = 0; i < 3; i++)
            {
                float phase = Mathf.Repeat(time * (0.32f + i * 0.035f) + i * 0.31f, 1f);
                float streakX = Mathf.Lerp(area.x - area.width * 0.08f,
                    area.xMax + area.width * 0.08f, phase);
                float alpha = Mathf.Sin(phase * Mathf.PI) * (0.18f + i * 0.055f);
                var streak = new Rect(streakX, area.y + area.height * (0.2f + i * 0.22f),
                    Mathf.Max(2f, area.height * 0.055f), area.height * 0.7f);
                DrawRotatedRect(streak, -18f, new Color(1f, 0.78f, 0.22f, alpha));
            }

            // 게이지 윗선을 따라 떠오르는 작은 금가루.
            for (int i = 0; i < 9; i++)
            {
                float phase = time * (0.65f + i % 3 * 0.11f) + i * 1.73f;
                float px = area.x + area.width * (i + 0.5f) / 9f + Mathf.Sin(phase) * area.height * 0.15f;
                float py = area.y - area.height * (0.05f + 0.18f * (0.5f + 0.5f * Mathf.Sin(phase * 0.7f)));
                float size = area.height * (0.035f + (i % 3) * 0.014f);
                GUI.color = new Color(1f, 0.82f, 0.3f,
                    0.35f + 0.4f * (0.5f + 0.5f * Mathf.Sin(phase * 1.4f)));
                GUI.DrawTexture(new Rect(px - size * 0.5f, py - size * 0.5f, size, size),
                    Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        static void DrawGoldenIconHalo(Rect iconRect)
        {
            Color previous = GUI.color;
            float time = Time.unscaledTime;
            Vector2 center = iconRect.center;
            float radius = Mathf.Max(iconRect.width, iconRect.height) * 0.42f;
            for (int i = 0; i < 14; i++)
            {
                float angle = time * 1.35f + i * Mathf.PI * 2f / 14f;
                float size = iconRect.height * (0.025f + (i % 3) * 0.008f);
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                GUI.color = new Color(1f, 0.78f, 0.2f,
                    0.3f + 0.38f * (0.5f + 0.5f * Mathf.Sin(time * 4f + i)));
                GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size),
                    Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        static void DrawGoldenIconSparkles(Rect iconRect)
        {
            float time = Time.unscaledTime;
            Vector2 center = iconRect.center;
            Color previous = GUI.color;
            for (int i = 0; i < 4; i++)
            {
                float angle = time * -1.1f + i * Mathf.PI * 0.5f;
                float pulse = 0.55f + 0.45f * Mathf.Sin(time * 6.5f + i * 1.7f);
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    iconRect.height * 0.46f;
                float longSize = iconRect.height * (0.07f + pulse * 0.065f);
                float thin = Mathf.Max(1.5f, iconRect.height * 0.012f);
                GUI.color = new Color(1f, 0.9f, 0.48f, 0.45f + pulse * 0.5f);
                GUI.DrawTexture(new Rect(point.x - longSize * 0.5f, point.y - thin * 0.5f,
                    longSize, thin), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(point.x - thin * 0.5f, point.y - longSize * 0.5f,
                    thin, longSize), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        static void DrawRotatedRect(Rect rect, float angle, Color color)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUIUtility.RotateAroundPivot(angle, rect.center);
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        static Rect ScaleAroundCenter(Rect rect, float scale)
        {
            Vector2 center = rect.center;
            rect.width *= scale;
            rect.height *= scale;
            rect.center = center;
            return rect;
        }

        static Texture2D CreateColoredSilhouette(Texture2D source, Color color)
        {
            if (source == null) return null;
            var temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32);
            Graphics.Blit(source, temporary);
            var previous = RenderTexture.active;
            RenderTexture.active = temporary;
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            var pixels = result.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(color.r, color.g, color.b, pixels[i].a);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        static void DrawRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

    }
}
