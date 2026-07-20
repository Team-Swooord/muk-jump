using UnityEngine;
using MukJump.Drawing;
using MukJump.Player;

namespace MukJump.Core
{
    /// 그레이박스 단계용 임시 HUD (OnGUI). Week 2에서 한지 스타일 uGUI로 교체 예정.
    public class PrototypeHud : MonoBehaviour
    {
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

            // 화면 하단 먹 게이지: 한 획에 쓸 수 있는 잉크 잔량
            if (strokeCapture != null)
            {
                float w = Screen.width * 0.6f;
                float h = Screen.height * 0.014f;
                float y = Screen.height * 0.955f;
                var back = new Rect((Screen.width - w) / 2, y, w, h);
                DrawRect(back, InkPalette.Paper2);
                var fill = back;
                fill.width = w * strokeCapture.InkRemaining01;
                DrawRect(fill, InkPalette.Ink);

                bodyStyle.normal.textColor = InkPalette.TextMuted;
                GUI.Label(new Rect(0, y - bodyH - 2, Screen.width, bodyH), "먹", bodyStyle);
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
