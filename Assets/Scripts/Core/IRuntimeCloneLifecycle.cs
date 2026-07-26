namespace MukJump.Core
{
    /// 런타임 분신 복제에서 게임 상태가 아닌 캐시 자식을 잠시 제외하는 확장 계약.
    /// Core는 구체적인 아이템·표현 컴포넌트를 참조하지 않고 이 경계만 호출한다.
    public interface IRuntimeCloneLifecycle
    {
        void PrepareForRuntimeClone();
        void RestoreAfterRuntimeClone();
    }
}
