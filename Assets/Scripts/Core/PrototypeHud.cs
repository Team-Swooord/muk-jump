using UnityEngine;
using MukJump.Drawing;

namespace MukJump.Core
{
    /// 화면 하단에 남은 총 먹자리와 붓 여유를 표시하는 경량 HUD.
    public class PrototypeHud : MonoBehaviour
    {
        const float BaseGaugeWidthRatio = 0.33f;

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
        bool ownsGoldenBrushIcon;

        void OnEnable()
        {
            EnsureRuntimeReferences();
        }

        void Start()
        {
            EnsureRuntimeReferences();
        }

        void OnDestroy()
        {
            if (ownsGoldenBrushIcon)
            {
                if (Application.isPlaying)
                    Destroy(goldenBrushIcon);
                else
                    DestroyImmediate(goldenBrushIcon);
            }
            goldenBrushIcon = null;
            ownsGoldenBrushIcon = false;
        }

        void OnGUI()
        {
            if (GameManager.Instance == null) return;

            // Play 중 스크립트 재컴파일에서는 Start가 다시 호출되지 않을 수 있다.
            // 비직렬화 참조가 사라진 경우 즉시 다시 묶어 게이지가 숨는 일을 막는다.
            if (strokeCapture == null)
                strokeCapture = FindFirstObjectByType<StrokeCapture>();

            if (GameManager.Instance.State == GameState.Lobby)
                return;

            if (GameManager.Instance.State == GameState.GameOver)
            {
                return;
            }

            // 화면 하단 먹 게이지: 화면에 더 남길 수 있는 먹자리
            if (strokeCapture != null)
                DrawInkGauge(
                    strokeCapture.InkRemaining01,
                    strokeCapture.InkCapacityRatio,
                    strokeCapture.InkCapacityBonusRatio);
        }

        void EnsureRuntimeReferences()
        {
            if (strokeCapture == null)
                strokeCapture = FindFirstObjectByType<StrokeCapture>();
            if (goldenBrushIcon != null)
                return;

            goldenBrushIcon = goldenBrushItemIcon != null
                ? goldenBrushItemIcon
                : CreateColoredSilhouette(
                    inkBrushIcon,
                    new Color(1f, 0.68f, 0.08f));
            ownsGoldenBrushIcon =
                goldenBrushItemIcon == null && goldenBrushIcon != null;
        }

        /// 붓 획 모양 먹 게이지: 트랙 위에 fill을 왼쪽부터 잔량만큼 잘라 그리고,
        /// 오른쪽 끝에 붓 아이콘을 붙인다. 이미지 미할당 시 단색 막대 폴백.
        void DrawInkGauge(
            float ratio,
            float capacityRatio,
            float reserveRatio)
        {
            float baseRatio = Mathf.Clamp01(ratio);
            // 기본 1획부터 영구 성장 2.5획까지 트랙 폭으로 구분한다.
            // 붓 여유가 더 쌓이면 Safe Area 폭에서 자연스럽게 상한에 닿는다.
            bool golden = strokeCapture != null && strokeCapture.HasUnlimitedInk;
            Rect safeGui = MobileUiLayout.ToGuiSafeArea(
                Screen.safeArea,
                Screen.width,
                Screen.height);
            float horizontalMargin = Mathf.Max(18f, safeGui.width * 0.04f);
            float bottomMargin = Mathf.Max(18f, Screen.height * 0.014f);
            float maximumGaugeWidth = Mathf.Max(1f,
                safeGui.width - horizontalMargin * 2f);
            float gaugeTrackWidth = CalculateGaugeTrackWidth(
                safeGui.width,
                horizontalMargin,
                capacityRatio);
            if (inkGaugeFill == null || inkGaugeTrack == null)
            {
                float bw = gaugeTrackWidth;
                float bh = Screen.height * 0.014f;
                float by = safeGui.yMax - bottomMargin - bh;
                var back = new Rect(
                    safeGui.center.x - bw * 0.5f,
                    by,
                    bw,
                    bh);
                DrawRect(back, InkPalette.Paper2);
                var fillRect = back;
                fillRect.width = bw * baseRatio;
                DrawRect(fillRect, InkPalette.Ink);
                DrawReserveGauge(back, reserveRatio);
                return;
            }

            // 기본부터 충분히 길게 보이고, 성장·날씨·붓 여유가 실제 최대 폭에도 반영된다.
            // 반복 아이템으로 최대치가 커져도 트랙은 화면 밖으로 나가지 않는다.
            float w = gaugeTrackWidth;
            float h = w * (inkGaugeFill.height / (float)inkGaugeFill.width);
            float iconSize = inkBrushIcon != null ? h * 1.0f : 0f;
            float overlap = iconSize * 0.08f;     // 회복 직후의 짧은 먹색도 붓 뒤에 가려지지 않게 한다
            float totalW = w + iconSize - overlap;
            if (totalW > maximumGaugeWidth)
            {
                float fit = maximumGaugeWidth / totalW;
                w *= fit;
                h *= fit;
                iconSize *= fit;
                overlap *= fit;
                totalW = w + iconSize - overlap;
            }

            // 아이콘(게이지보다 큼)까지 포함한 전체가 화면 아래로 짤리지 않도록 배치
            float clusterH = Mathf.Max(h, iconSize);
            float centerY = safeGui.yMax - bottomMargin - clusterH * 0.5f;
            float x = safeGui.center.x - totalW * 0.5f;
            float y = centerY - h / 2;

            var area = new Rect(x, y, w, h);
            GUI.DrawTexture(area, inkGaugeTrack, ScaleMode.StretchToFill);

            if (baseRatio > 0f)
            {
                // 먹이 돌아오는 순간부터 두꺼운 붓자국 쪽에 색이 나타나도록 오른쪽부터 채운다.
                // Rect와 UV를 같은 비율로 잘라 회복 중에도 원본 붓결을 늘이지 않는다.
                float fillX = x + w * (1f - baseRatio);
                var clipped = new Rect(fillX, y, w * baseRatio, h);
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

        static float CalculateGaugeTrackWidth(
            float safeWidth,
            float horizontalMargin,
            float capacityRatio)
        {
            float width = Mathf.Max(1f, safeWidth);
            float maximumWidth = Mathf.Max(1f, width - Mathf.Max(0f, horizontalMargin) * 2f);
            float representedCapacity = Mathf.Clamp(capacityRatio, 0.72f, 3.5f);
            return Mathf.Min(
                maximumWidth,
                width * BaseGaugeWidthRatio * representedCapacity);
        }

        static void DrawReserveGauge(Rect area, float reserveRatio)
        {
            if (reserveRatio <= 0f) return;
            float blockWidth = Mathf.Max(8f, area.width * 0.085f);
            float gap = Mathf.Max(2f, area.width * 0.008f);
            int fullBlocks = Mathf.FloorToInt(
                reserveRatio / StrokeCapture.InkReserveItemRatio);
            float partial = Mathf.Repeat(
                reserveRatio,
                StrokeCapture.InkReserveItemRatio) /
                StrokeCapture.InkReserveItemRatio;
            int visibleBlocks = Mathf.Min(8, fullBlocks + (partial > 0.01f ? 1 : 0));
            for (int i = 0; i < visibleBlocks; i++)
            {
                float fill = i < fullBlocks ? 1f : partial;
                var block = new Rect(area.x + i * (blockWidth + gap),
                    area.y - Mathf.Max(4f, area.height * 0.08f), blockWidth * fill,
                    Mathf.Max(3f, area.height * 0.08f));
                DrawRect(block, new Color(0.18f, 0.5f, 0.42f, 0.95f));
            }
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
