namespace MukJump.Core.Pooling
{
    /// 풀에서 빌려오거나 돌려보낼 때 런타임 상태를 명시적으로 초기화하는 계약.
    /// OnDestroy에 의존하지 않아 같은 인스턴스를 안전하게 반복 사용할 수 있다.
    public interface IPoolableEntity
    {
        void OnPoolAcquire();
        void OnPoolRelease();
    }
}
