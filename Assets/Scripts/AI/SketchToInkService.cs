using UnityEngine;

namespace MukJump.AI
{
    /// 런타임 스트로크에 내장 수묵 스타일을 적용하는 단일 진입점.
    /// 네트워크나 API 키 없이 모든 빌드에서 같은 결과를 내는 것이 제출 기준이다.
    public class SketchToInkService : MonoBehaviour
    {
        public static SketchToInkService Instance { get; private set; }

        // OnEnable: Play 중 스크립트 재컴파일로 static이 초기화돼도 다시 할당된다
        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Stylize(Drawing.PlatformCollider platform)
        {
            if (platform == null || platform.Line == null) return;
            FallbackInkStyle.Apply(platform.Line, platform.Length);
        }
    }
}
