using UnityEngine;
using MukJump.Drawing;

namespace MukJump.Core
{
    /// 화면 하단에 남은 먹 용량을 표시하는 경량 HUD.
    public class PrototypeHud : MonoBehaviour
    {
        const float BaseGaugeWidthRatio = 0.33f;
        const float GaugeVisualHeightRatio = 0.12f;
        const float BrushIconSizeRatio = 0.14f;
        const float BrushOverlapRatio = 0.32f;
        const float FallbackGaugeHeightRatio = 0.05f;
        const float EmptyGaugeGuideAlpha = 0.12f;
        const float GaugeConsumeFadeSeconds = 0.24f;
        const float GaugeRecoverFadeSeconds = 0.75f;

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
        float displayedInkRatio = 1f;
        bool displayedInkRatioInitialized;

        void OnEnable()
        {
            EnsureRuntimeReferences();
        }

        void Start()
        {
            EnsureRuntimeReferences();
            InitializeDisplayedInkRatio();
        }

        void Update()
        {
            if (strokeCapture == null)
                EnsureRuntimeReferences();
            if (strokeCapture == null)
                return;

            float target = Mathf.Clamp01(strokeCapture.InkRemaining01);
            if (!displayedInkRatioInitialized)
            {
                displayedInkRatio = target;
                displayedInkRatioInitialized = true;
                return;
            }

            float seconds = target < displayedInkRatio
                ? GaugeConsumeFadeSeconds
                : GaugeRecoverFadeSeconds;
            displayedInkRatio = Mathf.MoveTowards(
                displayedInkRatio,
                target,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
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

            // 화면 하단 먹 게이지: 화면에 더 남길 수 있는 먹 용량
            if (strokeCapture != null)
                DrawInkGauge(
                    displayedInkRatioInitialized
                        ? displayedInkRatio
                        : strokeCapture.InkRemaining01,
                    strokeCapture.InkCapacityRatio);
        }

        void EnsureRuntimeReferences()
        {
            if (strokeCapture == null)
                strokeCapture = FindFirstObjectByType<StrokeCapture>();
        }

        void InitializeDisplayedInkRatio()
        {
            if (strokeCapture == null)
                return;

            displayedInkRatio = Mathf.Clamp01(strokeCapture.InkRemaining01);
            displayedInkRatioInitialized = true;
        }

        Texture2D EnsureGoldenGaugeFill()
        {
            Texture2D silhouette = inkGaugeFill != null
                ? inkGaugeFill
                : inkGaugeTrack;
            if (goldenGaugeFill == null && silhouette != null)
            {
                goldenGaugeFill = CreateColoredSilhouette(
                    silhouette,
                    InkPalette.Gold);
                ownsGoldenGaugeFill = goldenGaugeFill != null;
            }
            return goldenGaugeFill;
        }

        /// 붓 획 모양 먹 게이지: 옅은 전체 용량 안내 위에서 남은 먹의 양만큼
        /// 불투명 채움 폭을 붓 쪽에 붙여 표시한다. 이미지 미할당 시 단색 막대 폴백.
        void DrawInkGauge(
            float ratio,
            float capacityRatio)
        {
            float baseRatio = Mathf.Clamp01(ratio);
            // 기본 1획부터 영구 성장 2.5획까지 트랙 폭으로 구분한다.
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
            Texture2D gaugeSilhouette = inkGaugeFill != null
                ? inkGaugeFill
                : inkGaugeTrack;
            if (gaugeSilhouette == null)
            {
                float bw = gaugeTrackWidth;
                float bh = CalculateFallbackGaugeHeight(safeGui.width);
                float by = safeGui.yMax - bottomMargin - bh;
                var back = new Rect(
                    safeGui.center.x - bw * 0.5f,
                    by,
                    bw,
                    bh);
                // 비어 있어도 총 용량의 위치만 옅은 먹색으로 남긴다. 한지색 트랙을
                // 깔면 먹을 쓸수록 검정→흰색으로 바뀌어 농도 UI로 읽히지 않는다.
                Color guideColor = InkPalette.Ink;
                guideColor.a = EmptyGaugeGuideAlpha;
                DrawRect(back, guideColor);
                Color fillColor = golden ? InkPalette.Gold : InkPalette.Ink;
                Rect fillRect = CalculateGaugeFillRect(
                    back,
                    baseRatio,
                    golden);
                if (fillRect.width > 0f)
                    DrawRect(fillRect, fillColor);
                return;
            }

            // 기본부터 충분히 길게 보이고, 성장·날씨가 실제 최대 폭에도 반영된다.
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
            // 빈 상태도 채움과 완전히 같은 실루엣을 쓴다. 별도 트랙
            // 이미지를 깔면 실기기에서 먹색이 바뀌는 것처럼 보일 수 있다.
            DrawTextureWithTint(
                area,
                gaugeSilhouette,
                InkPalette.Ink,
                EmptyGaugeGuideAlpha);

            Rect gaugeFillRect = CalculateGaugeFillRect(
                area,
                baseRatio,
                golden);
            if (gaugeFillRect.width > 0f)
            {
                // 붓에 붙은 오른쪽 끝은 고정하고 왼쪽부터 실제 사용량만큼 비운다.
                // UV도 같은 비율로 잘라 부분 이미지가 눌려 보이지 않게 한다.
                Texture2D fillTexture = golden
                    ? EnsureGoldenGaugeFill()
                    : gaugeSilhouette;
                if (fillTexture != null)
                    DrawTextureWithTintCropped(
                        gaugeFillRect,
                        fillTexture,
                        Color.white,
                        CalculateGaugeFillUv(baseRatio, golden));
            }
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

        static float ResolveGaugeFillRatio(float ratio, bool golden)
        {
            return golden ? 1f : Mathf.Clamp01(ratio);
        }

        static Rect CalculateGaugeFillRect(
            Rect area,
            float ratio,
            bool golden)
        {
            float fillRatio = ResolveGaugeFillRatio(ratio, golden);
            float fillWidth = area.width * fillRatio;
            return new Rect(
                area.xMax - fillWidth,
                area.y,
                fillWidth,
                area.height);
        }

        static Rect CalculateGaugeFillUv(float ratio, bool golden)
        {
            float fillRatio = ResolveGaugeFillRatio(ratio, golden);
            return new Rect(1f - fillRatio, 0f, fillRatio, 1f);
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

        static void DrawTextureWithTint(
            Rect rect,
            Texture2D texture,
            Color tint,
            float alpha)
        {
            if (texture == null) return;
            Color previous = GUI.color;
            GUI.color = new Color(
                tint.r,
                tint.g,
                tint.b,
                tint.a * Mathf.Clamp01(alpha));
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        static void DrawTextureWithTintCropped(
            Rect rect,
            Texture2D texture,
            Color tint,
            Rect uv)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
                return;
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            GUI.color = previous;
        }

    }
}
