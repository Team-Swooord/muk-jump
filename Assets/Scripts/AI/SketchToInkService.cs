using System.Collections;
using UnityEngine;

namespace MukJump.AI
{
    /// 스케치 → 수묵 변환 파이프라인의 진입점.
    ///
    /// 동작 원칙 (CLAUDE.md 7절):
    ///  1. 발판 생성 즉시 폴백 잉크 스타일을 적용한다 → 끊김 없는 플레이 보장
    ///  2. 원격 API가 설정된 경우에만 비동기로 img2img(스크리블 조건) 변환을 요청하고,
    ///     응답이 오면 폴백 비주얼을 AI 결과물로 교체한다
    ///  3. API 키가 없거나 실패하면 폴백이 그대로 최종 비주얼 (심사자 실행 요건)
    public class SketchToInkService : MonoBehaviour
    {
        public static SketchToInkService Instance { get; private set; }

        [Header("원격 AI 변환 (Week 2)")]
        [Tooltip("비워두면 항상 폴백 잉크 스타일만 사용")]
        [SerializeField] string apiEndpoint = "";

        public bool RemoteEnabled => !string.IsNullOrEmpty(apiEndpoint);

        // OnEnable: Play 중 스크립트 재컴파일로 static이 초기화돼도 다시 할당된다
        void OnEnable()
        {
            Instance = this;
        }

        public void Stylize(Drawing.PlatformCollider platform)
        {
            // 1단계: 폴백은 항상 즉시 적용
            FallbackInkStyle.Apply(platform.Line, platform.Length);

            // 2단계: 원격 변환은 비동기 (미설정 시 생략)
            if (RemoteEnabled)
                StartCoroutine(RemoteStylize(platform));
        }

        IEnumerator RemoteStylize(Drawing.PlatformCollider platform)
        {
            // TODO(Week 2, feature/ai-ink-pipeline):
            //  1. platform 스트로크를 오프스크린 렌더 → PNG 인코딩
            //  2. img2img(ControlNet-scribble 계열) API에 UnityWebRequest POST
            //  3. 응답 텍스처를 SpriteRenderer로 발판 위에 오버레이 (LineRenderer는 유지하되 투명화)
            //  4. 실패/타임아웃 시 아무것도 하지 않음 (폴백 유지)
            yield break;
        }
    }
}
