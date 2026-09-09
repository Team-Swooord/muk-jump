using UnityEngine;
using UnityEngine.UI;

namespace MukJump.Core
{
    /// 공통 행동 버튼의 구조와 의미를 저장해 도메인 리로드와 동적 문구 변경에도
    /// 동일한 한지 카드 스타일을 다시 적용할 수 있게 한다.
    [DisallowMultipleComponent]
    public sealed class InkActionButtonVisual : MonoBehaviour
    {
        [field: SerializeField] public ActionButtonRole Role { get; internal set; }
        [field: SerializeField] public ActionButtonLayout Layout { get; internal set; }
        [field: SerializeField] public FontStyle LabelStyle { get; internal set; }
        [field: SerializeField] public Image Border { get; internal set; }
        [field: SerializeField] public Image Surface { get; internal set; }
        [field: SerializeField] public Text Label { get; internal set; }
    }
}
