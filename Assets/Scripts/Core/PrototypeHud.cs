using UnityEngine;
using MukJump.Player;

namespace MukJump.Core
{
    /// 그레이박스 단계용 임시 HUD (OnGUI). Week 2에서 한지 스타일 uGUI로 교체 예정.
    public class PrototypeHud : MonoBehaviour
    {
        AutoJump autoJump;
        GUIStyle titleStyle;
        GUIStyle bodyStyle;

        void Start()
        {
            autoJump = FindFirstObjectByType<AutoJump>();
        }

        void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Screen.height / 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                bodyStyle = new GUIStyle(titleStyle)
                {
                    fontSize = Screen.height / 34,
                    fontStyle = FontStyle.Normal,
                };
            }

            var score = ScoreManager.Instance;
            if (score != null)
            {
                titleStyle.normal.textColor = InkPalette.TextDark;
                GUI.Label(new Rect(0, Screen.height * 0.03f, Screen.width, 50), $"고도 {score.Height}", titleStyle);
                bodyStyle.normal.textColor = InkPalette.TextMuted;
                GUI.Label(new Rect(0, Screen.height * 0.03f + 55, Screen.width, 40), $"최고 {score.Best}", bodyStyle);
            }

            if (GameManager.Instance.State == GameState.GameOver)
            {
                titleStyle.normal.textColor = InkPalette.Red;
                GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, 60), "추락…", titleStyle);
                bodyStyle.normal.textColor = InkPalette.TextDark;
                GUI.Label(new Rect(0, Screen.height * 0.42f + 70, Screen.width, 40), "화면을 터치해 다시 도전", bodyStyle);
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
