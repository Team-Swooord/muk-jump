using UnityEngine;
using MukJump.Drawing;
using MukJump.Player;

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

        AutoJump autoJump;
        StrokeCapture strokeCapture;
        GUIStyle titleStyle;
        GUIStyle bodyStyle;
        Texture2D goldenBrushIcon;

        void Start()
        {
            autoJump = FindFirstObjectByType<AutoJump>();
            strokeCapture = FindFirstObjectByType<StrokeCapture>();
            goldenBrushIcon = goldenBrushItemIcon != null
                ? goldenBrushItemIcon
                : CreateColoredSilhouette(inkBrushIcon, new Color(1f, 0.68f, 0.08f));
        }

        void OnGUI()
        {
            if (GameManager.Instance == null) return;

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                bodyStyle = new GUIStyle(titleStyle) { fontStyle = FontStyle.Normal };
                MakeNonInteractive(titleStyle);
                MakeNonInteractive(bodyStyle);
            }

            // 뷰(Game/Simulator) 전환으로 해상도가 바뀌어도 잘리지 않도록 매 프레임 갱신
            titleStyle.fontSize = Screen.height / 22;
            bodyStyle.fontSize = Screen.height / 34;
            float titleH = titleStyle.fontSize * 1.6f;
            float bodyH = bodyStyle.fontSize * 1.6f;

            if (GameManager.Instance.State == GameState.Lobby)
                return;

            if (GameManager.Instance.State == GameState.GameOver)
            {
                SetTextColor(titleStyle, InkPalette.Red);
                GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, titleH), "추락…", titleStyle);
                SetTextColor(bodyStyle, InkPalette.TextDark);
                GUI.Label(new Rect(0, Screen.height * 0.42f + titleH, Screen.width, bodyH), "화면을 터치해 메인으로", bodyStyle);
                return;
            }

            // 다음 점프까지 남은 시간 게이지 (발판 그릴 타이밍 안내) — 점수 텍스트 아래
            if (autoJump != null && autoJump.IsCharging)
            {
                float w = Screen.width * 0.4f;
                float h = Screen.height * 0.012f;
                float gaugeY = Screen.height * 0.115f;
                var back = new Rect((Screen.width - w) / 2, gaugeY, w, h);
                DrawRect(back, InkPalette.Paper2);
                var fill = back;
                fill.width = w * autoJump.ChargeRatio;
                DrawRect(fill, InkPalette.Red);
            }

            // 화면 하단 먹 게이지: 전역 잉크 잔량
            if (strokeCapture != null)
                DrawInkGauge(strokeCapture.InkRemaining01);
        }

        /// 붓 획 모양 먹 게이지: 트랙 위에 fill을 왼쪽부터 잔량만큼 잘라 그리고,
        /// 오른쪽 끝에 붓 아이콘을 붙인다. 이미지 미할당 시 단색 막대 폴백.
        void DrawInkGauge(float ratio)
        {
            if (inkGaugeFill == null || inkGaugeTrack == null)
            {
                float bw = Screen.width * 0.6f;
                float bh = Screen.height * 0.014f;
                float by = Screen.height * 0.955f;
                var back = new Rect((Screen.width - bw) / 2, by, bw, bh);
                DrawRect(back, InkPalette.Paper2);
                var fillRect = back;
                fillRect.width = bw * ratio;
                DrawRect(fillRect, InkPalette.Ink);
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

            if (ratio > 0f)
            {
                // 왼쪽부터 잔량 비율만큼만 가로로 잘라 그린다 (UV도 같은 비율로 잘라 왜곡 방지)
                // 먹이 줄어들 때 왼쪽부터 비워지고 붓이 있는 오른쪽 방향으로 잔량이 남는다.
                float remainingX = x + w * (1f - ratio);
                var clipped = new Rect(remainingX, y, w * ratio, h);
                GUI.DrawTextureWithTexCoords(clipped, inkGaugeFill,
                    new Rect(1f - ratio, 0f, ratio, 1f));
            }

            if (inkBrushIcon != null)
            {
                var iconRect = new Rect(x + w - overlap, centerY - iconSize / 2, iconSize, iconSize);
                bool golden = strokeCapture != null && strokeCapture.HasUnlimitedInk;
                Color previousColor = GUI.color;
                if (golden)
                {
                    float flash = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 8f);
                    GUI.color = new Color(1f, 0.88f, 0.42f, flash);
                    float pulse = 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f);
                    iconRect = ScaleAroundCenter(iconRect, pulse);
                }
                GUI.DrawTexture(iconRect, golden && goldenBrushIcon != null
                    ? goldenBrushIcon : inkBrushIcon, ScaleMode.ScaleToFit);
                GUI.color = previousColor;
            }
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

        /// 기본 GUI 스킨의 hover/active 상태가 마우스 오버 시 버튼처럼 보이지 않게 고정한다.
        static void MakeNonInteractive(GUIStyle style)
        {
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.onHover.background = null;
            style.onActive.background = null;
            style.onFocused.background = null;

        }

        static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }
    }
}
