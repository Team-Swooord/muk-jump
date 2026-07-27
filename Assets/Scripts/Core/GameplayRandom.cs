using System;
using UnityEngine;

namespace MukJump.Core
{
    /// 서로의 호출 횟수에 영향을 받지 않아야 하는 게임 규칙 난수 영역.
    /// 연출은 기존 UnityEngine.Random을 사용하고, 판정·스폰만 이 스트림을 사용한다.
    public enum GameplayRandomStream
    {
        Items,
        Obstacles,
        FallingRocks,
        Weather,
        Platforms,
        Player,
    }

    /// 세션 seed에서 기능별 독립 xorshift32 스트림을 파생한다.
    /// 같은 seed와 같은 기능 호출 순서는 실행 환경과 무관하게 같은 결과를 만든다.
    public static class GameplayRandom
    {
        const int DefaultSeed = 0x4D554B;
        const uint NonZeroFallback = 0x6D2B79F5u;
        const float FloatUnit = 1f / 16777216f;

        static readonly uint[] states = new uint[6];
        static bool initialized;
        static int sessionSeed;
        static int sessionVersion;
        static int generatedSessionCount;

        public static int SessionSeed
        {
            get
            {
                EnsureInitialized();
                return sessionSeed;
            }
        }

        /// 같은 seed로 명시적으로 reset해도 소비자가 새 세션을 감지하도록 별도 세대를 둔다.
        public static int SessionVersion
        {
            get
            {
                EnsureInitialized();
                return sessionVersion;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            initialized = false;
            sessionSeed = 0;
            sessionVersion = 0;
            generatedSessionCount = 0;
            ResetSession(DefaultSeed);
        }

        /// 일반 플레이용 새 seed를 만들고 모든 기능 스트림을 함께 초기화한다.
        public static int ResetSession()
        {
            int seed = unchecked(
                Environment.TickCount + (++generatedSessionCount * -1640531527));
            ResetSession(seed);
            return seed;
        }

        /// 테스트·리플레이용으로 지정한 seed에서 모든 기능 스트림을 초기화한다.
        public static void ResetSession(int seed)
        {
            sessionSeed = seed;
            sessionVersion = unchecked(sessionVersion + 1);
            uint root = unchecked((uint)seed);
            for (int i = 0; i < states.Length; i++)
            {
                uint domainSeed = root + 0x9E3779B9u * (uint)(i + 1);
                uint mixed = Mix(domainSeed);
                states[i] = mixed != 0u ? mixed : NonZeroFallback ^ (uint)i;
            }
            initialized = true;
        }

        /// 0 이상 1 미만의 결정론적 값을 반환한다.
        public static float Value(GameplayRandomStream stream)
        {
            // 상위 24비트만 사용해 모든 결과가 float에서 정확히 표현되게 한다.
            return (NextUInt(stream) >> 8) * FloatUnit;
        }

        /// Unity Random.Range(int, int)와 같은 최소 포함·최대 제외 범위다.
        public static int Range(GameplayRandomStream stream, int minimumInclusive,
            int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
                return minimumInclusive;

            uint range = (uint)((long)maximumExclusive - minimumInclusive);
            // 단순 나머지 연산의 편향을 없애기 위해 불완전한 최상단 구간을 버린다.
            uint threshold = unchecked(0u - range) % range;
            uint sample;
            do
            {
                sample = NextUInt(stream);
            } while (sample < threshold);

            return (int)(minimumInclusive + (long)(sample % range));
        }

        /// 최소 포함·최대 제외의 float 범위다.
        public static float Range(GameplayRandomStream stream, float minimumInclusive,
            float maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
                return minimumInclusive;
            return minimumInclusive +
                   (maximumExclusive - minimumInclusive) * Value(stream);
        }

        static uint NextUInt(GameplayRandomStream stream)
        {
            EnsureInitialized();
            int index = (int)stream;
            if ((uint)index >= states.Length)
                throw new ArgumentOutOfRangeException(nameof(stream), stream,
                    "정의되지 않은 게임 난수 스트림입니다.");

            uint state = states[index];
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            states[index] = state;
            return state;
        }

        static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        static void EnsureInitialized()
        {
            if (!initialized)
                ResetSession(DefaultSeed);
        }
    }
}
