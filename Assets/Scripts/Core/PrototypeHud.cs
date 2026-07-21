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

        AutoJump autoJump;
        StrokeCapture strokeCapture;
        GUIStyle titleStyle;
        GUIStyle bodyStyle;

        void Start()
        {
            autoJump = FindFirstObjectByType<AutoJump>();
            strokeCapture = FindFirstObjectByType<StrokeCapture>();
        }

        void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                bodyStyle = new GUIStyle(titleStyle) { fontStyle = FontStyle.Normal };
            }

            // 뷰(Game/Simulator) 전환으로 해상도가 바뀌어도 잘리지 않도록 매 프레임 갱신
            titleStyle.fontSize = Screen.height / 22;
            bodyStyle.fontSize = Screen.height / 34;
            float titleH = titleStyle.fontSize * 1.6f;
            float bodyH = bodyStyle.fontSize * 1.6f;

            var score = ScoreManager.Instance;
            if (score != null)
            {
                titleStyle.normal.textColor = InkPalette.TextDark;
                GUI.Label(new Rect(0, Screen.height * 0.03f, Screen.width, titleH), $"고도 {score.Height}", titleStyle);
                bodyStyle.normal.textColor = InkPalette.TextMuted;
                GUI.Label(new Rect(0, Screen.height * 0.03f + titleH, Screen.width, bodyH), $"최고 {score.Best}", bodyStyle);
            }

            if (GameManager.Instance.State == GameState.GameOver)
            {
                titleStyle.normal.textColor = InkPalette.Red;
                GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, titleH), "추락…", titleStyle);
                bodyStyle.normal.textColor = InkPalette.TextDark;
                GUI.Label(new Rect(0, Screen.height * 0.42f + titleH, Screen.width, bodyH), "화면을 터치해 다시 도전", bodyStyle);
                return;
            }

            // 다음 점프까지 남은 시간 게이지 (발판 그릴 타이밍 안내)
            if (autoJump != null && autoJump.IsCharging)
            {
                float w = Screen.width * 0.4f;
                float h = Screen.height * 0.012f;
                var back = new Rect((Screen.width - w) / 2, Screen.height * 0.12f, w, h);
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
            float iconSize = inkBrushIcon != null ? h * 1.35f : 0f;
            float totalW = w + iconSize * 0.7f; // 아이콘은 게이지 끝에 살짝 겹침
            float x = (Screen.width - totalW) / 2;
            float y = Screen.height * 0.985f - h;

            var area = new Rect(x, y, w, h);
            GUI.DrawTexture(area, inkGaugeTrack, ScaleMode.StretchToFill);

            if (ratio > 0f)
            {
                // 왼쪽부터 잔량 비율만큼만 가로로 잘라 그린다 (UV도 같은 비율로 잘라 왜곡 방지)
                var clipped = new Rect(x, y, w * ratio, h);
                GUI.DrawTextureWithTexCoords(clipped, inkGaugeFill, new Rect(0f, 0f, ratio, 1f));
            }

            if (inkBrushIcon != null)
            {
                var iconRect = new Rect(x + w - iconSize * 0.3f, y + h / 2 - iconSize / 2, iconSize, iconSize);
                GUI.DrawTexture(iconRect, inkBrushIcon, ScaleMode.ScaleToFit);
            }
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
