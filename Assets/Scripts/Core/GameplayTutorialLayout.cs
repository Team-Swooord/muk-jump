using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 첫 안내와 설정 다시 보기의 그림·문구·버튼 배치를 한 곳에서 소유한다.
    public static class GameplayTutorialLayout
    {
        public static void Apply(Text progress, Image icon, RectTransform iconPaper,
            Text title, Text description, RectTransform previous, RectTransform next,
            RectTransform exit)
        {
            Place(progress.rectTransform, 0, 510, 200, 80);
            Place(icon.rectTransform, 0, 360, 200, 200);
            if (iconPaper != null) Place(iconPaper, 0, 360, 240, 200);
            // 아이콘이 받침 자식인 다시 보기에서도 같은 패널 좌표를 사용한다.
            if (iconPaper != null && icon.transform.parent == iconPaper)
                icon.rectTransform.anchoredPosition = Vector2.zero;
            Place(title.rectTransform, 0, 193, 700, 96);
            Place(description.rectTransform, 0, -27, 700, 320);
            Place(previous, -190, -337, 300, 120);
            Place(next, 190, -337, 320, 120);
            // 설정 다시 보기는 바깥 dim으로 돌아가므로 별도 종료 버튼이 없다.
            if (exit != null) Place(exit, 0, -482, 340, 120);
            progress.alignment = title.alignment = description.alignment = TextAnchor.MiddleCenter;
            progress.fontSize = InkUiStyle.InstructionCaptionSize;
            title.fontSize = InkUiStyle.InstructionTitleSize;
            description.fontSize = InkUiStyle.InstructionBodySize;
            description.lineSpacing = 1.12f;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
