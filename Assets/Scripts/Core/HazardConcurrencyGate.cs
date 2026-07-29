using UnityEngine;

namespace MukJump.Core
{
    /// 낙묵석·강풍·수문장처럼 동시에 나오면 회피 정보를 읽기 어려운 큰 위험의
    /// 예약 상태를 기능 간 직접 참조 없이 공유한다.
    public static class HazardConcurrencyGate
    {
        static int haetaeReservations;

        public static bool HasHaetaeReservation => haetaeReservations > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            haetaeReservations = 0;
        }

        public static void RegisterHaetae()
        {
            haetaeReservations++;
        }

        public static void UnregisterHaetae()
        {
            haetaeReservations = Mathf.Max(0, haetaeReservations - 1);
        }
    }
}
