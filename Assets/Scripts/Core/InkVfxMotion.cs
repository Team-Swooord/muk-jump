using UnityEngine;

namespace MukJump.Core
{
    /// 판정과 분리된 먹의 운동. 프레임률에 따라 파편의 도착점이 달라지지 않게
    /// 선형 항력과 일정 중력을 해석적으로 적분한다.
    public static class InkVfxMotion
    {
        public static float TailAlpha(float progress, float hold = 0.16f)
        {
            float t = Mathf.InverseLerp(hold, 1f, Mathf.Clamp01(progress));
            return 1f - t * t * (3f - 2f * t);
        }

        public static void Integrate(ref Vector3 position, ref Vector3 velocity,
            Vector3 acceleration, float drag, float delta)
        {
            if (delta <= 0f) return;
            if (drag <= 0.0001f)
            {
                position += velocity * delta + acceleration * (0.5f * delta * delta);
                velocity += acceleration * delta;
                return;
            }
            float decay = Mathf.Exp(-drag * delta);
            Vector3 terminal = acceleration / drag;
            position += terminal * delta + (velocity - terminal) * ((1f - decay) / drag);
            velocity = terminal + (velocity - terminal) * decay;
        }
    }

    /// 분신 수나 FPS가 아닌 화면 선두의 실제 이동량에 따라 짧은 잔향을 방출한다.
    public struct InkWakeSampler
    {
        int owner;
        bool ready;
        Vector3 previous;
        float elapsed;
        float distance;

        public void Reset() => this = default;

        public bool Sample(int ownerId, Vector3 position, float delta)
        {
            float step = ready ? Vector3.Distance(position, previous) : 0f;
            if (!ready || owner != ownerId || step > 2.5f || delta > 0.15f)
            {
                ready = true;
                owner = ownerId;
                previous = position;
                elapsed = distance = 0f;
                return false;
            }
            previous = position;
            if (delta <= 0f) return false;
            elapsed += delta;
            distance += step;
            if (elapsed < 0.1f || distance < 0.6f) return false;
            // 누적분은 버린다. 앱 복귀·50m 급상승에 과거 궤적을 일괄 생성하지 않는다.
            elapsed = distance = 0f;
            return true;
        }
    }
}
