using UnityEngine;
using MukJump.Drawing;

namespace MukJump.Core
{
    /// 화면 하단에 남은 총 먹자리와 붓 여유를 표시하는 경량 HUD.
    public class PrototypeHud : MonoBehaviour
    {
        const float BaseGaugeWidthRatio = 0.33f;
        const float GaugeVisualHeightRatio = 0.12f;
        const float BrushIconSizeRatio = 0.14f;
        const float BrushOverlapRatio = 0.32f;
        const float FallbackGaugeHeightRatio = 0.05f;

        [Header("먹 게이지 이미지 (붓 획 모양) — 미할당 시 단색 막대로 폴백")]
        [Tooltip("붓 획 실루엣, 채워진 상태 (왼쪽 가늘게 → 오른쪽 두껍게)")]
        [SerializeField] Texture2D inkGaugeFill;
        [Tooltip("같은 실루엣의 빈 상태 트랙 (fill과 캔버스·위치 동일)")]
        [SerializeField] Texture2D inkGaugeTrack;
        [Tooltip("게이지 오른쪽 끝의 붓 아이콘")]
        [SerializeField] Texture2D inkBrushIcon;

        StrokeCapture strokeCapture;
        Texture2D goldenGaugeFill;
        bool ownsGoldenGaugeFill;

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
            if (ownsGoldenGaugeFill)
            {
                if (Application.isPlaying)
                    Destroy(goldenGaugeFill);
                else
                    DestroyImmediate(goldenGaugeFill);
            }
            goldenGaugeFill = null;
            ownsGoldenGaugeFill = false;
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
        }

        Texture2D EnsureGoldenGaugeFill()
        {
            if (goldenGaugeFill == null && inkGaugeFill != null)
            {
                goldenGaugeFill = CreateColoredSilhouette(
                    inkGaugeFill,
                    InkPalette.Gold);
                ownsGoldenGaugeFill = goldenGaugeFill != null;
            }
            return goldenGaugeFill;
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
            float visibleRatio = golden ? 1f : baseRatio;
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
                float bh = CalculateFallbackGaugeHeight(safeGui.width);
                float by = safeGui.yMax - bottomMargin - bh;
                var back = new Rect(
                    safeGui.center.x - bw * 0.5f,
                    by,
                    bw,
                    bh);
                DrawRect(back, InkPalette.Paper2);
                var fillRect = back;
                fillRect.width = bw * visibleRatio;
                DrawRect(fillRect, golden ? InkPalette.Gold : InkPalette.Ink);
                DrawReserveGauge(back, reserveRatio);
                return;
            }

            // 기본부터 충분히 길게 보이고, 성장·날씨·붓 여유가 실제 최대 폭에도 반영된다.
            // 반복 아이템으로 최대치가 커져도 트랙은 화면 밖으로 나가지 않는다.
            float w = gaugeTrackWidth;
            // 용량 성장은 트랙 길이만 바꾼다. 두께와 붓 크기는 기기 폭을 기준으로
            // 고정해 19.5:9 실기기에서도 시뮬레이터와 같은 시각 비율을 유지한다.
            float h = CalculateGaugeVisualHeight(safeGui.width);
            float iconSize = inkBrushIcon != null
                ? CalculateBrushIconSize(safeGui.width)
                : 0f;
            float overlap = iconSize * BrushOverlapRatio;
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

            if (visibleRatio > 0f)
            {
                // 먹이 돌아오는 순간부터 두꺼운 붓자국 쪽에 색이 나타나도록 오른쪽부터 채운다.
                // Rect와 UV를 같은 비율로 잘라 회복 중에도 원본 붓결을 늘이지 않는다.
                float fillX = x + w * (1f - visibleRatio);
                var clipped = new Rect(fillX, y, w * visibleRatio, h);
                Texture2D fillTexture = golden
                    ? EnsureGoldenGaugeFill()
                    : inkGaugeFill;
                if (fillTexture != null)
                    GUI.DrawTextureWithTexCoords(clipped, fillTexture,
                        new Rect(1f - visibleRatio, 0f, visibleRatio, 1f));
            }
            DrawReserveGauge(area, reserveRatio);

            if (inkBrushIcon != null)
            {
                var iconRect = new Rect(x + w - overlap, centerY - iconSize / 2, iconSize, iconSize);
                GUI.DrawTexture(iconRect, inkBrushIcon, ScaleMode.ScaleToFit);
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

        static float CalculateGaugeVisualHeight(float safeWidth)
        {
            return Mathf.Max(1f, safeWidth) * GaugeVisualHeightRatio;
        }

        static float CalculateBrushIconSize(float safeWidth)
        {
            return Mathf.Max(1f, safeWidth) * BrushIconSizeRatio;
        }

        static float CalculateFallbackGaugeHeight(float safeWidth)
        {
            return Mathf.Max(1f, safeWidth) * FallbackGaugeHeightRatio;
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
