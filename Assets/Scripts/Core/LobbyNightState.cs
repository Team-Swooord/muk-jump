using UnityEngine;

namespace MukJump.Core
{
    /// 같은 실행 안에서 로비·게임·씬 재시작이 공유하는 시각 상태. 계정 저장과 무관하다.
    public static class LobbyNightState
    {
        public static float Progress { get; private set; }
        public static bool TargetNight { get; private set; }
        public static float DepartureHold { get; private set; }
        public static float CelestialTravel { get; private set; }
        public static float CelestialVelocity { get; private set; }
        public static bool Initialized { get; private set; }
        public static float Blend => Mathf.SmoothStep(0f, 1f, Progress);
        public static Color CloudTint => Color.Lerp(Color.white,
            new Color(0.46f, 0.58f, 0.72f, 1f), Blend);

        internal static void Set(float progress, bool targetNight, float departureHold = 0f,
            float celestialTravel = 0f, float celestialVelocity = 0f)
        {
            Progress = Mathf.Clamp01(progress);
            TargetNight = targetNight;
            DepartureHold = Mathf.Max(0f, departureHold);
            CelestialTravel = Mathf.Clamp01(celestialTravel);
            CelestialVelocity = celestialVelocity;
            Initialized = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Set(0f, false);
        }
    }
}
