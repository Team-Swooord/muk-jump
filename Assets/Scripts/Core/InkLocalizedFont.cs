using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    // 이름/입력칸처럼 번역 제외인 글자도 일본어 글리프를 표시한다.
    [DisallowMultipleComponent]
    public sealed class InkLocalizedFont : MonoBehaviour
    {
        static Font japanese;
        Text target;
        Font original;
        int originalSize;
        int appliedSize;
        string lastText;
        GameLanguage lastLanguage;

        public static void Bind(Text label)
        {
            if (label == null) return;
            var binding = label.GetComponent<InkLocalizedFont>() ?? label.gameObject.AddComponent<InkLocalizedFont>();
            binding.Refresh();
        }

        void OnEnable() => Refresh();
        void LateUpdate()
        {
            if (target != null && (target.text != lastText || lastLanguage != GameLocalization.Language)) Refresh();
        }

        public void Refresh()
        {
            if (target == null) target = GetComponent<Text>();
            if (target == null) return;
            if (target.font != japanese) original = target.font;
            if (originalSize == 0 || target.fontSize != appliedSize) originalSize = target.fontSize;
            lastText = target.text;
            lastLanguage = GameLocalization.Language;
            bool useJapanese = GameLocalization.IsJapanese;
            foreach (char c in lastText ?? string.Empty)
                if ((c >= '\u3040' && c <= '\u30ff') || (c >= '\u3400' && c <= '\u9fff')) { useJapanese = true; break; }
            if (useJapanese && japanese == null) japanese = Resources.Load<Font>("MukJump/Fonts/KaiseiDecol-Regular");
            Font selected = useJapanese ? japanese : original;
            if (selected != null && target.font != selected) target.font = selected;
            // 일본어 폰트의 넓은 상하 메트릭을 기존 한글 UI 행 높이에 맞춘다.
            appliedSize = useJapanese ? Mathf.Max(1, Mathf.RoundToInt(originalSize * .76f)) : originalSize;
            target.fontSize = appliedSize;
        }
    }
}
