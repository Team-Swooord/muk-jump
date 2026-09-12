using UnityEngine.EventSystems;
namespace MukJump.Core
{
    /// 준비 중인 버튼에서 시작한 드래그도 먹선으로 새지 않는다. 실제 발동은 정상 클릭 때만 한다.
    public sealed class InkSkinSkillButton : UnityEngine.UI.Button
    {
        public override void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) PointerInput.SuppressUntilRelease();
            base.OnPointerDown(data);
        }
    }
}
